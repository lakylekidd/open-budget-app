using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OpenBudget.Api;

namespace OpenBudget.IntegrationTests;

public sealed class AccountOrderApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateOnly Date = new(2026, 10, 21);

    [Fact]
    public async Task New_accounts_append_last_and_a_reorder_persists_across_restart_and_replays_exactly_once()
    {
        await using var app = await fixture.CreateAppAsync();
        var client = app.CreateClient();
        var checking = await AddAccount(client, "Checking");
        var savings = await AddAccount(client, "Savings");
        var card = await AddAccount(client, "Card", "creditCard");
        Assert.Equal([checking, savings, card], await Order(client));

        var version = (await Accounts(client)).Version;
        var request = new ReorderAccountsRequest(Guid.NewGuid(), version, [card, savings, checking]);
        var first = await Put<MutationDto>(client, "/api/accounts/order", request);
        Assert.Equal(version + 1, first.Version);
        var replay = await Put<MutationDto>(client, "/api/accounts/order", request);
        Assert.Equal(first, replay);
        Assert.Equal(first.Version, (await Accounts(client)).Version);
        await using (var db = app.CreateDb())
            Assert.Single(await db.LedgerCommands.Where(c => c.Kind == "account.order").ToListAsync());

        var cash = await AddAccount(client, "Wallet", "cash");
        await using var restarted = new TestApp(app.ConnectionString);
        Assert.Equal([card, savings, checking, cash], await Order(restarted.CreateClient()));
    }

    [Fact]
    public async Task Invalid_order_stale_version_and_reused_command_id_change_nothing()
    {
        await using var app = await fixture.CreateAppAsync();
        var client = app.CreateClient();
        var a = await AddAccount(client, "A");
        var b = await AddAccount(client, "B");
        var c = await AddAccount(client, "C");
        var version = (await Accounts(client)).Version;
        List<Guid>?[] invalid = [null, [], [a, b], [a, b, b], [a, b, c, c], [a, b, Guid.NewGuid()], [a, b, c, Guid.NewGuid()]];
        foreach (var ids in invalid)
        {
            var response = await client.PutAsJsonAsync("/api/accounts/order", new ReorderAccountsRequest(Guid.NewGuid(), version, ids));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(AccountOrderRules.InvalidOrder, await response.Content.ReadAsStringAsync());
        }
        Assert.Equal([a, b, c], await Order(client));
        Assert.Equal(version, (await Accounts(client)).Version);

        var stale = await client.PutAsJsonAsync("/api/accounts/order", new ReorderAccountsRequest(Guid.NewGuid(), version - 1, [c, b, a]));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal([a, b, c], await Order(client));

        var commandId = Guid.NewGuid();
        await Put<MutationDto>(client, "/api/accounts/order", new ReorderAccountsRequest(commandId, version, [b, a, c]));
        var reused = await client.PutAsJsonAsync("/api/accounts/order", new ReorderAccountsRequest(commandId, version + 1, [c, b, a]));
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal([b, a, c], await Order(client));
        Assert.Equal(version + 1, (await Accounts(client)).Version);
    }

    [Fact]
    public async Task Reorder_is_scoped_to_the_active_budget_and_rejects_another_budgets_accounts()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "orderalice", "orderalice@example.test");
        await CreateUserAsync(app, "orderbob", "orderbob@example.test");
        using var alice = await LoginAsync(app, "orderalice");
        using var bob = await LoginAsync(app, "orderbob");
        var aliceBudget = await CreateBudgetAsync(alice, "Alice order");
        var bobBudget = await CreateBudgetAsync(bob, "Bob order");
        alice.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={aliceBudget.Id}");
        bob.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={bobBudget.Id}");
        var a1 = await AddAccount(alice, "Alice 1");
        var a2 = await AddAccount(alice, "Alice 2");
        var b1 = await AddAccount(bob, "Bob 1");

        var foreign = await alice.PutAsJsonAsync("/api/accounts/order", new ReorderAccountsRequest(Guid.NewGuid(), (await Accounts(alice)).Version, [a2, a1, b1]));
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        var swapped = await alice.PutAsJsonAsync("/api/accounts/order", new ReorderAccountsRequest(Guid.NewGuid(), (await Accounts(alice)).Version, [b1, a1]));
        Assert.Equal(HttpStatusCode.BadRequest, swapped.StatusCode);
        Assert.Equal([a1, a2], await Order(alice));

        await Put<MutationDto>(alice, "/api/accounts/order", new ReorderAccountsRequest(Guid.NewGuid(), (await Accounts(alice)).Version, [a2, a1]));
        Assert.Equal([a2, a1], await Order(alice));
        Assert.Equal([b1], await Order(bob));
    }

    [Fact]
    public async Task Upgrade_backfills_each_budgets_order_from_the_previous_display_order()
    {
        await using var app = await fixture.CreateAppAsync();
        var zeta = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var alpha = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var beta = Guid.Parse("00000000-0000-0000-0000-000000000003");
        await using (var db = app.CreateDb())
        {
            await db.GetService<IMigrator>().MigrateAsync("20261031152909_UserPreferences");
            await db.Database.ExecuteSqlRawAsync($"""
                INSERT INTO "Accounts" ("Id", "BudgetId", "Name", "IsClosed", "AccountType", "IsFavorite", "Notes") VALUES
                ('{zeta}', 'default', 'Zeta', false, 'checking', false, ''),
                ('{alpha}', 'default', 'Alpha', true, 'savings', false, ''),
                ('{beta}', 'default', 'Beta', false, 'creditCard', false, '');
                """);
            await db.Database.MigrateAsync();
            var rows = await db.Accounts.IgnoreQueryFilters().OrderBy(x => x.SortOrder).Select(x => new { x.Id, x.SortOrder }).ToListAsync();
            Assert.Equal([(beta, 0), (zeta, 1), (alpha, 2)], rows.Select(x => (x.Id, x.SortOrder)));
        }
        var client = app.CreateClient();
        Assert.Equal([beta, zeta, alpha], await Order(client));
        var added = await AddAccount(client, "Gamma");
        Assert.Equal([beta, zeta, alpha, added], await Order(client));
    }

    private static async Task<List<Guid>> Order(HttpClient client) => (await Accounts(client)).Accounts.Select(a => a.Id).ToList();
    private static async Task<AccountsDto> Accounts(HttpClient client) => (await client.GetFromJsonAsync<AccountsDto>("/api/accounts"))!;
    private static async Task<Guid> AddAccount(HttpClient client, string name, string accountType = "checking") =>
        (await Read<MutationDto>(await client.PostAsJsonAsync("/api/accounts",
            new CreateAccountRequest(Guid.NewGuid(), (await Accounts(client)).Version, name, Date, 0, accountType)))).Id;
    private static async Task<T> Put<T>(HttpClient client, string path, object request) => await Read<T>(await client.PutAsJsonAsync(path, request));
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
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
