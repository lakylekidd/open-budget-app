using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed class BudgetDbContext(DbContextOptions<BudgetDbContext> options) : DbContext(options)
{
    public DbSet<BudgetEntity> Budgets => Set<BudgetEntity>();
    public DbSet<CategoryGroupEntity> CategoryGroups => Set<CategoryGroupEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BudgetEntity>(entity =>
        {
            entity.HasKey(x => x.Month);
            entity.Property(x => x.Month).HasMaxLength(7);
            entity.Property(x => x.Note).HasMaxLength(1000);
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
            entity.Property(x => x.Assigned).HasPrecision(14, 2);
            entity.Property(x => x.Activity).HasPrecision(14, 2);
            entity.Property(x => x.Available).HasPrecision(14, 2);
            entity.Property(x => x.Target).HasPrecision(14, 2);
        });
    }
}

public sealed class BudgetEntity
{
    public string Month { get; set; } = "";
    public string Note { get; set; } = "";
    public List<CategoryGroupEntity> Groups { get; set; } = [];
}

public sealed class CategoryGroupEntity
{
    public string BudgetMonth { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public List<CategoryEntity> Categories { get; set; } = [];
}

public sealed class CategoryEntity
{
    public string BudgetMonth { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Assigned { get; set; }
    public decimal Activity { get; set; }
    public decimal Available { get; set; }
    public decimal Target { get; set; }
    public string? Icon { get; set; }
    public bool Snoozed { get; set; }
    public int SortOrder { get; set; }
}

public sealed record BudgetDto(string Month, string Note, List<CategoryGroupDto> Groups)
{
    public static BudgetDto From(BudgetEntity budget) => new(
        budget.Month, budget.Note,
        budget.Groups.OrderBy(g => g.SortOrder).Select(g => new CategoryGroupDto(
            g.Id, g.Name, g.SortOrder,
            g.Categories.OrderBy(c => c.SortOrder).Select(c => new CategoryDto(
                c.Id, c.Name, c.Assigned, c.Activity, c.Available, c.Target, c.Icon, c.Snoozed, c.SortOrder)).ToList())).ToList());
}

public sealed record CategoryGroupDto(string Id, string Name, int SortOrder, List<CategoryDto> Categories);
public sealed record CategoryDto(string Id, string Name, decimal Assigned, decimal Activity, decimal Available, decimal Target,
    string? Icon = null, bool Snoozed = false, int SortOrder = 0);

public static class DemoBudget
{
    public static BudgetEntity Create(string month)
    {
        var groups = new (string Id, string Name, (string Id, string Name, decimal Assigned, decimal Activity, decimal Available, decimal Target, string Icon, bool Snoozed)[] Items)[]
        {
            ("home", "Home", [("rent", "Rent & mortgage", 1450, -1450, 0, 1450, "⌂", false), ("utilities", "Electricity & water", 180, -132.45m, 47.55m, 180, "ϟ", false), ("internet", "Internet", 55, -55, 0, 55, "⌁", false)]),
            ("daily", "Everyday", [("groceries", "Groceries", 520, -376.2m, 143.8m, 520, "▤", false), ("transport", "Transport", 120, -96.5m, 23.5m, 120, "↗", false), ("coffee", "Coffee & lunch", 90, -42.75m, 47.25m, 90, "☕", false), ("health", "Health & pharmacy", 75, -92.4m, -17.4m, 75, "✚", false)]),
            ("life", "Life & fun", [("dining", "Dining out", 160, -185.3m, -25.3m, 160, "♡", false), ("entertainment", "Entertainment", 80, -34.99m, 45.01m, 80, "✦", false), ("clothing", "Clothing", 100, 0, 100, 100, "◌", false)]),
            ("future", "True expenses", [("annual", "Annual bills", 210, 0, 790, 1000, "◷", false), ("home-repair", "Home maintenance", 125, 0, 625, 750, "⌁", false), ("travel", "Next trip", 200, 0, 600, 1200, "✈", false)]),
            ("savings", "Savings goals", [("buffer", "Emergency fund", 300, 0, 2400, 5000, "◉", false), ("gifts", "Gifts", 40, 0, 160, 300, "◇", true)])
        };
        return new BudgetEntity
        {
            Month = month,
            Groups = groups.Select((g, groupIndex) => new CategoryGroupEntity
            {
                BudgetMonth = month, Id = g.Id, Name = g.Name, SortOrder = groupIndex,
                Categories = g.Items.Select((c, categoryIndex) => new CategoryEntity
                {
                    BudgetMonth = month, GroupId = g.Id, Id = c.Id, Name = c.Name, Assigned = c.Assigned,
                    Activity = c.Activity, Available = c.Available, Target = c.Target, Icon = c.Icon,
                    Snoozed = c.Snoozed, SortOrder = categoryIndex
                }).ToList()
            }).ToList()
        };
    }
}
