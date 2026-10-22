using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed class BudgetDbContext(DbContextOptions<BudgetDbContext> options) : DbContext(options)
{
    public DbSet<BudgetPlanEntity> BudgetPlans => Set<BudgetPlanEntity>();
    public DbSet<LongTermPlanningStateEntity> LongTermPlanningStates => Set<LongTermPlanningStateEntity>();
    public DbSet<LongTermPlanningIncomeEntity> LongTermPlanningIncomes => Set<LongTermPlanningIncomeEntity>();
    public DbSet<LongTermPlanningCategoryEntity> LongTermPlanningCategories => Set<LongTermPlanningCategoryEntity>();
    public DbSet<BudgetEntity> Budgets => Set<BudgetEntity>();
    public DbSet<CategoryGroupEntity> CategoryGroups => Set<CategoryGroupEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
    public DbSet<FlagDefinitionEntity> FlagDefinitions => Set<FlagDefinitionEntity>();
    public DbSet<ScheduleEntity> Schedules => Set<ScheduleEntity>();
    public DbSet<ScheduleOccurrenceEntity> ScheduleOccurrences => Set<ScheduleOccurrenceEntity>();
    public DbSet<RegisterViewEntity> RegisterViews => Set<RegisterViewEntity>();
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
    public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();
    public DbSet<PayeeEntity> Payees => Set<PayeeEntity>();
    public DbSet<LedgerStateEntity> LedgerStates => Set<LedgerStateEntity>();
    public DbSet<LedgerCommandEntity> LedgerCommands => Set<LedgerCommandEntity>();
    public DbSet<ReconciliationEntity> Reconciliations => Set<ReconciliationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        LedgerModel.Configure(modelBuilder);
        modelBuilder.Entity<FlagDefinitionEntity>(e => {
            e.HasKey(x=>x.Color); e.Property(x=>x.Color).HasMaxLength(10); e.Property(x=>x.Name).HasMaxLength(40);
            e.HasData(LedgerService.FlagColors.Select(color=>new FlagDefinitionEntity{Color=color,Name=char.ToUpperInvariant(color[0])+color[1..]}));
        });
        modelBuilder.Entity<ScheduleEntity>(e => {
            e.HasKey(x => x.Id); e.HasIndex(x => new { x.AccountId, x.NextDate });
            e.HasOne<AccountEntity>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AccountEntity>().WithMany().HasForeignKey(x => x.ToAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ScheduleOccurrenceEntity>(e => {
            e.HasKey(x => x.Id); e.HasIndex(x => new { x.ScheduleId, x.Sequence }).IsUnique();
            e.HasOne<ScheduleEntity>().WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RegisterViewEntity>(e => { e.HasKey(x => x.Scope); e.Property(x => x.Scope).HasMaxLength(36); e.Property(x => x.Version).IsConcurrencyToken(); });
        modelBuilder.Entity<BudgetPlanEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasMany(x => x.Months).WithOne(x => x.Plan).HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LongTermPlanningStateEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(100);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasData(new LongTermPlanningStateEntity { Id = "default", Version = 1 });
        });
        modelBuilder.Entity<LongTermPlanningIncomeEntity>(entity =>
        {
            entity.HasKey(x => x.Month);
            entity.Property(x => x.Month).HasMaxLength(7);
            entity.Property(x => x.ExpectedIncome).HasPrecision(14, 2);
        });
        modelBuilder.Entity<LongTermPlanningCategoryEntity>(entity =>
        {
            entity.HasKey(x => new { x.Month, x.CategoryId });
            entity.Property(x => x.Month).HasMaxLength(7);
            entity.Property(x => x.CategoryId).HasMaxLength(100);
            entity.Property(x => x.PlannedBudgetOverride).HasPrecision(14, 2);
            entity.Property(x => x.PlannedExpenses).HasPrecision(14, 2);
        });
        modelBuilder.Entity<BudgetEntity>(entity =>
        {
            entity.HasKey(x => x.Month);
            entity.Property(x => x.Month).HasMaxLength(7);
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.Property(x => x.FundsAdded).HasPrecision(14, 2);
            entity.Property(x => x.LedgerIncome).HasPrecision(14, 2);
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
            entity.HasKey(x => new { x.BudgetMonth, x.Id });
            entity.Property(x => x.Id).HasMaxLength(100);
            entity.Property(x => x.GroupId).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Icon).HasMaxLength(24);
            entity.Property(x => x.OpeningAvailable).HasPrecision(14, 2);
            entity.Property(x => x.Assigned).HasPrecision(14, 2);
            entity.Property(x => x.Activity).HasPrecision(14, 2);
            entity.Property(x => x.LegacyActivity).HasPrecision(14, 2);
            entity.Property(x => x.Target).HasPrecision(14, 2);
        });
    }
}

public sealed class LongTermPlanningStateEntity
{
    public string Id { get; set; } = "default";
    public long Version { get; set; } = 1;
}

public sealed class LongTermPlanningIncomeEntity
{
    public string Month { get; set; } = "";
    public decimal ExpectedIncome { get; set; }
}

public sealed class LongTermPlanningCategoryEntity
{
    public string Month { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public decimal? PlannedBudgetOverride { get; set; }
    public decimal PlannedExpenses { get; set; }
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
    public decimal LedgerIncome { get; set; }
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
    public decimal LegacyActivity { get; set; }
    // Materialized legacy baseline plus ledger activity; only the server can change it.
    public decimal Activity { get; set; }
    public decimal Target { get; set; }
    public string? Icon { get; set; }
    public bool Snoozed { get; set; }
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }

    public decimal Available => decimal.Round(OpeningAvailable + Assigned + Activity, 2, MidpointRounding.ToEven);
}
