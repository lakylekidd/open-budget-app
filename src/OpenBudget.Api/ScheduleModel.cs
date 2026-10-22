namespace OpenBudget.Api;

public sealed class ScheduleEntity
{
    public string? PostingError { get; set; }
    public Guid Id { get; set; }
    public string? FlagColor { get; set; }
    public Guid AccountId { get; set; }
    public Guid? ToAccountId { get; set; }
    public DateOnly AnchorDate { get; set; }
    public DateOnly? NextDate { get; set; }
    public int RecurrenceIndex { get; set; }
    public int Sequence { get; set; }
    public string Repeat { get; set; } = "never";
    public string TemplateJson { get; set; } = "";
    public bool IsPaused { get; set; }
    public bool IsEnded { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? Count { get; set; }
}
public sealed class ScheduleOccurrenceEntity
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public int Sequence { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? DeferredDate { get; set; }
    public string? OverrideJson { get; set; }
    public string Disposition { get; set; } = "pending";
    public Guid? TransactionId { get; set; }
}
