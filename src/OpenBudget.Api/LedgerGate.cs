using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

// The single-budget lock orders month initialization, plan writes and ledger commands.
// Commit before returning the HTTP response, including reads that initialize a month.
public sealed class LedgerGate(BudgetDbContext db)
{
    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken, Func<T, bool>? commitWhen = null)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LedgerStates.FromSqlRaw("SELECT * FROM \"LedgerStates\" WHERE \"Id\" = 1 FOR UPDATE").SingleAsync(cancellationToken);
        var result = await action();
        if (commitWhen is null || commitWhen(result)) await transaction.CommitAsync(cancellationToken);
        else await transaction.RollbackAsync(cancellationToken);
        return result;
    }
}
