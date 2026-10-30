using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed class AccountEntity
{
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public string Notes { get; set; } = "";
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsClosed { get; set; }
    public string AccountType { get; set; } = "checking";
    public bool IsFavorite { get; set; }
}

public sealed class PayeeEntity
{
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public bool IsHidden { get; set; }
    public string CategoryMode { get; set; } = "history";
    public string? DefaultCategoryId { get; set; }
    public string RulesJson { get; set; } = "[]";
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
}

public sealed class TransactionEntity
{
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public bool IsApproved { get; set; } = true;
    public Guid? ScheduleId { get; set; }
    public string? FlagColor { get; set; }
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public AccountEntity Account { get; set; } = null!;
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public Guid? PayeeId { get; set; }
    public PayeeEntity? Payee { get; set; }
    public string Memo { get; set; } = "";
    public string? CategoryId { get; set; }
    public string Status { get; set; } = "uncleared";
    public Guid? TransferId { get; set; }
    public bool IsOpening { get; set; }
    public bool IsDeleted { get; set; }
    public List<SplitEntity> Splits { get; set; } = [];
}

public sealed class SplitEntity
{
    public Guid TransactionId { get; set; }
    public int Position { get; set; }
    public string CategoryId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Payee { get; set; } = "";
    public string Memo { get; set; } = "";
}

public sealed class LedgerStateEntity
{
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public int Id { get; set; } = 1;
    public long Version { get; set; } = 1;
}

// A command receipt is also the append-only audit record. Financial rows are soft deleted.
public sealed class LedgerCommandEntity
{
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public string Kind { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string BeforeJson { get; set; } = "null";
    public string AfterJson { get; set; } = "null";
    public long Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ReconciliationEntity
{
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public bool IsInvalidated { get; set; }
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public decimal StatementBalance { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public static class LedgerModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<AccountEntity>(e =>
        {
            e.HasKey(x => x.Id); e.Property(x => x.BudgetId).HasMaxLength(100); e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.AccountType).HasMaxLength(20).HasDefaultValue("checking");
        });
        model.Entity<PayeeEntity>(e =>
        {
            e.Property(x => x.CategoryMode).HasMaxLength(12).HasDefaultValue("history");
            e.Property(x => x.DefaultCategoryId).HasMaxLength(100);
            e.Property(x => x.RulesJson).HasDefaultValue("[]");
            e.HasKey(x => x.Id); e.Property(x => x.BudgetId).HasMaxLength(100);
            e.Property(x => x.Name).HasMaxLength(160);
            e.Property(x => x.NormalizedName).HasMaxLength(160);
            e.HasIndex(x => new { x.BudgetId, x.NormalizedName }).IsUnique();
        });
        model.Entity<TransactionEntity>(e =>
        {
            e.HasKey(x => x.Id); e.Property(x => x.BudgetId).HasMaxLength(100);
            e.Property(x => x.IsApproved).HasDefaultValue(true);
            e.Property(x => x.Amount).HasPrecision(14, 2);
            e.Property(x => x.Memo).HasMaxLength(1000);
            e.Property(x => x.CategoryId).HasMaxLength(100);
            e.Property(x => x.Status).HasMaxLength(12);
            e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Payee).WithMany().HasForeignKey(x => x.PayeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Splits).WithOne().HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AccountId, x.Date, x.Id });
            e.HasIndex(x => x.TransferId);
            e.HasIndex(x => x.Date);
        });
        model.Entity<SplitEntity>(e =>
        {
            e.HasKey(x => new { x.TransactionId, x.Position });
            e.Property(x => x.CategoryId).HasMaxLength(100);
            e.Property(x => x.Amount).HasPrecision(14, 2);
            e.Property(x => x.Payee).HasMaxLength(160);
            e.Property(x => x.Memo).HasMaxLength(1000);
        });
        model.Entity<LedgerStateEntity>(e => { e.HasKey(x => new { x.BudgetId, x.Id }); e.Property(x => x.BudgetId).HasMaxLength(100); e.Property(x => x.Version).IsConcurrencyToken(); e.HasData(new LedgerStateEntity()); });
        model.Entity<LedgerCommandEntity>(e =>
        {
            e.HasKey(x => x.Id); e.Property(x => x.BudgetId).HasMaxLength(100);
            e.Property(x => x.Kind).HasMaxLength(100);
            e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.HasIndex(x => new { x.ResourceId, x.Version });
        });
        model.Entity<CreditPayoffTargetEntity>(e =>
        {
            e.HasKey(x => x.AccountId); e.Property(x => x.BudgetId).HasMaxLength(100);
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.TargetMonth).HasMaxLength(7);
            e.Property(x => x.MonthlyAmount).HasPrecision(14, 2);
            e.Property(x => x.PayoffAmount).HasPrecision(14, 2);
            e.HasOne<AccountEntity>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.BudgetId);
        });
        model.Entity<ReconciliationEntity>(e =>
        {
            e.HasKey(x => x.Id); e.Property(x => x.BudgetId).HasMaxLength(100);
            e.Property(x => x.StatementBalance).HasPrecision(14, 2);
            e.HasOne<AccountEntity>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public sealed class RegisterViewEntity
{
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public string Scope { get; set; } = "";
    public long Version { get; set; }
    public string DatePreset { get; set; } = "allDates";
    public string? FromMonth { get; set; }
    public string? ToMonth { get; set; }
    public bool ShowReconciled { get; set; } = true;
    public bool ShowRunningBalance { get; set; } = true;
    public bool ShowFlags { get; set; } = true;
    public bool ShowImages { get; set; } = true;
    public bool ShowScheduledTransactions { get; set; } = true;
    public string ColumnWidthsJson { get; set; } = "{}";
}


public sealed class FlagDefinitionEntity
{
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public string Color { get; set; } = "";
    public string Name { get; set; } = "";
}
