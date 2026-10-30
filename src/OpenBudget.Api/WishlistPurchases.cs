using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

// Pure rules for "Record purchase": a wishlist item bought through a ledger transaction.
public static class WishlistPurchaseRules
{
    // The transaction must be a single-category outflow in the item's bucket category, created once (no schedule).
    public static string? Validate(WishlistItemEntity item, TransactionRequest request)
    {
        if (item.Status != WishlistRules.ActiveStatus) return "This item was already purchased or removed. Restore it on the Wishlist screen first.";
        if (request.Amount >= 0) return "A wishlist purchase must be an outflow.";
        if (request.Splits is { Count: > 0 }) return "A wishlist purchase cannot be split.";
        if (request.Repeat != "never") return "A wishlist purchase cannot repeat.";
        if (request.CategoryId != WishlistRules.CategoryId(item.BucketId)) return "A wishlist purchase must use its bucket's category.";
        return null;
    }

    // A linked purchase stays linked while its transaction exists and still spends from the item's bucket category.
    // Amount, date, account, payee, memo and status edits keep the link; deleting the transaction or moving it to another
    // category (including splitting it) unlinks it and returns the item to the wishlist.
    public static bool KeepsLink(WishlistItemEntity item, TransactionEntity transaction) =>
        !transaction.IsDeleted && transaction.TransferId is null && transaction.CategoryId == WishlistRules.CategoryId(item.BucketId);

    // An item returned to the wishlist goes back to its previous desire position, clamped to the end of the bucket.
    public static int RestorePosition(int previousSortOrder, int activeCount) => Math.Clamp(previousSortOrder, 0, activeCount);

    // Inserts the restored item at the position and renumbers the bucket's active items 0..n.
    public static void InsertAt(List<WishlistItemEntity> ordered, WishlistItemEntity item, int position)
    {
        ordered.Insert(Math.Clamp(position, 0, ordered.Count), item);
        for (var index = 0; index < ordered.Count; index++) ordered[index].SortOrder = index;
    }
}

// Ledger side of "Record purchase". Runs inside LedgerService.ExecuteAsync, so the transaction, the item status change
// and the Wishlist version bump commit (or roll back) together under the budget's LedgerGate, with one command receipt.
public sealed partial class LedgerService
{
    // Validated before the transaction is built so failures leave no writes (the gate rolls back on any exception).
    private async Task<WishlistItemEntity> LoadWishlistPurchaseAsync(Guid itemId, TransactionRequest request, CancellationToken ct)
    {
        var budgetId = db.ActiveBudgetId;
        if (!await db.WishlistSettings.AnyAsync(x => x.BudgetId == budgetId && x.IsEnabled, ct))
            throw new LedgerException("Enable Wishlist for this budget first.");
        var item = await db.WishlistItems.SingleOrDefaultAsync(x => x.Id == itemId && x.BudgetId == budgetId, ct);
        if (item is null || !await db.WishlistBuckets.AnyAsync(b => b.Id == item.BucketId && b.BudgetId == budgetId && !b.IsDeleted, ct))
            throw new LedgerException("Wishlist item not found.", 404);
        if (WishlistPurchaseRules.Validate(item, request) is { } error) throw new LedgerException(error);
        return item;
    }

    private async Task MarkWishlistPurchasedAsync(WishlistItemEntity item, TransactionEntity entry, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var others = await db.WishlistItems
            .Where(x => x.BudgetId == item.BudgetId && x.BucketId == item.BucketId && x.Status == WishlistRules.ActiveStatus && x.Id != item.Id)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        // SortOrder is kept on the purchased item so deleting the transaction can return it to the same place.
        item.Status = WishlistRules.PurchasedStatus;
        item.PurchasedAt = now;
        item.PurchaseTransactionId = entry.Id;
        item.UpdatedAt = now;
        for (var index = 0; index < others.Count; index++) others[index].SortOrder = index;
        await TouchWishlistAsync(item.BudgetId, ct);
    }

