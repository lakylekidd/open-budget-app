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
        using var aliceMonthResponse = await alice.GetAsync($"/api/budgets/{aliceBudget.Id}/months/2026-10");
        Assert.Equal(HttpStatusCode.OK, aliceMonthResponse.StatusCode);
        var aliceMonth = (await aliceMonthResponse.Content.ReadFromJsonAsync<BudgetDto>())!;
        var update = new BudgetUpdateDto(aliceMonth.Month, aliceMonth.Note, aliceMonth.Version, aliceMonth.FundsAdded,
            aliceMonth.Groups.Select(group => new CategoryGroupUpdateDto(group.Id, group.Name, group.SortOrder, group.IsArchived,
                group.Categories.Select(category => new CategoryUpdateDto(category.Id, category.Name,
                    category.Id == "groceries" ? 125m : category.Assigned, category.Target, category.Icon, category.Snoozed, category.SortOrder, category.IsArchived)).ToList())).ToList());
        Assert.Equal(HttpStatusCode.OK, (await alice.PutAsJsonAsync($"/api/budgets/{aliceBudget.Id}/months/2026-10", update)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/budgets/{aliceBudget.Id}/months/2026-10")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/budgets/{aliceBudget.Id}/months/2026-10", update)).StatusCode);
        using var bobMonthResponse = await bob.GetAsync($"/api/budgets/{bobBudget.Id}/months/2026-10");
        var bobMonth = (await bobMonthResponse.Content.ReadFromJsonAsync<BudgetDto>())!;
        Assert.Equal(0m, bobMonth.Groups.SelectMany(group => group.Categories).Single(category => category.Id == "groceries").Assigned);
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

    [Fact]
    public async Task Legacy_ledger_recalculation_and_planning_reads_do_not_cross_owned_budget_snapshots()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "isolated", "isolated@example.test");
        using var owner = await LoginAsync(app, "isolated");
        var privateBudget = await CreateBudgetAsync(owner, "Private plan");
        using var privateMonthResponse = await owner.GetAsync($"/api/budgets/{privateBudget.Id}/months/2026-10");
        var privateMonth = (await privateMonthResponse.Content.ReadFromJsonAsync<BudgetDto>())!;
        Assert.Equal(0m, privateMonth.Groups.SelectMany(group => group.Categories).Single(category => category.Id == "groceries").Activity);

        using var legacyClient = app.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await legacyClient.GetAsync("/api/budget/2026-10")).StatusCode);
        var accounts = await legacyClient.GetFromJsonAsync<AccountsDto>("/api/accounts");
        var account = await legacyClient.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(Guid.NewGuid(), accounts!.Version,
            "Legacy checking", new DateOnly(2026, 10, 21), 100m));
        Assert.Equal(HttpStatusCode.OK, account.StatusCode);
        var created = (await account.Content.ReadFromJsonAsync<MutationDto>())!;
        accounts = await legacyClient.GetFromJsonAsync<AccountsDto>("/api/accounts");
        var transaction = await legacyClient.PostAsJsonAsync("/api/transactions", new TransactionRequest(Guid.NewGuid(), accounts!.Version,
            created.Id, new DateOnly(2026, 10, 21), -25m, "Market", "", "groceries", "uncleared", []));
        Assert.Equal(HttpStatusCode.OK, transaction.StatusCode);

        using var updatedPrivateMonthResponse = await owner.GetAsync($"/api/budgets/{privateBudget.Id}/months/2026-10");
        var updatedPrivateMonth = (await updatedPrivateMonthResponse.Content.ReadFromJsonAsync<BudgetDto>())!;
        Assert.Equal(0m, updatedPrivateMonth.Groups.SelectMany(group => group.Categories).Single(category => category.Id == "groceries").Activity);
        var legacyMonth = await legacyClient.GetFromJsonAsync<BudgetDto>("/api/budget/2026-10");
        Assert.Equal(-25m, legacyMonth!.Groups.SelectMany(group => group.Categories).Single(category => category.Id == "groceries").Activity);
        Assert.Equal(HttpStatusCode.OK, (await legacyClient.GetAsync("/api/long-term-planning/2026")).StatusCode);
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
