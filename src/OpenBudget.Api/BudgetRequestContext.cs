namespace OpenBudget.Api;

/// <summary>Identifies the financial data partition for one request. Background work uses the legacy default budget.</summary>
public sealed class BudgetRequestContext
{
    public const string DefaultBudgetId = "default";
    public string BudgetId { get; internal set; } = DefaultBudgetId;
}
