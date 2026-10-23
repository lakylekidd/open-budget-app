using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

// The budget-specific lock orders month initialization, monthly Plan writes, annual planning writes and ledger commands.
// Commit before returning the HTTP response, including reads that initialize a month.
public sealed class LedgerGate(BudgetDbContext db)
{
    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken, Func<T, bool>? commitWhen = null)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({db.ActiveBudgetId}, 1))", cancellationToken);
        if (!await db.LedgerStates.AnyAsync(cancellationToken))
        {
            db.LedgerStates.Add(new LedgerStateEntity());
        }
        if (!await db.FlagDefinitions.AnyAsync(cancellationToken))
        {
            db.FlagDefinitions.AddRange(LedgerService.FlagColors.Select(color => new FlagDefinitionEntity
            {
                Color = color,
                Name = char.ToUpperInvariant(color[0]) + color[1..]
            }));
        }
        if (!await db.LongTermPlanningStates.AnyAsync(x => x.Id == "default", cancellationToken))
        {
            db.LongTermPlanningStates.Add(new LongTermPlanningStateEntity());
        }
        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        await db.LedgerStates.FromSqlInterpolated($"SELECT * FROM \"LedgerStates\" WHERE \"BudgetId\" = {db.ActiveBudgetId} AND \"Id\" = 1 FOR UPDATE").SingleAsync(cancellationToken);
        var result = await action();
        if (commitWhen is null || commitWhen(result)) await transaction.CommitAsync(cancellationToken);
        else await transaction.RollbackAsync(cancellationToken);
        return result;
    }
}
