using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed class BankConnectionEntity
{
    public Guid Id { get; set; }
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public string OwnerUserId { get; set; } = "";
    public string Provider { get; set; } = "enablebanking";
    public string AspspName { get; set; } = "";
    public string Country { get; set; } = "";
    public string ProtectedSessionId { get; set; } = "";
    public DateTimeOffset AuthorizedAt { get; set; }
    public DateTimeOffset AccessValidUntil { get; set; }
    public string Status { get; set; } = "active";
    public int SyncIntervalMinutes { get; set; } = 60;
    public DateTimeOffset NextSyncAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset? RetryAfter { get; set; }
    public string? LastErrorCode { get; set; }
    public DateTimeOffset? SyncLeaseUntil { get; set; }
    public DateOnly? LastImportedDate { get; set; }
    public int FailureCount { get; set; }
    public List<LinkedBankAccountEntity> Accounts { get; set; } = [];
}

public sealed class LinkedBankAccountEntity
{
    public Guid Id { get; set; }
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public Guid ConnectionId { get; set; }
    public BankConnectionEntity Connection { get; set; } = null!;
    public string ProviderAccountUid { get; set; } = "";
    public string IdentificationHash { get; set; } = "";
    public string Name { get; set; } = "";
    public string Currency { get; set; } = "";
    public Guid? AccountId { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public sealed class BankImportEntity
{
    public Guid Id { get; set; }
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public Guid LinkedBankAccountId { get; set; }
    public LinkedBankAccountEntity LinkedBankAccount { get; set; } = null!;
    public string ProviderTransactionId { get; set; } = "";
    public string? EntryReference { get; set; }
    public string ProviderStatus { get; set; } = "booked";
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public string Payee { get; set; } = "";
    public string Memo { get; set; } = "";
    public string ReviewStatus { get; set; } = "pending";
    public Guid? MatchedTransactionId { get; set; }
    public decimal? MatchScore { get; set; }
    public Guid? CreatedTransactionId { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class BankAuthorizationStateEntity
{
    public string State { get; set; } = "";
    public string UserId { get; set; } = "";
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public string AspspName { get; set; } = "";
    public string Country { get; set; } = "";
    public int SyncIntervalMinutes { get; set; }
    public Guid? ReauthorizationConnectionId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public static class EnableBankingModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<BankConnectionEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.OwnerUserId).HasMaxLength(450);
            entity.Property(x => x.Provider).HasMaxLength(40);
            entity.Property(x => x.AspspName).HasMaxLength(160);
            entity.Property(x => x.Country).HasMaxLength(2);
            entity.Property(x => x.ProtectedSessionId).HasMaxLength(4000);
            entity.Property(x => x.Status).HasMaxLength(30);
            entity.Property(x => x.LastErrorCode).HasMaxLength(80);
            entity.Property(x => x.FailureCount).HasDefaultValue(0);
            entity.HasIndex(x => new { x.BudgetId, x.NextSyncAt, x.Status });
            entity.HasIndex(x => new { x.BudgetId, x.OwnerUserId });
            entity.HasOne<OpenBudgetUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<LinkedBankAccountEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.ProviderAccountUid).HasMaxLength(120);
            entity.Property(x => x.IdentificationHash).HasMaxLength(1000);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.IsAvailable).HasDefaultValue(true);
            entity.HasIndex(x => new { x.ConnectionId, x.ProviderAccountUid }).IsUnique();
            entity.HasIndex(x => new { x.BudgetId, x.IdentificationHash });
            entity.HasIndex(x => new { x.BudgetId, x.AccountId }).IsUnique().HasFilter("\"AccountId\" IS NOT NULL");
            entity.HasOne(x => x.Connection).WithMany(x => x.Accounts).HasForeignKey(x => x.ConnectionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<AccountEntity>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<BankImportEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.ProviderTransactionId).HasMaxLength(256);
            entity.Property(x => x.EntryReference).HasMaxLength(256);
            entity.Property(x => x.ProviderStatus).HasMaxLength(20);
            entity.Property(x => x.Amount).HasPrecision(14, 2);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Payee).HasMaxLength(160);
            entity.Property(x => x.Memo).HasMaxLength(1000);
            entity.Property(x => x.ReviewStatus).HasMaxLength(20);
            entity.Property(x => x.MatchScore).HasPrecision(5, 4);
            entity.HasIndex(x => new { x.LinkedBankAccountId, x.ProviderTransactionId }).IsUnique();
            entity.HasIndex(x => new { x.BudgetId, x.ReviewStatus, x.Date });
            entity.HasOne(x => x.LinkedBankAccount).WithMany().HasForeignKey(x => x.LinkedBankAccountId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<BankAuthorizationStateEntity>(entity =>
        {
            entity.HasKey(x => x.State);
            entity.Property(x => x.State).HasMaxLength(512);
            entity.Property(x => x.UserId).HasMaxLength(450);
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.AspspName).HasMaxLength(160);
            entity.Property(x => x.Country).HasMaxLength(2);
            entity.HasIndex(x => x.ExpiresAt);
            entity.HasIndex(x => x.ReauthorizationConnectionId);
            entity.HasOne<BankConnectionEntity>().WithMany().HasForeignKey(x => x.ReauthorizationConnectionId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
