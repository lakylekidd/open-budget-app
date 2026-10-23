using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpenBudget.Api;
using System.Net;
using System.Net.Http.Json;

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

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
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
}
