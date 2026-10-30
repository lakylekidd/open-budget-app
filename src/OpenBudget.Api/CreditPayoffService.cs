using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

// Debt payoff targets for credit payment categories. Writes are ledger commands (command ID, ledger version,
// replay receipt and audit); progress is a read-only calculation for the requested Plan month.
public sealed partial class LedgerService
{
    public async Task<CreditPayoffDto> GetPayoffTargetAsync(Guid id, string month, CancellationToken ct)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new LedgerException("Account not found.", 404);
        if (!CreditCardRules.IsCredit(account.AccountType)) throw new LedgerException("Only credit accounts have a debt payoff target.");
        if (!CreditPayoffRules.TryParseMonth(month, out var start)) throw new LedgerException("Month must use YYYY-MM format.");
        var version = await db.LedgerStates.Select(x => x.Version).SingleAsync(ct);
        var target = await db.CreditPayoffTargets.AsNoTracking().SingleOrDefaultAsync(t => t.AccountId == id, ct);
        if (target is null) return new(id, month, version, null, null);

        var end = start.AddMonths(1);
        var balance = await db.Transactions.AsNoTracking().Where(t => t.AccountId == id && !t.IsDeleted && t.Date < end).SumAsync(t => t.Amount, ct);
        var budgetMonth = await budgets.GetMonthAsync(db.ActiveBudgetId, month, ct);
        var paymentId = CreditCardRules.PaymentCategoryId(id);
        var category = budgetMonth.Groups.SelectMany(g => g.Categories).SingleOrDefault(c => c.Id == paymentId);
        var progress = CreditPayoffRules.Calculate(target.Kind, target.TargetMonth, target.MonthlyAmount, month, balance,
            category?.Available ?? 0, category?.Assigned ?? 0);
        return new(id, month, version, ToDto(target), progress);
    }

    public Task<MutationDto> SavePayoffTargetAsync(Guid id, SaveCreditPayoffTargetRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, $"payoffTarget.save/{id}", request, async () =>
        {
            var account = await OpenAccountAsync(id, ct);
            if (!CreditCardRules.IsCredit(account.AccountType)) throw new LedgerException("Debt payoff targets are only available for credit accounts.");
            var currentMonth = Month(DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
            if (CreditPayoffRules.Validate(request.Kind, request.TargetMonth, request.MonthlyAmount, currentMonth) is { } error) throw new LedgerException(error);
            var balance = await db.Transactions.Where(t => t.AccountId == id && !t.IsDeleted).SumAsync(t => t.Amount, ct);
            var target = await db.CreditPayoffTargets.SingleOrDefaultAsync(t => t.AccountId == id, ct);
            var before = target is null ? null : ToDto(target);
            var now = clock.GetUtcNow();
            if (target is null)
            {
                target = new CreditPayoffTargetEntity { AccountId = id, CreatedAt = now };
                db.CreditPayoffTargets.Add(target);
            }
            target.Kind = request.Kind;
            target.TargetMonth = request.TargetMonth;
            target.MonthlyAmount = request.MonthlyAmount;
            target.PayoffAmount = Math.Max(-balance, 0);
            target.UpdatedAt = now;
            return new Change(id, before, ToDto(target));
        }, ct);

    public Task<MutationDto> DeletePayoffTargetAsync(Guid id, DeleteCreditPayoffTargetRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, $"payoffTarget.delete/{id}", request, async () =>
        {
            var target = await db.CreditPayoffTargets.SingleOrDefaultAsync(t => t.AccountId == id, ct) ?? throw new LedgerException("Debt payoff target not found.", 404);
            var before = ToDto(target);
            db.CreditPayoffTargets.Remove(target);
            return new Change(id, before, new { deleted = true });
        }, ct);

    private static CreditPayoffTargetDto ToDto(CreditPayoffTargetEntity t) => new(t.Kind, t.TargetMonth, t.MonthlyAmount, t.PayoffAmount, t.CreatedAt, t.UpdatedAt);
}
