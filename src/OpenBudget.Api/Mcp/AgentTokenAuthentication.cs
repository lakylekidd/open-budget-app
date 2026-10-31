using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace OpenBudget.Api.Mcp;

public static class AgentTokenClaims
{
    public const string Scheme = "AgentToken";
    public const string TokenId = "openbudget:agent_token_id";
    public const string Permission = "openbudget:agent_permission";
    public const string Budget = "openbudget:agent_budget";
}

/// <summary>Validated agent token identity for one HTTP request.</summary>
public sealed record AgentTokenIdentity(Guid TokenId, string UserId, AgentPermission Permission, IReadOnlyCollection<string> BudgetIds)
{
    public static AgentTokenIdentity? From(ClaimsPrincipal? principal)
    {
        if (principal?.Identity is not { IsAuthenticated: true, AuthenticationType: AgentTokenClaims.Scheme }) return null;
        if (!Guid.TryParse(principal.FindFirstValue(AgentTokenClaims.TokenId), out var tokenId)) return null;
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || !McpPermissions.TryParse(principal.FindFirstValue(AgentTokenClaims.Permission), out var permission)) return null;
        return new AgentTokenIdentity(tokenId, userId, permission, principal.FindAll(AgentTokenClaims.Budget).Select(x => x.Value).ToArray());
    }
}

/// <summary>
/// Authenticates <c>Authorization: Bearer obk_…</c> on the MCP endpoint only. Every request re-reads the token and its
/// creator, so revocation, expiry, suspension, approval and Administrator-role loss take effect immediately.
/// Failures return one generic 401 and never log the presented credential.
/// </summary>
public sealed class AgentTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    BudgetDbContext db, UserManager<OpenBudgetUser> users, TimeProvider clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private static readonly TimeSpan LastUsedResolution = TimeSpan.FromMinutes(1);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var presented = AgentTokenSecrets.ReadBearer(Request.Headers.Authorization);
        if (presented is null) return AuthenticateResult.NoResult();
        if (!AgentTokenSecrets.HasValidShape(presented)) return AuthenticateResult.Fail("Invalid agent token.");

        var hash = AgentTokenSecrets.Hash(presented);
        var token = await db.Set<AgentTokenEntity>().Include(x => x.Budgets).SingleOrDefaultAsync(x => x.TokenHash == hash, Context.RequestAborted);
        var now = clock.GetUtcNow();
        if (token is null || !AgentTokenSecrets.Verify(presented, token.TokenHash) || !AgentTokenLifetime.IsActive(token, now))
            return AuthenticateResult.Fail("Invalid agent token.");

        var user = await users.FindByIdAsync(token.UserId);
        if (user is null || user.IsSuspended || user.RequiresApproval || !user.EmailConfirmed || (user.LockoutEnd is { } lockout && lockout > now))
            return AuthenticateResult.Fail("Invalid agent token.");
        if (token.Permission == AgentPermission.Admin && !await users.IsInRoleAsync(user, AuthenticationSetup.AdministratorRole))
            return AuthenticateResult.Fail("Invalid agent token.");

        if (token.LastUsedAt is null || now - token.LastUsedAt.Value >= LastUsedResolution)
        {
            token.LastUsedAt = now;
            await db.SaveChangesAsync(Context.RequestAborted);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(AgentTokenClaims.TokenId, token.Id.ToString("D")),
            new(AgentTokenClaims.Permission, token.Permission.ToString())
        };
        claims.AddRange(token.Budgets.Select(x => new Claim(AgentTokenClaims.Budget, x.BudgetId)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AgentTokenClaims.Scheme));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, AgentTokenClaims.Scheme));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer realm=\"OpenBudget MCP\"";
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
