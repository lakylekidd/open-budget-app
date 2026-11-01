namespace OpenBudget.Api;

public static class LedgerRules
{
    public static bool IsMoney(decimal amount) => amount is >= -999_999_999.99m and <= 999_999_999.99m && decimal.Round(amount, 2) == amount;

    public static string? Validate(TransactionRequest request)
    {
        if (request.Date.Year is < 1900 or > 9998) return "Date must be between 1900 and 9998.";
        if (!IsMoney(request.Amount) || request.Amount == 0) return "Amount must be nonzero with at most two decimal places.";
        if (request.Status is not ("uncleared" or "cleared")) return "Status must be uncleared or cleared; use reconciliation to reconcile entries.";
        if (request.Payee?.Trim().Length > 160 || request.Memo?.Length > 1000) return "Payee or memo is too long.";
        if (request.CategoryId?.Length > 100) return "Category identifier is too long.";
        var splits = request.Splits ?? [];
        if (splits.Count > 50 || splits.Any(s => s is null || string.IsNullOrWhiteSpace(s.CategoryId) || s.CategoryId.Length > 100 || !IsMoney(s.Amount) || s.Amount == 0 || s.Payee?.Length > 160 || s.Memo?.Length > 1000))
            return "Splits require a category and a nonzero money amount (maximum 50 splits).";
        if (splits.Count > 0 && (!string.IsNullOrEmpty(request.CategoryId) || splits.Sum(s => s.Amount) != request.Amount))
            return "Split amounts must equal the transaction amount and replace its single category.";
        return null;
    }

    // Tracking accounts (assets such as investments, and liabilities) sit outside the budget: their own activity never touches
    // Ready to Assign or categories. Money moving between a tracking account and a budget account is categorized on the budget side.
    public const string TrackingAsset = "trackingAsset", TrackingLiability = "trackingLiability";
    public static readonly string[] TrackingTypes = [TrackingAsset, TrackingLiability];
    public static bool IsTracking(string? accountType) => accountType is TrackingAsset or TrackingLiability;

    public static decimal Income(TransactionEntity entry) =>
        !entry.IsDeleted && entry.TransferId is null && entry.CategoryId is null && entry.Splits.Count == 0 ? entry.Amount : 0;

    public static Dictionary<string, decimal> Activity(IEnumerable<TransactionEntity> entries) => entries
        .Where(t => !t.IsDeleted && (t.TransferId is null || t.CategoryId is not null))
        .SelectMany(t => t.Splits.Count > 0 ? t.Splits.Select(s => new SplitDto(s.CategoryId, s.Amount))
            : t.CategoryId is not null ? [new SplitDto(t.CategoryId, t.Amount)] : Enumerable.Empty<SplitDto>())
        .GroupBy(s => s.CategoryId).ToDictionary(g => g.Key, g => g.Sum(s => s.Amount));

    // Loan activity (interest, fees, corrections) is never categorized, so it never needs a category.
    public static bool NeedsAttention(TransactionEntity entry) => !entry.IsDeleted && entry.TransferId is null && !entry.IsOpening
        && entry.Amount < 0 && entry.CategoryId is null && entry.Splits.Count == 0 && !LoanRules.IsLoan(entry.Account?.AccountType) && !IsTracking(entry.Account?.AccountType);

    public static decimal Balance(IEnumerable<TransactionEntity> entries, bool clearedOnly = false) =>
        entries.Where(t => !t.IsDeleted && (!clearedOnly || t.Status != "uncleared")).Sum(t => t.Amount);

    // Only an account without ledger history or planned activity can be deleted. Any nondeleted entry other than its own
    // opening balance, which is removed with the account (transfer leg, loan activity, posted schedule, imported or reconciled row) blocks, as does an active schedule from or
    // to the account and money still held in its server-owned payment category. Returns null when deletion is allowed.
    public static string? AccountDeletionError(int transactions, int activeSchedules, bool paymentCategoryHasMoney) =>
        transactions > 0 ? "This account has transactions. Delete them first or close the account instead."
        : activeSchedules > 0 ? "This account has scheduled transactions. End them first or close the account instead."
        : paymentCategoryHasMoney ? "This account's payment category still has money assigned. Move it to another category first."
        : null;
}
