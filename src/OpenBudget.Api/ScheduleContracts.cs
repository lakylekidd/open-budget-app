namespace OpenBudget.Api;

public sealed record ScheduleTemplateDto(decimal Amount, string? Payee, string? Memo, string? CategoryId, List<SplitDto>? Splits);
public sealed record ScheduleRequest(Guid CommandId, long Version, Guid AccountId, DateOnly Date, decimal Amount, string? Payee,
    string? Memo, string? CategoryId, List<SplitDto>? Splits, string Repeat = "never", Guid? ToAccountId = null,
    bool IsPaused = false, DateOnly? EndDate = null, int? Count = null, string? FlagColor = null);
public sealed record ScheduleActionRequest(Guid CommandId, long Version, string Action, Guid OccurrenceId,
    DateOnly? Date = null, ScheduleTemplateDto? Template = null, bool AutomaticallyPosted = false);
public sealed record ScheduledItemDto(Guid Id, Guid OccurrenceId, string Repeat, DateOnly StartDate, bool IsPaused,
    DateOnly? EndDate, int? Count, Guid? ToAccountId, TransactionDto Transaction, string? PostingError = null);
public sealed record SchedulesDto(long Version, int Page, int PageSize, int Total, List<ScheduledItemDto> Items);
