namespace OpenBudget.Api;

public sealed record LongTermPlanningDto(
    int Year,
    long Version,
    bool HasPlanningData,
    List<LongTermPlanningMonthDto> Months,
    List<LongTermPlanningGroupDto> Groups);

public sealed record LongTermPlanningActivityDto(List<TransactionDto> Scheduled);

public sealed record LongTermPlanningMonthDto(
    string Month,
    decimal ExpectedIncome,
    bool HasExpectedIncome,
    decimal ExpectedExpenses,
    decimal Remaining,
    decimal RemainingToBeAssigned,
    bool HasActualData);

public sealed record LongTermPlanningGroupDto(
    string Id,
    string Name,
    int SortOrder,
    bool IsArchived,
    List<LongTermPlanningCategoryDto> Categories);

public sealed record LongTermPlanningCategoryDto(
    string Id,
    string Name,
    int SortOrder,
    bool IsArchived,
    List<LongTermPlanningCategoryMonthDto> Months);

public sealed record LongTermPlanningCategoryMonthDto(
    string Month,
    decimal? PlannedBudgetOverride,
    decimal PlannedBudgetBeforeMonth,
    decimal PlannedBudget,
    decimal PlannedExpenses,
    decimal PlannedAvailable,
    decimal? ActualAssigned,
    decimal? ActualExpenses,
    decimal? ActualAvailable);

public sealed record LongTermPlanningUpdateDto(
    long Version,
    string Month,
    string Field,
    string? CategoryId,
    decimal? Amount);

public sealed record LongTermPlanningUpdateResult(LongTermPlanningDto? Plan, string? Error, bool IsConflict)
{
    public static LongTermPlanningUpdateResult Success(LongTermPlanningDto plan) => new(plan, null, false);
    public static LongTermPlanningUpdateResult Invalid(string error) => new(null, error, false);
    public static LongTermPlanningUpdateResult Conflict(LongTermPlanningDto plan) => new(plan, null, true);
}
