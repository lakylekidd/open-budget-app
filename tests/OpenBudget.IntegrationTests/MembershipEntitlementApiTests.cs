using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenBudget.Api;

namespace OpenBudget.IntegrationTests;

public sealed class MembershipEntitlementApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Plan_entitlements_and_user_overrides_drive_reusable_authorization_requirements()
    {
        await using var app = await fixture.CreateAppAsync();
        string adminId;
        string userId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
            await roles.CreateAsync(new IdentityRole(AuthenticationSetup.AdministratorRole));
            var admin = new OpenBudgetUser { UserName = "root", Email = "root@example.test", EmailConfirmed = true };
            Assert.True((await users.CreateAsync(admin, "Strong-pass1!")).Succeeded);
            await users.AddToRoleAsync(admin, AuthenticationSetup.AdministratorRole);
            adminId = admin.Id;
            var user = new OpenBudgetUser { UserName = "member", Email = "member@example.test", EmailConfirmed = true };
            Assert.True((await users.CreateAsync(user, "Strong-pass1!")).Succeeded);
            userId = user.Id;
        }

        using var adminClient = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        using var userClient = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("root", "Strong-pass1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await userClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("member", "Strong-pass1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.GetAsync("/api/admin/membership-plans/free/entitlements")).StatusCode);

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test");
        var principal = new ClaimsPrincipal(identity);
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
            .AddRequirements(new EntitlementRequirement("ledger.export")).Build();
        async Task<bool> IsAuthorizedAsync()
        {
            await using var scope = app.Services.CreateAsyncScope();
            var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            return (await authorization.AuthorizeAsync(principal, resource: null, policy)).Succeeded;
        }

        Assert.False(await IsAuthorizedAsync());
        var planEntitlements = new SaveMembershipEntitlementsRequest([new("ledger.export", true), new("future.beta", false)]);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PutAsJsonAsync("/api/admin/membership-plans/free/entitlements", planEntitlements)).StatusCode);
        Assert.True(await IsAuthorizedAsync());
        var configured = await adminClient.GetFromJsonAsync<MembershipEntitlementDto[]>("/api/admin/membership-plans/free/entitlements");
        Assert.Equal(2, configured?.Length);
        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.PutAsJsonAsync("/api/admin/membership-plans/free/entitlements",
            new SaveMembershipEntitlementsRequest([new("Bad Key", true)]))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PutAsJsonAsync($"/api/admin/users/{userId}/entitlements/ledger.export", new UserEntitlementOverrideRequest(false))).StatusCode);
        Assert.False(await IsAuthorizedAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PutAsJsonAsync($"/api/admin/users/{userId}/entitlements/ledger.export", new UserEntitlementOverrideRequest(true))).StatusCode);
        Assert.True(await IsAuthorizedAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.DeleteAsync($"/api/admin/users/{userId}/entitlements/ledger.export")).StatusCode);
        Assert.True(await IsAuthorizedAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PutAsJsonAsync("/api/admin/membership-plans/free/entitlements",
            new SaveMembershipEntitlementsRequest([new("ledger.export", false)]))).StatusCode);
        Assert.False(await IsAuthorizedAsync());
        await using var db = app.CreateDb();
        Assert.Contains(await db.AdminAuditLogs.ToListAsync(), entry => entry.ActorUserId == adminId && entry.Action == "membership.entitlements.update");
        Assert.Contains(await db.AdminAuditLogs.ToListAsync(), entry => entry.TargetUserId == userId && entry.Action == "user.entitlement.override.clear");
    }
}
