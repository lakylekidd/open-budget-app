namespace OpenBudget.Api;

// Wishlist domain rules: validation, desire-order progress and the Plan structure owned by Wishlist buckets.
// When Wishlist is enabled, every initialized month holds the "wishlist" group with one "wl-{bucketId}" category per
// bucket, named after the bucket and ordered like the buckets. Deleted buckets keep an archived category so their
// history and carried money are never discarded. Disabling archives the group; re-enabling restores it.
public static class WishlistRules
{
    public const string EntitlementKey = "wishlist";
    public const string GroupId = "wishlist";
    public const string GroupName = "Wishlist";
    public const string CategoryIcon = "🎁";
    public const string ActiveStatus = "active";
    public const string PurchasedStatus = "purchased";
    public const string RemovedStatus = "removed";
    private const string CategoryPrefix = "wl-";

    public const int MaxBucketNameLength = 60;
    public const int MaxItemNameLength = 120;
    public const int MaxUrlLength = 2048;
    public const int MaxDescriptionLength = 1000;
    public const int MaxActiveBuckets = 50;
    public const int MaxBuckets = 200;
    public const int MaxActiveItemsPerBucket = 200;
    public const int MaxItemsPerBudget = 5000;
    public const decimal MaxCost = 999_999_999_999.99m;

    public static string CategoryId(Guid bucketId) => CategoryPrefix + bucketId.ToString("D");
    public static bool IsWishlistCategory(string? categoryId) => categoryId?.StartsWith(CategoryPrefix, StringComparison.Ordinal) == true;

