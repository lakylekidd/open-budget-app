using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenBudget.Api;

namespace OpenBudget.IntegrationTests;

public sealed class UserPreferenceApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Preferences_require_a_signed_in_user()
    {
        await using var app = await fixture.CreateAppAsync();
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/me/preferences")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/api/me/preferences", new UpdateUserPreferencesRequest("ocean"))).StatusCode);
    }

    [Fact]
    public async Task A_new_user_gets_the_default_theme_without_a_stored_row()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "fresh", "fresh@example.test");
        using var client = await LoginAsync(app, "fresh");

        var preferences = await Read<UserPreferencesDto>(await client.GetAsync("/api/me/preferences"));

        Assert.Equal("midnight", preferences.Theme);
        await using var db = app.CreateDb();
        Assert.Empty(await db.UserPreferences.ToListAsync());
    }

    [Fact]
    public async Task The_chosen_theme_is_saved_per_account_and_survives_a_new_sign_in()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "alice", "alice@example.test");
        await CreateUserAsync(app, "bob", "bob@example.test");
        using (var alice = await LoginAsync(app, "alice"))
        {
            Assert.Equal("ocean", (await Read<UserPreferencesDto>(await alice.PutAsJsonAsync("/api/me/preferences", new UpdateUserPreferencesRequest("ocean")))).Theme);
            // Saving again replaces the value instead of adding a second row.
            Assert.Equal("forest", (await Read<UserPreferencesDto>(await alice.PutAsJsonAsync("/api/me/preferences", new UpdateUserPreferencesRequest("forest")))).Theme);
        }

        using var aliceAgain = await LoginAsync(app, "alice");
        using var bob = await LoginAsync(app, "bob");
        Assert.Equal("forest", (await Read<UserPreferencesDto>(await aliceAgain.GetAsync("/api/me/preferences"))).Theme);
        Assert.Equal("midnight", (await Read<UserPreferencesDto>(await bob.GetAsync("/api/me/preferences"))).Theme);

        await using var db = app.CreateDb();
        var rows = await db.UserPreferences.ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal("forest", row.Theme);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ocean")]
    [InlineData("neon")]
    [InlineData("ocean; drop table")]
    public async Task Unknown_or_malformed_themes_are_rejected_and_change_nothing(string? theme)
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "carol", "carol@example.test");
        using var client = await LoginAsync(app, "carol");
        await Read<UserPreferencesDto>(await client.PutAsJsonAsync("/api/me/preferences", new UpdateUserPreferencesRequest("rosewood")));

        var response = await client.PutAsJsonAsync("/api/me/preferences", new UpdateUserPreferencesRequest(theme));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("rosewood", (await Read<UserPreferencesDto>(await client.GetAsync("/api/me/preferences"))).Theme);
    }

    [Fact]
    public async Task A_stored_theme_that_is_no_longer_offered_falls_back_to_the_default()
    {
        await using var app = await fixture.CreateAppAsync();
        var userId = await CreateUserAsync(app, "dave", "dave@example.test");
        await using (var db = app.CreateDb())
        {
            db.UserPreferences.Add(new UserPreferenceEntity { UserId = userId, Theme = "retired-theme", UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        using var client = await LoginAsync(app, "dave");

        Assert.Equal("midnight", (await Read<UserPreferencesDto>(await client.GetAsync("/api/me/preferences"))).Theme);
        Assert.Equal("graphite", (await Read<UserPreferencesDto>(await client.PutAsJsonAsync("/api/me/preferences", new UpdateUserPreferencesRequest("graphite")))).Theme);
    }

    [Fact]
    public async Task Every_offered_theme_can_be_saved()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "erin", "erin@example.test");
        using var client = await LoginAsync(app, "erin");
        foreach (var key in UserThemes.Keys)
            Assert.Equal(key, (await Read<UserPreferencesDto>(await client.PutAsJsonAsync("/api/me/preferences", new UpdateUserPreferencesRequest(key)))).Theme);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<string> CreateUserAsync(TestApp app, string username, string email)
    {
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<OpenBudgetUser>>();
        var user = new OpenBudgetUser { UserName = username, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, "Strong-pass1!");
        Assert.True(result.Succeeded, string.Join(",", result.Errors.Select(x => x.Code)));
        return user.Id;
    }

    private static async Task<HttpClient> LoginAsync(TestApp app, string username)
    {
        var client = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "Strong-pass1!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }
}
