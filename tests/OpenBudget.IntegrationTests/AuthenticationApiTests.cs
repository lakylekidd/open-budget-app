using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OpenBudget.Api;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;

namespace OpenBudget.IntegrationTests;

public sealed class AuthenticationApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Identity_and_budget_scope_migrations_preserve_legacy_financial_data_and_assign_it_to_development_admin()
    {
        await using var app = await fixture.CreateAppAsync();
        var accountId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        await using (var db = app.CreateDb())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261022104319_LongTermPlanning");
            db.Budgets.Add(new BudgetEntity { BudgetId = "default", Month = "2026-10", FundsAdded = 75m });
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "Accounts" ("Id", "Name", "IsClosed", "AccountType", "IsFavorite", "Notes")
                VALUES ({0}, 'Legacy checking', false, 'checking', false, '');
                INSERT INTO "Transactions" ("Id", "AccountId", "Date", "Amount", "Memo", "Status", "IsOpening", "IsDeleted")
                VALUES ({1}, {0}, DATE '2026-10-15', -12.34, 'Legacy purchase', 'uncleared', false, false);
                """, accountId, transactionId);
            await migrator.MigrateAsync();
        }

        var environment = app.Services.GetRequiredService<IWebHostEnvironment>();
        var originalEnvironment = environment.EnvironmentName;
        environment.EnvironmentName = Environments.Development;
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BootstrapAdmin:Username"] = "devroot",
                ["BootstrapAdmin:Email"] = "devroot@example.test",
                ["BootstrapAdmin:Password"] = "Strong-pass1!"
            }).Build();
            await AuthenticationSetup.EnsureBootstrapAdministratorAsync(app.Services, configuration, environment);
            await using var verify = app.CreateDb();
            var administrator = await verify.Users.SingleAsync(user => user.UserName == "devroot");
            var budgets = await verify.BudgetPlans.SingleAsync(budget => budget.Id == "default");
            Assert.Equal(administrator.Id, budgets.OwnerUserId);
            Assert.Equal(75m, (await verify.Budgets.SingleAsync(month => month.BudgetId == "default" && month.Month == "2026-10")).FundsAdded);
            var account = await verify.Accounts.SingleAsync(item => item.Id == accountId);
            var transaction = await verify.Transactions.SingleAsync(item => item.Id == transactionId);
            Assert.Equal("Legacy checking", account.Name);
            Assert.Equal(-12.34m, transaction.Amount);
            Assert.Equal("Legacy purchase", transaction.Memo);
            var users = app.Services.GetRequiredService<UserManager<OpenBudgetUser>>();
            Assert.True(await users.CheckPasswordAsync(administrator, "Strong-pass1!"));
        }
        finally
        {
            environment.EnvironmentName = originalEnvironment;
        }
    }

    [Fact]
    public async Task Production_bootstrap_admin_uses_configured_credentials_and_preserves_existing_data_on_restart()
    {
        await using var app = await fixture.CreateAppAsync();
        var environment = app.Services.GetRequiredService<IWebHostEnvironment>();
        var originalEnvironment = environment.EnvironmentName;
        environment.EnvironmentName = Environments.Production;
        var legacyAccountId = Guid.NewGuid();
        var legacyTransactionId = Guid.NewGuid();
        try
        {
            await using (var seedScope = app.Services.CreateAsyncScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<BudgetDbContext>();
                db.Accounts.Add(new AccountEntity { Id = legacyAccountId, Name = "Legacy checking" });
                db.Transactions.Add(new TransactionEntity
                {
                    Id = legacyTransactionId,
                    AccountId = legacyAccountId,
                    Date = new DateOnly(2026, 8, 15),
                    Amount = -12.34m,
                    Memo = "Legacy transaction"
                });
                await db.SaveChangesAsync();
            }
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BootstrapAdmin:Username"] = "configured-root",
                ["BootstrapAdmin:Email"] = "root@example.test",
                ["BootstrapAdmin:Password"] = "Strong-pass1!"
            }).Build();
            await AuthenticationSetup.EnsureBootstrapAdministratorAsync(app.Services, configuration, environment);

            await using (var scope = app.Services.CreateAsyncScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
                var user = await users.FindByNameAsync("configured-root");
                Assert.NotNull(user);
                Assert.True(user.IsBootstrapAdministrator);
                Assert.True(user.EmailConfirmed);
                Assert.True(await users.IsInRoleAsync(user, AuthenticationSetup.AdministratorRole));
                Assert.True(await users.CheckPasswordAsync(user, "Strong-pass1!"));
                var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
                Assert.Equal("pro", (await db.UserMemberships.SingleAsync(membership => membership.UserId == user.Id)).MembershipPlanId);
                var defaultBudget = await db.BudgetPlans.SingleAsync(budget => budget.Id == "default");
                Assert.Equal(user.Id, defaultBudget.OwnerUserId);
                var preservedTransaction = await db.Transactions.SingleAsync(transaction => transaction.Id == legacyTransactionId);
                Assert.Equal(legacyAccountId, preservedTransaction.AccountId);
                Assert.Equal(-12.34m, preservedTransaction.Amount);
                Assert.Equal("Legacy transaction", preservedTransaction.Memo);
            }

            using var adminClient = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true, BaseAddress = new Uri("https://localhost") });
            adminClient.DefaultRequestHeaders.Add("Origin", "https://localhost");
            Assert.Equal(HttpStatusCode.OK, (await adminClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("configured-root", "Strong-pass1!"))).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest("Strong-pass1!", "Changed-pass2!"))).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await adminClient.GetAsync("/api/auth/session")).StatusCode);

            var changedConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BootstrapAdmin:Username"] = "configured-root",
                ["BootstrapAdmin:Email"] = "root@example.test",
                ["BootstrapAdmin:Password"] = "Other-strong1!"
            }).Build();
            await AuthenticationSetup.EnsureBootstrapAdministratorAsync(app.Services, changedConfiguration, environment);
            await using var verifyScope = app.Services.CreateAsyncScope();
            var verifyUsers = verifyScope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
            var existing = await verifyUsers.FindByNameAsync("configured-root");
            Assert.NotNull(existing);
            Assert.True(await verifyUsers.CheckPasswordAsync(existing, "Changed-pass2!"));
            Assert.False(await verifyUsers.CheckPasswordAsync(existing, "Strong-pass1!"));
            Assert.False(await verifyUsers.CheckPasswordAsync(existing, "Other-strong1!"));
            var audit = verifyScope.ServiceProvider.GetRequiredService<BudgetDbContext>();
            Assert.Contains(await audit.AdminAuditLogs.ToListAsync(), entry => entry.TargetUserId == existing.Id && entry.Action == "user.password.change");
        }
        finally
        {
            environment.EnvironmentName = originalEnvironment;
        }
    }

    [Fact]
    public async Task Non_development_bootstrap_requires_configured_credentials_and_does_not_create_development_admin()
    {
        await using var app = await fixture.CreateAppAsync();
        var environment = app.Services.GetRequiredService<IWebHostEnvironment>();
        var originalEnvironment = environment.EnvironmentName;
        environment.EnvironmentName = Environments.Production;
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => AuthenticationSetup.EnsureBootstrapAdministratorAsync(app.Services, configuration, environment));
            Assert.Contains("Non-Development environments require BootstrapAdmin", error.Message);

            await using var verify = app.CreateDb();
            Assert.DoesNotContain(await verify.Users.ToListAsync(), user => user.UserName == "admin");
        }
        finally
        {
            environment.EnvironmentName = originalEnvironment;
        }
    }

    [Fact]
    public async Task Login_session_logout_and_lockout_use_identity_cookie_and_persisted_user()
    {
        await using var app = await fixture.CreateAppAsync();
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
            var create = await users.CreateAsync(new OpenBudgetUser { UserName = "alice", Email = "alice@example.test", EmailConfirmed = true }, "Strong-pass1!");
            Assert.True(create.Succeeded, string.Join(",", create.Errors.Select(error => error.Code)));
            var other = await users.CreateAsync(new OpenBudgetUser { UserName = "bob", Email = "bob@example.test", EmailConfirmed = true }, "Strong-pass1!");
            Assert.True(other.Succeeded, string.Join(",", other.Errors.Select(error => error.Code)));
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
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest("bob"))).StatusCode);
        Assert.Equal("alice", (await client.GetFromJsonAsync<UserSessionDto>("/api/auth/session"))?.UserName);
        var updatedProfile = await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest("  alice-renamed  "));
        Assert.Equal(HttpStatusCode.OK, updatedProfile.StatusCode);
        Assert.Equal("alice-renamed", (await updatedProfile.Content.ReadFromJsonAsync<UserSessionDto>())?.UserName);
        var refreshedSession = await client.GetFromJsonAsync<UserSessionDto>("/api/auth/session");
        Assert.Equal("alice-renamed", refreshedSession?.UserName);
        Assert.Equal("alice@example.test", refreshedSession?.Email);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);

        using var secondSession = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.OK, (await secondSession.PostAsJsonAsync("/api/auth/login", new LoginRequest("alice-renamed", "Strong-pass1!"))).StatusCode);
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("alice-renamed", "Strong-pass1!"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest("Strong-pass1!", "New-strong1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await secondSession.GetAsync("/api/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("alice-renamed", "Strong-pass1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("alice-renamed", "New-strong1!"))).StatusCode);
        await using var db = app.CreateDb();
        var alice = await db.Users.SingleAsync(user => user.UserName == "alice-renamed");
        Assert.Contains(db.AdminAuditLogs, entry => entry.TargetUserId == alice.Id && entry.Action == "user.password.change");
        Assert.DoesNotContain(db.AdminAuditLogs, entry => entry.Action.Contains("Strong-pass", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Admin_password_reset_sends_expiring_link_without_disclosing_token_or_existing_password()
    {
        await using var app = await fixture.CreateAppAsync();
        string targetId;
        using (var scope = app.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
            await roles.CreateAsync(new IdentityRole(AuthenticationSetup.AdministratorRole));
            var admin = new OpenBudgetUser { UserName = "root", Email = "root@example.test", EmailConfirmed = true };
            Assert.True((await users.CreateAsync(admin, "Strong-pass1!")).Succeeded);
            await users.AddToRoleAsync(admin, AuthenticationSetup.AdministratorRole);
            var target = new OpenBudgetUser { UserName = "alice", Email = "alice@example.test", EmailConfirmed = true };
            Assert.True((await users.CreateAsync(target, "Strong-pass1!")).Succeeded);
            targetId = target.Id;
        }

        using var adminClient = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        using var userClient = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("root", "Strong-pass1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await userClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("alice", "Strong-pass1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.PostAsync($"/api/admin/users/{targetId}/password-reset", null)).StatusCode);

        using var response = await adminClient.PostAsync($"/api/admin/users/{targetId}/password-reset", null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Strong-pass1!", responseBody, StringComparison.Ordinal);
        var resetMessage = Assert.Single(app.Email.Messages, message => message.Subject.Contains("Reset"));
        var resetLink = new Uri(resetMessage.Body);
        var query = QueryHelpers.ParseQuery(resetLink.Query);
        Assert.Equal(targetId, query["userId"].ToString());
        var resetRequest = new ResetPasswordRequest(query["userId"]!, query["token"]!, "Changed-pass2!");
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsJsonAsync("/api/auth/reset-password", resetRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.GetAsync("/api/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("alice", "Strong-pass1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("alice", "Changed-pass2!"))).StatusCode);
        await using var db = app.CreateDb();
        Assert.Contains(db.AdminAuditLogs, entry => entry.TargetUserId == targetId && entry.Action == "user.password-reset.initiated");
        Assert.Contains(db.AdminAuditLogs, entry => entry.TargetUserId == targetId && entry.Action == "user.password.reset");
    }

    [Fact]
    public async Task Session_cookie_is_secure_and_state_changes_require_a_matching_origin_or_referer()
    {
        await using var app = await fixture.CreateAppAsync();
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
            Assert.True((await users.CreateAsync(new OpenBudgetUser { UserName = "alice", Email = "alice@example.test", EmailConfirmed = true }, "Strong-pass1!")).Succeeded);
        }
        using var client = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.Add("Origin", "https://localhost");
        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("alice", "Strong-pass1!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var setCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        client.DefaultRequestHeaders.Add("Cookie", setCookie.Split(';', 2)[0]);

        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Add("Origin", "https://attacker.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest("blocked"))).StatusCode);

        var environment = app.Services.GetRequiredService<IWebHostEnvironment>();
        var originalEnvironment = environment.EnvironmentName;
        environment.EnvironmentName = Environments.Production;
        try
        {
            client.DefaultRequestHeaders.Remove("Origin");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest("missing-origin"))).StatusCode);
            client.DefaultRequestHeaders.Add("Referer", "https://localhost/profile");
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest("alice-secure"))).StatusCode);
        }
        finally
        {
            environment.EnvironmentName = originalEnvironment;
        }
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
        using var ownedBudgets = await client.GetFromJsonAsync<JsonDocument>("/api/budgets?includeArchived=true");
        Assert.Empty(ownedBudgets!.RootElement.GetProperty("budgets").EnumerateArray());
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
        var adminSession = await adminClient.GetFromJsonAsync<UserSessionDto>("/api/auth/session");
        var aliceSession = await userClient.GetFromJsonAsync<UserSessionDto>("/api/auth/session");
        Assert.NotEqual(adminSession?.Id, aliceSession?.Id);
        Assert.True(adminSession?.IsAdministrator);
        Assert.False(aliceSession?.IsAdministrator);
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.GetAsync($"/api/admin/users/{aliceId}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.PostAsync($"/api/admin/users/{aliceId}/suspend", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.PutAsJsonAsync($"/api/admin/users/{aliceId}/membership", new AdminMembershipRequest("plus", 2))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync("/api/admin/settings/registration-mode", new RegistrationModeRequest("ApprovalRequired"))).StatusCode);
        var tiers = await adminClient.GetFromJsonAsync<MembershipPlanEntity[]>("/api/admin/membership-plans");
        Assert.Contains(tiers!, tier => tier.Id == "free" && tier.MaxActiveBudgets == 1);
        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.PutAsJsonAsync($"/api/admin/users/{aliceId}/membership",
            new AdminMembershipRequest("plus", 2, new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 1)))).StatusCode);
        var membershipRequest = new AdminMembershipRequest("plus", 2, new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1), "stripe", "sub_test_123");
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PutAsJsonAsync($"/api/admin/users/{aliceId}/membership", membershipRequest)).StatusCode);
        var assignedMembership = await adminClient.GetFromJsonAsync<AdminUserDto[]>($"/api/admin/users?q=alice");
        var aliceMembership = Assert.Single(assignedMembership!);
        Assert.Equal(new DateOnly(2026, 10, 1), aliceMembership.MembershipStartsOn);
        Assert.Equal(new DateOnly(2027, 10, 1), aliceMembership.MembershipExpiresOn);
        Assert.Equal("stripe", aliceMembership.BillingProvider);
        Assert.Equal("sub_test_123", aliceMembership.ExternalSubscriptionId);
        using var aliceBudgetOne = await userClient.PostAsJsonAsync("/api/budgets", new CreateOwnedBudgetRequest("First"));
        using var aliceBudgetTwo = await userClient.PostAsJsonAsync("/api/budgets", new CreateOwnedBudgetRequest("Second"));
        Assert.Equal(HttpStatusCode.Created, aliceBudgetOne.StatusCode);
        Assert.Equal(HttpStatusCode.Created, aliceBudgetTwo.StatusCode);
        var aliceBudgetOneDto = (await aliceBudgetOne.Content.ReadFromJsonAsync<OwnedBudgetDto>())!;
        var aliceBudgetTwoDto = (await aliceBudgetTwo.Content.ReadFromJsonAsync<OwnedBudgetDto>())!;
        using var administratorBudgets = await adminClient.GetFromJsonAsync<JsonDocument>("/api/budgets?includeArchived=true");
        var administratorBudgetIds = administratorBudgets!.RootElement.GetProperty("budgets").EnumerateArray()
            .Select(budget => budget.GetProperty("id").GetString()).ToArray();
        Assert.DoesNotContain(aliceBudgetOneDto.Id, administratorBudgetIds);
        Assert.DoesNotContain(aliceBudgetTwoDto.Id, administratorBudgetIds);
        Assert.Equal(HttpStatusCode.NotFound, (await adminClient.GetAsync($"/api/budgets/{aliceBudgetOneDto.Id}/months/2026-10")).StatusCode);
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
        Assert.Contains(dashboard!.UsersByMembership, membership => membership.MembershipPlanId == "free" && membership.UserCount == 3);
        Assert.Contains(dashboard.RecentRegistrations, registration => registration.UserId == pendingId && registration.Status == "Pending");
        Assert.Contains(dashboard.RecentSecurityEvents, entry => entry.Action == "user.membership.update");
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
        var aliceHistory = await adminClient.GetFromJsonAsync<AdminAuditDto[]>($"/api/admin/users/{aliceId}/history");
        Assert.Contains(aliceHistory!, item => item.Action == "user.suspend");
        Assert.Contains(aliceHistory!, item => item.Action == "user.reactivate");
        Assert.DoesNotContain(aliceHistory!, item => item.TargetUserId != aliceId);
        Assert.Equal(HttpStatusCode.NotFound, (await adminClient.GetAsync("/api/admin/users/unknown/history")).StatusCode);
    }

    [Fact]
    public async Task Administrator_invites_verified_email_user_without_exposing_a_password_or_token()
    {
        await using var app = await fixture.CreateAppAsync();
        string adminId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
            await roles.CreateAsync(new IdentityRole(AuthenticationSetup.AdministratorRole));
            var admin = new OpenBudgetUser { UserName = "root", Email = "root@example.test", EmailConfirmed = true, IsBootstrapAdministrator = true };
            Assert.True((await users.CreateAsync(admin, "Strong-pass1!")).Succeeded);
            await users.AddToRoleAsync(admin, AuthenticationSetup.AdministratorRole);
            adminId = admin.Id;
            var ordinary = new OpenBudgetUser { UserName = "ordinary", Email = "ordinary@example.test", EmailConfirmed = true };
            Assert.True((await users.CreateAsync(ordinary, "Strong-pass1!")).Succeeded);
        }

        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/admin/users/invite", new AdminInviteUserRequest("new-user", "new@example.test"))).StatusCode);
        using var ordinaryClient = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.OK, (await ordinaryClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("ordinary", "Strong-pass1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinaryClient.PostAsJsonAsync("/api/admin/users/invite", new AdminInviteUserRequest("new-user", "new@example.test"))).StatusCode);

        using var adminClient = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("root", "Strong-pass1!"))).StatusCode);
        app.Email.IsConfigured = false;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await adminClient.PostAsJsonAsync("/api/admin/users/invite", new AdminInviteUserRequest("no-email", "no-email@example.test"))).StatusCode);
        await using (var db = app.CreateDb())
            Assert.False(await db.Users.AnyAsync(user => user.UserName == "no-email"));
        app.Email.IsConfigured = true;
        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.PostAsJsonAsync("/api/admin/users/invite", new AdminInviteUserRequest("new-user", "new@example.test", "missing"))).StatusCode);
        using var response = await adminClient.PostAsJsonAsync("/api/admin/users/invite", new AdminInviteUserRequest("new-user", "new@example.test", "plus"));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", responseBody, StringComparison.OrdinalIgnoreCase);

        var invite = Assert.Single(app.Email.Messages);
        Assert.Equal("new@example.test", invite.To);
        Assert.Contains("Verify your email", invite.Body);
        Assert.Contains("Forgot password", invite.Body);
        var inviteUri = new Uri(invite.Body.Split("Verify your email using this link: ")[1].Split('\n')[0]);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(inviteUri.Query);
        var invitedId = query["userId"].ToString();
        var encodedToken = query["token"].ToString();
        Assert.NotEqual(adminId, invitedId);

        await using (var db = app.CreateDb())
        {
            var invited = await db.Users.SingleAsync(user => user.Id == invitedId);
            Assert.False(invited.EmailConfirmed);
            Assert.False(invited.RequiresApproval);
            Assert.Equal("plus", (await db.UserMemberships.SingleAsync(membership => membership.UserId == invitedId)).MembershipPlanId);
            Assert.Contains(db.AdminAuditLogs, entry => entry.ActorUserId == adminId && entry.TargetUserId == invitedId && entry.Action == "user.invite");
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/auth/confirm-email", new TokenRequest(invitedId, encodedToken + "invalid"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("/api/auth/confirm-email", new TokenRequest(invitedId, encodedToken))).StatusCode);

        app.Email.FailSending = true;
        using var failedInvite = await adminClient.PostAsJsonAsync("/api/admin/users/invite", new AdminInviteUserRequest("delivery-failure", "delivery-failure@example.test"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failedInvite.StatusCode);
        await using var verify = app.CreateDb();
        Assert.False(await verify.Users.AnyAsync(user => user.UserName == "delivery-failure"));
    }
}
