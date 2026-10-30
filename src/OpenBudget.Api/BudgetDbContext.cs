using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed class BudgetDbContext(DbContextOptions<BudgetDbContext> options, BudgetRequestContext budgetContext) : IdentityDbContext<OpenBudgetUser>(options)
{
    public BudgetDbContext(DbContextOptions<BudgetDbContext> options) : this(options, new BudgetRequestContext()) { }
    private readonly BudgetRequestContext budgetContext = budgetContext;
    public string ActiveBudgetId => budgetContext.BudgetId;
    public DbSet<BudgetPlanEntity> BudgetPlans => Set<BudgetPlanEntity>();
    public DbSet<BudgetMemberEntity> BudgetMembers => Set<BudgetMemberEntity>();
    public DbSet<BudgetInvitationEntity> BudgetInvitations => Set<BudgetInvitationEntity>();
    public DbSet<PlanActionEntity> PlanActions => Set<PlanActionEntity>();
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
    public DbSet<CreditPayoffTargetEntity> CreditPayoffTargets => Set<CreditPayoffTargetEntity>();
    public DbSet<CategoryTargetEntity> CategoryTargets => Set<CategoryTargetEntity>();
    public DbSet<ApplicationSettingEntity> ApplicationSettings => Set<ApplicationSettingEntity>();
    public DbSet<SmtpSettingsEntity> SmtpSettings => Set<SmtpSettingsEntity>();
    public DbSet<AdminAuditLogEntity> AdminAuditLogs => Set<AdminAuditLogEntity>();
    public DbSet<MembershipPlanEntity> MembershipPlans => Set<MembershipPlanEntity>();
    public DbSet<UserMembershipEntity> UserMemberships => Set<UserMembershipEntity>();
    public DbSet<MembershipPlanEntitlementEntity> MembershipPlanEntitlements => Set<MembershipPlanEntitlementEntity>();
    public DbSet<UserEntitlementOverrideEntity> UserEntitlementOverrides => Set<UserEntitlementOverrideEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        LedgerModel.Configure(modelBuilder);
        modelBuilder.Entity<ApplicationSettingEntity>(e => { e.HasKey(x => x.Key); e.Property(x => x.Key).HasMaxLength(100); e.Property(x => x.Value).HasMaxLength(500); });
        modelBuilder.Entity<SmtpSettingsEntity>(e => { e.HasKey(x => x.Id); e.Property(x => x.Host).HasMaxLength(255); e.Property(x => x.From).HasMaxLength(256); e.Property(x => x.Username).HasMaxLength(256); e.Property(x => x.EncryptedPassword).HasMaxLength(2000); });
        modelBuilder.Entity<AdminAuditLogEntity>(e => { e.HasKey(x => x.Id); e.Property(x => x.ActorUserId).HasMaxLength(450); e.Property(x => x.TargetUserId).HasMaxLength(450); e.Property(x => x.Action).HasMaxLength(80); e.HasIndex(x => x.CreatedAt); });
        modelBuilder.Entity<MembershipPlanEntity>(e => { e.HasKey(x => x.Id); e.Property(x => x.Id).HasMaxLength(40); e.Property(x => x.Name).HasMaxLength(80); e.Property(x => x.MaxActiveBudgets).IsRequired(false); e.HasData(new MembershipPlanEntity { Id = "free", Name = "Free", MaxActiveBudgets = 1 }, new MembershipPlanEntity { Id = "plus", Name = "Plus", MaxActiveBudgets = 3 }, new MembershipPlanEntity { Id = "pro", Name = "Pro", MaxActiveBudgets = null }); });
        modelBuilder.Entity<UserMembershipEntity>(e => { e.HasKey(x => x.UserId); e.Property(x => x.UserId).HasMaxLength(450); e.Property(x => x.MembershipPlanId).HasMaxLength(40); e.Property(x => x.BillingProvider).HasMaxLength(80); e.Property(x => x.ExternalSubscriptionId).HasMaxLength(160); e.Property(x => x.Version).IsConcurrencyToken(); e.HasOne<OpenBudgetUser>().WithOne().HasForeignKey<UserMembershipEntity>(x => x.UserId).OnDelete(DeleteBehavior.Cascade); e.HasOne<MembershipPlanEntity>().WithMany().HasForeignKey(x => x.MembershipPlanId).OnDelete(DeleteBehavior.Restrict); });
        modelBuilder.Entity<MembershipPlanEntitlementEntity>(e => { e.HasKey(x => new { x.MembershipPlanId, x.EntitlementKey }); e.Property(x => x.MembershipPlanId).HasMaxLength(40); e.Property(x => x.EntitlementKey).HasMaxLength(80); e.HasOne<MembershipPlanEntity>().WithMany().HasForeignKey(x => x.MembershipPlanId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<UserEntitlementOverrideEntity>(e => { e.HasKey(x => new { x.UserId, x.EntitlementKey }); e.Property(x => x.UserId).HasMaxLength(450); e.Property(x => x.EntitlementKey).HasMaxLength(80); e.HasOne<OpenBudgetUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<FlagDefinitionEntity>(e => {
            e.HasKey(x=>new { x.BudgetId, x.Color }); e.Property(x=>x.BudgetId).HasMaxLength(100); e.Property(x=>x.Color).HasMaxLength(10); e.Property(x=>x.Name).HasMaxLength(40);
            e.HasData(LedgerService.FlagColors.Select(color=>new FlagDefinitionEntity{BudgetId=BudgetRequestContext.DefaultBudgetId,Color=color,Name=char.ToUpperInvariant(color[0])+color[1..]}));
        });
        modelBuilder.Entity<ScheduleEntity>(e => {
            e.HasKey(x => x.Id); e.Property(x => x.BudgetId).HasMaxLength(100); e.HasIndex(x => new { x.BudgetId, x.AccountId, x.NextDate });
            e.HasOne<AccountEntity>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AccountEntity>().WithMany().HasForeignKey(x => x.ToAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ScheduleOccurrenceEntity>(e => {
            e.HasKey(x => x.Id); e.Property(x => x.BudgetId).HasMaxLength(100); e.HasIndex(x => new { x.BudgetId, x.ScheduleId, x.Sequence }).IsUnique();
            e.HasOne<ScheduleEntity>().WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RegisterViewEntity>(e => { e.HasKey(x => new { x.BudgetId, x.Scope }); e.Property(x => x.BudgetId).HasMaxLength(100); e.Property(x => x.Scope).HasMaxLength(36); e.Property(x => x.Version).IsConcurrencyToken(); });
        modelBuilder.Entity<BudgetPlanEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.Property(x => x.OwnerUserId).HasMaxLength(450);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.HasIndex(x => new { x.OwnerUserId, x.IsArchived });
            entity.HasOne<OpenBudgetUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Months).WithOne(x => x.Plan).HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BudgetMemberEntity>(entity =>
        {
            entity.HasKey(x => new { x.BudgetId, x.UserId });
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.UserId).HasMaxLength(450);
            entity.Property(x => x.InvitedByUserId).HasMaxLength(450);
            entity.Property(x => x.Role).HasMaxLength(20);
            entity.HasIndex(x => x.UserId);
            entity.HasOne<BudgetPlanEntity>().WithMany().HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<OpenBudgetUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<BudgetInvitationEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.NormalizedEmail).HasMaxLength(256);
            entity.Property(x => x.InvitedByUserId).HasMaxLength(450);
            entity.HasIndex(x => new { x.BudgetId, x.NormalizedEmail }).IsUnique();
            entity.HasOne<BudgetPlanEntity>().WithMany().HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PlanActionEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.Month).HasMaxLength(7);
            entity.Property(x => x.ActorUserId).HasMaxLength(450);
            entity.Property(x => x.Kind).HasMaxLength(20);
            entity.Property(x => x.FromCategoryName).HasMaxLength(160);
            entity.Property(x => x.ToCategoryName).HasMaxLength(160);
            entity.Property(x => x.Amount).HasPrecision(14, 2);
            entity.HasIndex(x => new { x.BudgetId, x.CreatedAt });
            entity.HasOne<BudgetPlanEntity>().WithMany().HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CategoryTargetEntity>(entity =>
        {
            entity.HasKey(x => new { x.BudgetId, x.CategoryId });
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.CategoryId).HasMaxLength(100);
            entity.Property(x => x.Kind).HasMaxLength(10);
            entity.Property(x => x.Behavior).HasMaxLength(10);
            entity.Property(x => x.RepeatUnit).HasMaxLength(5);
            entity.Property(x => x.StartMonth).HasMaxLength(7);
            entity.Property(x => x.Amount).HasPrecision(14, 2);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasIndex(x => x.Id).IsUnique();
            entity.HasOne<BudgetPlanEntity>().WithMany().HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<LongTermPlanningStateEntity>(entity =>
        {
            entity.HasKey(x => new { x.BudgetId, x.Id });
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.Id).HasMaxLength(100);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasData(new LongTermPlanningStateEntity { Id = "default", Version = 1 });
        });
        modelBuilder.Entity<LongTermPlanningIncomeEntity>(entity =>
        {
            entity.HasKey(x => new { x.BudgetId, x.Month });
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.Month).HasMaxLength(7);
            entity.Property(x => x.ExpectedIncome).HasPrecision(14, 2);
        });
        modelBuilder.Entity<LongTermPlanningCategoryEntity>(entity =>
        {
            entity.HasKey(x => new { x.BudgetId, x.Month, x.CategoryId });
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.Month).HasMaxLength(7);
            entity.Property(x => x.CategoryId).HasMaxLength(100);
            entity.Property(x => x.PlannedBudgetOverride).HasPrecision(14, 2);
            entity.Property(x => x.PlannedExpenses).HasPrecision(14, 2);
        });
        modelBuilder.Entity<BudgetEntity>(entity =>
        {
            entity.HasKey(x => new { x.BudgetId, x.Month });
            entity.Property(x => x.Month).HasMaxLength(7);
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.Property(x => x.FundsAdded).HasPrecision(14, 2);
            entity.Property(x => x.LedgerIncome).HasPrecision(14, 2);
            entity.Property(x => x.OpeningReadyToAssign).HasPrecision(14, 2);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasMany(x => x.Groups).WithOne().HasForeignKey(x => new { x.BudgetId, x.BudgetMonth }).HasPrincipalKey(x => new { x.BudgetId, x.Month }).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CategoryGroupEntity>(entity =>
        {
            entity.HasKey(x => new { x.BudgetId, x.BudgetMonth, x.Id });
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.Id).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.HasMany(x => x.Categories).WithOne().HasForeignKey(x => new { x.BudgetId, x.BudgetMonth, x.GroupId }).HasPrincipalKey(x => new { x.BudgetId, x.BudgetMonth, x.Id }).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CategoryEntity>(entity =>
        {
            entity.HasKey(x => new { x.BudgetId, x.BudgetMonth, x.Id });
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.Id).HasMaxLength(100);
            entity.Property(x => x.GroupId).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Icon).HasMaxLength(24);
            entity.Property(x => x.OpeningAvailable).HasPrecision(14, 2);
            entity.Property(x => x.Assigned).HasPrecision(14, 2);
            entity.Property(x => x.Activity).HasPrecision(14, 2);
            entity.Property(x => x.LegacyActivity).HasPrecision(14, 2);
            entity.Property(x => x.CreditOverspending).HasPrecision(14, 2);
            entity.Property(x => x.Target).HasPrecision(14, 2);
        });

        modelBuilder.Entity<AccountEntity>().HasQueryFilter(x => x.BudgetId == ActiveBudgetId);
        modelBuilder.Entity<PayeeEntity>().HasQueryFilter(x => x.BudgetId == ActiveBudgetId);
        modelBuilder.Entity<TransactionEntity>().HasQueryFilter(x => x.BudgetId == ActiveBudgetId);
        modelBuilder.Entity<LedgerStateEntity>().HasQueryFilter(x => x.BudgetId == ActiveBudgetId);
        modelBuilder.Entity<LedgerCommandEntity>().HasQueryFilter(x => x.BudgetId == ActiveBudgetId);
        modelBuilder.Entity<ReconciliationEntity>().HasQueryFilter(x => x.BudgetId == ActiveBudgetId);
        modelBuilder.Entity<CreditPayoffTargetEntity>().HasQueryFilter(x => x.BudgetId == ActiveBudgetId);
        modelBuilder.Entity<CategoryTargetEntity>().HasQueryFilter(x => x.BudgetId == ActiveBudgetId);
        modelBuilder.Entity<RegisterViewEntity>().HasQueryFilter(x => x.BudgetId == ActiveBudgetId);
        modelBuilder.Entity<FlagDefinitionEntity>().HasQueryFilter(x => x.BudgetId == ActiveBudgetId);
        modelBuilder.Entity<ScheduleEntity>().HasQueryFilter(x => x.BudgetId == ActiveBudgetId);
        modelBuilder.Entity<ScheduleOccurrenceEntity>().HasQueryFilter(x => x.BudgetId == ActiveBudgetId);
        modelBuilder.Entity<LongTermPlanningStateEntity>().HasQueryFilter(x => x.BudgetId == ActiveBudgetId);
        modelBuilder.Entity<LongTermPlanningIncomeEntity>().HasQueryFilter(x => x.BudgetId == ActiveBudgetId);
        modelBuilder.Entity<LongTermPlanningCategoryEntity>().HasQueryFilter(x => x.BudgetId == ActiveBudgetId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampBudgetId();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampBudgetId();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampBudgetId()
    {
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State == EntityState.Added))
        {
            switch (entry.Entity)
            {
                case AccountEntity row: row.BudgetId = budgetContext.BudgetId; break;
                case PayeeEntity row: row.BudgetId = budgetContext.BudgetId; break;
                case TransactionEntity row: row.BudgetId = budgetContext.BudgetId; break;
                case LedgerStateEntity row: row.BudgetId = budgetContext.BudgetId; break;
                case LedgerCommandEntity row: row.BudgetId = budgetContext.BudgetId; break;
                case ReconciliationEntity row: row.BudgetId = budgetContext.BudgetId; break;
                case CreditPayoffTargetEntity row: row.BudgetId = budgetContext.BudgetId; break;
                case CategoryTargetEntity row: row.BudgetId = budgetContext.BudgetId; break;
                case RegisterViewEntity row: row.BudgetId = budgetContext.BudgetId; break;
                case FlagDefinitionEntity row: row.BudgetId = budgetContext.BudgetId; break;
                case ScheduleEntity row: row.BudgetId = budgetContext.BudgetId; break;
                case ScheduleOccurrenceEntity row: row.BudgetId = budgetContext.BudgetId; break;
                case LongTermPlanningStateEntity row: row.BudgetId = budgetContext.BudgetId; break;
                case LongTermPlanningIncomeEntity row: row.BudgetId = budgetContext.BudgetId; break;
                case LongTermPlanningCategoryEntity row: row.BudgetId = budgetContext.BudgetId; break;
            }
        }
    }
}

public sealed class ApplicationSettingEntity
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class SmtpSettingsEntity
{
    public int Id { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string From { get; set; } = "";
    public string? Username { get; set; }
    public string? EncryptedPassword { get; set; }
    public bool EnableSsl { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AdminAuditLogEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ActorUserId { get; set; } = "";
    public string TargetUserId { get; set; } = "";
    public string Action { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LongTermPlanningStateEntity
{
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public string Id { get; set; } = "default";
    public long Version { get; set; } = 1;
}

public sealed class LongTermPlanningIncomeEntity
{
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public string Month { get; set; } = "";
    public decimal ExpectedIncome { get; set; }
}

public sealed class LongTermPlanningCategoryEntity
{
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public string Month { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public decimal? PlannedBudgetOverride { get; set; }
    public decimal PlannedExpenses { get; set; }
}

public sealed class BudgetPlanEntity
{
    public string Id { get; set; } = "default";
    public string? OwnerUserId { get; set; }
    public string Name { get; set; } = "My Budget";
    public string Currency { get; set; } = "EUR";
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long Version { get; set; } = 1;
    public List<BudgetEntity> Months { get; set; } = [];
}

public sealed class BudgetMemberEntity
{
    public string BudgetId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string InvitedByUserId { get; set; } = "";
    public string Role { get; set; } = "editor";
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BudgetInvitationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BudgetId { get; set; } = "";
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string InvitedByUserId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PlanActionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BudgetId { get; set; } = "";
    public string Month { get; set; } = "";
    public string ActorUserId { get; set; } = "";
    public string Kind { get; set; } = "updated";
    public decimal? Amount { get; set; }
    public string? FromCategoryName { get; set; }
    public string? ToCategoryName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MembershipPlanEntity
{
    public string Id { get; set; } = "free";
    public string Name { get; set; } = "Free";
    public int? MaxActiveBudgets { get; set; }
}

public sealed class UserMembershipEntity
{
    public string UserId { get; set; } = "";
    public string MembershipPlanId { get; set; } = "free";
    public int? BudgetLimitOverride { get; set; }
    public DateOnly? StartsOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string? BillingProvider { get; set; }
    public string? ExternalSubscriptionId { get; set; }
    public long Version { get; set; } = 1;
}

public sealed class MembershipPlanEntitlementEntity
{
    public string MembershipPlanId { get; set; } = "free";
    public string EntitlementKey { get; set; } = "";
    public bool IsEnabled { get; set; }
}

public sealed class UserEntitlementOverrideEntity
{
    public string UserId { get; set; } = "";
    public string EntitlementKey { get; set; } = "";
    public bool IsEnabled { get; set; }
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
    public string BudgetId { get; set; } = "default";
    public string BudgetMonth { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public List<CategoryEntity> Categories { get; set; } = [];
}

public sealed class CategoryEntity
{
    public string BudgetId { get; set; } = "default";
    public string BudgetMonth { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal OpeningAvailable { get; set; }
    public decimal Assigned { get; set; }
    public decimal LegacyActivity { get; set; }
    // Materialized legacy baseline plus ledger activity; only the server can change it.
    public decimal Activity { get; set; }
    // Server-owned portion of negative Available caused by unfunded credit-card spending. It becomes card
    // debt at rollover instead of reducing the next month's Ready to Assign.
    public decimal CreditOverspending { get; set; }
    public decimal Target { get; set; }
    public string? Icon { get; set; }
    public bool Snoozed { get; set; }
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }

    public decimal Available => decimal.Round(OpeningAvailable + Assigned + Activity, 2, MidpointRounding.ToEven);
}
