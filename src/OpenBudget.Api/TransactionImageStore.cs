using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

/// <summary>
/// Stores validated image bytes by unguessable image ID. Keys are server-generated GUIDs scoped by budget; no caller-supplied
/// names or paths ever reach the store. Production deployments should implement this over Azure Blob Storage with Managed
/// Identity (see docs/architecture.md); the PostgreSQL implementation is the default for local and self-hosted use.
/// </summary>
public interface ITransactionImageStore
{
    /// <summary>Stages bytes for the image. The PostgreSQL store commits them with the caller's unit of work.</summary>
    Task SaveAsync(string budgetId, Guid imageId, byte[] content, CancellationToken ct);
    Task<byte[]?> ReadAsync(string budgetId, Guid imageId, CancellationToken ct);
    Task DeleteAsync(string budgetId, Guid imageId, CancellationToken ct);
}

/// <summary>Keeps bytes in the separate <c>TransactionImageBlobs</c> table so metadata queries stay light.</summary>
public sealed class PostgresTransactionImageStore(BudgetDbContext db) : ITransactionImageStore
{
    public Task SaveAsync(string budgetId, Guid imageId, byte[] content, CancellationToken ct)
    {
        db.TransactionImageBlobs.Add(new TransactionImageBlobEntity { ImageId = imageId, BudgetId = budgetId, Content = content });
        return Task.CompletedTask;
    }

    // The global budget query filter applies in addition to the explicit budget predicate.
    public Task<byte[]?> ReadAsync(string budgetId, Guid imageId, CancellationToken ct) =>
        db.TransactionImageBlobs.AsNoTracking().Where(x => x.ImageId == imageId && x.BudgetId == budgetId).Select(x => x.Content).SingleOrDefaultAsync(ct);

    public async Task DeleteAsync(string budgetId, Guid imageId, CancellationToken ct)
    {
        var row = await db.TransactionImageBlobs.SingleOrDefaultAsync(x => x.ImageId == imageId && x.BudgetId == budgetId, ct);
        if (row is not null) db.TransactionImageBlobs.Remove(row);
    }
}
