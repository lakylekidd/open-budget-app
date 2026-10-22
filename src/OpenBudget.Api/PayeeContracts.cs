namespace OpenBudget.Api;

public sealed record PayeeRuleDto(string Operator, string Value);
public sealed record PayeeDto(Guid Id, string Name, bool IsHidden, string CategoryMode, string? DefaultCategoryId,
    string? EffectiveCategoryId, int TransactionCount, List<PayeeRuleDto> Rules);
public sealed record PayeesDto(long Version, List<PayeeDto> Payees);
public sealed record PayeeUpdateDto(Guid Id, string Name, bool IsHidden, string CategoryMode,
    string? DefaultCategoryId, List<PayeeRuleDto> Rules, bool Delete = false);
public sealed record SavePayeesRequest(Guid CommandId, long Version, List<PayeeUpdateDto> Payees);

public static class PayeeRules
{
    public static string? Validate(PayeeUpdateDto p)
    {
        if (string.IsNullOrWhiteSpace(p.Name) || p.Name.Trim().Length > 160) return "Payee names must contain 1–160 characters.";
        if (p.CategoryMode is not ("history" or "always" or "never")) return "Choose a valid categorization option.";
        if (p.CategoryMode == "always" && string.IsNullOrWhiteSpace(p.DefaultCategoryId)) return "Choose a default category.";
        if (p.Rules is null || p.Rules.Count > 50 || p.Rules.Any(r => r is null || r.Operator is not ("is" or "contains") || string.IsNullOrWhiteSpace(r.Value) || r.Value.Trim().Length > 160))
            return "Provide at most 50 nonempty renaming rules using is or contains (160 characters each).";
        return null;
    }

    public static bool Matches(PayeeRuleDto rule, string imported) => !string.IsNullOrWhiteSpace(rule.Value) && (rule.Operator switch
    {
        "is" => string.Equals(imported.Trim(), rule.Value.Trim(), StringComparison.OrdinalIgnoreCase),
        "contains" => imported.Contains(rule.Value.Trim(), StringComparison.OrdinalIgnoreCase),
        _ => false
    });

    // Of the last three entries, the most frequent choice wins; recency breaks ties.
    public static string? HistoryCategory(IEnumerable<string?> newestFirst) => newestFirst.Take(3)
        .Select((category, index) => new { category, index }).GroupBy(x => x.category)
        .OrderByDescending(g => g.Count()).ThenBy(g => g.Min(x => x.index)).FirstOrDefault()?.Key;
}
