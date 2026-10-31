using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api.Mcp;

/// <summary><paramref name="NeverExpires"/> must be explicitly <c>true</c> to create a non-expiring token; omission means the default expiry.</summary>
public sealed record CreateAgentTokenRequest(string Name, string Permission, IReadOnlyList<string>? BudgetIds, int? ExpiresInDays, bool? NeverExpires = null);
/// <summary>Optional lifetime for a renewal. Omitting both fields keeps the token's expiry mode.</summary>
public sealed record RenewAgentTokenRequest(int? ExpiresInDays = null, bool? NeverExpires = null);
public sealed record AgentTokenBudgetDto(string Id, string Name);
public sealed record AgentTokenDto(Guid Id, string Name, string Prefix, string Permission, IReadOnlyList<AgentTokenBudgetDto> Budgets,
    DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt, string Status, DateTimeOffset? RenewedAt = null);
public sealed record CreatedAgentTokenDto(string Token, AgentTokenDto Details);
public sealed record AgentActivityDto(Guid Id, string Tool, string? BudgetId, bool Success, string Outcome, DateTimeOffset CreatedAt);

/// <summary>Creates, lists, renews (rotates) and revokes the signed-in user's agent tokens. Tokens are never readable after creation.</summary>
public sealed class AgentTokenService(BudgetDbContext db, UserManager<OpenBudgetUser> users, BudgetSharingService sharing, TimeProvider clock)
{
    public const int DefaultExpiryDays = 90;
    public const int MaxExpiryDays = 365;
    public const int MaxActiveTokensPerUser = 25;
    public const int MaxBudgetsPerToken = 50;

    public async Task<IResult> ListAsync(string userId, CancellationToken ct)
    {
        var tokens = await db.Set<AgentTokenEntity>().AsNoTracking().Include(x => x.Budgets)
            .Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
        var budgetIds = tokens.SelectMany(x => x.Budgets.Select(b => b.BudgetId)).Distinct().ToArray();
        var names = await db.BudgetPlans.AsNoTracking().Where(x => budgetIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return Results.Ok(tokens.Select(token => ToDto(token, names)).ToList());
    }

    public async Task<IResult> CreateAsync(ClaimsPrincipal principal, CreateAgentTokenRequest request, CancellationToken ct)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 1 or > AgentAccessModel.MaxNameLength || name.Any(char.IsControl))
            return Results.BadRequest(new { error = $"Token name is required and must be at most {AgentAccessModel.MaxNameLength} printable characters." });
        if (!McpPermissions.TryParse(request.Permission, out var permission))
            return Results.BadRequest(new { error = "Permission must be Admin, Budget or Reporting." });
        var now = clock.GetUtcNow();
        if (!AgentTokenLifetime.TryResolve(request.ExpiresInDays, request.NeverExpires, currentlyNeverExpires: false, now, out var expiresAt, out var lifetimeError))
            return Results.BadRequest(new { error = lifetimeError });
        if (permission == AgentPermission.Admin && !await users.IsInRoleAsync(user, AuthenticationSetup.AdministratorRole))
            return Results.Json(new { error = "Only administrators can create Admin agent tokens." }, statusCode: StatusCodes.Status403Forbidden);

        var requested = (request.BudgetIds ?? []).Select(id => id?.Trim() ?? "").ToList();
        if (permission == AgentPermission.Admin && requested.Count > 0)
            return Results.BadRequest(new { error = "Admin tokens cover every budget you can access; do not select budgets." });
        if (permission != AgentPermission.Admin)
        {
            if (requested.Count is < 1 or > MaxBudgetsPerToken || requested.Distinct(StringComparer.Ordinal).Count() != requested.Count)
                return Results.BadRequest(new { error = $"Choose between 1 and {MaxBudgetsPerToken} distinct budgets." });
            foreach (var budgetId in requested)
            {
                // Same message for unknown and inaccessible budgets so existence is not disclosed.
                if (!McpBudgetIds.IsValid(budgetId) || !await sharing.CanAccessBudgetAsync(user.Id, budgetId, ct))
                    return Results.BadRequest(new { error = "One or more selected budgets are not available to you." });
            }
        }

        if (await CountActiveAsync(user.Id, null, now, ct) >= MaxActiveTokensPerUser)
            return Results.Conflict(new { error = $"You already have {MaxActiveTokensPerUser} active agent tokens. Revoke one first." });

