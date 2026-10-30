using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public sealed class WishlistPurchaseRulesTests
{
    private static readonly Guid BucketId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static WishlistItemEntity Item(string name = "Headphones", int order = 0, string status = WishlistRules.ActiveStatus) => new()
    {
        Id = Guid.NewGuid(), BucketId = BucketId, Name = name, Cost = 120m, SortOrder = order, Status = status
    };

    private static TransactionRequest Request(decimal amount = -120m, string? categoryId = null, List<SplitDto>? splits = null, string repeat = "never") =>
        new(Guid.NewGuid(), 1, Guid.NewGuid(), new DateOnly(2026, 10, 21), amount, "Headphones", null,
            categoryId ?? WishlistRules.CategoryId(BucketId), "uncleared", splits, repeat);

    [Fact]
    public void An_outflow_in_the_bucket_category_is_a_valid_purchase()
    {
        Assert.Null(WishlistPurchaseRules.Validate(Item(), Request()));
        // The actual price may differ from the listed cost.
        Assert.Null(WishlistPurchaseRules.Validate(Item(), Request(-99.95m)));
    }

    [Theory]
    [InlineData(WishlistRules.PurchasedStatus)]
    [InlineData(WishlistRules.RemovedStatus)]
    public void Only_active_items_can_be_purchased(string status) =>
        Assert.Contains("already purchased or removed", WishlistPurchaseRules.Validate(Item(status: status), Request()));

    [Fact]
    public void Inflows_other_categories_splits_and_repeats_are_rejected()
    {
        Assert.Equal("A wishlist purchase must be an outflow.", WishlistPurchaseRules.Validate(Item(), Request(25m)));
        Assert.Equal("A wishlist purchase must use its bucket's category.", WishlistPurchaseRules.Validate(Item(), Request(categoryId: "groceries")));
        Assert.Equal("A wishlist purchase must use its bucket's category.", WishlistPurchaseRules.Validate(Item(), Request(categoryId: WishlistRules.CategoryId(Guid.NewGuid()))));
        Assert.Equal("A wishlist purchase cannot be split.", WishlistPurchaseRules.Validate(Item(),
            Request(categoryId: "", splits: [new SplitDto(WishlistRules.CategoryId(BucketId), -120m)])));
        Assert.Equal("A wishlist purchase cannot repeat.", WishlistPurchaseRules.Validate(Item(), Request(repeat: "monthly")));
    }

    [Theory]
    [InlineData(0, 3, 0)]
    [InlineData(2, 3, 2)]
    [InlineData(5, 3, 3)]
    [InlineData(-1, 3, 0)]
    [InlineData(4, 0, 0)]
    public void Restored_items_return_to_their_previous_position_clamped_to_the_bucket(int previous, int activeCount, int expected) =>
        Assert.Equal(expected, WishlistPurchaseRules.RestorePosition(previous, activeCount));

    [Fact]
    public void Inserting_a_restored_item_renumbers_the_desire_order()
    {
        var first = Item("First", 0);
        var second = Item("Second", 1);
        var restored = Item("Restored", 1, WishlistRules.PurchasedStatus);
        var ordered = new List<WishlistItemEntity> { first, second };
        WishlistPurchaseRules.InsertAt(ordered, restored, 1);
        Assert.Equal(["First", "Restored", "Second"], ordered.Select(x => x.Name));
        Assert.Equal([0, 1, 2], ordered.Select(x => x.SortOrder));
    }
}
