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
            db.ChangeTracker.Clear();
            var latest = await db.BudgetPlans.SingleAsync(x => x.Id == "default", cancellationToken);
            return BudgetPlanUpdateResult.Conflict(ToPlanDto(latest));
        }
    }

    private static BudgetPlanDto ToPlanDto(BudgetPlanEntity plan) => new(plan.Id, plan.Name, plan.Currency, plan.Version);

    public Task<BudgetEntity> GetMonthAsync(string month, CancellationToken cancellationToken) => GetMonthAsync("default", month, cancellationToken);

    public async Task<BudgetEntity> GetMonthAsync(string budgetId, string month, CancellationToken cancellationToken)
    {
        var existing = await QueryMonth(budgetId, month).SingleOrDefaultAsync(cancellationToken);
        if (existing is not null) return existing;

        var previous = await db.Budgets
            .Where(x => x.BudgetId == budgetId && string.Compare(x.Month, month) < 0)
            .Include(x => x.Plan)
            .Include(x => x.Groups).ThenInclude(x => x.Categories)
            .OrderByDescending(x => x.Month)
            .FirstOrDefaultAsync(cancellationToken);

        // A month before the budget's first one copies the nearest later month's groups and categories (balances start at zero)
        // instead of seeding the starter template, which would add categories the user never created. Only a budget with no
        // months at all starts from the template.
        var later = previous is not null ? null : await db.Budgets
            .Where(x => x.BudgetId == budgetId && string.Compare(x.Month, month) > 0)
            .Include(x => x.Plan)
            .Include(x => x.Groups).ThenInclude(x => x.Categories)
            .OrderBy(x => x.Month)
            .FirstOrDefaultAsync(cancellationToken);

        var created = previous is not null ? CreateNextMonth(month, previous)
            : later is not null ? CreateEarlierMonth(month, later)
            : CreateStarterMonth(month, budgetId);
        if (previous is null && later is null)
            created.Plan = await db.BudgetPlans.SingleAsync(x => x.Id == budgetId, cancellationToken);
        db.Budgets.Add(created);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return await QueryMonth(budgetId, month).SingleAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var concurrent = await QueryMonth(budgetId, month).SingleOrDefaultAsync(cancellationToken);
            if (concurrent is not null) return concurrent;
            throw;
        }
    }

    public Task<BudgetUpdateResult> UpdateMonthAsync(BudgetUpdateDto request, CancellationToken cancellationToken) => UpdateMonthAsync("default", request, null, cancellationToken);

    public Task<BudgetUpdateResult> UpdateMonthAsync(string budgetId, BudgetUpdateDto request, CancellationToken cancellationToken) =>
        UpdateMonthAsync(budgetId, request, null, cancellationToken);

    public async Task<BudgetUpdateResult> UpdateMonthAsync(string budgetId, BudgetUpdateDto request, string? actorUserId, CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null) return BudgetUpdateResult.Invalid(validation);

        var budget = await GetMonthAsync(budgetId, request.Month, cancellationToken);
        if (budget.Version != request.Version)
            return BudgetUpdateResult.Conflict(await ToDtoAsync(budget, cancellationToken));
        if (budget.FundsAdded != request.FundsAdded)
            return BudgetUpdateResult.Invalid("Funds added is a preserved legacy baseline. Record new income through a cash account transaction.");

        var currentGroups = budget.Groups.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var currentCategories = budget.Groups.SelectMany(g => g.Categories)
            .ToDictionary(x => x.Id, StringComparer.Ordinal);
        var requestedCategoryIds = request.Groups.SelectMany(g => g.Categories.Select(c => c.Id)).ToHashSet(StringComparer.Ordinal);

        // Preserve historical category records; archive them instead of deleting their month state.
        if (currentGroups.Keys.Any(id => request.Groups.All(g => g.Id != id)) ||
            currentCategories.Keys.Any(id => !requestedCategoryIds.Contains(id)))
            return BudgetUpdateResult.Invalid("Existing groups and categories must be archived instead of removed.");
        if (WishlistRules.ValidatePlanUpdate(budget, request.Groups) is { } wishlistError)
            return BudgetUpdateResult.Invalid(wishlistError);

        var previousReadyToAssign = CalculateReadyToAssign(budget);
        var previousAvailable = budget.Groups.SelectMany(g => g.Categories).ToDictionary(c => c.Id, c => c.Available);
        var previousCreditOverspending = budget.Groups.SelectMany(g => g.Categories).ToDictionary(c => c.Id, c => c.CreditOverspending);
        var previousCategories = budget.Groups.SelectMany(g => g.Categories).ToDictionary(c => c.Id, c => (c.Name, c.Assigned));
        var orderingChanged = false;

        foreach (var groupRequest in request.Groups)
        {
            if (!currentGroups.TryGetValue(groupRequest.Id, out var group))
            {
                group = new CategoryGroupEntity { BudgetId = budgetId, BudgetMonth = request.Month, Id = groupRequest.Id };
                budget.Groups.Add(group);
                currentGroups.Add(group.Id, group);
                orderingChanged = true;
            }
            else if (group.SortOrder != groupRequest.SortOrder)
            {
                orderingChanged = true;
            }
            group.Name = groupRequest.Name.Trim();
            group.SortOrder = groupRequest.SortOrder;
            group.IsArchived = groupRequest.IsArchived;

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
                    category = new CategoryEntity { BudgetId = budgetId, BudgetMonth = request.Month, GroupId = groupRequest.Id, Id = categoryRequest.Id };
                    group.Categories.Add(category);
                    currentCategories.Add(category.Id, category);
                    orderingChanged = true;
                }
                else if (category.GroupId != groupRequest.Id)
                {
                    var previousGroup = currentGroups[category.GroupId];
                    if (previousGroup.IsArchived || group.IsArchived || category.IsArchived)
                        return BudgetUpdateResult.Invalid("An archived category cannot be moved until it is restored.");
                    category.GroupId = groupRequest.Id;
                    orderingChanged = true;
                }
                else if ((group.IsArchived || category.IsArchived) && categoryRequest.Assigned != category.Assigned)
                {
                    return BudgetUpdateResult.Invalid("An archived category cannot be assigned until it is restored.");
                }

                if (category.SortOrder != categoryRequest.SortOrder)
                    orderingChanged = true;

                category.Name = categoryRequest.Name.Trim();
                category.Assigned = categoryRequest.Assigned;
                category.Target = categoryRequest.Target;
                category.Icon = categoryRequest.Icon;
                category.Snoozed = categoryRequest.Snoozed;
                category.SortOrder = categoryRequest.SortOrder;
                category.IsArchived = categoryRequest.IsArchived;
            }
        }

        if (orderingChanged)
        {
            var otherMonths = await db.Budgets
                .Where(b => b.BudgetId == budgetId && b.Month != budget.Month)
                .Include(b => b.Groups).ThenInclude(g => g.Categories)
                .ToListAsync(cancellationToken);
            foreach (var otherMonth in otherMonths)
            {
                if (ApplySharedOrdering(otherMonth, request.Groups))
                    otherMonth.Version++;
            }
        }

        budget.Note = request.Note.Trim();
        budget.FundsAdded = request.FundsAdded;
        budget.Version++;

        var carryChanges = BudgetCarryForward.CalculateChanges(
            previousReadyToAssign,
            CalculateReadyToAssign(budget),
            previousAvailable,
            budget.Groups.SelectMany(g => g.Categories),
            previousCreditOverspending);
        if (carryChanges.HasChanges)
        {
            var laterMonths = await db.Budgets.Where(b => b.BudgetId == budgetId && string.Compare(b.Month, budget.Month) > 0)
                .Include(b => b.Groups).ThenInclude(g => g.Categories).OrderBy(b => b.Month).ToListAsync(cancellationToken);
            var source = budget;
            foreach (var later in laterMonths)
            {
                var oldReadyToAssign = CalculateReadyToAssign(later);
                var oldAvailable = later.Groups.SelectMany(g => g.Categories).ToDictionary(c => c.Id, c => c.Available);
                var oldCreditOverspending = later.Groups.SelectMany(g => g.Categories).ToDictionary(c => c.Id, c => c.CreditOverspending);
                if (BudgetCarryForward.Apply(later, carryChanges.ReadyToAssignDelta, carryChanges.AvailableDeltas, source)) later.Version++;
                carryChanges = BudgetCarryForward.CalculateChanges(
                    oldReadyToAssign,
                    CalculateReadyToAssign(later),
                    oldAvailable,
                    later.Groups.SelectMany(g => g.Categories),
                    oldCreditOverspending);
                source = later;
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
            db.ChangeTracker.Clear();
            var latest = await QueryMonth(budgetId, request.Month).SingleAsync(cancellationToken);
            return BudgetUpdateResult.Conflict(await ToDtoAsync(latest, cancellationToken));
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

    public static CategoryTargetProgressDto CalculateTarget(CategoryTargetEntity target, string month, CategoryEntity category,
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
        var rows = await db.Categories.AsNoTracking()
            .Where(c => c.BudgetId == budgetId && ids.Contains(c.Id) && string.Compare(c.BudgetMonth, from) >= 0 && string.Compare(c.BudgetMonth, month) < 0)
            .Select(c => new { c.Id, c.BudgetMonth, c.Assigned }).ToListAsync(cancellationToken);
        return rows.GroupBy(x => x.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.BudgetMonth, x => x.Assigned, StringComparer.Ordinal), StringComparer.Ordinal);
    }

    public static decimal CalculateReadyToAssign(BudgetEntity budget) =>
        decimal.Round(budget.OpeningReadyToAssign + budget.FundsAdded + budget.LedgerIncome - budget.Groups.SelectMany(x => x.Categories).Sum(x => x.Assigned), 2, MidpointRounding.ToEven);

    private static bool ApplySharedOrdering(BudgetEntity target, IReadOnlyList<CategoryGroupUpdateDto> sourceGroups)
    {
        var targetGroups = target.Groups.ToDictionary(group => group.Id, StringComparer.Ordinal);
        var targetCategories = target.Groups.SelectMany(group => group.Categories).ToDictionary(category => category.Id, StringComparer.Ordinal);
        var changed = false;

        var orderedGroups = sourceGroups.OrderBy(group => group.SortOrder)
            .Where(group => targetGroups.ContainsKey(group.Id))
            .Select(group => targetGroups[group.Id])
            .Concat(target.Groups.OrderBy(group => group.SortOrder).Where(group => !sourceGroups.Any(source => source.Id == group.Id)))
            .ToList();
        for (var index = 0; index < orderedGroups.Count; index++)
        {
            if (orderedGroups[index].SortOrder != index) changed = true;
            orderedGroups[index].SortOrder = index;
        }

        foreach (var sourceGroup in sourceGroups)
        {
            if (!targetGroups.ContainsKey(sourceGroup.Id)) continue;
            foreach (var sourceCategory in sourceGroup.Categories)
            {
                if (targetCategories.TryGetValue(sourceCategory.Id, out var targetCategory) && targetCategory.GroupId != sourceGroup.Id)
                {
                    targetCategory.GroupId = sourceGroup.Id;
                    changed = true;
                }
            }
        }

        foreach (var targetGroup in target.Groups)
        {
            var categoriesInGroup = targetCategories.Values.Where(category => category.GroupId == targetGroup.Id).ToList();
            var sourceCategoryIds = sourceGroups.Where(group => group.Id == targetGroup.Id)
                .SelectMany(group => group.Categories.OrderBy(category => category.SortOrder).Select(category => category.Id))
                .Where(targetCategories.ContainsKey)
                .ToHashSet(StringComparer.Ordinal);
            var orderedCategories = sourceGroups.Where(group => group.Id == targetGroup.Id)
                .SelectMany(group => group.Categories.OrderBy(category => category.SortOrder))
                .Where(category => targetCategories.ContainsKey(category.Id) && targetCategories[category.Id].GroupId == targetGroup.Id)
                .Select(category => targetCategories[category.Id])
                .Concat(categoriesInGroup.Where(category => !sourceCategoryIds.Contains(category.Id)).OrderBy(category => category.SortOrder))
                .ToList();

            for (var index = 0; index < orderedCategories.Count; index++)
            {
                if (orderedCategories[index].SortOrder != index) changed = true;
                orderedCategories[index].SortOrder = index;
            }
        }

        return changed;
    }

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
        .Include(x => x.Groups).ThenInclude(x => x.Categories)
        .Where(x => x.BudgetId == budgetId && x.Month == month);

    private static BudgetEntity CreateNextMonth(string month, BudgetEntity previous) => new()
    {
        Month = month,
        BudgetId = previous.BudgetId,
        Plan = previous.Plan,
        Note = "",
        OpeningReadyToAssign = BudgetCarryForward.OpeningReadyToAssign(previous),
        FundsAdded = 0,
        Version = 1,
        Groups = previous.Groups.OrderBy(x => x.SortOrder).Select(group => new CategoryGroupEntity
        {
            BudgetId = previous.BudgetId,
            BudgetMonth = month,
            Id = group.Id,
            Name = group.Name,
            SortOrder = group.SortOrder,
            IsArchived = group.IsArchived,
            Categories = group.Categories.OrderBy(x => x.SortOrder).Select(category => new CategoryEntity
            {
                BudgetId = previous.BudgetId,
                BudgetMonth = month,
                GroupId = group.Id,
                Id = category.Id,
                Name = category.Name,
                OpeningAvailable = BudgetCarryForward.OpeningAvailable(category.Available),
                Assigned = 0,
                Activity = 0,
                Target = category.Target,
                Icon = category.Icon,
                Snoozed = category.Snoozed,
                SortOrder = category.SortOrder,
                IsArchived = category.IsArchived
            }).ToList()
        }).ToList()
    };

    // Structure-only copy used for a month that precedes every existing month: same groups, categories, targets and order,
    // but no assigned, activity or carried-over amounts.
    private static BudgetEntity CreateEarlierMonth(string month, BudgetEntity later) => new()
    {
        Month = month,
        BudgetId = later.BudgetId,
        Plan = later.Plan,
        Note = "",
        OpeningReadyToAssign = 0,
        FundsAdded = 0,
        Version = 1,
        Groups = later.Groups.OrderBy(x => x.SortOrder).Select(group => new CategoryGroupEntity
        {
            BudgetId = later.BudgetId,
            BudgetMonth = month,
            Id = group.Id,
            Name = group.Name,
            SortOrder = group.SortOrder,
            IsArchived = group.IsArchived,
            Categories = group.Categories.OrderBy(x => x.SortOrder).Select(category => new CategoryEntity
            {
                BudgetId = later.BudgetId,
                BudgetMonth = month,
                GroupId = group.Id,
                Id = category.Id,
                Name = category.Name,
                OpeningAvailable = 0,
                Assigned = 0,
                Activity = 0,
                Target = category.Target,
                Icon = category.Icon,
                Snoozed = category.Snoozed,
                SortOrder = category.SortOrder,
                IsArchived = category.IsArchived
            }).ToList()
        }).ToList()
    };

    private static BudgetEntity CreateStarterMonth(string month, string budgetId)
    {
        var groups = new (string Id, string Name, (string Id, string Name, string Icon)[] Categories)[]
        {
            ("home", "Home", [("rent", "Rent & mortgage", "⌂"), ("utilities", "Electricity & water", "ϟ"), ("internet", "Internet", "⌁")]),
            ("daily", "Everyday", [("groceries", "Groceries", "▤"), ("transport", "Transport", "↗"), ("coffee", "Coffee & lunch", "☕"), ("health", "Health & pharmacy", "✚")]),
            ("life", "Life & fun", [("dining", "Dining out", "♡"), ("entertainment", "Entertainment", "✦"), ("clothing", "Clothing", "◌")]),
            ("future", "True expenses", [("annual", "Annual bills", "◷"), ("home-repair", "Home maintenance", "⌁"), ("travel", "Next trip", "✈")]),
            ("savings", "Savings goals", [("buffer", "Emergency fund", "◉"), ("gifts", "Gifts", "◇")])
        };

        return new BudgetEntity
        {
            Month = month,
            BudgetId = budgetId,
            Note = "",
            Version = 1,
            Groups = groups.Select((group, groupOrder) => new CategoryGroupEntity
            {
                BudgetId = budgetId,
                BudgetMonth = month,
                Id = group.Id,
                Name = group.Name,
                SortOrder = groupOrder,
                Categories = group.Categories.Select((category, categoryOrder) => new CategoryEntity
                {
                    BudgetId = budgetId,
                    BudgetMonth = month,
                    GroupId = group.Id,
                    Id = category.Id,
                    Name = category.Name,
                    Icon = category.Icon,
                    SortOrder = categoryOrder
                }).ToList()
            }).ToList()
        };
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

