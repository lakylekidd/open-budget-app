using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

// Budget-scoped Wishlist. Every endpoint requires the "wishlist" entitlement plus owner/editor access to the budget.
// Writes run under the budget's LedgerGate so Plan structure changes (Wishlist group/bucket categories in every
// initialized month) commit atomically with the Wishlist rows; validation failures and conflicts roll back.
public sealed class WishlistService(BudgetDbContext db, BudgetService budgets, TimeProvider clock)
{
    private const int HistoryLimit = 50;

    public async Task<WishlistResult> GetAsync(string budgetId, string? month, CancellationToken ct)
    {
        if (month is not null && !IsMonth(month)) return WishlistResult.Fail("Month must use YYYY-MM format.");
        var state = await LoadAsync(budgetId, ct);
        return WishlistResult.Success(await OverviewAsync(budgetId, state, month, ct));
    }

    public Task<WishlistResult> SetEnabledAsync(string budgetId, bool enabled, WishlistVersionRequest request, CancellationToken ct) =>
        WriteAsync(budgetId, request.Version, requireEnabled: false, touchesPlan: true, ct, async state =>
        {
            if (state.Settings.IsEnabled == enabled) return null;
            // Make sure at least the current month exists so the Wishlist group appears in Plan right away.
            if (enabled) await budgets.GetMonthAsync(budgetId, CurrentMonth(), ct);
            state.Settings.IsEnabled = enabled;
            return null;
        });

