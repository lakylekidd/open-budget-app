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
        if (splits.Count > 50 || splits.Any(s => s is null || string.IsNullOrWhiteSpace(s.CategoryId) || s.CategoryId.Length > 100 || !IsMoney(s.Amount) || s.Amount == 0))
            return "Splits require a category and a nonzero money amount (maximum 50 splits).";
        if (splits.Count > 0 && (!string.IsNullOrEmpty(request.CategoryId) || splits.Sum(s => s.Amount) != request.Amount))
            return "Split amounts must equal the transaction amount and replace its single category.";
        return null;
    }

    public static decimal Income(TransactionEntity entry) =>
        !entry.IsDeleted && entry.TransferId is null && entry.CategoryId is null && entry.Splits.Count == 0 ? entry.Amount : 0;

    public static Dictionary<string, decimal> Activity(IEnumerable<TransactionEntity> entries) => entries
        .Where(t => !t.IsDeleted && t.TransferId is null)
        .SelectMany(t => t.Splits.Count > 0 ? t.Splits.Select(s => new SplitDto(s.CategoryId, s.Amount))
            : t.CategoryId is not null ? [new SplitDto(t.CategoryId, t.Amount)] : Enumerable.Empty<SplitDto>())
        .GroupBy(s => s.CategoryId).ToDictionary(g => g.Key, g => g.Sum(s => s.Amount));

    public static bool NeedsAttention(TransactionEntity entry) => !entry.IsDeleted && entry.TransferId is null && !entry.IsOpening
        && entry.Amount < 0 && entry.CategoryId is null && entry.Splits.Count == 0;

    public static decimal Balance(IEnumerable<TransactionEntity> entries, bool clearedOnly = false) =>
        entries.Where(t => !t.IsDeleted && (!clearedOnly || t.Status != "uncleared")).Sum(t => t.Amount);
}
