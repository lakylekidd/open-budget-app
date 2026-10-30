using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed record MembershipEntitlementDto(string EntitlementKey, bool IsEnabled);
public sealed record SaveMembershipEntitlementsRequest(IReadOnlyList<MembershipEntitlementDto> Entitlements);
public sealed record UserEntitlementOverrideRequest(bool IsEnabled);

public sealed class EntitlementService(BudgetDbContext db)
{
    private static readonly Regex KeyPattern = new("^[a-z][a-z0-9._-]{0,79}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Feature keys enforced by endpoints; the admin panel lists them even before a tier configures them.
    public static readonly IReadOnlyList<string> KnownKeys = [WishlistRules.EntitlementKey];

    public static bool IsValidKey(string? key) => key is not null && KeyPattern.IsMatch(key);

    public async Task<bool> HasAsync(string userId, string entitlementKey, CancellationToken cancellationToken = default)
    {
        if (!IsValidKey(entitlementKey)) return false;
        var userOverride = await db.UserEntitlementOverrides.AsNoTracking()
            .Where(item => item.UserId == userId && item.EntitlementKey == entitlementKey)
            .Select(item => (bool?)item.IsEnabled).SingleOrDefaultAsync(cancellationToken);
        if (userOverride.HasValue) return userOverride.Value;

        var planId = await db.UserMemberships.AsNoTracking().Where(item => item.UserId == userId)
            .Select(item => item.MembershipPlanId).SingleOrDefaultAsync(cancellationToken) ?? "free";
        return await db.MembershipPlanEntitlements.AsNoTracking().AnyAsync(item =>
            item.MembershipPlanId == planId && item.EntitlementKey == entitlementKey && item.IsEnabled, cancellationToken);
    }

    public static void MapEndpoints(WebApplication app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(policy => policy.RequireRole(AuthenticationSetup.AdministratorRole));
        admin.MapGet("/entitlement-keys", async (BudgetDbContext db, CancellationToken ct) =>
        {
            var configured = await db.MembershipPlanEntitlements.AsNoTracking().Select(item => item.EntitlementKey).Distinct().ToListAsync(ct);
            return Results.Ok(KnownKeys.Union(configured, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList());
        });
        admin.MapGet("/users/{id}/entitlements", async (string id, BudgetDbContext db, CancellationToken ct) =>
        {
            if (!await db.Users.AnyAsync(user => user.Id == id, ct)) return Results.NotFound();
            return Results.Ok(await db.UserEntitlementOverrides.AsNoTracking().Where(item => item.UserId == id)
                .OrderBy(item => item.EntitlementKey).Select(item => new MembershipEntitlementDto(item.EntitlementKey, item.IsEnabled)).ToListAsync(ct));
        });
        admin.MapGet("/membership-plans/{id}/entitlements", async (string id, BudgetDbContext db, CancellationToken ct) =>
        {
            if (!await db.MembershipPlans.AnyAsync(plan => plan.Id == id, ct)) return Results.NotFound();
            return Results.Ok(await db.MembershipPlanEntitlements.AsNoTracking().Where(item => item.MembershipPlanId == id)
                .OrderBy(item => item.EntitlementKey).Select(item => new MembershipEntitlementDto(item.EntitlementKey, item.IsEnabled)).ToListAsync(ct));
        });
        admin.MapPut("/membership-plans/{id}/entitlements", async (string id, SaveMembershipEntitlementsRequest request,
            ClaimsPrincipal principal, BudgetDbContext db, CancellationToken ct) =>
        {
            var requested = request.Entitlements ?? [];
            if (requested.Any(item => !IsValidKey(item.EntitlementKey)) || requested.Select(item => item.EntitlementKey).Distinct(StringComparer.Ordinal).Count() != requested.Count)
                return Results.BadRequest(new { error = "Entitlement keys must be unique and use lowercase letters, digits, dots, underscores or hyphens." });
            if (!await db.MembershipPlans.AnyAsync(plan => plan.Id == id, ct)) return Results.NotFound();

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var current = await db.MembershipPlanEntitlements.Where(item => item.MembershipPlanId == id).ToListAsync(ct);
            db.MembershipPlanEntitlements.RemoveRange(current);
            db.MembershipPlanEntitlements.AddRange(requested.Select(item => new MembershipPlanEntitlementEntity
            {
                MembershipPlanId = id,
                EntitlementKey = item.EntitlementKey,
                IsEnabled = item.IsEnabled
            }));
            await db.SaveChangesAsync(ct);
            await AddAuditAsync(db, principal, $"membership-plan:{id}", "membership.entitlements.update", ct);
            await transaction.CommitAsync(ct);
            return Results.NoContent();
        });
        admin.MapPut("/users/{id}/entitlements/{key}", async (string id, string key, UserEntitlementOverrideRequest request,
            ClaimsPrincipal principal, BudgetDbContext db, CancellationToken ct) =>
        {
            if (!IsValidKey(key)) return Results.BadRequest(new { error = "Entitlement key is invalid." });
            if (!await db.Users.AnyAsync(user => user.Id == id, ct)) return Results.NotFound();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var item = await db.UserEntitlementOverrides.SingleOrDefaultAsync(value => value.UserId == id && value.EntitlementKey == key, ct);
            if (item is null) db.UserEntitlementOverrides.Add(new UserEntitlementOverrideEntity { UserId = id, EntitlementKey = key, IsEnabled = request.IsEnabled });
            else item.IsEnabled = request.IsEnabled;
            await db.SaveChangesAsync(ct);
            await AddAuditAsync(db, principal, id, "user.entitlement.override", ct);
            await transaction.CommitAsync(ct);
            return Results.NoContent();
        });
        admin.MapDelete("/users/{id}/entitlements/{key}", async (string id, string key, ClaimsPrincipal principal, BudgetDbContext db, CancellationToken ct) =>
        {
            if (!IsValidKey(key)) return Results.BadRequest(new { error = "Entitlement key is invalid." });
            if (!await db.Users.AnyAsync(user => user.Id == id, ct)) return Results.NotFound();
            var item = await db.UserEntitlementOverrides.SingleOrDefaultAsync(value => value.UserId == id && value.EntitlementKey == key, ct);
            if (item is null) return Results.NoContent();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.UserEntitlementOverrides.Remove(item);
            await db.SaveChangesAsync(ct);
            await AddAuditAsync(db, principal, id, "user.entitlement.override.clear", ct);
            await transaction.CommitAsync(ct);
            return Results.NoContent();
        });
    }

    private static async Task AddAuditAsync(BudgetDbContext db, ClaimsPrincipal principal, string targetId, string action, CancellationToken ct)
    {
        db.AdminAuditLogs.Add(new AdminAuditLogEntity
        {
            ActorUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
            TargetUserId = targetId,
            Action = action
        });
        await db.SaveChangesAsync(ct);
    }
}

public sealed record EntitlementRequirement(string Key) : IAuthorizationRequirement;

public sealed class EntitlementAuthorizationHandler(EntitlementService entitlements) : AuthorizationHandler<EntitlementRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, EntitlementRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null && await entitlements.HasAsync(userId, requirement.Key)) context.Succeed(requirement);
    }
}

public static class EntitlementEndpointExtensions
{
    public static RouteHandlerBuilder RequireEntitlement(this RouteHandlerBuilder endpoint, string key)
    {
        if (!EntitlementService.IsValidKey(key)) throw new ArgumentException("Entitlement key is invalid.", nameof(key));
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(new EntitlementRequirement(key)).Build();
        return endpoint.RequireAuthorization(policy);
    }
}
