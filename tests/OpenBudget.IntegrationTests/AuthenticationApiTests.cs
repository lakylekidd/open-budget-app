using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpenBudget.Api;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;

namespace OpenBudget.IntegrationTests;

public sealed class AuthenticationApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Login_session_logout_and_lockout_use_identity_cookie_and_persisted_user()
    {
        await using var app = await fixture.CreateAppAsync();
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
            var create = await users.CreateAsync(new OpenBudgetUser { UserName = "alice", Email = "alice@example.test", EmailConfirmed = true }, "Strong-pass1!");
            Assert.True(create.Succeeded, string.Join(",", create.Errors.Select(error => error.Code)));
        }

        using var client = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        var failed = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("alice", "wrong-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("alice@example.test", "Strong-pass1!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await client.GetFromJsonAsync<UserSessionDto>("/api/auth/session");
        Assert.Equal("alice", session?.UserName);
        Assert.False(session!.IsAdministrator);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/budget")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/profile")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);

        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("alice", "Strong-pass1!"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest("Strong-pass1!", "New-strong1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("alice", "Strong-pass1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("alice", "New-strong1!"))).StatusCode);
    }

    [Fact]
    public async Task Suspended_users_cannot_login()
    {
        await using var app = await fixture.CreateAppAsync();
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
            var user = new OpenBudgetUser { UserName = "suspended", Email = "suspended@example.test", EmailConfirmed = true, IsSuspended = true };
            var create = await users.CreateAsync(user, "Strong-pass1!");
            Assert.True(create.Succeeded, string.Join(",", create.Errors.Select(error => error.Code)));
        }
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("suspended", "Strong-pass1!"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Registration_is_disabled_by_default_and_open_registration_requires_email_confirmation()
    {
        await using var app = await fixture.CreateAppAsync();
        using var client = app.CreateClient();
        var mode = await client.GetFromJsonAsync<JsonElement>("/api/auth/registration-mode");
        Assert.Equal("Disabled", mode.GetProperty("mode").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/admin/settings/registration-mode", new RegistrationModeRequest("Open"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("newuser", "new@example.test", "Strong-pass1!"))).StatusCode);

        await using (var db = app.CreateDb())
        {
            db.ApplicationSettings.Add(new ApplicationSettingEntity { Key = "RegistrationMode", Value = "Open" });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("newuser", "new@example.test", "Strong-pass1!"))).StatusCode);
        Assert.Contains(app.Email.Messages, message => message.Subject.Contains("Verify"));
        var confirmationLink = new Uri(app.Email.Messages.Single().Body);
        var query = QueryHelpers.ParseQuery(confirmationLink.Query);
        var confirmed = await client.PostAsJsonAsync("/api/auth/confirm-email", new TokenRequest(query["userId"]!, query["token"]!));
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("newuser", "Strong-pass1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("new@example.test"))).StatusCode);
        var resetLink = new Uri(app.Email.Messages.Single(message => message.Subject.Contains("Reset")).Body);
        var resetQuery = QueryHelpers.ParseQuery(resetLink.Query);
        var resetRequest = new ResetPasswordRequest(resetQuery["userId"]!, resetQuery["token"]!, "Changed-pass2!");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/reset-password", resetRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset-password", resetRequest with { NewPassword = "Another-pass3!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("newuser", "Strong-pass1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("newuser", "Changed-pass2!"))).StatusCode);
        await using (var db = app.CreateDb())
        {
            Assert.Contains(db.AdminAuditLogs, entry => entry.Action == "user.register");
            var registered = db.Users.Single(user => user.UserName == "newuser");
            Assert.Equal("free", db.UserMemberships.Single(membership => membership.UserId == registered.Id).MembershipPlanId);
        }
    }

    [Fact]
    public async Task Admin_user_lifecycle_is_role_protected_and_audited()
    {
        await using var app = await fixture.CreateAppAsync();
        string adminId;
        string aliceId;
        string pendingId;
        using (var scope = app.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
            await roles.CreateAsync(new IdentityRole(AuthenticationSetup.AdministratorRole));
            var admin = new OpenBudgetUser { UserName = "root", Email = "root@example.test", EmailConfirmed = true, IsBootstrapAdministrator = true };
            Assert.True((await users.CreateAsync(admin, "Strong-pass1!")).Succeeded);
            await users.AddToRoleAsync(admin, AuthenticationSetup.AdministratorRole);
            adminId = admin.Id;
            var alice = new OpenBudgetUser { UserName = "alice", Email = "alice@example.test", EmailConfirmed = true };
            Assert.True((await users.CreateAsync(alice, "Strong-pass1!")).Succeeded);
            aliceId = alice.Id;
            var pending = new OpenBudgetUser { UserName = "pending", Email = "pending@example.test", EmailConfirmed = true, RequiresApproval = true };
            Assert.True((await users.CreateAsync(pending, "Strong-pass1!")).Succeeded);
            pendingId = pending.Id;
        }
        using var adminClient = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        using var userClient = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("root", "Strong-pass1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await userClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("alice", "Strong-pass1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.PostAsync($"/api/admin/users/{aliceId}/suspend", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.PutAsJsonAsync($"/api/admin/users/{aliceId}/membership", new AdminMembershipRequest("plus", 2))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync("/api/admin/settings/registration-mode", new RegistrationModeRequest("ApprovalRequired"))).StatusCode);
        var tiers = await adminClient.GetFromJsonAsync<MembershipPlanEntity[]>("/api/admin/membership-plans");
        Assert.Contains(tiers!, tier => tier.Id == "free" && tier.MaxActiveBudgets == 1);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PutAsJsonAsync($"/api/admin/users/{aliceId}/membership", new AdminMembershipRequest("plus", 2))).StatusCode);
        using var aliceBudgetOne = await userClient.PostAsJsonAsync("/api/budgets", new CreateOwnedBudgetRequest("First"));
        using var aliceBudgetTwo = await userClient.PostAsJsonAsync("/api/budgets", new CreateOwnedBudgetRequest("Second"));
        Assert.Equal(HttpStatusCode.Created, aliceBudgetOne.StatusCode);
        Assert.Equal(HttpStatusCode.Created, aliceBudgetTwo.StatusCode);
        var aliceBudgetOneDto = (await aliceBudgetOne.Content.ReadFromJsonAsync<OwnedBudgetDto>())!;
        var aliceBudgetTwoDto = (await aliceBudgetTwo.Content.ReadFromJsonAsync<OwnedBudgetDto>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PutAsJsonAsync($"/api/admin/users/{aliceId}/membership", new AdminMembershipRequest("free", null))).StatusCode);
        using var afterLimitReduction = await userClient.GetFromJsonAsync<JsonDocument>("/api/budgets?includeArchived=true");
        var retained = afterLimitReduction!.RootElement.GetProperty("budgets").EnumerateArray().ToArray();
        Assert.Contains(retained, budget => budget.GetProperty("id").GetString() == aliceBudgetOneDto.Id);
        Assert.Contains(retained, budget => budget.GetProperty("id").GetString() == aliceBudgetTwoDto.Id);
        Assert.Equal(2, afterLimitReduction.RootElement.GetProperty("limit").GetProperty("activeBudgets").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await userClient.PostAsJsonAsync("/api/budgets", new CreateOwnedBudgetRequest("Third"))).StatusCode);
        var pendingList = await adminClient.GetFromJsonAsync<AdminUserDto[]>("/api/admin/users?status=Pending");
        Assert.Contains(pendingList!, user => user.Id == pendingId && user.Status == "Pending");
        var dashboard = await adminClient.GetFromJsonAsync<AdminDashboardDto>("/api/admin/dashboard");
        Assert.Equal(3, dashboard?.TotalUsers);
        Assert.Equal(1, dashboard?.PendingUsers);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsync($"/api/admin/users/{pendingId}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("pending", "Strong-pass1!"))).StatusCode);
        var usersList = await adminClient.GetFromJsonAsync<AdminUserDto[]>("/api/admin/users?q=alice&status=Active");
        Assert.Contains(usersList!, user => user.Id == aliceId && user.Status == "Active");
        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.GetAsync("/api/admin/users?status=Other")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsync($"/api/admin/users/{aliceId}/suspend", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.GetAsync("/api/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await adminClient.PostAsync($"/api/admin/users/{adminId}/suspend", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsync($"/api/admin/users/{aliceId}/reactivate", null)).StatusCode);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
            var alice = await users.FindByIdAsync(aliceId);
            alice!.IsSuspended = true;
            await users.SetLockoutEndDateAsync(alice, DateTimeOffset.UtcNow.AddMinutes(15));
            await users.UpdateAsync(alice);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsync($"/api/admin/users/{aliceId}/unlock", null)).StatusCode);
        await using (var db = app.CreateDb())
        {
            var alice = await db.Users.FindAsync(aliceId);
            Assert.True(alice!.IsSuspended);
            Assert.Null(alice.LockoutEnd);
        }
        var audit = await adminClient.GetFromJsonAsync<AdminAuditDto[]>("/api/admin/audit");
        Assert.Contains(audit!, item => item.TargetUserId == aliceId && item.Action == "user.suspend");
        Assert.Contains(audit!, item => item.TargetUserId == aliceId && item.Action == "user.reactivate");
        Assert.Contains(audit!, item => item.TargetUserId == pendingId && item.Action == "registration.approve");
        Assert.Contains(audit!, item => item.TargetUserId == aliceId && item.Action == "user.membership.update");
        Assert.Contains(audit!, item => item.Action == "settings.registration-mode");
    }
}
