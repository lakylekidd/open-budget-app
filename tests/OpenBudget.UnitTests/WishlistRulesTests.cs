using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public sealed class WishlistRulesTests
{
    private static readonly DateTimeOffset Created = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    private static WishlistItemEntity Item(string name, decimal cost, int order, string status = WishlistRules.ActiveStatus) => new()
    {
        Id = Guid.NewGuid(), Name = name, Cost = cost, SortOrder = order, Status = status, CreatedAt = Created.AddMinutes(order)
    };

    [Fact]
    public void Progress_targets_the_top_item_by_desire_order()
    {
        var items = new[] { Item("Console", 450m, 1), Item("Headphones", 120m, 0) };
        var progress = WishlistRules.Progress(items, 50m)!;
        Assert.Equal("Headphones", progress.NextItemName);
        Assert.Equal(120m, progress.Cost);
        Assert.Equal(50m, progress.Available);
        Assert.Equal(70m, progress.Remaining);
        Assert.False(progress.CanPurchase);
        Assert.Equal(0.4166m, progress.FundedFraction);
    }

    [Theory]
    [InlineData(119.99, false, 0.01)]
    [InlineData(120.00, true, 0)]
    [InlineData(150.00, true, 0)]
    public void Can_purchase_starts_exactly_when_available_equals_cost(decimal available, bool canPurchase, decimal remaining)
    {
        var progress = WishlistRules.Progress([Item("Headphones", 120m, 0)], available)!;
        Assert.Equal(canPurchase, progress.CanPurchase);
        Assert.Equal(remaining, progress.Remaining);
        Assert.Equal(canPurchase ? 1m : 0.9999m, progress.FundedFraction);
    }

    [Fact]
    public void Reordering_changes_the_next_item()
    {
        var cheap = Item("Cable", 15m, 0);
        var expensive = Item("Monitor", 300m, 1);
        Assert.True(WishlistRules.Progress([cheap, expensive], 20m)!.CanPurchase);
        cheap.SortOrder = 1;
        expensive.SortOrder = 0;
        var progress = WishlistRules.Progress([cheap, expensive], 20m)!;
        Assert.Equal(expensive.Id, progress.NextItemId);
        Assert.False(progress.CanPurchase);
        Assert.Equal(280m, progress.Remaining);
    }

    [Fact]
    public void Purchased_and_removed_items_are_skipped()
    {
        var items = new[]
        {
            Item("Bought", 10m, 0, WishlistRules.PurchasedStatus),
            Item("Dropped", 5m, 1, WishlistRules.RemovedStatus),
            Item("Keyboard", 80m, 2)
        };
        Assert.Equal("Keyboard", WishlistRules.Progress(items, 0m)!.NextItemName);
    }

    [Fact]
    public void Empty_or_fully_purchased_bucket_has_no_progress()
    {
        Assert.Null(WishlistRules.Progress([], 100m));
        Assert.Null(WishlistRules.Progress([Item("Bought", 10m, 0, WishlistRules.PurchasedStatus)], 100m));
    }

    [Fact]
    public void Negative_available_counts_as_unfunded()
    {
        var progress = WishlistRules.Progress([Item("Bike", 200m, 0)], -25m)!;
        Assert.Equal(0m, progress.FundedFraction);
        Assert.Equal(225m, progress.Remaining);
        Assert.False(progress.CanPurchase);
    }

    [Theory]
    [InlineData("https://shop.example/item?id=1", true)]
    [InlineData("http://shop.example", true)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("ftp://shop.example/file", false)]
    [InlineData("shop.example/item", false)]
    [InlineData("https://user:pass@shop.example", false)]
    [InlineData("https://shop.example/a b", false)]
    public void Links_must_be_http_or_https(string? url, bool valid) => Assert.Equal(valid, WishlistRules.ValidateUrl(url) is null);

    [Fact]
    public void Links_longer_than_the_limit_are_rejected() =>
        Assert.NotNull(WishlistRules.ValidateUrl("https://shop.example/" + new string('a', WishlistRules.MaxUrlLength)));

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(10.005, false)]
    [InlineData(0.01, true)]
    [InlineData(999999999999.99, true)]
    public void Cost_must_be_positive_cents(decimal cost, bool valid) =>
        Assert.Equal(valid, WishlistRules.ValidateItem(new WishlistItemRequest(1, "Item", null, cost, null)) is null);

    [Fact]
    public void Item_and_bucket_names_are_required_and_bounded()
    {
        Assert.NotNull(WishlistRules.ValidateItem(new WishlistItemRequest(1, " ", null, 5m, null)));
        Assert.NotNull(WishlistRules.ValidateItem(new WishlistItemRequest(1, new string('x', 121), null, 5m, null)));
        Assert.NotNull(WishlistRules.ValidateItem(new WishlistItemRequest(1, "Item", null, 5m, new string('x', 1001))));
        Assert.NotNull(WishlistRules.ValidateBucketName(""));
        Assert.NotNull(WishlistRules.ValidateBucketName(new string('x', 61)));
        Assert.Null(WishlistRules.ValidateBucketName(" Games "));
    }

    [Fact]
    public void Ensure_categories_creates_orders_renames_and_archives_bucket_categories()
    {
        var month = new BudgetEntity { BudgetId = "b1", Month = "2026-10", Groups = [new CategoryGroupEntity { BudgetId = "b1", BudgetMonth = "2026-10", Id = "home", Name = "Home", SortOrder = 0 }] };
        var games = new WishlistBucketEntity { Id = Guid.NewGuid(), Name = "Games", SortOrder = 1 };
        var tech = new WishlistBucketEntity { Id = Guid.NewGuid(), Name = "Electronics", SortOrder = 0 };
        Assert.True(WishlistRules.EnsureCategories(month, [tech, games], enabled: true));
        var group = month.Groups.Single(g => g.Id == WishlistRules.GroupId);
        Assert.Equal(1, group.SortOrder);
        Assert.Equal([WishlistRules.CategoryId(tech.Id), WishlistRules.CategoryId(games.Id)], group.Categories.OrderBy(c => c.SortOrder).Select(c => c.Id));
        Assert.False(WishlistRules.EnsureCategories(month, [tech, games], enabled: true));

        games.Name = "Video games";
        games.SortOrder = 0;
        tech.SortOrder = 1;
        Assert.True(WishlistRules.EnsureCategories(month, [games, tech], enabled: true));
        Assert.Equal(["Video games", "Electronics"], group.Categories.OrderBy(c => c.SortOrder).Select(c => c.Name));

        tech.IsDeleted = true;
        Assert.True(WishlistRules.EnsureCategories(month, [games, tech], enabled: true));
        Assert.True(group.Categories.Single(c => c.Id == WishlistRules.CategoryId(tech.Id)).IsArchived);

        Assert.True(WishlistRules.EnsureCategories(month, [games, tech], enabled: false));
        Assert.True(group.IsArchived);
        Assert.False(group.Categories.Single(c => c.Id == WishlistRules.CategoryId(games.Id)).IsArchived);
    }

    [Fact]
    public void Disabled_wishlist_does_not_create_a_group()
    {
        var month = new BudgetEntity { BudgetId = "b1", Month = "2026-10" };
        Assert.False(WishlistRules.EnsureCategories(month, [new WishlistBucketEntity { Id = Guid.NewGuid(), Name = "Games" }], enabled: false));
        Assert.Empty(month.Groups);
    }

    [Fact]
    public void Plan_updates_cannot_restructure_the_wishlist_group()
    {
        var bucket = Guid.NewGuid();
        var month = new BudgetEntity { BudgetId = "b1", Month = "2026-10" };
        WishlistRules.EnsureCategories(month, [new WishlistBucketEntity { Id = bucket, Name = "Games" }], enabled: true);
        var id = WishlistRules.CategoryId(bucket);
        List<CategoryGroupUpdateDto> Request(string groupName = WishlistRules.GroupName, string categoryName = "Games", bool archived = false, string groupId = WishlistRules.GroupId, decimal assigned = 0) =>
            [new(groupId, groupName, 0, false, [new CategoryUpdateDto(id, categoryName, assigned, 0, WishlistRules.CategoryIcon, false, 0, archived)])];

        Assert.Null(WishlistRules.ValidatePlanUpdate(month, Request(assigned: 40m)));
        Assert.NotNull(WishlistRules.ValidatePlanUpdate(month, Request(groupName: "Wants")));
        Assert.NotNull(WishlistRules.ValidatePlanUpdate(month, Request(categoryName: "Renamed")));
        Assert.NotNull(WishlistRules.ValidatePlanUpdate(month, Request(archived: true)));
        Assert.NotNull(WishlistRules.ValidatePlanUpdate(month, Request(groupId: "home")));
        Assert.NotNull(WishlistRules.ValidatePlanUpdate(month,
            [new(WishlistRules.GroupId, WishlistRules.GroupName, 0, false, [new CategoryUpdateDto("custom", "Custom", 0, 0, null, false, 0, false)])]));
    }
}
