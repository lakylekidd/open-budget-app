namespace OpenBudget.Api;

// Version is 0 when creating a target and the current target version when editing it. Month is the Plan month the
// editor was opened from; the category must exist and be active there, and the response progress is for that month.
public sealed record SaveCategoryTargetRequest(Guid CommandId, long Version, string Month, string Kind, decimal Amount, string Behavior,
    int? Weekday = null, int? DayOfMonth = null, DateOnly? DueDate = null, int? RepeatEvery = null, string? RepeatUnit = null);

public sealed record DeleteCategoryTargetRequest(Guid CommandId, long Version, string Month);

public sealed record CategoryTargetDto(string Kind, decimal Amount, string Behavior, int? Weekday, int? DayOfMonth, DateOnly? DueDate,
    int? RepeatEvery, string? RepeatUnit, string StartMonth, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

// Status is funded (nothing more to assign this month) or underfunded. Segments is the number of equal bar segments
// (weekday occurrences for weekly targets, otherwise 1). Cadence is a short non-monetary description of the schedule.
public sealed record CategoryTargetProgressDto(decimal Needed, decimal Funded, decimal MoreNeeded, int Segments, decimal FundedFraction,
    string Status, DateOnly? NextDueDate, int? MonthsRemaining, bool IsPastDue, string Cadence);

public sealed record CategoryTargetResultDto(string CategoryId, string Month, CategoryTargetDto? Target, CategoryTargetProgressDto? Progress);

public sealed record CategoryTargetCommandResult(CategoryTargetResultDto? Value, string? Error, int StatusCode)
{
    public static CategoryTargetCommandResult Success(CategoryTargetResultDto value) => new(value, null, 200);
    public static CategoryTargetCommandResult Fail(string error, int statusCode = 400) => new(null, error, statusCode);
    public static CategoryTargetCommandResult Conflict(CategoryTargetResultDto current) => new(current, "This target changed in another session. Reload and review it.", 409);
}

public sealed class CategoryTargetEntity
{
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    // Logical category ID, the same in every month snapshot.
    public string CategoryId { get; set; } = "";
    // Stable resource ID used by command receipts/audit.
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = CategoryTargetRules.Monthly;
    public decimal Amount { get; set; }
    public string Behavior { get; set; } = CategoryTargetRules.SetAside;
    public int? Weekday { get; set; }
    // Null means the last day of the month.
    public int? DayOfMonth { get; set; }
    public DateOnly? DueDate { get; set; }
    public int? RepeatEvery { get; set; }
    public string? RepeatUnit { get; set; }
    // YYYY-MM of creation; "set aside" date targets count Assigned from this month onward.
    public string StartMonth { get; set; } = "";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public CategoryTargetDefinition ToDefinition() => new(Kind, Amount, Behavior, StartMonth, Weekday, DayOfMonth, DueDate, RepeatEvery, RepeatUnit);

    public CategoryTargetDto ToDto() => new(Kind, Amount, Behavior, Weekday, DayOfMonth, DueDate, RepeatEvery, RepeatUnit, StartMonth, Version, CreatedAt, UpdatedAt);
}