    // Called by ExecuteAsync for every ledger command. An item purchased through a transaction returns to its bucket's
    // active list when this command soft-deleted that transaction (single delete, selection delete, review reject) or
    // edited it so it no longer spends from the bucket's category (another category, a split, or a transfer).
    private async Task RestoreWishlistPurchasesForChangedAsync(CancellationToken ct)
    {
        var changed = db.ChangeTracker.Entries<TransactionEntity>()
            .Where(e => e.State == EntityState.Modified)
            .ToDictionary(e => e.Entity.Id, e => e.Entity);
        if (changed.Count == 0) return;
        var budgetId = db.ActiveBudgetId;
        var ids = changed.Keys.ToList();
        var linked = await db.WishlistItems
            .Where(x => x.BudgetId == budgetId && x.Status == WishlistRules.PurchasedStatus && x.PurchaseTransactionId != null && ids.Contains(x.PurchaseTransactionId.Value))
            .ToListAsync(ct);
        var restored = linked.Where(x => !WishlistPurchaseRules.KeepsLink(x, changed[x.PurchaseTransactionId!.Value])).ToList();
        if (restored.Count == 0) return;
        var now = clock.GetUtcNow();
        foreach (var item in restored.OrderBy(x => x.SortOrder))
        {
            var active = await db.WishlistItems
                .Where(x => x.BudgetId == budgetId && x.BucketId == item.BucketId && x.Status == WishlistRules.ActiveStatus)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
            item.Status = WishlistRules.ActiveStatus;
            item.PurchasedAt = null;
            item.PurchaseTransactionId = null;
            item.UpdatedAt = now;
            WishlistPurchaseRules.InsertAt(active, item, WishlistPurchaseRules.RestorePosition(item.SortOrder, active.Count));
            // Make the next lookup in this loop see the restored item as active.
            await db.SaveChangesAsync(ct);
        }
        await TouchWishlistAsync(budgetId, ct);
    }

    // Advances the per-budget Wishlist version so open Wishlist screens reload instead of overwriting the change.
    private async Task TouchWishlistAsync(string budgetId, CancellationToken ct)
    {
        var settings = await db.WishlistSettings.SingleOrDefaultAsync(x => x.BudgetId == budgetId, ct);
        if (settings is null) return;
        settings.Version++;
        settings.UpdatedAt = clock.GetUtcNow();
    }
}

public static class WishlistPurchases
{
    // Linked purchase transactions for an overview, keyed by transaction id (deleted transactions are omitted).
    public static async Task<IReadOnlyDictionary<Guid, WishlistPurchaseDto>> LoadAsync(BudgetDbContext db, string budgetId,
        IEnumerable<WishlistItemEntity> items, CancellationToken ct)
    {
        var ids = items.Where(x => x.PurchaseTransactionId != null).Select(x => x.PurchaseTransactionId!.Value).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, WishlistPurchaseDto>();
        return await db.Transactions.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.BudgetId == budgetId && !t.IsDeleted && ids.Contains(t.Id))
            .Select(t => new WishlistPurchaseDto(t.Id, t.AccountId, t.Date, -t.Amount))
            .ToDictionaryAsync(x => x.TransactionId, ct);
    }

    // POST /api/budgets/{id}/wishlist/items/{itemId}/purchase-transaction — body is a TransactionRequest (ledger
    // commandId + version). Creates the transaction and marks the item purchased in one ledger command.
    public static void MapEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/items/{itemId:guid}/purchase-transaction", async (string id, Guid itemId, TransactionRequest request, ClaimsPrincipal principal,
            BudgetOwnershipService ownership, BudgetRequestContext budgetContext, LedgerService ledger, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            if (!await ownership.CanAccessBudgetAsync(userId, id, ct)) return Results.NotFound();
            budgetContext.BudgetId = id;
            return Results.Ok(await ledger.SaveTransactionAsync(null, request, ct, wishlistItemId: itemId));
        }).RequireEntitlement(WishlistRules.EntitlementKey);
    }
}
