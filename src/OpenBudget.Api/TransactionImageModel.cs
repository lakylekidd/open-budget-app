using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

/// <summary>Image metadata. Kept separate from bytes so register counts and listings never load image content.</summary>
public sealed class TransactionImageEntity
{
    public Guid Id { get; set; }
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public Guid TransactionId { get; set; }
    /// <summary>Sanitized display name only; never used as a storage path.</summary>
    public string FileName { get; set; } = "";
    /// <summary>Content type detected from magic bytes, never the client-supplied type.</summary>
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Sha256 { get; set; } = "";
    public string Source { get; set; } = "upload";
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>PostgreSQL-backed image bytes for <see cref="PostgresTransactionImageStore"/>.</summary>
public sealed class TransactionImageBlobEntity
{
    public Guid ImageId { get; set; }
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public byte[] Content { get; set; } = [];
}

public static class TransactionImageModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<TransactionImageEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.BudgetId).HasMaxLength(100);
            e.Property(x => x.FileName).HasMaxLength(ImageFileNameSanitizer.MaxLength);
            e.Property(x => x.ContentType).HasMaxLength(20);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.Source).HasMaxLength(10);
            e.Property(x => x.CreatedByUserId).HasMaxLength(450);
            e.HasIndex(x => new { x.BudgetId, x.TransactionId, x.CreatedAt });
            e.HasOne<TransactionEntity>().WithMany().HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<TransactionImageBlobEntity>(e =>
        {
            e.HasKey(x => x.ImageId);
            e.Property(x => x.BudgetId).HasMaxLength(100);
            e.HasOne<TransactionImageEntity>().WithOne().HasForeignKey<TransactionImageBlobEntity>(x => x.ImageId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