        var secret = AgentTokenSecrets.Generate();
        var token = new AgentTokenEntity
        {
            UserId = user.Id, Name = name, Prefix = secret.DisplayPrefix, TokenHash = secret.Hash, Permission = permission,
            CreatedAt = now, ExpiresAt = expiresAt,
            Budgets = permission == AgentPermission.Admin ? [] : requested.Select(id => new AgentTokenBudgetEntity { BudgetId = id }).ToList()
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Add(token);
        db.AdminAuditLogs.Add(new AdminAuditLogEntity { ActorUserId = user.Id, TargetUserId = user.Id, Action = AgentTokenLifetime.AuditAction($"agent-token.create.{permission.ToString().ToLowerInvariant()}", expiresAt), CreatedAt = now });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var names = await db.BudgetPlans.AsNoTracking().Where(x => requested.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return Results.Json(new CreatedAgentTokenDto(secret.Plaintext, ToDto(token, names)), statusCode: StatusCodes.Status201Created);
    }

    public async Task<IResult> RevokeAsync(string userId, Guid id, CancellationToken ct)
    {
        var token = await db.Set<AgentTokenEntity>().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (token is null) return Results.NotFound();
        if (token.RevokedAt is null)
        {
            var now = clock.GetUtcNow();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            token.RevokedAt = now;
            db.AdminAuditLogs.Add(new AdminAuditLogEntity { ActorUserId = userId, TargetUserId = userId, Action = "agent-token.revoke", CreatedAt = now });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        return Results.NoContent();
    }

    /// <summary>
    /// Rotates the secret of an owned, unrevoked token: the old secret stops working immediately and the new one is returned once.
    /// Permission and budgets are unchanged. Not replay-safe by design, because only the hash is stored: a lost response means renew again.
    /// </summary>
    public async Task<IResult> RenewAsync(string userId, Guid id, RenewAgentTokenRequest? request, CancellationToken ct)
    {
        var token = await db.Set<AgentTokenEntity>().AsNoTracking().Include(x => x.Budgets).SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (token is null) return Results.NotFound();
        if (token.RevokedAt is not null) return Results.Conflict(new { error = "Revoked tokens cannot be renewed. Create a new token instead." });
        var now = clock.GetUtcNow();
        if (!AgentTokenLifetime.TryResolve(request?.ExpiresInDays, request?.NeverExpires, token.ExpiresAt is null, now, out var expiresAt, out var lifetimeError))
            return Results.BadRequest(new { error = lifetimeError });
        // Renewing an expired token reactivates it, so it counts against the active-token limit.
        if (!AgentTokenLifetime.IsActive(token, now) && await CountActiveAsync(userId, token.Id, now, ct) >= MaxActiveTokensPerUser)
            return Results.Conflict(new { error = $"You already have {MaxActiveTokensPerUser} active agent tokens. Revoke one first." });

        var secret = AgentTokenSecrets.Generate();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Conditional on the hash just read and on not being revoked: a concurrent renew or revoke wins and this call
        // returns 409, so at most one returned secret is ever valid.
        var updated = await db.Set<AgentTokenEntity>()
            .Where(x => x.Id == token.Id && x.UserId == userId && x.RevokedAt == null && x.TokenHash == token.TokenHash)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.TokenHash, secret.Hash)
                .SetProperty(x => x.Prefix, secret.DisplayPrefix)
                .SetProperty(x => x.ExpiresAt, expiresAt)
                .SetProperty(x => x.RenewedAt, now), ct);
        if (updated == 0)
        {
            await transaction.RollbackAsync(ct);
            return Results.Conflict(new { error = "The token was changed or revoked while renewing. Reload and try again." });
        }
        db.AdminAuditLogs.Add(new AdminAuditLogEntity { ActorUserId = userId, TargetUserId = userId, Action = AgentTokenLifetime.AuditAction("agent-token.renew", expiresAt), CreatedAt = now });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        token.TokenHash = secret.Hash;
        token.Prefix = secret.DisplayPrefix;
        token.ExpiresAt = expiresAt;
        token.RenewedAt = now;
        var budgetIds = token.Budgets.Select(x => x.BudgetId).ToArray();
        var names = await db.BudgetPlans.AsNoTracking().Where(x => budgetIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return Results.Ok(new CreatedAgentTokenDto(secret.Plaintext, ToDto(token, names)));
    }

    private Task<int> CountActiveAsync(string userId, Guid? excludeId, DateTimeOffset now, CancellationToken ct) =>
        db.Set<AgentTokenEntity>().CountAsync(x => x.UserId == userId && x.Id != excludeId && x.RevokedAt == null && (x.ExpiresAt == null || x.ExpiresAt > now), ct);

    public async Task<IResult> GetActivityAsync(string userId, Guid id, int limit, CancellationToken ct)
    {
        if (!await db.Set<AgentTokenEntity>().AnyAsync(x => x.Id == id && x.UserId == userId, ct)) return Results.NotFound();
        var rows = await db.Set<AgentAuditLogEntity>().AsNoTracking().Where(x => x.TokenId == id)
            .OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(limit, 1, 200))
            .Select(x => new AgentActivityDto(x.Id, x.Tool, x.BudgetId, x.Success, x.Outcome, x.CreatedAt)).ToListAsync(ct);
        return Results.Ok(rows);
    }

