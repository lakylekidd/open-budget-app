using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed class BudgetDbContext(DbContextOptions<BudgetDbContext> options) : DbContext(options)
{
    public DbSet<BudgetPlanEntity> BudgetPlans => Set<BudgetPlanEntity>();
    public DbSet<BudgetEntity> Budgets => Set<BudgetEntity>();
    public DbSet<CategoryGroupEntity> CategoryGroups => Set<CategoryGroupEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BudgetPlanEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasMany(x => x.Months).WithOne(x => x.Plan).HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BudgetEntity>(entity =>
        {
            entity.HasKey(x => x.Month);
            entity.Property(x => x.Month).HasMaxLength(7);
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.Property(x => x.FundsAdded).HasPrecision(14, 2);
            entity.Property(x => x.OpeningReadyToAssign).HasPrecision(14, 2);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasMany(x => x.Groups).WithOne().HasForeignKey(x => x.BudgetMonth).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CategoryGroupEntity>(entity =>
        {
            entity.HasKey(x => new { x.BudgetMonth, x.Id });
            entity.Property(x => x.Id).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.HasMany(x => x.Categories).WithOne().HasForeignKey(x => new { x.BudgetMonth, x.GroupId }).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CategoryEntity>(entity =>
        {
            entity.HasKey(x => new { x.BudgetMonth, x.GroupId, x.Id });
            entity.Property(x => x.Id).HasMaxLength(100);
            entity.Property(x => x.GroupId).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Icon).HasMaxLength(24);
            entity.Property(x => x.OpeningAvailable).HasPrecision(14, 2);
            entity.Property(x => x.Assigned).HasPrecision(14, 2);
            entity.Property(x => x.Activity).HasPrecision(14, 2);
            entity.Property(x => x.Target).HasPrecision(14, 2);
        });
    }
}

public sealed class BudgetPlanEntity
{
    public string Id { get; set; } = "default";
    public string Name { get; set; } = "My Budget";
    public string Currency { get; set; } = "EUR";
    public long Version { get; set; } = 1;
    public List<BudgetEntity> Months { get; set; } = [];
}

public sealed class BudgetEntity
{
    public string Month { get; set; } = "";
    public string BudgetId { get; set; } = "default";
    public BudgetPlanEntity? Plan { get; set; }
    public string Note { get; set; } = "";
    public decimal OpeningReadyToAssign { get; set; }
    public decimal FundsAdded { get; set; }
    public long Version { get; set; } = 1;
    public List<CategoryGroupEntity> Groups { get; set; } = [];
}

public sealed class CategoryGroupEntity
{
    public string BudgetMonth { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public List<CategoryEntity> Categories { get; set; } = [];
}

public sealed class CategoryEntity
{
    public string BudgetMonth { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal OpeningAvailable { get; set; }
    public decimal Assigned { get; set; }
    // Existing prototype activity is retained as a legacy month baseline until the transaction ledger ships.
    public decimal Activity { get; set; }
    public decimal Target { get; set; }
    public string? Icon { get; set; }
    public bool Snoozed { get; set; }
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }

    public decimal Available => decimal.Round(OpeningAvailable + Assigned + Activity, 2, MidpointRounding.ToEven);
}
