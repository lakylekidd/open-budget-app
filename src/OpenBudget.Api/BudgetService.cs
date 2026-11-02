using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed class BudgetService(BudgetDbContext db, LedgerProjection projection)
{
    private const int MaxGroups = 100;
    private const int MaxCategoriesPerGroup = 500;
    private const decimal MaxMoney = 999_999_999_999.99m;

    public async Task<BudgetPlanDto> GetPlanAsync(CancellationToken cancellationToken)
    {
        var plan = await db.BudgetPlans.SingleOrDefaultAsync(x => x.Id == "default", cancellationToken);
        if (plan is null)
        {
            plan = new BudgetPlanEntity();
            db.BudgetPlans.Add(plan);
            await db.SaveChangesAsync(cancellationToken);
        }
        return ToPlanDto(plan);
    }

    public async Task<BudgetPlanUpdateResult> UpdatePlanAsync(BudgetPlanUpdateDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
            return BudgetPlanUpdateResult.Invalid("Budget name is required and must be 120 characters or fewer.");
        if (request.Version < 1) return BudgetPlanUpdateResult.Invalid("Budget version is invalid.");

        var plan = await db.BudgetPlans.SingleAsync(x => x.Id == "default", cancellationToken);
        if (plan.Version != request.Version) return BudgetPlanUpdateResult.Conflict(ToPlanDto(plan));
        plan.Name = request.Name.Trim();
        plan.Version++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return BudgetPlanUpdateResult.Success(ToPlanDto(plan));
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear(); db.ResetCatalogs();
            var latest = await db.BudgetPlans.SingleAsync(x => x.Id == "default", cancellationToken);
            return BudgetPlanUpdateResult.Conflict(ToPlanDto(latest));
        }
    }

    private static BudgetPlanDto ToPlanDto(BudgetPlanEntity plan) => new(plan.Id, plan.Name, plan.Currency, plan.Version);

    public Task<BudgetEntity> GetMonthAsync(string month, CancellationToken cancellationToken) => GetMonthAsync("default", month, cancellationToken);

    // Loads a month composed with the budget's categories, creating it lazily. Categories are budget-wide: a new month only
    // gets per-month state (carried available, snooze and legacy target from the previous month; zero balances when it
    // precedes every existing month). The starter template is seeded once per budget, when it has no categories yet.
    public async Task<BudgetEntity> GetMonthAsync(string budgetId, string month, CancellationToken cancellationToken)
    {
        var existing = await db.LoadPlanMonthAsync(QueryMonth(budgetId, month), cancellationToken);
        if (existing is not null) return existing;

        var plan = await db.BudgetPlans.SingleAsync(x => x.Id == budgetId, cancellationToken);
        var catalog = await CategoryCatalog.LoadAsync(db, budgetId, cancellationToken);
        if (!plan.CategoriesInitialized)
        {
            if (catalog.Groups.Count == 0 && catalog.Categories.Count == 0) SeedStarterCategories(catalog);
            plan.CategoriesInitialized = true;
        }

        var previous = await db.LoadPlanMonthAsync(db.Budgets
            .Where(x => x.BudgetId == budgetId && string.Compare(x.Month, month) < 0)
            .OrderByDescending(x => x.Month).Take(1), cancellationToken);
        // A month before the budget's first one has no balances; it keeps the nearest later month's snooze and legacy target.
        var later = previous is not null ? null : await db.LoadPlanMonthAsync(db.Budgets
            .Where(x => x.BudgetId == budgetId && string.Compare(x.Month, month) > 0)
            .OrderBy(x => x.Month).Take(1), cancellationToken);

        var created = new BudgetEntity
        {
            Month = month,
            BudgetId = budgetId,
            Plan = plan,
            Note = "",
            OpeningReadyToAssign = previous is null ? 0 : BudgetCarryForward.OpeningReadyToAssign(previous),
            FundsAdded = 0,
            Version = 1
        };
        catalog.Attach(created);
        foreach (var state in created.CategoryStates)
        {
            var source = (previous ?? later)?.State(state.CategoryId);
            if (source is null) continue;
            state.Target = source.Target;
            state.Snoozed = source.Snoozed;
            if (previous is not null) state.OpeningAvailable = BudgetCarryForward.OpeningAvailable(source.Available);
        }
        db.Budgets.Add(created);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear(); db.ResetCatalogs();
            var concurrent = await db.LoadPlanMonthAsync(QueryMonth(budgetId, month), cancellationToken);
            if (concurrent is not null) return concurrent;
            throw;
        }
    }

    public Task<BudgetUpdateResult> UpdateMonthAsync(BudgetUpdateDto request, CancellationToken cancellationToken) => UpdateMonthAsync("default", request, null, cancellationToken);

    public Task<BudgetUpdateResult> UpdateMonthAsync(string budgetId, BudgetUpdateDto request, CancellationToken cancellationToken) =>
        UpdateMonthAsync(budgetId, request, null, cancellationToken);

    // Saves one month. Assigned amounts, snooze and the legacy target are this month's; names, groups, order, icons and
    // archive state are budget-wide and apply to every month. Any structural change advances every month's version so a
    // session holding another month reloads instead of overwriting the new structure with a stale copy.
    public async Task<BudgetUpdateResult> UpdateMonthAsync(string budgetId, BudgetUpdateDto request, string? actorUserId, CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null) return BudgetUpdateResult.Invalid(validation);

        var budget = await GetMonthAsync(budgetId, request.Month, cancellationToken);
        if (budget.Version != request.Version)
            return BudgetUpdateResult.Conflict(await ToDtoAsync(budget, cancellationToken));
        if (budget.FundsAdded != request.FundsAdded)
            return BudgetUpdateResult.Invalid("Funds added is a preserved legacy baseline. Record new income through a cash account transaction.");

        var catalog = budget.Catalog!;
        var currentGroups = catalog.Groups.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var currentCategories = catalog.Categories.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var requestedCategoryIds = request.Groups.SelectMany(g => g.Categories.Select(c => c.Id)).ToHashSet(StringComparer.Ordinal);

        // A month save can only archive groups and categories. Deleting a category goes through the reassign flow
        // (POST /api/budgets/{id}/categories/{categoryId}/delete), which moves its history first.
        if (currentGroups.Keys.Any(id => request.Groups.All(g => g.Id != id)) ||
            currentCategories.Keys.Any(id => !requestedCategoryIds.Contains(id)))
            return BudgetUpdateResult.Invalid("Existing groups and categories must be archived instead of removed.");
        if (WishlistRules.ValidatePlanUpdate(budget, request.Groups) is { } wishlistError)
            return BudgetUpdateResult.Invalid(wishlistError);

        var previousReadyToAssign = CalculateReadyToAssign(budget);
        var previousAvailable = BudgetCarryForward.AvailableById(budget);
        var previousCreditOverspending = BudgetCarryForward.CreditOverspendingById(budget);
        var previousCategories = budget.Groups.SelectMany(g => g.Categories).ToDictionary(c => c.Id, c => (c.Name, c.Assigned));
        var structureChanged = false;

        foreach (var groupRequest in request.Groups)
        {
            var name = groupRequest.Name.Trim();
            if (!currentGroups.TryGetValue(groupRequest.Id, out var group))
            {
                group = catalog.AddGroup(groupRequest.Id, name, groupRequest.SortOrder, groupRequest.IsArchived);
                currentGroups.Add(group.Id, group);
                structureChanged = true;
            }
            else if (group.Name != name || group.SortOrder != groupRequest.SortOrder || group.IsArchived != groupRequest.IsArchived)
            {
                group.Name = name;
                group.SortOrder = groupRequest.SortOrder;
                group.IsArchived = groupRequest.IsArchived;
                structureChanged = true;
            }

            foreach (var categoryRequest in groupRequest.Categories)
            {
                if (CreditCardRules.IsPaymentCategory(categoryRequest.Id) != (groupRequest.Id == CreditCardRules.PaymentGroupId))
                    return BudgetUpdateResult.Invalid("Credit card payment categories stay in the Credit Card Payments group, which holds only them.");
                if (!currentCategories.TryGetValue(categoryRequest.Id, out var category))
                {
                    if (CreditCardRules.IsPaymentCategory(categoryRequest.Id))
                        return BudgetUpdateResult.Invalid("Credit card payment categories are created with their credit account.");
                    if (categoryRequest.Id.StartsWith(LoanRules.CategoryPrefix, StringComparison.Ordinal))
                        return BudgetUpdateResult.Invalid("Loan categories are created with their loan account.");
                    if (categoryRequest.Assigned != 0)
                        return BudgetUpdateResult.Invalid("A new category must start with zero assigned.");
                    category = catalog.AddCategory(categoryRequest.Id, groupRequest.Id, categoryRequest.Name.Trim(), categoryRequest.SortOrder, categoryRequest.Icon, categoryRequest.IsArchived);
                    currentCategories.Add(category.Id, category);
                    structureChanged = true;
                }
                else if (category.GroupId != groupRequest.Id)
                {
                    var previousGroup = currentGroups[category.GroupId];
                    if (previousGroup.IsArchived || group.IsArchived || category.IsArchived)
                        return BudgetUpdateResult.Invalid("An archived category cannot be moved until it is restored.");
                    category.GroupId = groupRequest.Id;
                    structureChanged = true;
                }
                else if ((group.IsArchived || category.IsArchived) && categoryRequest.Assigned != catalog.EnsureState(budget, category.Id).Assigned)
                {
                    return BudgetUpdateResult.Invalid("An archived category cannot be assigned until it is restored.");
                }

                var categoryName = categoryRequest.Name.Trim();
                if (category.Name != categoryName || category.Icon != categoryRequest.Icon || category.SortOrder != categoryRequest.SortOrder || category.IsArchived != categoryRequest.IsArchived)
                {
                    category.Name = categoryName;
                    category.Icon = categoryRequest.Icon;
                    category.SortOrder = categoryRequest.SortOrder;
                    category.IsArchived = categoryRequest.IsArchived;
                    structureChanged = true;
                }
                var state = catalog.EnsureState(budget, category.Id);
                state.Assigned = categoryRequest.Assigned;
                state.Target = categoryRequest.Target;
                state.Snoozed = categoryRequest.Snoozed;
            }
        }

        if (structureChanged)
        {
            foreach (var otherMonth in await db.Budgets.Where(b => b.BudgetId == budgetId && b.Month != budget.Month).ToListAsync(cancellationToken))
                otherMonth.Version++;
        }

        budget.Note = request.Note.Trim();
        budget.FundsAdded = request.FundsAdded;
        budget.Version++;

        var carryChanges = BudgetCarryForward.CalculateChanges(
            previousReadyToAssign,
            CalculateReadyToAssign(budget),
            previousAvailable,
            budget.CategoryStates,
            previousCreditOverspending);
        if (carryChanges.HasChanges)
        {
            var laterMonths = (await db.LoadPlanMonthsAsync(db.Budgets.Where(b => b.BudgetId == budgetId && string.Compare(b.Month, budget.Month) > 0), cancellationToken))
                .OrderBy(b => b.Month, StringComparer.Ordinal).ToList();
            foreach (var later in laterMonths)
            {
                var oldReadyToAssign = CalculateReadyToAssign(later);
                var oldAvailable = BudgetCarryForward.AvailableById(later);
                var oldCreditOverspending = BudgetCarryForward.CreditOverspendingById(later);
                if (BudgetCarryForward.Apply(later, carryChanges.ReadyToAssignDelta, carryChanges.AvailableDeltas)) later.Version++;
                carryChanges = BudgetCarryForward.CalculateChanges(
                    oldReadyToAssign,
                    CalculateReadyToAssign(later),
                    oldAvailable,
                    later.CategoryStates,
                    oldCreditOverspending);
            }
        }

        // Assigning to a category with credit overspending funds that card spending, so payment
        // categories and their carry-forward must be recalculated with the new assignments.
        if (await projection.HasCreditAccountsAsync(budgetId, cancellationToken))
            await projection.RecalculateAsync(budgetId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(actorUserId))
            RecordPlanActions(budget, actorUserId, previousCategories);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return BudgetUpdateResult.Success(await ToDtoAsync(budget, cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear(); db.ResetCatalogs();
            var latest = await db.LoadPlanMonthAsync(QueryMonth(budgetId, request.Month), cancellationToken);
            return BudgetUpdateResult.Conflict(await ToDtoAsync(latest!, cancellationToken));
        }
    }

    // Month DTO with each category's structured target and its server-calculated progress for that month.
    public async Task<BudgetDto> ToDtoAsync(BudgetEntity budget, CancellationToken cancellationToken)
    {
        var targets = await db.CategoryTargets.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.BudgetId == budget.BudgetId).ToListAsync(cancellationToken);
        if (targets.Count == 0) return await LongTermPlanningPlanValues.AttachPlannedAsync(db, budget,
            await WishlistService.AttachPlanProgressAsync(db, budget, BudgetDto.From(budget), cancellationToken), cancellationToken);
        var history = await LoadTargetHistoryAsync(budget.BudgetId, budget.Month, targets, cancellationToken);
        var categories = budget.Groups.SelectMany(g => g.Categories.Select(c => (Group: g, Category: c))).ToDictionary(x => x.Category.Id, StringComparer.Ordinal);
        var result = new Dictionary<string, (CategoryTargetDto Target, CategoryTargetProgressDto? Progress)>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            if (!categories.TryGetValue(target.CategoryId, out var row)) continue;
            var active = !row.Category.IsArchived && !row.Group.IsArchived && !CategoryTargetRules.IsPaymentCategory(row.Category.Id, row.Group.Id);
            result[target.CategoryId] = (target.ToDto(), active ? CalculateTarget(target, budget.Month, row.Category, history) : null);
        }
        return await LongTermPlanningPlanValues.AttachPlannedAsync(db, budget,
            await WishlistService.AttachPlanProgressAsync(db, budget, BudgetDto.From(budget, result), cancellationToken), cancellationToken);
    }

    public static CategoryTargetProgressDto CalculateTarget(CategoryTargetEntity target, string month, PlanCategory category,
        IReadOnlyDictionary<string, Dictionary<string, decimal>> history) =>
        CategoryTargetRules.Calculate(target.ToDefinition(), month, category.Assigned, category.Available,
            history.TryGetValue(target.CategoryId, out var prior) ? prior : null);

    // Assigned amounts from earlier months, needed only by "set aside" yearly/custom targets.
    public async Task<IReadOnlyDictionary<string, Dictionary<string, decimal>>> LoadTargetHistoryAsync(string budgetId, string month,
        IReadOnlyCollection<CategoryTargetEntity> targets, CancellationToken cancellationToken)
    {
        var dated = targets.Where(x => CategoryTargetRules.NeedsHistory(x.ToDefinition())).ToList();
        if (dated.Count == 0) return new Dictionary<string, Dictionary<string, decimal>>();
        var ids = dated.Select(x => x.CategoryId).ToList();
        var from = dated.Min(x => x.StartMonth) ?? month;
        var rows = await db.CategoryMonths.AsNoTracking()
            .Where(c => c.BudgetId == budgetId && ids.Contains(c.CategoryId) && string.Compare(c.BudgetMonth, from) >= 0 && string.Compare(c.BudgetMonth, month) < 0)
            .Select(c => new { Id = c.CategoryId, c.BudgetMonth, c.Assigned }).ToListAsync(cancellationToken);
        return rows.GroupBy(x => x.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.BudgetMonth, x => x.Assigned, StringComparer.Ordinal), StringComparer.Ordinal);
    }

    public static decimal CalculateReadyToAssign(BudgetEntity budget) =>
        decimal.Round(budget.OpeningReadyToAssign + budget.FundsAdded + budget.LedgerIncome - budget.CategoryStates.Sum(x => x.Assigned), 2, MidpointRounding.ToEven);

    private void RecordPlanActions(BudgetEntity budget, string actorUserId, IReadOnlyDictionary<string, (string Name, decimal Assigned)> previousCategories)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var action in PlanActionRules.Describe(previousCategories, budget.Groups.SelectMany(group => group.Categories)))
        {
            db.PlanActions.Add(new PlanActionEntity
            {
                BudgetId = budget.BudgetId, Month = budget.Month, ActorUserId = actorUserId, Kind = action.Kind,
                Amount = action.Amount, FromCategoryName = action.FromCategoryName, ToCategoryName = action.ToCategoryName, CreatedAt = now
            });
        }
    }

    private IQueryable<BudgetEntity> QueryMonth(string budgetId, string month) => db.Budgets
        .Include(x => x.Plan)
        .Where(x => x.BudgetId == budgetId && x.Month == month);

    // The starter template, seeded once into a budget that has no categories yet (never per month).
    public static void SeedStarterCategories(CategoryCatalog catalog)
    {
        var groups = new (string Id, string Name, (string Id, string Name, string Icon)[] Categories)[]
        {
            ("home", "Home", [("rent", "Rent & mortgage", "⌂"), ("utilities", "Electricity & water", "ϟ"), ("internet", "Internet", "⌁")]),
            ("daily", "Everyday", [("groceries", "Groceries", "▤"), ("transport", "Transport", "↗"), ("coffee", "Coffee & lunch", "☕"), ("health", "Health & pharmacy", "✚")]),
            ("life", "Life & fun", [("dining", "Dining out", "♡"), ("entertainment", "Entertainment", "✦"), ("clothing", "Clothing", "◌")]),
            ("future", "True expenses", [("annual", "Annual bills", "◷"), ("home-repair", "Home maintenance", "⌁"), ("travel", "Next trip", "✈")]),
            ("savings", "Savings goals", [("buffer", "Emergency fund", "◉"), ("gifts", "Gifts", "◇")])
        };
        foreach (var (group, groupOrder) in groups.Select((group, index) => (group, index)))
        {
            catalog.AddGroup(group.Id, group.Name, groupOrder);
            foreach (var (category, categoryOrder) in group.Categories.Select((category, index) => (category, index)))
                catalog.AddCategory(category.Id, group.Id, category.Name, categoryOrder, category.Icon);
        }
    }

    private static string? Validate(BudgetUpdateDto request)
    {
        if (request.Month.Length != 7 || !DateOnly.TryParseExact($"{request.Month}-01", "yyyy-MM-dd", out _))
            return "Month must use YYYY-MM format.";
        if (request.Note is null || request.Note.Length > 1000) return "Note must be 1000 characters or fewer.";
        if (request.Version < 1) return "Budget version is invalid.";
        if (!IsMoney(request.FundsAdded) || request.FundsAdded < 0) return "Funds added must be a non-negative amount with at most two decimal places.";
        if (request.Groups is null || request.Groups.Count > MaxGroups) return "Budget contains too many groups.";
        if (request.Groups.Any(g => string.IsNullOrWhiteSpace(g.Id) || g.Id.Length > 100 || string.IsNullOrWhiteSpace(g.Name) || g.Name.Trim().Length > 120 || g.SortOrder < 0 || g.Categories is null || g.Categories.Count > MaxCategoriesPerGroup))
            return "Group data is invalid.";
        if (request.Groups.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count() != request.Groups.Count)
            return "Group IDs must be unique.";

        var categories = request.Groups.SelectMany(g => g.Categories).ToList();
        if (categories.Any(c => string.IsNullOrWhiteSpace(c.Id) || c.Id.Length > 100 || string.IsNullOrWhiteSpace(c.Name) || c.Name.Trim().Length > 160 || c.SortOrder < 0 || c.Icon?.Length > 24 || !IsMoney(c.Assigned) || !IsMoney(c.Target) || c.Target < 0))
            return "Category data is invalid. Names, IDs, amounts, and ordering must be valid; targets cannot be negative.";
        if (categories.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != categories.Count)
            return "Category IDs must be unique.";
        if (request.Groups.Any(g => g.Categories.Select(c => c.SortOrder).Distinct().Count() != g.Categories.Count))
            return "Category ordering values must be unique within a group.";
        if (request.Groups.Select(g => g.SortOrder).Distinct().Count() != request.Groups.Count)
            return "Group ordering values must be unique.";
        return null;
    }

    private static bool IsMoney(decimal amount) => decimal.Abs(amount) <= MaxMoney && decimal.Round(amount, 2) == amount;
}

