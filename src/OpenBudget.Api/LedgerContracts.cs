namespace OpenBudget.Api;

public sealed record AccountDto(Guid Id, string Name, bool IsClosed, decimal Balance, decimal ClearedBalance, decimal UnclearedBalance,
    string AccountType, bool IsFavorite, string ConnectionStatus, DateTimeOffset? LastReconciledAt, int NeedsCategoryCount, string Notes = "", int UnapprovedCount = 0, int AttentionCount = 0);
public sealed record AccountsDto(long Version, List<AccountDto> Accounts);
public sealed record SplitDto(string CategoryId, decimal Amount);
public sealed record TransactionDto(Guid Id, Guid AccountId, DateOnly Date, decimal Amount, string Payee, string Memo,
    string? CategoryId, string Status, Guid? TransferId, bool IsOpening, List<SplitDto> Splits, bool NeedsAttention,
    decimal? RunningBalance = null, string? TransferAccountName = null, string? CategoryName = null, bool IsLocked = false, string? FlagColor = null, Guid? ScheduleId = null, bool CanEdit = true, bool IsApproved = true, bool NeedsCategory = false);
public sealed record RegisterDto(long Version, int Page, int PageSize, int Total, List<TransactionDto> Items, AccountsDto Accounts);
public sealed record CreateAccountRequest(Guid CommandId, long Version, string Name, DateOnly Date, decimal OpeningBalance, string AccountType = "checking");
public sealed record UpdateAccountRequest(Guid CommandId, long Version, string Name, bool IsClosed, string? AccountType = null, bool? IsFavorite = null, string? Notes = null, decimal? WorkingBalance = null);
public sealed record TransactionRequest(Guid CommandId, long Version, Guid AccountId, DateOnly Date, decimal Amount,
    string? Payee, string? Memo, string? CategoryId, string Status, List<SplitDto>? Splits, string Repeat = "never", bool ConfirmReconciledEdit = false);
public sealed record TransferRequest(Guid CommandId, long Version, Guid FromAccountId, Guid ToAccountId, DateOnly Date,
    decimal Amount, string? Memo, string Repeat = "never", bool ConfirmReconciledEdit = false);
public sealed record DeleteTransactionRequest(Guid CommandId, long Version);
public sealed record StatusRequest(Guid CommandId, long Version, string Status);
public sealed record ReconcileRequest(Guid CommandId, long Version, decimal StatementBalance);
public sealed record MutationDto(Guid Id, long Version);
public sealed record AuditDto(Guid Id, Guid ResourceId, string Kind, long Version, DateTimeOffset CreatedAt, string BeforeJson, string AfterJson);
public sealed record AuditPageDto(int Page, int PageSize, int Total, List<AuditDto> Items);

public sealed class LedgerException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed record RegisterViewDto(long Version, string DatePreset, string? FromMonth, string? ToMonth,
    bool ShowReconciled, bool ShowRunningBalance, bool ShowFlags, bool ShowImages);


public sealed record ReviewTransactionRequest(Guid CommandId, long Version, string Action);
