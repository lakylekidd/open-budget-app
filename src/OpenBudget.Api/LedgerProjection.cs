using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

// Recalculates materialized ledger activity, income and credit-card payment flows for every initialized
// month of one budget, propagating carry-forward deltas. Shared by ledger commands and Plan saves.
public sealed class LedgerProjection(BudgetDbContext db)
{
    public async Task RecalculateAsync(string budgetId, CancellationToken ct)
    {
        var months = await db.Budgets.Where(b => b.BudgetId == budgetId)
            .Include(b => b.Groups).ThenInclude(g => g.Categories).OrderBy(b => b.Month).ToListAsync(ct);
        var entries = await db.Transactions.IgnoreQueryFilters().Include(t => t.Splits)
            .Where(t => t.BudgetId == budgetId && !t.IsDeleted).ToListAsync(ct);
        var creditAccounts = await CreditAccountsAsync(budgetId, ct);
        var creditIds = creditAccounts.Select(a => a.Id).ToHashSet();
        var loans = await LoansAsync(budgetId, ct);
        var loanIds = loans.Select(x => x.Account.Id).ToHashSet();
        var loanPairs = loans.ToDictionary(x => x.Account.Id, x => new LoanRules.Pairing(x.Terms.PairedCategoryId,
            x.Terms.SeparateDeviceCategory, x.Terms.DevicePairedCategoryId, x.Terms.Contract));
        var autoPaired = loans.Where(x => x.Terms.PairedCategoryId == LoanRules.AutoCategoryId(x.Account.Id))
            .Select(x => (x.Account, x.Terms.PairedCategoryId!)).ToList();
        var byMonth = entries.GroupBy(t => Month(t.Date)).ToDictionary(g => g.Key, g => g.ToList());
        decimal rtaDelta = 0;
        var categoryDeltas = new Dictionary<string, decimal>();
        BudgetEntity? previous = null;
        foreach (var month in months)
        {
            var oldRta = BudgetService.CalculateReadyToAssign(month);
            var oldAvailable = month.Groups.SelectMany(g => g.Categories).ToDictionary(c => c.Id, c => c.Available);
            var oldCreditOverspending = month.Groups.SelectMany(g => g.Categories).ToDictionary(c => c.Id, c => c.CreditOverspending);
            var monthlyEntries = byMonth.GetValueOrDefault(month.Month) ?? [];
            var activity = LedgerRules.Activity(monthlyEntries);
            var changed = rtaDelta != 0;
            if (previous is not null) changed |= BudgetCarryForward.Apply(month, rtaDelta, categoryDeltas, previous);
            changed |= CreditCardRules.EnsurePaymentCategories(month, creditAccounts);
            changed |= LoanRules.EnsureCategories(month, autoPaired);
            var monthCategoryIds = month.Groups.SelectMany(g => g.Categories).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
            var loanFlows = LoanRules.Project(monthlyEntries, loanPairs, monthCategoryIds.Contains);
            // A credit account's starting balance is existing debt, not money to assign. Loan balances (opening debt,
            // interest, fees, corrections) never change Ready to Assign; unpaired loan payments do.
            var income = monthlyEntries.Where(t => !(t.IsOpening && creditIds.Contains(t.AccountId)) && !loanIds.Contains(t.AccountId)).Sum(LedgerRules.Income)
                + loanFlows.Income;
            changed |= income != month.LedgerIncome;
            month.LedgerIncome = income;
            var categories = month.Groups.SelectMany(g => g.Categories).ToList();
            var byId = categories.ToDictionary(c => c.Id, StringComparer.Ordinal);
            var cashActivity = LedgerRules.Activity(monthlyEntries.Where(t => !creditIds.Contains(t.AccountId)));
            var flows = CreditCardRules.Project(monthlyEntries, creditIds, id => byId.TryGetValue(id, out var c)
                ? c.OpeningAvailable + c.Assigned + c.LegacyActivity + cashActivity.GetValueOrDefault(id) : 0);
            foreach (var category in categories)
            {
                var nextActivity = category.LegacyActivity + activity.GetValueOrDefault(category.Id) + flows.PaymentActivity.GetValueOrDefault(category.Id)
                    + loanFlows.CategoryActivity.GetValueOrDefault(category.Id);
                var nextCreditOverspending = flows.CreditOverspending.GetValueOrDefault(category.Id);
                changed |= category.Activity != nextActivity || category.CreditOverspending != nextCreditOverspending;
                category.Activity = nextActivity;
                category.CreditOverspending = nextCreditOverspending;
            }
            var carryChanges = BudgetCarryForward.CalculateChanges(
                oldRta,
                BudgetService.CalculateReadyToAssign(month),
                oldAvailable,
                categories,
                oldCreditOverspending);
            rtaDelta = carryChanges.ReadyToAssignDelta;
            categoryDeltas = new Dictionary<string, decimal>(carryChanges.AvailableDeltas, StringComparer.Ordinal);
            if (changed) month.Version++;
            previous = month;
        }
    }

    // Credit and loan accounts both add server-owned category flows that Plan saves must recalculate.
    public Task<bool> HasCreditAccountsAsync(string budgetId, CancellationToken ct) =>
        db.Accounts.IgnoreQueryFilters().AnyAsync(a => a.BudgetId == budgetId && (CreditCardRules.CreditTypes.Contains(a.AccountType) || LoanRules.LoanTypes.Contains(a.AccountType)), ct);

    private async Task<List<(AccountEntity Account, LoanTermsEntity Terms)>> LoansAsync(string budgetId, CancellationToken ct)
    {
        var accounts = await db.Accounts.IgnoreQueryFilters().Where(a => a.BudgetId == budgetId && LoanRules.LoanTypes.Contains(a.AccountType))
            .OrderBy(a => a.Id).ToListAsync(ct);
        if (accounts.Count == 0) return [];
        var terms = await db.LoanTerms.IgnoreQueryFilters().Where(t => t.BudgetId == budgetId).ToDictionaryAsync(t => t.AccountId, ct);
        return accounts.Select(a => (a, terms.TryGetValue(a.Id, out var t) ? t : new LoanTermsEntity { AccountId = a.Id })).ToList();
    }

    private Task<List<AccountEntity>> CreditAccountsAsync(string budgetId, CancellationToken ct) =>
        db.Accounts.IgnoreQueryFilters().Where(a => a.BudgetId == budgetId && CreditCardRules.CreditTypes.Contains(a.AccountType))
            .OrderBy(a => a.Id).ToListAsync(ct);

    private static string Month(DateOnly date) => date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
}
