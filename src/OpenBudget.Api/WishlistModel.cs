using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

// Per-budget Wishlist switch. Version guards every Wishlist write for the budget (settings, buckets and items).
public sealed class WishlistSettingsEntity
{
    public string BudgetId { get; set; } = "";
    public bool IsEnabled { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

// A bucket owns one Plan category (wl-{id}) in the Wishlist group. Deleting a bucket archives it (and its category).
public sealed class WishlistBucketEntity
{
    public Guid Id { get; set; }
    public string BudgetId { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class WishlistItemEntity
{
    public Guid Id { get; set; }
    public string BudgetId { get; set; } = "";
    public Guid BucketId { get; set; }
    public string Name { get; set; } = "";
    public string? Url { get; set; }
    public decimal Cost { get; set; }
    public string Description { get; set; } = "";
    // Desire order among the bucket's active items; the lowest active item is the next purchase candidate.
    public int SortOrder { get; set; }
    public string Status { get; set; } = WishlistRules.ActiveStatus;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PurchasedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
}

public static class WishlistModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WishlistSettingsEntity>(entity =>
        {
            entity.HasKey(x => x.BudgetId);
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne<BudgetPlanEntity>().WithMany().HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<WishlistBucketEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(WishlistRules.MaxBucketNameLength);
            entity.HasIndex(x => new { x.BudgetId, x.IsDeleted, x.SortOrder });
            entity.HasOne<BudgetPlanEntity>().WithMany().HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<WishlistItemEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(WishlistRules.MaxItemNameLength);
            entity.Property(x => x.Url).HasMaxLength(WishlistRules.MaxUrlLength);
            entity.Property(x => x.Description).HasMaxLength(WishlistRules.MaxDescriptionLength);
            entity.Property(x => x.Status).HasMaxLength(10);
            entity.Property(x => x.Cost).HasPrecision(14, 2);
            entity.HasIndex(x => new { x.BucketId, x.Status, x.SortOrder });
            entity.HasIndex(x => x.BudgetId);
            entity.HasOne<WishlistBucketEntity>().WithMany().HasForeignKey(x => x.BucketId).OnDelete(DeleteBehavior.Cascade);
        });
        // Wishlist is a paid-tier entitlement by default: Plus and Pro allowed, Free denied (administrators can override per user).
        modelBuilder.Entity<MembershipPlanEntitlementEntity>().HasData(
            new MembershipPlanEntitlementEntity { MembershipPlanId = "free", EntitlementKey = WishlistRules.EntitlementKey, IsEnabled = false },
            new MembershipPlanEntitlementEntity { MembershipPlanId = "plus", EntitlementKey = WishlistRules.EntitlementKey, IsEnabled = true },
            new MembershipPlanEntitlementEntity { MembershipPlanId = "pro", EntitlementKey = WishlistRules.EntitlementKey, IsEnabled = true });
    }
}
