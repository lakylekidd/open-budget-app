using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api.Mcp;

/// <summary>Permission level granted to an agent (personal access) token. Admin implies everything the creator can do.</summary>
public enum AgentPermission
{
    Reporting = 0,
    Budget = 1,
    Admin = 2
}

/// <summary>A personal access token used by the owner's AI agents. Only the SHA-256 hash of the secret is stored.</summary>
public sealed class AgentTokenEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Non-secret display prefix (for example <c>obk_AbCdEfGh</c>) that helps the owner recognise the token.</summary>
    public string Prefix { get; set; } = "";
    /// <summary>Lowercase hex SHA-256 of the full token; unique.</summary>
    public string TokenHash { get; set; } = "";
    public AgentPermission Permission { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Null only when the owner explicitly chose "no expiration" at creation or renewal.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    /// <summary>When the secret was last rotated; null until the first renewal.</summary>
    public DateTimeOffset? RenewedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public List<AgentTokenBudgetEntity> Budgets { get; set; } = [];
}

/// <summary>A budget a Budget/Reporting token may access. Admin tokens use every budget their creator can access.</summary>
public sealed class AgentTokenBudgetEntity
{
    public Guid TokenId { get; set; }
    public string BudgetId { get; set; } = "";
}

/// <summary>One MCP tool call. Never stores arguments, results, financial values or the token.</summary>
public sealed class AgentAuditLogEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TokenId { get; set; }
    public string UserId { get; set; } = "";
    public string Tool { get; set; } = "";
    public string? BudgetId { get; set; }
    public bool Success { get; set; }
    /// <summary>Short outcome code: ok, invalid, denied, not_found, conflict, error.</summary>
    public string Outcome { get; set; } = "";
    public int? StatusCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public static class AgentAccessModel
{
    public const int MaxNameLength = 80;
    public const int ToolNameLength = 80;

    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgentTokenEntity>(entity =>
        {
            entity.ToTable("AgentTokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(450);
            entity.Property(x => x.Name).HasMaxLength(MaxNameLength);
            entity.Property(x => x.Prefix).HasMaxLength(16);
            entity.Property(x => x.TokenHash).HasMaxLength(64);
            entity.Property(x => x.Permission).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.HasOne<OpenBudgetUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Budgets).WithOne().HasForeignKey(x => x.TokenId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AgentTokenBudgetEntity>(entity =>
        {
            entity.ToTable("AgentTokenBudgets");
            entity.HasKey(x => new { x.TokenId, x.BudgetId });
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.HasOne<BudgetPlanEntity>().WithMany().HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AgentAuditLogEntity>(entity =>
        {
            entity.ToTable("AgentAuditLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(450);
            entity.Property(x => x.Tool).HasMaxLength(ToolNameLength);
            entity.Property(x => x.BudgetId).HasMaxLength(100);
            entity.Property(x => x.Outcome).HasMaxLength(20);
            entity.HasIndex(x => new { x.TokenId, x.CreatedAt });
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
        });
    }
}
