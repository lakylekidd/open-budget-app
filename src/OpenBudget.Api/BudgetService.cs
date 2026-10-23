using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed class BudgetService(BudgetDbContext db)
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

        var created = previous is null ? CreateStarterMonth(month, budgetId) : CreateNextMonth(month, previous);
        if (previous is null)
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

    public Task<BudgetUpdateResult> UpdateMonthAsync(BudgetUpdateDto request, CancellationToken cancellationToken) => UpdateMonthAsync("default", request, cancellationToken);

    public async Task<BudgetUpdateResult> UpdateMonthAsync(string budgetId, BudgetUpdateDto request, CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null) return BudgetUpdateResult.Invalid(validation);

        var budget = await GetMonthAsync(budgetId, request.Month, cancellationToken);
        if (budget.Version != request.Version)
            return BudgetUpdateResult.Conflict(BudgetDto.From(budget));
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

        var previousReadyToAssign = CalculateReadyToAssign(budget);
        var previousAvailable = budget.Groups.SelectMany(g => g.Categories).ToDictionary(c => c.Id, c => c.Available);

        foreach (var groupRequest in request.Groups)
        {
            if (!currentGroups.TryGetValue(groupRequest.Id, out var group))
            {
                group = new CategoryGroupEntity { BudgetId = budgetId, BudgetMonth = request.Month, Id = groupRequest.Id };
                budget.Groups.Add(group);
                currentGroups.Add(group.Id, group);
            }
            group.Name = groupRequest.Name.Trim();
            group.SortOrder = groupRequest.SortOrder;
            group.IsArchived = groupRequest.IsArchived;

            foreach (var categoryRequest in groupRequest.Categories)
            {
                if (!currentCategories.TryGetValue(categoryRequest.Id, out var category))
                {
                    if (categoryRequest.Assigned != 0)
                        return BudgetUpdateResult.Invalid("A new category must start with zero assigned.");
                    category = new CategoryEntity { BudgetId = budgetId, BudgetMonth = request.Month, GroupId = groupRequest.Id, Id = categoryRequest.Id };
                    group.Categories.Add(category);
                    currentCategories.Add(category.Id, category);
                }
                else if (category.GroupId != groupRequest.Id)
                {
                    var previousGroup = currentGroups[category.GroupId];
                    if (previousGroup.IsArchived || group.IsArchived || category.IsArchived)
                        return BudgetUpdateResult.Invalid("An archived category cannot be moved until it is restored.");
                    category.GroupId = groupRequest.Id;
                }
                else if ((group.IsArchived || category.IsArchived) && categoryRequest.Assigned != category.Assigned)
                {
                    return BudgetUpdateResult.Invalid("An archived category cannot be assigned until it is restored.");
                }

                category.Name = categoryRequest.Name.Trim();
                category.Assigned = categoryRequest.Assigned;
                category.Target = categoryRequest.Target;
                category.Icon = categoryRequest.Icon;
                category.Snoozed = categoryRequest.Snoozed;
                category.SortOrder = categoryRequest.SortOrder;
                category.IsArchived = categoryRequest.IsArchived;
            }
        }

        budget.Note = request.Note.Trim();
        budget.FundsAdded = request.FundsAdded;
        budget.Version++;

        var readyDelta = CalculateReadyToAssign(budget) - previousReadyToAssign;
        var availableDeltas = budget.Groups.SelectMany(g => g.Categories)
            .ToDictionary(c => c.Id, c => c.Available - previousAvailable.GetValueOrDefault(c.Id));
        if (readyDelta != 0 || availableDeltas.Values.Any(delta => delta != 0))
        {
            var laterMonths = await db.Budgets.Where(b => b.BudgetId == budgetId && string.Compare(b.Month, budget.Month) > 0)
                .Include(b => b.Groups).ThenInclude(g => g.Categories).OrderBy(b => b.Month).ToListAsync(cancellationToken);
            foreach (var later in laterMonths)
                if (BudgetCarryForward.Apply(later, readyDelta, availableDeltas, budget)) later.Version++;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return BudgetUpdateResult.Success(BudgetDto.From(budget));
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            var latest = await QueryMonth(budgetId, request.Month).SingleAsync(cancellationToken);
            return BudgetUpdateResult.Conflict(BudgetDto.From(latest));
        }
    }

    public static decimal CalculateReadyToAssign(BudgetEntity budget) =>
        decimal.Round(budget.OpeningReadyToAssign + budget.FundsAdded + budget.LedgerIncome - budget.Groups.SelectMany(x => x.Categories).Sum(x => x.Assigned), 2, MidpointRounding.ToEven);

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
        OpeningReadyToAssign = CalculateReadyToAssign(previous),
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
                OpeningAvailable = category.Available,
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

