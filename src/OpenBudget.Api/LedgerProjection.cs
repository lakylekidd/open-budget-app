using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

// Recalculates materialized ledger activity, income and credit-card payment flows for every initialized
// month of one budget, propagating carry-forward deltas. Shared by ledger commands and Plan saves.
public sealed class LedgerProjection(BudgetDbContext db)
{
    // adjust runs on each month (oldest first) after carried-in deltas and before activity is recalculated, so whatever it
    // moves between categories is carried into later months like any other change (used by category deletion).
    public async Task RecalculateAsync(string budgetId, CancellationToken ct, Action<BudgetEntity>? adjust = null)
    {
        var months = (await db.LoadPlanMonthsAsync(db.Budgets.Where(b => b.BudgetId == budgetId), ct))
            .OrderBy(b => b.Month, StringComparer.Ordinal).ToList();
        var catalog = await CategoryCatalog.LoadAsync(db, budgetId, ct);
        var entries = await db.Transactions.IgnoreQueryFilters().Include(t => t.Splits)
            .Where(t => t.BudgetId == budgetId && !t.IsDeleted).ToListAsync(ct);
        var creditAccounts = await CreditAccountsAsync(budgetId, ct);
        var creditIds = creditAccounts.Select(a => a.Id).ToHashSet();
        var loans = await LoansAsync(budgetId, ct);
        var loanIds = loans.Select(x => x.Account.Id).ToHashSet();
        var trackingIds = (await db.Accounts.IgnoreQueryFilters().Where(a => a.BudgetId == budgetId && LedgerRules.TrackingTypes.Contains(a.AccountType)).Select(a => a.Id).ToListAsync(ct)).ToHashSet();
        var loanPairs = loans.ToDictionary(x => x.Account.Id, x => new LoanRules.Pairing(x.Terms.PairedCategoryId,
            x.Terms.SeparateDeviceCategory, x.Terms.DevicePairedCategoryId, x.Terms.Contract));
        var autoPaired = loans.Where(x => x.Terms.PairedCategoryId == LoanRules.AutoCategoryId(x.Account.Id))
            .Select(x => (x.Account, x.Terms.PairedCategoryId!)).ToList();
        // System categories are budget-wide: a structural change applies to (and versions) every month.
        var structureChanged = CreditCardRules.EnsurePaymentCategories(catalog, creditAccounts);
        structureChanged |= LoanRules.EnsureCategories(catalog, autoPaired);
        var categoryIds = catalog.Categories.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var byMonth = entries.GroupBy(t => Month(t.Date)).ToDictionary(g => g.Key, g => g.ToList());
        decimal rtaDelta = 0;
        var categoryDeltas = new Dictionary<string, decimal>();
        foreach (var month in months)
        {
            var oldRta = BudgetService.CalculateReadyToAssign(month);
            var oldAvailable = BudgetCarryForward.AvailableById(month);
            var oldCreditOverspending = BudgetCarryForward.CreditOverspendingById(month);
            var monthlyEntries = byMonth.GetValueOrDefault(month.Month) ?? [];
            var activity = LedgerRules.Activity(monthlyEntries.Where(t => !trackingIds.Contains(t.AccountId)));
            var changed = structureChanged || rtaDelta != 0;
            changed |= BudgetCarryForward.Apply(month, rtaDelta, categoryDeltas);
            if (adjust is not null) { adjust(month); changed = true; }
            var loanFlows = LoanRules.Project(monthlyEntries, loanPairs, categoryIds.Contains);
            // A credit account's starting balance is existing debt, not money to assign. Loan balances (opening debt,
            // interest, fees, corrections) never change Ready to Assign; unpaired loan payments do.
            var income = monthlyEntries.Where(t => !(t.IsOpening && creditIds.Contains(t.AccountId)) && !loanIds.Contains(t.AccountId) && !trackingIds.Contains(t.AccountId)).Sum(LedgerRules.Income)
                + loanFlows.Income;
            changed |= income != month.LedgerIncome;
            month.LedgerIncome = income;
            var categories = month.CategoryStates.ToList();
            var byId = categories.ToDictionary(c => c.CategoryId, StringComparer.Ordinal);
            var cashActivity = LedgerRules.Activity(monthlyEntries.Where(t => !creditIds.Contains(t.AccountId) && !trackingIds.Contains(t.AccountId)));
            var flows = CreditCardRules.Project(monthlyEntries, creditIds, id => byId.TryGetValue(id, out var c)
                ? c.OpeningAvailable + c.Assigned + c.LegacyActivity + cashActivity.GetValueOrDefault(id) : 0);
            foreach (var category in categories)
            {
                var nextActivity = category.LegacyActivity + activity.GetValueOrDefault(category.CategoryId) + flows.PaymentActivity.GetValueOrDefault(category.CategoryId)
                    + loanFlows.CategoryActivity.GetValueOrDefault(category.CategoryId);
                var nextCreditOverspending = flows.CreditOverspending.GetValueOrDefault(category.CategoryId);
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
