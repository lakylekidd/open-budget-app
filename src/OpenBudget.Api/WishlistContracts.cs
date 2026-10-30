namespace OpenBudget.Api;

// Every Wishlist write carries the budget's Wishlist version (from the last overview) and returns the new overview.
public sealed record WishlistVersionRequest(long Version);
public sealed record WishlistBucketRequest(long Version, string Name);
public sealed record WishlistBucketOrderRequest(long Version, IReadOnlyList<Guid> BucketIds);
public sealed record WishlistItemRequest(long Version, string Name, string? Url, decimal Cost, string? Description, Guid? BucketId = null);
public sealed record WishlistItemOrderRequest(long Version, IReadOnlyList<Guid> ItemIds);

// Server-calculated progress toward a bucket's next purchasable item (its top active item by desire order).
// Available is the bucket category's Available in the evaluated month; Remaining = max(Cost - Available, 0).
public sealed record WishlistProgressDto(Guid NextItemId, string NextItemName, decimal Cost, decimal Available, decimal Remaining,
    bool CanPurchase, decimal FundedFraction);

public sealed record WishlistItemDto(Guid Id, Guid BucketId, string Name, string? Url, decimal Cost, string Description, int SortOrder,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset? PurchasedAt, DateTimeOffset? RemovedAt);

public sealed record WishlistBucketDto(Guid Id, string Name, int SortOrder, string CategoryId, decimal Available,
    decimal? MinCost, decimal? MaxCost, WishlistProgressDto? Progress,
    List<WishlistItemDto> Items, List<WishlistItemDto> Purchased, List<WishlistItemDto> Removed);

public sealed record WishlistOverviewDto(bool IsEnabled, long Version, string Month, int ReadyCount, List<WishlistBucketDto> Buckets);

public sealed record WishlistResult(WishlistOverviewDto? Value, string? Error, int StatusCode)
{
    public static WishlistResult Success(WishlistOverviewDto value) => new(value, null, 200);
    public static WishlistResult Fail(string error, int statusCode = 400) => new(null, error, statusCode);
    public static WishlistResult Conflict(WishlistOverviewDto current) => new(current, "Wishlist changed in another session. Reload and retry.", 409);
}