    private AgentTokenDto ToDto(AgentTokenEntity token, IReadOnlyDictionary<string, string> names)
    {
        return new AgentTokenDto(token.Id, token.Name, token.Prefix, token.Permission.ToString(),
            token.Budgets.OrderBy(x => x.BudgetId).Select(x => new AgentTokenBudgetDto(x.BudgetId, names.GetValueOrDefault(x.BudgetId) ?? "Unavailable budget")).ToList(),
            token.CreatedAt, token.ExpiresAt, token.LastUsedAt, token.RevokedAt, AgentTokenLifetime.Status(token, clock.GetUtcNow()), token.RenewedAt);
    }

    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        static string? UserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier);
        // Cookie-authenticated (default scheme) only: agent tokens cannot manage agent tokens.
        var group = app.MapGroup("/api/auth/agent-tokens").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        group.MapGet("", async (ClaimsPrincipal principal, AgentTokenService service, CancellationToken ct) =>
            UserId(principal) is { } userId ? await service.ListAsync(userId, ct) : Results.Unauthorized());
        group.MapPost("", async (CreateAgentTokenRequest request, ClaimsPrincipal principal, AgentTokenService service, CancellationToken ct) =>
            await service.CreateAsync(principal, request, ct));
        group.MapPost("/{id:guid}/revoke", async (Guid id, ClaimsPrincipal principal, AgentTokenService service, CancellationToken ct) =>
            UserId(principal) is { } userId ? await service.RevokeAsync(userId, id, ct) : Results.Unauthorized());
        group.MapPost("/{id:guid}/renew", async (Guid id, RenewAgentTokenRequest? request, ClaimsPrincipal principal, AgentTokenService service, CancellationToken ct) =>
            UserId(principal) is { } userId ? await service.RenewAsync(userId, id, request, ct) : Results.Unauthorized());
        group.MapGet("/{id:guid}/activity", async (Guid id, int? limit, ClaimsPrincipal principal, AgentTokenService service, CancellationToken ct) =>
            UserId(principal) is { } userId ? await service.GetActivityAsync(userId, id, limit ?? 50, ct) : Results.Unauthorized());
    }
}

/// <summary>Expiry rules for agent tokens. A null expiry (no expiration) is only ever produced by an explicit <c>neverExpires: true</c>.</summary>
public static class AgentTokenLifetime
{
    public static bool IsActive(AgentTokenEntity token, DateTimeOffset now) => token.RevokedAt is null && (token.ExpiresAt is null || token.ExpiresAt > now);

    public static string Status(AgentTokenEntity token, DateTimeOffset now) =>
        token.RevokedAt is not null ? "revoked" : IsActive(token, now) ? "active" : "expired";

    /// <summary>Audit action with the resulting expiry mode; never contains secrets.</summary>
    public static string AuditAction(string action, DateTimeOffset? expiresAt) => expiresAt is null ? $"{action}.no-expiry" : action;

    /// <summary>
    /// Resolves the expiry for a create or renew request. <c>neverExpires: true</c> (without days) means no expiration;
    /// explicit days or <c>neverExpires: false</c> mean a dated expiry (default <see cref="AgentTokenService.DefaultExpiryDays"/>);
    /// omitting both keeps the current mode (create always starts from the dated default).
    /// </summary>
    public static bool TryResolve(int? expiresInDays, bool? neverExpires, bool currentlyNeverExpires, DateTimeOffset now,
        out DateTimeOffset? expiresAt, out string error)
    {
        expiresAt = null;
        error = "";
        if (neverExpires == true)
        {
            if (expiresInDays is null) return true;
            error = "Choose either an expiry in days or no expiration, not both.";
            return false;
        }
        if (expiresInDays is null && neverExpires is null && currentlyNeverExpires) return true;
        var days = expiresInDays ?? AgentTokenService.DefaultExpiryDays;
        if (days is < 1 or > AgentTokenService.MaxExpiryDays)
        {
            error = $"Expiry must be between 1 and {AgentTokenService.MaxExpiryDays} days.";
            return false;
        }
        expiresAt = now.AddDays(days);
        return true;
    }
}

public static class McpBudgetIds
{
    public static bool IsValid(string? id) => id is { Length: >= 1 and <= 100 } && id.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');
}
