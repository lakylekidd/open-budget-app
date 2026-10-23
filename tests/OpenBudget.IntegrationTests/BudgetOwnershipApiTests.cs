using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenBudget.Api;

namespace OpenBudget.IntegrationTests;

public sealed class BudgetOwnershipApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Budgets_are_owned_archivable_and_limited_by_membership()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "alice", "alice@example.test");
        await CreateUserAsync(app, "bob", "bob@example.test");
        using var alice = await LoginAsync(app, "alice");
        using var bob = await LoginAsync(app, "bob");

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.CreateClient().GetAsync("/api/budgets")).StatusCode);
        var aliceBudget = await CreateBudgetAsync(alice, "Home");
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsJsonAsync("/api/budgets", new CreateOwnedBudgetRequest("Second"))).StatusCode);
        var bobBudget = await CreateBudgetAsync(bob, "Private");

        Assert.Equal(HttpStatusCode.NotFound, (await alice.PutAsJsonAsync($"/api/budgets/{bobBudget.Id}", new RenameOwnedBudgetRequest("Stolen", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsync($"/api/budgets/{aliceBudget.Id}/archive", null)).StatusCode);
        var replacement = await CreateBudgetAsync(alice, "Replacement");
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsync($"/api/budgets/{aliceBudget.Id}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsync($"/api/budgets/{replacement.Id}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsync($"/api/budgets/{aliceBudget.Id}/restore", null)).StatusCode);

        using var response = await alice.GetAsync("/api/budgets?includeArchived=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var listing = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var listedIds = listing.RootElement.GetProperty("budgets").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray();
        Assert.Contains(aliceBudget.Id, listedIds);
        Assert.Contains(replacement.Id, listedIds);
        Assert.DoesNotContain(bobBudget.Id, listedIds);
        Assert.Equal("free", listing.RootElement.GetProperty("limit").GetProperty("membership").GetString());
        Assert.Equal(1, listing.RootElement.GetProperty("limit").GetProperty("activeBudgets").GetInt32());

        await using var db = app.CreateDb();
        var aliceUser = await db.Users.SingleAsync(x => x.UserName == "alice");
        var owned = await db.BudgetPlans.SingleAsync(x => x.Id == aliceBudget.Id);
        Assert.Equal(aliceUser.Id, owned.OwnerUserId);
        Assert.True(owned.IsArchived is false);
    }

    [Fact]
    public async Task Concurrent_budget_creation_cannot_exceed_free_membership_limit()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "concurrent", "concurrent@example.test");
        using var client = await LoginAsync(app, "concurrent");
        var writes = await Task.WhenAll(
            client.PostAsJsonAsync("/api/budgets", new CreateOwnedBudgetRequest("First")),
            client.PostAsJsonAsync("/api/budgets", new CreateOwnedBudgetRequest("Second")));
        Assert.Single(writes, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(writes, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in writes) response.Dispose();
    }

    private static async Task CreateUserAsync(TestApp app, string username, string email)
    {
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
        var result = await users.CreateAsync(new OpenBudgetUser { UserName = username, Email = email, EmailConfirmed = true }, "Strong-pass1!");
        Assert.True(result.Succeeded, string.Join(",", result.Errors.Select(x => x.Code)));
    }

    private static async Task<HttpClient> LoginAsync(TestApp app, string username)
    {
        var client = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "Strong-pass1!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    private static async Task<OwnedBudgetDto> CreateBudgetAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/budgets", new CreateOwnedBudgetRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OwnedBudgetDto>())!;
    }
}