    public Task<WishlistResult> CreateBucketAsync(string budgetId, WishlistBucketRequest request, CancellationToken ct) =>
        WriteAsync(budgetId, request.Version, requireEnabled: true, touchesPlan: true, ct, state =>
        {
            if (WishlistRules.ValidateBucketName(request.Name) is { } error) return Task.FromResult<WishlistResult?>(WishlistResult.Fail(error));
            var active = state.ActiveBuckets;
            if (active.Count >= WishlistRules.MaxActiveBuckets || state.Buckets.Count >= WishlistRules.MaxBuckets)
                return Task.FromResult<WishlistResult?>(WishlistResult.Fail($"A wishlist can have at most {WishlistRules.MaxActiveBuckets} buckets."));
            var name = request.Name.Trim();
            if (active.Any(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)))
                return Task.FromResult<WishlistResult?>(WishlistResult.Fail("A bucket with this name already exists."));
            var now = clock.GetUtcNow();
            var bucket = new WishlistBucketEntity { Id = Guid.NewGuid(), BudgetId = budgetId, Name = name, SortOrder = active.Count, CreatedAt = now, UpdatedAt = now };
            db.WishlistBuckets.Add(bucket);
            state.Buckets.Add(bucket);
            return Task.FromResult<WishlistResult?>(null);
        });

    public Task<WishlistResult> RenameBucketAsync(string budgetId, Guid bucketId, WishlistBucketRequest request, CancellationToken ct) =>
        WriteAsync(budgetId, request.Version, requireEnabled: true, touchesPlan: true, ct, state =>
        {
            if (WishlistRules.ValidateBucketName(request.Name) is { } error) return Task.FromResult<WishlistResult?>(WishlistResult.Fail(error));
            var bucket = state.ActiveBuckets.SingleOrDefault(b => b.Id == bucketId);
            if (bucket is null) return Task.FromResult<WishlistResult?>(WishlistResult.Fail("Bucket not found.", 404));
            var name = request.Name.Trim();
            if (state.ActiveBuckets.Any(b => b.Id != bucketId && string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)))
                return Task.FromResult<WishlistResult?>(WishlistResult.Fail("A bucket with this name already exists."));
            bucket.Name = name;
            bucket.UpdatedAt = clock.GetUtcNow();
            return Task.FromResult<WishlistResult?>(null);
        });

    public Task<WishlistResult> ReorderBucketsAsync(string budgetId, WishlistBucketOrderRequest request, CancellationToken ct) =>
        WriteAsync(budgetId, request.Version, requireEnabled: true, touchesPlan: true, ct, state =>
        {
            var active = state.ActiveBuckets;
            var ids = request.BucketIds ?? [];
            if (ids.Count != active.Count || ids.Distinct().Count() != ids.Count || !ids.All(id => active.Any(b => b.Id == id)))
                return Task.FromResult<WishlistResult?>(WishlistResult.Fail("Bucket order must list every bucket exactly once."));
            for (var index = 0; index < ids.Count; index++) active.Single(b => b.Id == ids[index]).SortOrder = index;
            return Task.FromResult<WishlistResult?>(null);
        });

    // Deleting a bucket archives it and its Plan category. Money must be moved out first so nothing is stranded in an
    // archived category; item history is kept with the archived bucket.
    public Task<WishlistResult> DeleteBucketAsync(string budgetId, Guid bucketId, WishlistVersionRequest request, CancellationToken ct) =>
        WriteAsync(budgetId, request.Version, requireEnabled: true, touchesPlan: true, ct, async state =>
        {
            var bucket = state.ActiveBuckets.SingleOrDefault(b => b.Id == bucketId);
            if (bucket is null) return WishlistResult.Fail("Bucket not found.", 404);
            var categoryId = WishlistRules.CategoryId(bucketId);
            var current = CurrentMonth();
            var balances = await db.Categories.AsNoTracking()
                .Where(c => c.BudgetId == budgetId && c.Id == categoryId)
                .Select(c => new { c.BudgetMonth, c.OpeningAvailable, c.Assigned, c.Activity }).ToListAsync(ct);
            var latest = balances.OrderByDescending(x => x.BudgetMonth, StringComparer.Ordinal).FirstOrDefault()?.BudgetMonth;
            var blocking = balances
                .Where(x => string.CompareOrdinal(x.BudgetMonth, current) >= 0 || x.BudgetMonth == latest)
                .Select(x => (x.BudgetMonth, Available: decimal.Round(x.OpeningAvailable + x.Assigned + x.Activity, 2)))
                .Where(x => x.Available != 0).OrderBy(x => x.BudgetMonth, StringComparer.Ordinal).FirstOrDefault();
            if (blocking.BudgetMonth is not null)
                return WishlistResult.Fail($"Move this bucket's money ({blocking.Available.ToString("0.00", CultureInfo.InvariantCulture)} available in {blocking.BudgetMonth}) to another category in Plan before deleting it.");
            bucket.IsDeleted = true;
            bucket.UpdatedAt = clock.GetUtcNow();
            Renumber(state.ActiveBuckets);
            return null;
        });

    public Task<WishlistResult> CreateItemAsync(string budgetId, Guid bucketId, WishlistItemRequest request, CancellationToken ct) =>
        WriteAsync(budgetId, request.Version, requireEnabled: true, touchesPlan: false, ct, async state =>
        {
            if (WishlistRules.ValidateItem(request) is { } error) return WishlistResult.Fail(error);
            if (state.ActiveBuckets.All(b => b.Id != bucketId)) return WishlistResult.Fail("Bucket not found.", 404);
            var items = await db.WishlistItems.Where(x => x.BudgetId == budgetId).ToListAsync(ct);
            var active = items.Where(x => x.BucketId == bucketId && x.Status == WishlistRules.ActiveStatus).ToList();
            if (active.Count >= WishlistRules.MaxActiveItemsPerBucket || items.Count >= WishlistRules.MaxItemsPerBudget)
                return WishlistResult.Fail($"A bucket can hold at most {WishlistRules.MaxActiveItemsPerBucket} items.");
            var now = clock.GetUtcNow();
            db.WishlistItems.Add(new WishlistItemEntity
            {
                Id = Guid.NewGuid(), BudgetId = budgetId, BucketId = bucketId, Name = request.Name.Trim(), Url = WishlistRules.NormalizeUrl(request.Url),
                Cost = request.Cost, Description = request.Description?.Trim() ?? "", SortOrder = active.Count,
                Status = WishlistRules.ActiveStatus, CreatedAt = now, UpdatedAt = now
            });
            return null;
        });

    // Edits an active item; a different BucketId moves it to the end of that bucket's desire order.
    public Task<WishlistResult> UpdateItemAsync(string budgetId, Guid itemId, WishlistItemRequest request, CancellationToken ct) =>
        WriteAsync(budgetId, request.Version, requireEnabled: true, touchesPlan: false, ct, async state =>
        {
            if (WishlistRules.ValidateItem(request) is { } error) return WishlistResult.Fail(error);
            var items = await db.WishlistItems.Where(x => x.BudgetId == budgetId).ToListAsync(ct);
            var item = items.SingleOrDefault(x => x.Id == itemId);
            if (item is null || state.ActiveBuckets.All(b => b.Id != item.BucketId)) return WishlistResult.Fail("Item not found.", 404);
            if (item.Status != WishlistRules.ActiveStatus) return WishlistResult.Fail("Restore this item before editing it.");
            var target = request.BucketId ?? item.BucketId;
            if (target != item.BucketId)
            {
                if (state.ActiveBuckets.All(b => b.Id != target)) return WishlistResult.Fail("Destination bucket not found.", 404);
                var destination = ActiveItems(items, target);
                if (destination.Count >= WishlistRules.MaxActiveItemsPerBucket) return WishlistResult.Fail($"A bucket can hold at most {WishlistRules.MaxActiveItemsPerBucket} items.");
                var source = item.BucketId;
                item.BucketId = target;
                item.SortOrder = destination.Count;
                Renumber(ActiveItems(items, source));
            }
            item.Name = request.Name.Trim();
            item.Url = WishlistRules.NormalizeUrl(request.Url);
            item.Cost = request.Cost;
            item.Description = request.Description?.Trim() ?? "";
            item.UpdatedAt = clock.GetUtcNow();
            return null;
        });

    public Task<WishlistResult> ReorderItemsAsync(string budgetId, Guid bucketId, WishlistItemOrderRequest request, CancellationToken ct) =>
        WriteAsync(budgetId, request.Version, requireEnabled: true, touchesPlan: false, ct, async state =>
        {
            if (state.ActiveBuckets.All(b => b.Id != bucketId)) return WishlistResult.Fail("Bucket not found.", 404);
            var active = ActiveItems(await db.WishlistItems.Where(x => x.BudgetId == budgetId && x.BucketId == bucketId).ToListAsync(ct), bucketId);
            var ids = request.ItemIds ?? [];
            if (ids.Count != active.Count || ids.Distinct().Count() != ids.Count || !ids.All(id => active.Any(x => x.Id == id)))
                return WishlistResult.Fail("Item order must list every active item in the bucket exactly once.");
            var now = clock.GetUtcNow();
            for (var index = 0; index < ids.Count; index++)
            {
                var item = active.Single(x => x.Id == ids[index]);
                if (item.SortOrder == index) continue;
                item.SortOrder = index;
                item.UpdatedAt = now;
            }
            return null;
        });

    public Task<WishlistResult> SetItemStatusAsync(string budgetId, Guid itemId, string status, WishlistVersionRequest request, CancellationToken ct) =>
        WriteAsync(budgetId, request.Version, requireEnabled: true, touchesPlan: false, ct, async state =>
        {
            var items = await db.WishlistItems.Where(x => x.BudgetId == budgetId).ToListAsync(ct);
            var item = items.SingleOrDefault(x => x.Id == itemId);
            if (item is null || state.ActiveBuckets.All(b => b.Id != item.BucketId)) return WishlistResult.Fail("Item not found.", 404);
            var now = clock.GetUtcNow();
            if (status == WishlistRules.ActiveStatus)
            {
                if (item.Status == WishlistRules.ActiveStatus) return WishlistResult.Fail("This item is already on the wishlist.");
                var active = ActiveItems(items, item.BucketId);
                if (active.Count >= WishlistRules.MaxActiveItemsPerBucket) return WishlistResult.Fail($"A bucket can hold at most {WishlistRules.MaxActiveItemsPerBucket} items.");
                item.Status = WishlistRules.ActiveStatus;
                item.SortOrder = active.Count;
                item.PurchasedAt = null;
                item.RemovedAt = null;
                // Restoring from history keeps any recorded transaction; it only unlinks it from the item.
                item.PurchaseTransactionId = null;
            }
            else
            {
                if (item.Status != WishlistRules.ActiveStatus) return WishlistResult.Fail("Only items still on the wishlist can be marked purchased or removed.");
                item.Status = status;
                if (status == WishlistRules.PurchasedStatus) item.PurchasedAt = now; else item.RemovedAt = now;
                Renumber(ActiveItems(items, item.BucketId));
            }
            item.UpdatedAt = now;
            return null;
        });

    private async Task<WishlistResult> WriteAsync(string budgetId, long version, bool requireEnabled, bool touchesPlan, CancellationToken ct,
        Func<WishlistState, Task<WishlistResult?>> change)
    {
        if (version < 0) return WishlistResult.Fail("Wishlist version is invalid.");
        var state = await LoadAsync(budgetId, ct);
        if (state.Settings.Version != version) return WishlistResult.Conflict(await OverviewAsync(budgetId, state, null, ct));
        if (requireEnabled && !state.Settings.IsEnabled) return WishlistResult.Fail("Enable Wishlist for this budget first.");

        var failure = await change(state);
        if (failure is not null) return failure;

        if (touchesPlan) await SyncPlanAsync(budgetId, state, ct);
        state.Settings.Version++;
        state.Settings.UpdatedAt = clock.GetUtcNow();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return WishlistResult.Conflict(await OverviewAsync(budgetId, await LoadAsync(budgetId, ct), null, ct));
        }
        return WishlistResult.Success(await OverviewAsync(budgetId, state, null, ct));
    }

    // Applies the Wishlist structure to every initialized month, advancing the version of each changed month.
    private async Task SyncPlanAsync(string budgetId, WishlistState state, CancellationToken ct)
    {
        var months = await db.Budgets.Where(b => b.BudgetId == budgetId)
            .Include(b => b.Groups).ThenInclude(g => g.Categories).OrderBy(b => b.Month).ToListAsync(ct);
        var ordered = OrderedBuckets(state);
        foreach (var month in months)
        {
            if (WishlistRules.EnsureCategories(month, ordered, state.Settings.IsEnabled)) month.Version++;
        }
    }

    private async Task<WishlistOverviewDto> OverviewAsync(string budgetId, WishlistState state, string? requestedMonth, CancellationToken ct)
    {
        var month = requestedMonth ?? CurrentMonth();
        var buckets = state.ActiveBuckets;
        var items = await db.WishlistItems.AsNoTracking().Where(x => x.BudgetId == budgetId).ToListAsync(ct);
        var purchases = await WishlistPurchases.LoadAsync(db, budgetId, items, ct);
        WishlistItemDto ToDto(WishlistItemEntity x) => WishlistService.ToDto(x, purchases);
        var available = new Dictionary<string, decimal>(StringComparer.Ordinal);
        if (state.Settings.IsEnabled)
        {
            var snapshot = await budgets.GetMonthAsync(budgetId, month, ct);
            // A month created before the earliest initialized month starts from the default structure; restore Wishlist.
            if (WishlistRules.EnsureCategories(snapshot, OrderedBuckets(state), enabled: true))
            {
                snapshot.Version++;
                await db.SaveChangesAsync(ct);
            }
            foreach (var category in snapshot.Groups.SelectMany(g => g.Categories)) available[category.Id] = category.Available;
        }

        var result = new List<WishlistBucketDto>();
        foreach (var bucket in buckets.OrderBy(b => b.SortOrder))
        {
            var bucketItems = items.Where(x => x.BucketId == bucket.Id).ToList();
            var active = ActiveItems(bucketItems, bucket.Id);
            var categoryId = WishlistRules.CategoryId(bucket.Id);
            var funds = available.GetValueOrDefault(categoryId);
            result.Add(new WishlistBucketDto(bucket.Id, bucket.Name, bucket.SortOrder, categoryId, funds,
                active.Count == 0 ? null : active.Min(x => x.Cost), active.Count == 0 ? null : active.Max(x => x.Cost),
                state.Settings.IsEnabled ? WishlistRules.Progress(active, funds) : null,
                active.Select(ToDto).ToList(),
                bucketItems.Where(x => x.Status == WishlistRules.PurchasedStatus).OrderByDescending(x => x.PurchasedAt).Take(HistoryLimit).Select(ToDto).ToList(),
                bucketItems.Where(x => x.Status == WishlistRules.RemovedStatus).OrderByDescending(x => x.RemovedAt).Take(HistoryLimit).Select(ToDto).ToList()));
        }
        return new WishlistOverviewDto(state.Settings.IsEnabled, state.Settings.Version, month,
            result.Count(b => b.Progress?.CanPurchase == true), result);
    }

    // Attaches next-item progress to each Wishlist category of a Plan month DTO (enabled budgets only).
    public static async Task<BudgetDto> AttachPlanProgressAsync(BudgetDbContext db, BudgetEntity month, BudgetDto dto, CancellationToken ct)
    {
        var categories = month.Groups.Where(g => g.Id == WishlistRules.GroupId).SelectMany(g => g.Categories).Where(c => !c.IsArchived).ToList();
        if (categories.Count == 0) return dto;
        var enabled = await db.WishlistSettings.AsNoTracking().AnyAsync(x => x.BudgetId == month.BudgetId && x.IsEnabled, ct);
        if (!enabled) return dto;
        var items = await db.WishlistItems.AsNoTracking()
            .Where(x => x.BudgetId == month.BudgetId && x.Status == WishlistRules.ActiveStatus).ToListAsync(ct);
        var progress = categories.ToDictionary(c => c.Id, c => WishlistRules.Progress(
            items.Where(x => WishlistRules.CategoryId(x.BucketId) == c.Id), c.Available), StringComparer.Ordinal);
        return dto with
        {
            Groups = dto.Groups.Select(g => g.Id != WishlistRules.GroupId ? g : g with
            {
                Categories = g.Categories.Select(c => progress.TryGetValue(c.Id, out var value) ? c with { WishlistProgress = value } : c).ToList()
            }).ToList()
        };
    }

    private async Task<WishlistState> LoadAsync(string budgetId, CancellationToken ct)
    {
        var settings = await db.WishlistSettings.SingleOrDefaultAsync(x => x.BudgetId == budgetId, ct);
        if (settings is null)
        {
            settings = new WishlistSettingsEntity { BudgetId = budgetId, Version = 0, UpdatedAt = clock.GetUtcNow() };
            db.WishlistSettings.Add(settings);
        }
        var buckets = await db.WishlistBuckets.Where(x => x.BudgetId == budgetId).ToListAsync(ct);
        return new WishlistState(settings, buckets);
    }

    private static List<WishlistBucketEntity> OrderedBuckets(WishlistState state) =>
        state.Buckets.OrderBy(b => b.IsDeleted).ThenBy(b => b.SortOrder).ThenBy(b => b.CreatedAt).ThenBy(b => b.Id).ToList();

    private static List<WishlistItemEntity> ActiveItems(IEnumerable<WishlistItemEntity> items, Guid bucketId) => items
        .Where(x => x.BucketId == bucketId && x.Status == WishlistRules.ActiveStatus)
        .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).ToList();

    private static void Renumber(IReadOnlyList<WishlistBucketEntity> buckets)
    {
        var ordered = buckets.OrderBy(b => b.SortOrder).ThenBy(b => b.CreatedAt).ToList();
        for (var index = 0; index < ordered.Count; index++) ordered[index].SortOrder = index;
    }

    private static void Renumber(IReadOnlyList<WishlistItemEntity> items)
    {
        for (var index = 0; index < items.Count; index++) items[index].SortOrder = index;
    }

    private static WishlistItemDto ToDto(WishlistItemEntity x, IReadOnlyDictionary<Guid, WishlistPurchaseDto> purchases) =>
        new(x.Id, x.BucketId, x.Name, x.Url, x.Cost, x.Description, x.SortOrder, x.Status, x.CreatedAt, x.PurchasedAt, x.RemovedAt,
            x.PurchaseTransactionId is { } transactionId ? purchases.GetValueOrDefault(transactionId) : null);

    private string CurrentMonth() => clock.GetLocalNow().ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static bool IsMonth(string month) => Regex.IsMatch(month, @"^\d{4}-(0[1-9]|1[0-2])$");

    private sealed record WishlistState(WishlistSettingsEntity Settings, List<WishlistBucketEntity> Buckets)
    {
        public List<WishlistBucketEntity> ActiveBuckets => Buckets.Where(b => !b.IsDeleted).OrderBy(b => b.SortOrder).ThenBy(b => b.CreatedAt).ToList();
    }

    public static void MapEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/budgets/{id}/wishlist").RequireAuthorization();
        group.MapGet("", (string id, string? month, ClaimsPrincipal principal, HttpContext http, WishlistService service, CancellationToken ct) =>
            Run(id, principal, http, ct, () => service.GetAsync(id, month, ct))).RequireEntitlement(WishlistRules.EntitlementKey);
        group.MapPost("/enable", (string id, WishlistVersionRequest request, ClaimsPrincipal principal, HttpContext http, WishlistService service, CancellationToken ct) =>
            Run(id, principal, http, ct, () => service.SetEnabledAsync(id, true, request, ct))).RequireEntitlement(WishlistRules.EntitlementKey);
        group.MapPost("/disable", (string id, WishlistVersionRequest request, ClaimsPrincipal principal, HttpContext http, WishlistService service, CancellationToken ct) =>
            Run(id, principal, http, ct, () => service.SetEnabledAsync(id, false, request, ct))).RequireEntitlement(WishlistRules.EntitlementKey);
        group.MapPost("/buckets", (string id, WishlistBucketRequest request, ClaimsPrincipal principal, HttpContext http, WishlistService service, CancellationToken ct) =>
            Run(id, principal, http, ct, () => service.CreateBucketAsync(id, request, ct))).RequireEntitlement(WishlistRules.EntitlementKey);
        group.MapPut("/buckets/order", (string id, WishlistBucketOrderRequest request, ClaimsPrincipal principal, HttpContext http, WishlistService service, CancellationToken ct) =>
            Run(id, principal, http, ct, () => service.ReorderBucketsAsync(id, request, ct))).RequireEntitlement(WishlistRules.EntitlementKey);
        group.MapPut("/buckets/{bucketId:guid}", (string id, Guid bucketId, WishlistBucketRequest request, ClaimsPrincipal principal, HttpContext http, WishlistService service, CancellationToken ct) =>
            Run(id, principal, http, ct, () => service.RenameBucketAsync(id, bucketId, request, ct))).RequireEntitlement(WishlistRules.EntitlementKey);
        group.MapPost("/buckets/{bucketId:guid}/delete", (string id, Guid bucketId, WishlistVersionRequest request, ClaimsPrincipal principal, HttpContext http, WishlistService service, CancellationToken ct) =>
            Run(id, principal, http, ct, () => service.DeleteBucketAsync(id, bucketId, request, ct))).RequireEntitlement(WishlistRules.EntitlementKey);
        group.MapPost("/buckets/{bucketId:guid}/items", (string id, Guid bucketId, WishlistItemRequest request, ClaimsPrincipal principal, HttpContext http, WishlistService service, CancellationToken ct) =>
            Run(id, principal, http, ct, () => service.CreateItemAsync(id, bucketId, request, ct))).RequireEntitlement(WishlistRules.EntitlementKey);
        group.MapPut("/buckets/{bucketId:guid}/items/order", (string id, Guid bucketId, WishlistItemOrderRequest request, ClaimsPrincipal principal, HttpContext http, WishlistService service, CancellationToken ct) =>
            Run(id, principal, http, ct, () => service.ReorderItemsAsync(id, bucketId, request, ct))).RequireEntitlement(WishlistRules.EntitlementKey);
        group.MapPut("/items/{itemId:guid}", (string id, Guid itemId, WishlistItemRequest request, ClaimsPrincipal principal, HttpContext http, WishlistService service, CancellationToken ct) =>
            Run(id, principal, http, ct, () => service.UpdateItemAsync(id, itemId, request, ct))).RequireEntitlement(WishlistRules.EntitlementKey);
        group.MapPost("/items/{itemId:guid}/purchase", (string id, Guid itemId, WishlistVersionRequest request, ClaimsPrincipal principal, HttpContext http, WishlistService service, CancellationToken ct) =>
            Run(id, principal, http, ct, () => service.SetItemStatusAsync(id, itemId, WishlistRules.PurchasedStatus, request, ct))).RequireEntitlement(WishlistRules.EntitlementKey);
        group.MapPost("/items/{itemId:guid}/remove", (string id, Guid itemId, WishlistVersionRequest request, ClaimsPrincipal principal, HttpContext http, WishlistService service, CancellationToken ct) =>
            Run(id, principal, http, ct, () => service.SetItemStatusAsync(id, itemId, WishlistRules.RemovedStatus, request, ct))).RequireEntitlement(WishlistRules.EntitlementKey);
        group.MapPost("/items/{itemId:guid}/restore", (string id, Guid itemId, WishlistVersionRequest request, ClaimsPrincipal principal, HttpContext http, WishlistService service, CancellationToken ct) =>
            Run(id, principal, http, ct, () => service.SetItemStatusAsync(id, itemId, WishlistRules.ActiveStatus, request, ct))).RequireEntitlement(WishlistRules.EntitlementKey);
        WishlistPurchases.MapEndpoint(group);
    }

    // Owners and joined editors of an active budget; the gate transaction commits only successful results.
    private static async Task<IResult> Run(string budgetId, ClaimsPrincipal principal, HttpContext http, CancellationToken ct, Func<Task<WishlistResult>> action)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();
        var services = http.RequestServices;
        if (!await services.GetRequiredService<BudgetOwnershipService>().CanAccessBudgetAsync(userId, budgetId, ct)) return Results.NotFound();
        services.GetRequiredService<BudgetRequestContext>().BudgetId = budgetId;
        var result = await services.GetRequiredService<LedgerGate>().RunAsync(action, ct, value => value.Error is null);
        return result.StatusCode switch
        {
            200 => Results.Ok(result.Value),
            409 => Results.Conflict(new { error = result.Error, current = result.Value }),
            404 => Results.NotFound(new { error = result.Error }),
            _ => Results.BadRequest(new { error = result.Error })
        };
    }
}
