namespace OpenBudget.Api;

public sealed record BudgetPlanDto(string Id, string Name, string Currency, long Version);
public sealed record BudgetPlanUpdateDto(string Name, long Version);

public sealed record BudgetDto(
    string Month,
    string Note,
    long Version,
    decimal OpeningReadyToAssign,
    decimal FundsAdded,
    decimal LedgerIncome,
    decimal ReadyToAssign,
    List<CategoryGroupDto> Groups)
{
    // Targets are keyed by logical category ID; progress is calculated only for active, non-payment categories.
    public static BudgetDto From(BudgetEntity budget, IReadOnlyDictionary<string, (CategoryTargetDto Target, CategoryTargetProgressDto? Progress)>? targets = null) => new(
        budget.Month,
        budget.Note,
        budget.Version,
        budget.OpeningReadyToAssign,
        budget.FundsAdded,
        budget.LedgerIncome,
        BudgetService.CalculateReadyToAssign(budget),
        budget.Groups.OrderBy(g => g.SortOrder).Select(g => new CategoryGroupDto(
            g.Id, g.Name, g.SortOrder, g.IsArchived,
            g.Categories.OrderBy(c => c.SortOrder).Select(c => new CategoryDto(
                c.Id, c.Name, c.Assigned, c.Activity, c.Available, c.Target, c.Icon, c.Snoozed, c.SortOrder, c.IsArchived, c.CreditOverspending,
                targets is not null && targets.TryGetValue(c.Id, out var target) ? target.Target : null,
                targets is not null && targets.TryGetValue(c.Id, out var progress) ? progress.Progress : null)).ToList())).ToList());
}

public sealed record BudgetUpdateDto(
    string Month,
    string Note,
    long Version,
    decimal FundsAdded,
    List<CategoryGroupUpdateDto> Groups);

public sealed record CategoryGroupDto(string Id, string Name, int SortOrder, bool IsArchived, List<CategoryDto> Categories);
// Target is the legacy manual amount kept for compatibility; CategoryTarget/TargetProgress are the structured target
// and its server-calculated progress for this month (null without a target; progress is null for hidden categories).
public sealed record CategoryDto(string Id, string Name, decimal Assigned, decimal Activity, decimal Available, decimal Target,
    string? Icon, bool Snoozed, int SortOrder, bool IsArchived, decimal CreditOverspending = 0,
    CategoryTargetDto? CategoryTarget = null, CategoryTargetProgressDto? TargetProgress = null,
    WishlistProgressDto? WishlistProgress = null, decimal? Planned = null);
public sealed record CategoryGroupUpdateDto(string Id, string Name, int SortOrder, bool IsArchived, List<CategoryUpdateDto> Categories);
public sealed record CategoryUpdateDto(string Id, string Name, decimal Assigned, decimal Target, string? Icon,
    bool Snoozed, int SortOrder, bool IsArchived);

public sealed record BudgetUpdateResult(BudgetDto? Budget, string? Error, bool IsConflict)
{
    public static BudgetUpdateResult Success(BudgetDto budget) => new(budget, null, false);
    public static BudgetUpdateResult Invalid(string error) => new(null, error, false);
    public static BudgetUpdateResult Conflict(BudgetDto budget) => new(budget, null, true);
}

public sealed record BudgetPlanUpdateResult(BudgetPlanDto? Plan, string? Error, bool IsConflict)
{
    public static BudgetPlanUpdateResult Success(BudgetPlanDto plan) => new(plan, null, false);
    public static BudgetPlanUpdateResult Invalid(string error) => new(null, error, false);
    public static BudgetPlanUpdateResult Conflict(BudgetPlanDto plan) => new(plan, null, true);
}
