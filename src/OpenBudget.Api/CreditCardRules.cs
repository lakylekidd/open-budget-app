namespace OpenBudget.Api;

// Credit accounts are budget accounts whose categorized spending reserves cash for the card payment.
// Each credit account owns one payment category in the Credit Card Payments group:
//   - card spending moves the portion covered by the spending category's funds into the payment category;
//   - the uncovered portion is credit overspending, which becomes card debt rather than reducing Ready to Assign;
//   - card refunds move money from the payment category back to the spending category;
//   - transfers into a card (payments) reduce the payment category; transfers out of a card increase it.
public static class CreditCardRules
{
    public const string PaymentGroupId = "credit-card-payments";
    public const string PaymentGroupName = "Credit Card Payments";
    // Icon earlier versions assigned automatically; account names now carry any icon the user wants.
    public const string LegacyPaymentIcon = "💳";
    private const string PaymentCategoryPrefix = "ccp-";

    public static readonly string[] CreditTypes = ["creditCard", "lineOfCredit"];

    public static bool IsCredit(string accountType) => CreditTypes.Contains(accountType);
    public static string PaymentCategoryId(Guid accountId) => PaymentCategoryPrefix + accountId.ToString("D");
    public static bool IsPaymentCategory(string? categoryId) => categoryId?.StartsWith(PaymentCategoryPrefix, StringComparison.Ordinal) == true;

    public sealed record Flows(IReadOnlyDictionary<string, decimal> PaymentActivity, IReadOnlyDictionary<string, decimal> CreditOverspending);

    // fundsBeforeCredit returns a category's opening + assigned + legacy + non-credit activity for the month.
    public static Flows Project(IEnumerable<TransactionEntity> monthEntries, IReadOnlySet<Guid> creditAccounts, Func<string, decimal> fundsBeforeCredit)
    {
        var payments = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var overspending = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var nets = new Dictionary<string, SortedDictionary<Guid, decimal>>(StringComparer.Ordinal);
        foreach (var entry in monthEntries.Where(t => !t.IsDeleted && creditAccounts.Contains(t.AccountId)))
        {
            if (entry.TransferId is not null)
            {
                Add(payments, PaymentCategoryId(entry.AccountId), -entry.Amount);
                continue;
            }
            var parts = entry.Splits.Count > 0 ? entry.Splits.Select(s => (s.CategoryId, s.Amount))
                : entry.CategoryId is not null ? [(entry.CategoryId, entry.Amount)] : [];
            foreach (var (categoryId, amount) in parts)
            {
                if (!nets.TryGetValue(categoryId, out var byAccount)) nets[categoryId] = byAccount = [];
                byAccount[entry.AccountId] = byAccount.GetValueOrDefault(entry.AccountId) + amount;
            }
        }
        foreach (var (categoryId, byAccount) in nets.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var funds = fundsBeforeCredit(categoryId);
            foreach (var (accountId, refund) in byAccount.Where(x => x.Value > 0))
            {
                Add(payments, PaymentCategoryId(accountId), -refund);
                funds += refund;
            }
            var remaining = Math.Max(funds, 0);
            decimal unfunded = 0;
            foreach (var (accountId, net) in byAccount.Where(x => x.Value < 0))
            {
                var covered = Math.Min(-net, remaining);
                remaining -= covered;
                Add(payments, PaymentCategoryId(accountId), covered);
                unfunded += -net - covered;
            }
            if (unfunded > 0) overspending[categoryId] = unfunded;
        }
        return new(payments, overspending);
    }

    // Keeps the payment group and one payment category per credit account present and named after the account.
    public static bool EnsurePaymentCategories(BudgetEntity month, IReadOnlyList<AccountEntity> creditAccounts)
    {
        if (creditAccounts.Count == 0) return false;
        var changed = false;
        var group = month.Groups.SingleOrDefault(g => g.Id == PaymentGroupId);
        if (group is null)
        {
            foreach (var other in month.Groups) other.SortOrder++;
            group = new CategoryGroupEntity { BudgetId = month.BudgetId, BudgetMonth = month.Month, Id = PaymentGroupId, Name = PaymentGroupName, SortOrder = 0 };
            month.Groups.Add(group);
            changed = true;
        }
        foreach (var account in creditAccounts)
        {
            var id = PaymentCategoryId(account.Id);
            var category = month.Groups.SelectMany(g => g.Categories).SingleOrDefault(c => c.Id == id);
            if (category is null)
            {
                group.Categories.Add(new CategoryEntity { BudgetId = month.BudgetId, BudgetMonth = month.Month, GroupId = group.Id, Id = id,
                    Name = account.Name, SortOrder = group.Categories.Count == 0 ? 0 : group.Categories.Max(c => c.SortOrder) + 1 });
                changed = true;
            }
            else if (category.Name != account.Name || category.Icon == LegacyPaymentIcon)
            {
                category.Name = account.Name;
                if (category.Icon == LegacyPaymentIcon) category.Icon = null;
                changed = true;
            }
        }
        return changed;
    }

    private static void Add(Dictionary<string, decimal> totals, string key, decimal amount) => totals[key] = totals.GetValueOrDefault(key) + amount;
}