    public static string? ValidateBucketName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > MaxBucketNameLength
            ? $"Bucket name is required and must be {MaxBucketNameLength} characters or fewer."
            : null;
    }

    public static string? ValidateItem(WishlistItemRequest request)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxItemNameLength)
            return $"Item name is required and must be {MaxItemNameLength} characters or fewer.";
        if (request.Cost <= 0 || request.Cost > MaxCost || decimal.Round(request.Cost, 2) != request.Cost)
            return "Cost must be a positive amount with at most two decimal places.";
        if ((request.Description?.Length ?? 0) > MaxDescriptionLength)
            return $"Description must be {MaxDescriptionLength} characters or fewer.";
        return ValidateUrl(request.Url);
    }

    // Links are optional; when present they must be absolute http(s) URLs without embedded credentials.
    public static string? ValidateUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.Length > MaxUrlLength) return $"Link must be {MaxUrlLength} characters or fewer.";
        if (trimmed.Any(char.IsWhiteSpace) || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
            return "Link must be a web address starting with http:// or https://.";
        return null;
    }

    public static string? NormalizeUrl(string? url) => string.IsNullOrWhiteSpace(url) ? null : url.Trim();

    // The next item is the bucket's first active item in desire order; purchased and removed items are skipped.
    public static WishlistItemEntity? NextItem(IEnumerable<WishlistItemEntity> items) => items
        .Where(x => x.Status == ActiveStatus)
        .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)
        .FirstOrDefault();

    public static WishlistProgressDto? Progress(IEnumerable<WishlistItemEntity> items, decimal available)
    {
        var next = NextItem(items);
        if (next is null) return null;
        var remaining = Math.Max(next.Cost - available, 0m);
        var fraction = available <= 0 ? 0m : Math.Min(1m, decimal.Round(available / next.Cost, 4, MidpointRounding.ToZero));
        return new WishlistProgressDto(next.Id, next.Name, next.Cost, available, remaining, available >= next.Cost, fraction);
    }

    // Keeps one month's Wishlist group/categories aligned with the buckets. Buckets must be ordered by SortOrder.
    // Returns whether anything changed so the caller can advance the month version.
    public static bool EnsureCategories(BudgetEntity month, IReadOnlyList<WishlistBucketEntity> buckets, bool enabled)
    {
        var changed = false;
        var group = month.Groups.SingleOrDefault(g => g.Id == GroupId);
        if (group is null)
        {
            if (!enabled) return false;
            group = new CategoryGroupEntity
            {
                BudgetId = month.BudgetId, BudgetMonth = month.Month, Id = GroupId, Name = GroupName,
                SortOrder = month.Groups.Count == 0 ? 0 : month.Groups.Max(g => g.SortOrder) + 1
            };
            month.Groups.Add(group);
            changed = true;
        }
        if (group.Name != GroupName) { group.Name = GroupName; changed = true; }
        if (group.IsArchived == enabled) { group.IsArchived = !enabled; changed = true; }

        var byId = group.Categories.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var ordered = new List<CategoryEntity>();
        foreach (var bucket in buckets.Where(b => !b.IsDeleted))
        {
            var id = CategoryId(bucket.Id);
            if (!byId.TryGetValue(id, out var category))
            {
                category = new CategoryEntity
                {
                    BudgetId = month.BudgetId, BudgetMonth = month.Month, GroupId = GroupId, Id = id, Name = bucket.Name,
                    Icon = CategoryIcon, SortOrder = int.MaxValue
                };
                group.Categories.Add(category);
                byId[id] = category;
                changed = true;
            }
            changed |= Apply(category, bucket.Name, archived: false);
            ordered.Add(category);
        }
        var deleted = buckets.Where(b => b.IsDeleted).ToDictionary(b => CategoryId(b.Id), StringComparer.Ordinal);
        foreach (var category in group.Categories.Where(c => !ordered.Contains(c)).OrderBy(c => c.SortOrder).ToList())
        {
            if (deleted.TryGetValue(category.Id, out var bucket)) changed |= Apply(category, bucket.Name, archived: true);
            ordered.Add(category);
        }
        for (var index = 0; index < ordered.Count; index++)
        {
            if (ordered[index].SortOrder == index) continue;
            ordered[index].SortOrder = index;
            changed = true;
        }
        return changed;
    }

    private static bool Apply(CategoryEntity category, string name, bool archived)
    {
        if (category.Name == name && category.IsArchived == archived && category.Icon == CategoryIcon) return false;
        category.Name = name;
        category.IsArchived = archived;
        category.Icon = CategoryIcon;
        return true;
    }

    // Plan saves may assign/move money in Wishlist categories and move the whole group, but the group's name, archive
    // state, membership and category order/names/archive state are owned by Wishlist buckets.
    public static string? ValidatePlanUpdate(BudgetEntity current, IReadOnlyList<CategoryGroupUpdateDto> requested)
    {
        const string message = "The Wishlist group is managed from the Wishlist screen. Assign money here; rename, reorder, add or archive buckets in Wishlist.";
        var currentGroup = current.Groups.SingleOrDefault(g => g.Id == GroupId);
        var currentCategories = currentGroup?.Categories.ToDictionary(c => c.Id, StringComparer.Ordinal) ?? [];
        foreach (var group in requested)
        {
            var inWishlistGroup = group.Id == GroupId;
            if (group.Categories.Any(c => IsWishlistCategory(c.Id) != inWishlistGroup))
                return "Wishlist categories stay in the Wishlist group, which holds only them.";
            if (!inWishlistGroup) continue;
            if (currentGroup is null || group.Name.Trim() != currentGroup.Name || group.IsArchived != currentGroup.IsArchived) return message;
            foreach (var category in group.Categories)
            {
                if (!currentCategories.TryGetValue(category.Id, out var existing) || category.Name.Trim() != existing.Name ||
                    category.IsArchived != existing.IsArchived || category.Icon != existing.Icon)
                    return message;
            }
            var requestedOrder = group.Categories.OrderBy(c => c.SortOrder).Select(c => c.Id);
            var currentOrder = currentGroup.Categories.OrderBy(c => c.SortOrder).Select(c => c.Id);
            if (!requestedOrder.SequenceEqual(currentOrder, StringComparer.Ordinal)) return message;
        }
        return null;
    }
}
