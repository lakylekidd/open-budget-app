using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api.Mcp;

public sealed record CreateAgentTokenRequest(string Name, string Permission, IReadOnlyList<string>? BudgetIds, int? ExpiresInDays);
public sealed record AgentTokenBudgetDto(string Id, string Name);
public sealed record AgentTokenDto(Guid Id, string Name, string Prefix, string Permission, IReadOnlyList<AgentTokenBudgetDto> Budgets,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt, string Status);
public sealed record CreatedAgentTokenDto(string Token, AgentTokenDto Details);
public sealed record AgentActivityDto(Guid Id, string Tool, string? BudgetId, bool Success, string Outcome, DateTimeOffset CreatedAt);

/// <summary>Creates, lists and revokes the signed-in user's agent tokens. Tokens are never readable after creation.</summary>
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
        var days = request.ExpiresInDays ?? DefaultExpiryDays;
        if (days is < 1 or > MaxExpiryDays)
            return Results.BadRequest(new { error = $"Expiry must be between 1 and {MaxExpiryDays} days." });
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

        var now = clock.GetUtcNow();
        var active = await db.Set<AgentTokenEntity>().CountAsync(x => x.UserId == user.Id && x.RevokedAt == null && x.ExpiresAt > now, ct);
        if (active >= MaxActiveTokensPerUser)
            return Results.Conflict(new { error = $"You already have {MaxActiveTokensPerUser} active agent tokens. Revoke one first." });

        var secret = AgentTokenSecrets.Generate();
        var token = new AgentTokenEntity
        {
            UserId = user.Id, Name = name, Prefix = secret.DisplayPrefix, TokenHash = secret.Hash, Permission = permission,
            CreatedAt = now, ExpiresAt = now.AddDays(days),
            Budgets = permission == AgentPermission.Admin ? [] : requested.Select(id => new AgentTokenBudgetEntity { BudgetId = id }).ToList()
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Add(token);
        db.AdminAuditLogs.Add(new AdminAuditLogEntity { ActorUserId = user.Id, TargetUserId = user.Id, Action = $"agent-token.create.{permission.ToString().ToLowerInvariant()}", CreatedAt = now });
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
        var now = clock.GetUtcNow();
        var status = token.RevokedAt is not null ? "revoked" : token.ExpiresAt <= now ? "expired" : "active";
        return new AgentTokenDto(token.Id, token.Name, token.Prefix, token.Permission.ToString(),
            token.Budgets.OrderBy(x => x.BudgetId).Select(x => new AgentTokenBudgetDto(x.BudgetId, names.GetValueOrDefault(x.BudgetId) ?? "Unavailable budget")).ToList(),
            token.CreatedAt, token.ExpiresAt, token.LastUsedAt, token.RevokedAt, status);
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
        group.MapGet("/{id:guid}/activity", async (Guid id, int? limit, ClaimsPrincipal principal, AgentTokenService service, CancellationToken ct) =>
            UserId(principal) is { } userId ? await service.GetActivityAsync(userId, id, limit ?? 50, ct) : Results.Unauthorized());
    }
}

public static class McpBudgetIds
{
    public static bool IsValid(string? id) => id is { Length: >= 1 and <= 100 } && id.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');
}
