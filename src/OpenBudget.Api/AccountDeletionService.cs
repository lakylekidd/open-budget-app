using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

// Deletes an account created by mistake. It is a ledger command (command ID, ledger version, replay receipt and audit)
// and runs inside the budget's ledger gate, so a refused or failed delete writes nothing.
public sealed partial class LedgerService
{
    public Task<MutationDto> DeleteAccountAsync(Guid id, DeleteAccountRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, $"account.delete/{id}", request, async () =>
        {
            // The budget query filter scopes this lookup, so another budget's account is "not found".
            var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new LedgerException("Account not found.", 404);
            // The opening balance is created with the account, so it does not make the account "used": it is removed with it.
            var transactions = await db.Transactions.CountAsync(t => t.AccountId == id && !t.IsDeleted && !t.IsOpening, ct);
            var schedules = await db.Schedules.Where(s => s.AccountId == id || s.ToAccountId == id).ToListAsync(ct);
            // Server-owned categories that exist only for this account: a credit card's payment category and a legacy loan category.
            string[] categoryIds = [CreditCardRules.PaymentCategoryId(id), LoanRules.AutoCategoryId(id)];
            var catalog = await CategoryCatalog.LoadAsync(db, db.ActiveBudgetId, ct);
            var ownedDefinitions = catalog.Categories.Where(c => categoryIds.Contains(c.Id)).ToList();
            var months = await db.LoadPlanMonthsAsync(db.Budgets.Where(b => b.BudgetId == db.ActiveBudgetId), ct);
            var owned = months.SelectMany(m => m.CategoryStates.Where(c => categoryIds.Contains(c.CategoryId))).ToList();
            var error = LedgerRules.AccountDeletionError(transactions, schedules.Count(s => !s.IsEnded), owned.Any(x => x.Assigned != 0 || x.Available != 0));
            if (error is not null) throw new LedgerException(error);

            var terms = await db.LoanTerms.SingleOrDefaultAsync(t => t.AccountId == id, ct);
            var payoff = await db.CreditPayoffTargets.SingleOrDefaultAsync(t => t.AccountId == id, ct);
            var before = new
            {
                account = new { account.Id, account.Name, account.AccountType, account.IsClosed, account.IsFavorite, account.Notes },
                loan = terms is null ? null : ToTermsAudit(terms),
                payoffTarget = payoff is null ? null : ToDto(payoff),
                endedSchedules = schedules.Count,
                removedCategories = ownedDefinitions.Select(x => x.Id).ToList()
            };

            // Preserve the provider connection and staged review rows, but release this account mapping.
            foreach (var linked in await db.LinkedBankAccounts.Where(x => x.AccountId == id).ToListAsync(ct)) linked.AccountId = null;

            // Rows that exist only for this account and would otherwise block the account row's foreign keys.
            // Soft-deleted entries and the opening balance entry (and their splits/images by cascade) are removed with the account; their receipts stay in the audit log.
            db.Transactions.RemoveRange(await db.Transactions.Where(t => t.AccountId == id && (t.IsDeleted || t.IsOpening)).ToListAsync(ct));
            var scheduleIds = schedules.Select(s => s.Id).ToList();
            db.ScheduleOccurrences.RemoveRange(await db.ScheduleOccurrences.Where(o => scheduleIds.Contains(o.ScheduleId)).ToListAsync(ct));
            db.Schedules.RemoveRange(schedules);
            db.Reconciliations.RemoveRange(await db.Reconciliations.Where(r => r.AccountId == id).ToListAsync(ct));
            if (terms is not null) db.LoanTerms.Remove(terms);
            if (payoff is not null) db.CreditPayoffTargets.Remove(payoff);
            var scope = id.ToString("D");
            db.RegisterViews.RemoveRange(await db.RegisterViews.Where(v => v.Scope == scope).ToListAsync(ct));
            db.LongTermPlanningCategories.RemoveRange(await db.LongTermPlanningCategories.Where(c => categoryIds.Contains(c.CategoryId)).ToListAsync(ct));
            // Categories are budget-wide: removing them (and an emptied system group) changes every month's structure.
            foreach (var category in ownedDefinitions) catalog.RemoveCategory(category);
            foreach (var group in catalog.Groups.Where(g => (g.Id == CreditCardRules.PaymentGroupId || g.Id == LoanRules.PaymentGroupId)
                && catalog.Categories.All(c => c.GroupId != g.Id)).ToList())
                catalog.RemoveGroup(group);
            if (ownedDefinitions.Count > 0)
                foreach (var month in months) month.Version++;
            db.Accounts.Remove(account);
            return new Change(id, before, new { deleted = true });
        }, ct);
}
