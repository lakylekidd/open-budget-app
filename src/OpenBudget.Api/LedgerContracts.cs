namespace OpenBudget.Api;

public sealed record AccountDto(Guid Id, string Name, bool IsClosed, decimal Balance, decimal ClearedBalance, decimal UnclearedBalance,
    string AccountType, bool IsFavorite, string ConnectionStatus, DateTimeOffset? LastReconciledAt, int NeedsCategoryCount, string Notes = "", int UnapprovedCount = 0, int AttentionCount = 0, bool HasTransactions = false);
public sealed record AccountsDto(long Version, List<AccountDto> Accounts);
public sealed record SplitDto(string CategoryId, decimal Amount, string? Payee = null, string? Memo = null);
public sealed record TransactionDto(Guid Id, Guid AccountId, DateOnly Date, decimal Amount, string Payee, string Memo,
    string? CategoryId, string Status, Guid? TransferId, bool IsOpening, List<SplitDto> Splits, bool NeedsAttention,
    decimal? RunningBalance = null, string? TransferAccountName = null, string? CategoryName = null, bool IsLocked = false, string? FlagColor = null, Guid? ScheduleId = null, bool CanEdit = true, bool IsApproved = true, bool NeedsCategory = false, int ImageCount = 0);
public sealed record RegisterDto(long Version, int Page, int PageSize, int Total, List<TransactionDto> Items, AccountsDto Accounts);
public sealed record CreateAccountRequest(Guid CommandId, long Version, string Name, DateOnly Date, decimal OpeningBalance, string AccountType = "checking", LoanTermsRequest? Loan = null);
public sealed record UpdateAccountRequest(Guid CommandId, long Version, string Name, bool IsClosed, string? AccountType = null, bool? IsFavorite = null, string? Notes = null, decimal? WorkingBalance = null, LoanTermsRequest? Loan = null);
public sealed record TransactionRequest(Guid CommandId, long Version, Guid AccountId, DateOnly Date, decimal Amount,
    string? Payee, string? Memo, string? CategoryId, string Status, List<SplitDto>? Splits, string Repeat = "never", bool ConfirmReconciledEdit = false);
public sealed record TransferRequest(Guid CommandId, long Version, Guid FromAccountId, Guid ToAccountId, DateOnly Date,
    decimal Amount, string? Memo, string Repeat = "never", bool ConfirmReconciledEdit = false);
public sealed record DeleteTransactionRequest(Guid CommandId, long Version);
public sealed record DeleteAccountRequest(Guid CommandId, long Version);
public sealed record StatusRequest(Guid CommandId, long Version, string Status);
public sealed record ReconcileRequest(Guid CommandId, long Version, decimal StatementBalance);
public sealed record MutationDto(Guid Id, long Version);
public sealed record CreditSummaryDto(Guid AccountId, string Month, decimal PriorBalance, decimal SpendingAndOutflows, decimal InterestCharges,
    decimal PaymentsAndInflows, decimal CurrentBalance);
// Month breakdown of one credit card's categorized activity; definitions live in docs/credit-card-activity.md.
public sealed record CreditActivityDto(Guid AccountId, string Month, decimal Spending, decimal Returns, decimal TotalSpending,
    decimal FundedSpending, decimal PaymentsAndReturns, decimal TotalActivity);
public sealed record SaveCreditPayoffTargetRequest(Guid CommandId, long Version, string Kind, string? TargetMonth = null, decimal? MonthlyAmount = null);
public sealed record DeleteCreditPayoffTargetRequest(Guid CommandId, long Version);
public sealed record CreditPayoffTargetDto(string Kind, string? TargetMonth, decimal? MonthlyAmount, decimal PayoffAmount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
// Status is funded (nothing to go), onTrack (nothing more to assign this month) or behind.
public sealed record CreditPayoffProgressDto(decimal NeededForPayoff, decimal AvailableForPayment, decimal AssignedThisMonth, decimal ToGo,
    int Percent, decimal AssignThisMonth, int? MonthsRemaining, bool IsPastDue, string Status);
public sealed record CreditPayoffDto(Guid AccountId, string Month, long Version, CreditPayoffTargetDto? Target, CreditPayoffProgressDto? Progress);
public sealed record AuditDto(Guid Id, Guid ResourceId, string Kind, long Version, DateTimeOffset CreatedAt, string BeforeJson, string AfterJson);
public sealed record AuditPageDto(int Page, int PageSize, int Total, List<AuditDto> Items);

public sealed class LedgerException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed record RegisterViewDto(long Version, string DatePreset, string? FromMonth, string? ToMonth,
    bool ShowReconciled, bool ShowRunningBalance, bool ShowFlags, bool ShowImages, Dictionary<string, int>? ColumnWidths = null,
    bool ShowScheduledTransactions = true)
{
    public bool Equals(RegisterViewDto? other) => other is not null && Version == other.Version && DatePreset == other.DatePreset &&
        FromMonth == other.FromMonth && ToMonth == other.ToMonth && ShowReconciled == other.ShowReconciled &&
        ShowRunningBalance == other.ShowRunningBalance && ShowFlags == other.ShowFlags && ShowImages == other.ShowImages &&
        ShowScheduledTransactions == other.ShowScheduledTransactions &&
        (ColumnWidths ?? []).OrderBy(x => x.Key).SequenceEqual((other.ColumnWidths ?? []).OrderBy(x => x.Key));

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Version); hash.Add(DatePreset); hash.Add(FromMonth); hash.Add(ToMonth); hash.Add(ShowReconciled);
        hash.Add(ShowRunningBalance); hash.Add(ShowFlags); hash.Add(ShowImages); hash.Add(ShowScheduledTransactions);
        foreach (var pair in (ColumnWidths ?? []).OrderBy(x => x.Key)) { hash.Add(pair.Key); hash.Add(pair.Value); }
        return hash.ToHashCode();
    }
}


public sealed record ReviewTransactionRequest(Guid CommandId, long Version, string Action);
