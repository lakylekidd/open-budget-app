using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenBudget.Api;

namespace OpenBudget.IntegrationTests;

// "Record purchase": a ledger transaction that also marks a Wishlist item purchased. Test clock: 21 October 2026.
public sealed class WishlistPurchaseApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    [Fact]
    public async Task Recording_a_purchase_creates_the_transaction_marks_the_item_and_advances_the_bucket()
    {
        await using var app = await fixture.CreateAppAsync();
        var (client, budget) = await SetUpAsync(app, "buyer");
        var (overview, bucket) = await BucketAsync(client, budget, "Games", ("Racing game", 59.99m), ("Console", 450m));
        await Assign(client, budget, "2026-10", bucket.CategoryId, 100m);
        var account = await CheckingAsync(client);
        var racing = bucket.Items[0];

        // The actual price differs from the listed cost.
        var request = Purchase(await LedgerVersion(client), account, bucket.CategoryId, -54.50m, racing.Name, "Wishlist: Racing game");
        var created = await Read<MutationDto>(await client.PostAsJsonAsync(PurchasePath(budget, racing.Id), request));

        var register = await client.GetFromJsonAsync<RegisterDto>($"/api/transactions?accountId={account}");
        var entry = Assert.Single(register!.Items, t => t.Id == created.Id);
        Assert.Equal(-54.50m, entry.Amount);
        Assert.Equal(bucket.CategoryId, entry.CategoryId);
        Assert.Equal("Racing game", entry.Payee);
        Assert.Equal("Wishlist: Racing game", entry.Memo);

        var after = await Overview(client, budget);
        Assert.True(after.Version > overview.Version);
        var updated = after.Buckets.Single();
        Assert.Equal(45.50m, updated.Available);
        Assert.Equal("Console", updated.Progress!.NextItemName);
        Assert.Equal(0, Assert.Single(updated.Items).SortOrder);
        var purchased = Assert.Single(updated.Purchased);
        Assert.Equal(racing.Id, purchased.Id);
        Assert.NotNull(purchased.PurchasedAt);
        Assert.Equal(new WishlistPurchaseDto(created.Id, account, Today, 54.50m), purchased.Purchase);

        // One command receipt covers both writes; editing the amount keeps the item purchased and linked.
        await using (var db = app.CreateDb())
        {
            var receipt = await db.LedgerCommands.IgnoreQueryFilters().SingleAsync(c => c.Id == request.CommandId);
            Assert.Equal($"wishlist.purchase/{racing.Id}", receipt.Kind);
            Assert.Equal(created.Id, (await db.WishlistItems.SingleAsync(x => x.Id == racing.Id)).PurchaseTransactionId);
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/transactions/{created.Id}",
            request with { CommandId = Guid.NewGuid(), Version = created.Version, Amount = -60m })).StatusCode);
        var edited = (await Overview(client, budget)).Buckets.Single();
        Assert.Equal(60m, edited.Purchased.Single().Purchase!.Amount);
        Assert.Equal(40m, edited.Available);
    }

    [Fact]
    public async Task Replaying_the_command_does_not_purchase_twice()
    {
        await using var app = await fixture.CreateAppAsync();
        var (client, budget) = await SetUpAsync(app, "replayer");
        var (_, bucket) = await BucketAsync(client, budget, "Books", ("Atlas", 30m), ("Novel", 12m));
        var account = await CheckingAsync(client);
        var request = Purchase(await LedgerVersion(client), account, bucket.CategoryId, -30m, "Atlas");

        var first = await Read<MutationDto>(await client.PostAsJsonAsync(PurchasePath(budget, bucket.Items[0].Id), request));
        var replay = await Read<MutationDto>(await client.PostAsJsonAsync(PurchasePath(budget, bucket.Items[0].Id), request));
        Assert.Equal(first, replay);
        // The same command ID for a different item is a different request.
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(PurchasePath(budget, bucket.Items[1].Id), request)).StatusCode);

        await using var db = app.CreateDb();
        Assert.Equal(1, await db.Transactions.IgnoreQueryFilters().CountAsync(t => t.BudgetId == budget && t.Amount == -30m));
        var items = await db.WishlistItems.Where(x => x.BudgetId == budget).OrderBy(x => x.Name).ToListAsync();
        Assert.Equal([WishlistRules.PurchasedStatus, WishlistRules.ActiveStatus], items.Select(x => x.Status));
        Assert.Equal(first.Version, await db.LedgerStates.IgnoreQueryFilters().Where(x => x.BudgetId == budget).Select(x => x.Version).SingleAsync());
    }

    [Fact]
    public async Task Invalid_purchases_are_rejected_without_writes()
    {
        await using var app = await fixture.CreateAppAsync();
        var (client, budget) = await SetUpAsync(app, "strict");
        var (overview, bucket) = await BucketAsync(client, budget, "Gadgets", ("Phone", 300m), ("Watch", 200m));
        var (_, other) = await BucketAsync(client, budget, "Travel", ("Flight", 150m));
        var account = await CheckingAsync(client);
        var phone = bucket.Items[0];
        var removed = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"/api/budgets/{budget}/wishlist/items/{bucket.Items[1].Id}/remove",
            new WishlistVersionRequest((await Overview(client, budget)).Version)));
        var version = await LedgerVersion(client);
        var before = await Snapshot(app, budget);

        async Task Rejected(Guid itemId, TransactionRequest request, HttpStatusCode status, string message)
        {
            var response = await client.PostAsJsonAsync(PurchasePath(budget, itemId), request);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(status == response.StatusCode, $"{(int)response.StatusCode}: {body}");
            Assert.Contains(message, body);
        }
        var valid = Purchase(version, account, bucket.CategoryId, -300m, "Phone");
        await Rejected(bucket.Items[1].Id, valid, HttpStatusCode.BadRequest, "already purchased or removed");
        await Rejected(phone.Id, valid with { Amount = 300m }, HttpStatusCode.BadRequest, "must be an outflow");
        await Rejected(phone.Id, valid with { CategoryId = "groceries" }, HttpStatusCode.BadRequest, "bucket's category");
        await Rejected(phone.Id, valid with { CategoryId = other.CategoryId }, HttpStatusCode.BadRequest, "bucket's category");
        await Rejected(phone.Id, valid with { Repeat = "monthly" }, HttpStatusCode.BadRequest, "cannot repeat");
        await Rejected(phone.Id, valid with { CategoryId = null, Splits = [new SplitDto(bucket.CategoryId, -300m)] }, HttpStatusCode.BadRequest, "cannot be split");
        await Rejected(phone.Id, valid with { AccountId = Guid.NewGuid() }, HttpStatusCode.NotFound, "Account not found");
        await Rejected(Guid.NewGuid(), valid, HttpStatusCode.NotFound, "not found");
        await Rejected(phone.Id, valid with { Version = version - 1 }, HttpStatusCode.Conflict, "ledger changed");
        Assert.Equal(before, await Snapshot(app, budget));

        // Disabled Wishlist: the bucket category is archived and the purchase is refused.
        await client.PostAsJsonAsync($"/api/budgets/{budget}/wishlist/disable", new WishlistVersionRequest((await Overview(client, budget)).Version));
        await Rejected(phone.Id, valid, HttpStatusCode.BadRequest, "Enable Wishlist");
        Assert.Equal(before.Transactions, (await Snapshot(app, budget)).Transactions);
        Assert.Equal(WishlistRules.ActiveStatus, (await Snapshot(app, budget)).PhoneStatus);
        Assert.NotNull(overview);
        Assert.NotNull(removed);
    }

    [Fact]
    public async Task Users_without_the_entitlement_and_other_budgets_cannot_record_purchases()
    {
        await using var app = await fixture.CreateAppAsync();
        var (owner, budget) = await SetUpAsync(app, "owner");
        var (_, bucket) = await BucketAsync(owner, budget, "Music", ("Guitar", 400m));
        var account = await CheckingAsync(owner);
        var request = Purchase(await LedgerVersion(owner), account, bucket.CategoryId, -400m, "Guitar");

        // A Free user is denied by the entitlement even on their own budget.
        var freeId = await CreateUserAsync(app, "freebie", "freebie@example.test");
        using var free = await LoginAsync(app, "freebie");
        var freeBudget = await CreateBudgetAsync(free, "Free");
        Assert.Equal(HttpStatusCode.Forbidden, (await free.PostAsJsonAsync(PurchasePath(freeBudget.Id, bucket.Items[0].Id), request)).StatusCode);

        // An entitled user cannot reach another user's budget, nor address its item through their own budget.
        var (intruder, intruderBudget) = await SetUpAsync(app, "intruder");
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostAsJsonAsync(PurchasePath(budget, bucket.Items[0].Id), request)).StatusCode);
        await intruder.PostAsJsonAsync($"/api/budgets/{intruderBudget}/wishlist/enable", new WishlistVersionRequest(0));
        var intruderAccount = await CheckingAsync(intruder);
        var cross = await intruder.PostAsJsonAsync(PurchasePath(intruderBudget, bucket.Items[0].Id),
            Purchase(await LedgerVersion(intruder), intruderAccount, bucket.CategoryId, -400m, "Guitar"));
        Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);

        await using var db = app.CreateDb();
        Assert.Equal(WishlistRules.ActiveStatus, (await db.WishlistItems.SingleAsync(x => x.Id == bucket.Items[0].Id)).Status);
        Assert.False(await db.Transactions.IgnoreQueryFilters().AnyAsync(t => t.Amount == -400m));
        Assert.NotNull(freeId);
    }

    [Fact]
    public async Task Deleting_the_purchase_transaction_returns_the_item_to_its_previous_position()
    {
        await using var app = await fixture.CreateAppAsync();
        var (client, budget) = await SetUpAsync(app, "undoer");
        var (_, bucket) = await BucketAsync(client, budget, "Home", ("Lamp", 40m), ("Sofa", 900m), ("Rug", 120m));
        await Assign(client, budget, "2026-10", bucket.CategoryId, 50m);
        var account = await CheckingAsync(client);
        var sofa = bucket.Items[1];
        var created = await Read<MutationDto>(await client.PostAsJsonAsync(PurchasePath(budget, sofa.Id),
            Purchase(await LedgerVersion(client), account, bucket.CategoryId, -45m, "Sofa")));
        var purchased = (await Overview(client, budget)).Buckets.Single();
        Assert.Equal(["Lamp", "Rug"], purchased.Items.Select(i => i.Name));
        Assert.Equal(5m, purchased.Available);
        var versionBefore = (await Overview(client, budget)).Version;

        var deleted = await Read<MutationDto>(await client.PostAsJsonAsync($"/api/transactions/{created.Id}/delete",
            new DeleteTransactionRequest(Guid.NewGuid(), created.Version)));

        var restored = await Overview(client, budget);
        Assert.True(restored.Version > versionBefore);
        var restoredBucket = restored.Buckets.Single();
        Assert.Equal(["Lamp", "Sofa", "Rug"], restoredBucket.Items.Select(i => i.Name));
        Assert.Equal([0, 1, 2], restoredBucket.Items.Select(i => i.SortOrder));
        Assert.Empty(restoredBucket.Purchased);
        Assert.Equal(50m, restoredBucket.Available);
        Assert.Null(restoredBucket.Items[1].Purchase);

        // Restoring an item manually from history keeps its transaction; only the link is cleared.
        var again = await Read<MutationDto>(await client.PostAsJsonAsync(PurchasePath(budget, sofa.Id),
            Purchase(deleted.Version, account, bucket.CategoryId, -45m, "Sofa")));
        var overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"/api/budgets/{budget}/wishlist/items/{sofa.Id}/restore",
            new WishlistVersionRequest((await Overview(client, budget)).Version)));
        Assert.Equal(["Lamp", "Rug", "Sofa"], overview.Buckets.Single().Items.Select(i => i.Name));
        Assert.Equal(5m, overview.Buckets.Single().Available);
        await using var db = app.CreateDb();
        Assert.False((await db.Transactions.IgnoreQueryFilters().SingleAsync(t => t.Id == again.Id)).IsDeleted);
        Assert.Null((await db.WishlistItems.SingleAsync(x => x.Id == sofa.Id)).PurchaseTransactionId);
    }

    [Fact]
    public async Task Selection_delete_also_restores_purchased_items()
    {
        await using var app = await fixture.CreateAppAsync();
        var (client, budget) = await SetUpAsync(app, "selector");
        var (_, bucket) = await BucketAsync(client, budget, "Toys", ("Kite", 25m), ("Drone", 250m));
        var account = await CheckingAsync(client);
        var created = await Read<MutationDto>(await client.PostAsJsonAsync(PurchasePath(budget, bucket.Items[0].Id),
            Purchase(await LedgerVersion(client), account, bucket.CategoryId, -25m, "Kite")));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/transactions/selection",
            new TransactionSelectionRequest(Guid.NewGuid(), created.Version, [created.Id], "delete"))).StatusCode);
        Assert.Equal(["Kite", "Drone"], (await Overview(client, budget)).Buckets.Single().Items.Select(i => i.Name));
    }

    [Fact]
    public async Task The_mcp_create_tool_records_a_wishlist_purchase()
    {
        await using var app = await fixture.CreateAppAsync();
        // transactions_create is an Admin-token tool, so the creator is an administrator with a Plus membership.
        await McpTestKit.CreateUserAsync(app, "agentowner", administrator: true);
        await using (var db = app.CreateDb())
        {
            var userId = await db.Users.Where(u => u.UserName == "agentowner").Select(u => u.Id).SingleAsync();
            db.UserMemberships.Add(new UserMembershipEntity { UserId = userId, MembershipPlanId = "plus" });
            await db.SaveChangesAsync();
        }
        using var client = await LoginAsync(app, "agentowner");
        var budget = (await CreateBudgetAsync(client, "Agent budget")).Id;
        client.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={budget}");
        var (_, bucket) = await BucketAsync(client, budget, "Garden", ("Mower", 220m));
        var account = await CheckingAsync(client);
        using var mcp = McpTestKit.Mcp(app, (await McpTestKit.CreateTokenAsync(client, "Admin")).Token);

        var wrong = await mcp.CallAsync("transactions_create", new { budgetId = budget, accountId = account, date = "2026-10-21", amount = -220m,
            categoryId = "groceries", wishlistItemId = bucket.Items[0].Id });
        Assert.True(wrong.IsError());
        var created = await mcp.CallAsync("transactions_create", new { budgetId = budget, accountId = account, date = "2026-10-21", amount = -215m,
            payee = "Mower", categoryId = bucket.CategoryId, wishlistItemId = bucket.Items[0].Id });
        created.AssertOk();
        var purchased = Assert.Single((await Overview(client, budget)).Buckets.Single().Purchased);
        Assert.Equal(Guid.Parse(created.Structured().GetProperty("id").GetString()!), purchased.Purchase!.TransactionId);
    }

    private sealed record WriteSnapshot(int Transactions, int Commands, long LedgerVersion, string PhoneStatus);

    private static async Task<WriteSnapshot> Snapshot(TestApp app, string budget)
    {
        await using var db = app.CreateDb();
        return new WriteSnapshot(
            await db.Transactions.IgnoreQueryFilters().CountAsync(t => t.BudgetId == budget),
            await db.LedgerCommands.IgnoreQueryFilters().CountAsync(c => c.BudgetId == budget),
            await db.LedgerStates.IgnoreQueryFilters().Where(x => x.BudgetId == budget).Select(x => x.Version).SingleAsync(),
            await db.WishlistItems.Where(x => x.BudgetId == budget && x.Name == "Phone").Select(x => x.Status).SingleAsync());
    }

    private static string PurchasePath(string budget, Guid itemId) => $"/api/budgets/{budget}/wishlist/items/{itemId}/purchase-transaction";

    private static TransactionRequest Purchase(long version, Guid account, string categoryId, decimal amount, string payee, string? memo = null) =>
        new(Guid.NewGuid(), version, account, Today, amount, payee, memo, categoryId, "uncleared", null);

    private static async Task<(HttpClient Client, string Budget)> SetUpAsync(TestApp app, string username)
    {
        await CreateUserAsync(app, username, $"{username}@example.test", "plus");
        var client = await LoginAsync(app, username);
        var budget = await CreateBudgetAsync(client, $"{username} budget");
        client.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={budget.Id}");
        return (client, budget.Id);
    }

    private static async Task<(WishlistOverviewDto Overview, WishlistBucketDto Bucket)> BucketAsync(HttpClient client, string budget, string name,
        params (string Name, decimal Cost)[] items)
    {
        var path = $"/api/budgets/{budget}/wishlist";
        var overview = await Overview(client, budget);
        if (!overview.IsEnabled) overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/enable", new WishlistVersionRequest(overview.Version)));
        overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/buckets", new WishlistBucketRequest(overview.Version, name)));
        var bucketId = overview.Buckets.Single(b => b.Name == name).Id;
        foreach (var item in items)
            overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/buckets/{bucketId}/items",
                new WishlistItemRequest(overview.Version, item.Name, null, item.Cost, null)));
        return (overview, overview.Buckets.Single(b => b.Id == bucketId));
    }

    private static async Task<Guid> CheckingAsync(HttpClient client) =>
        (await Read<MutationDto>(await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await LedgerVersion(client),
            "Checking", new DateOnly(2026, 10, 1), 2000m)))).Id;

    private static async Task<long> LedgerVersion(HttpClient client) => (await client.GetFromJsonAsync<AccountsDto>("/api/accounts"))!.Version;

    private static async Task<WishlistOverviewDto> Overview(HttpClient client, string budget) =>
        await Read<WishlistOverviewDto>(await client.GetAsync($"/api/budgets/{budget}/wishlist"));

    private static async Task Assign(HttpClient client, string budgetId, string month, string categoryId, decimal amount)
    {
        var current = await Read<BudgetDto>(await client.GetAsync($"/api/budgets/{budgetId}/months/{month}"));
        var groups = current.Groups.Select(g => new CategoryGroupUpdateDto(g.Id, g.Name, g.SortOrder, g.IsArchived,
            g.Categories.Select(c => new CategoryUpdateDto(c.Id, c.Name, c.Id == categoryId ? amount : c.Assigned, c.Target, c.Icon, c.Snoozed, c.SortOrder, c.IsArchived)).ToList())).ToList();
        await Read<BudgetDto>(await client.PutAsJsonAsync($"/api/budgets/{budgetId}/months/{month}",
            new BudgetUpdateDto(current.Month, current.Note, current.Version, current.FundsAdded, groups)));
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<string> CreateUserAsync(TestApp app, string username, string email, string? membership = null)
    {
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
        var user = new OpenBudgetUser { UserName = username, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, "Strong-pass1!");
        Assert.True(result.Succeeded, string.Join(",", result.Errors.Select(x => x.Code)));
        if (membership is not null)
        {
            var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
            db.UserMemberships.Add(new UserMembershipEntity { UserId = user.Id, MembershipPlanId = membership });
            await db.SaveChangesAsync();
        }
        return user.Id;
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
