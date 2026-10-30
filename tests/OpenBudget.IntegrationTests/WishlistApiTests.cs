using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenBudget.Api;

namespace OpenBudget.IntegrationTests;

// The test clock is 21 October 2026 (UTC), so the Wishlist's current month is 2026-10.
public sealed class WishlistApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Wishlist_requires_the_entitlement_from_the_tier_or_a_user_override()
    {
        await using var app = await fixture.CreateAppAsync();
        var freeId = await CreateUserAsync(app, "free-user", "free@example.test");
        var plusId = await CreateUserAsync(app, "plus-user", "plus@example.test", "plus");
        await CreateUserAsync(app, "pro-user", "pro@example.test", "pro");
        using var free = await LoginAsync(app, "free-user");
        using var plus = await LoginAsync(app, "plus-user");
        using var pro = await LoginAsync(app, "pro-user");
        var freeBudget = await CreateBudgetAsync(free, "Free");
        var plusBudget = await CreateBudgetAsync(plus, "Plus");
        var proBudget = await CreateBudgetAsync(pro, "Pro");

        Assert.Equal(HttpStatusCode.Forbidden, (await free.GetAsync($"/api/budgets/{freeBudget.Id}/wishlist")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await free.PostAsJsonAsync($"/api/budgets/{freeBudget.Id}/wishlist/enable", new WishlistVersionRequest(0))).StatusCode);
        var plusOverview = await Read<WishlistOverviewDto>(await plus.GetAsync($"/api/budgets/{plusBudget.Id}/wishlist"));
        Assert.False(plusOverview.IsEnabled);
        Assert.Equal(0, plusOverview.Version);
        Assert.Equal(HttpStatusCode.OK, (await pro.GetAsync($"/api/budgets/{proBudget.Id}/wishlist")).StatusCode);

        await using (var db = app.CreateDb())
        {
            db.UserEntitlementOverrides.Add(new UserEntitlementOverrideEntity { UserId = freeId, EntitlementKey = WishlistRules.EntitlementKey, IsEnabled = true });
            db.UserEntitlementOverrides.Add(new UserEntitlementOverrideEntity { UserId = plusId, EntitlementKey = WishlistRules.EntitlementKey, IsEnabled = false });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await free.GetAsync($"/api/budgets/{freeBudget.Id}/wishlist")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await plus.GetAsync($"/api/budgets/{plusBudget.Id}/wishlist")).StatusCode);

        // Another user's budget is not reachable through the Wishlist routes.
        Assert.Equal(HttpStatusCode.NotFound, (await pro.GetAsync($"/api/budgets/{freeBudget.Id}/wishlist")).StatusCode);
    }

    [Fact]
    public async Task Administrators_see_the_wishlist_key_and_user_overrides()
    {
        await using var app = await fixture.CreateAppAsync();
        var userId = await CreateUserAsync(app, "member", "member@example.test");
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
            await roles.CreateAsync(new IdentityRole(AuthenticationSetup.AdministratorRole));
            var root = new OpenBudgetUser { UserName = "root", Email = "root@example.test", EmailConfirmed = true };
            Assert.True((await users.CreateAsync(root, "Strong-pass1!")).Succeeded);
            await users.AddToRoleAsync(root, AuthenticationSetup.AdministratorRole);
        }
        using var admin = await LoginAsync(app, "root");
        Assert.Contains("wishlist", (await admin.GetFromJsonAsync<string[]>("/api/admin/entitlement-keys"))!);
        Assert.Contains(await admin.GetFromJsonAsync<MembershipEntitlementDto[]>("/api/admin/membership-plans/plus/entitlements") ?? [],
            item => item.EntitlementKey == "wishlist" && item.IsEnabled);
        Assert.Contains(await admin.GetFromJsonAsync<MembershipEntitlementDto[]>("/api/admin/membership-plans/free/entitlements") ?? [],
            item => item.EntitlementKey == "wishlist" && !item.IsEnabled);
        Assert.Empty((await admin.GetFromJsonAsync<MembershipEntitlementDto[]>($"/api/admin/users/{userId}/entitlements"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/users/{userId}/entitlements/wishlist", new UserEntitlementOverrideRequest(true))).StatusCode);
        var overrides = (await admin.GetFromJsonAsync<MembershipEntitlementDto[]>($"/api/admin/users/{userId}/entitlements"))!;
        Assert.Equal(new MembershipEntitlementDto("wishlist", true), Assert.Single(overrides));
        using var member = await LoginAsync(app, "member");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/admin/entitlement-keys")).StatusCode);
    }

    [Fact]
    public async Task Enabling_creates_plan_categories_that_follow_bucket_renames_and_order_in_every_month()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "planner", "planner@example.test", "plus");
        using var client = await LoginAsync(app, "planner");
        var budget = await CreateBudgetAsync(client, "Wants");
        await Month(client, budget.Id, "2026-10");
        await Month(client, budget.Id, "2026-11");
        var path = $"/api/budgets/{budget.Id}/wishlist";

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{path}/buckets", new WishlistBucketRequest(0, "Games"))).StatusCode);
        var overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/enable", new WishlistVersionRequest(0)));
        Assert.True(overview.IsEnabled);
        Assert.Equal("2026-10", overview.Month);
        foreach (var month in new[] { "2026-10", "2026-11" })
        {
            var group = (await Month(client, budget.Id, month)).Groups.Single(g => g.Id == WishlistRules.GroupId);
            Assert.Equal("Wishlist", group.Name);
            Assert.False(group.IsArchived);
            Assert.Empty(group.Categories);
        }

        overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/buckets", new WishlistBucketRequest(overview.Version, "Electronics")));
        overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/buckets", new WishlistBucketRequest(overview.Version, " Games ")));
        Assert.Equal(["Electronics", "Games"], overview.Buckets.Select(b => b.Name));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{path}/buckets", new WishlistBucketRequest(overview.Version, "games"))).StatusCode);
        var electronics = overview.Buckets[0];
        var games = overview.Buckets[1];
        Assert.Equal($"wl-{electronics.Id:D}", electronics.CategoryId);

        overview = await Read<WishlistOverviewDto>(await client.PutAsJsonAsync($"{path}/buckets/{games.Id}", new WishlistBucketRequest(overview.Version, "Video games")));
        overview = await Read<WishlistOverviewDto>(await client.PutAsJsonAsync($"{path}/buckets/order", new WishlistBucketOrderRequest(overview.Version, [games.Id, electronics.Id])));
        Assert.Equal(["Video games", "Electronics"], overview.Buckets.Select(b => b.Name));
        foreach (var month in new[] { "2026-10", "2026-11" })
        {
            var categories = (await Month(client, budget.Id, month)).Groups.Single(g => g.Id == WishlistRules.GroupId).Categories;
            Assert.Equal(["Video games", "Electronics"], categories.Select(c => c.Name));
            Assert.Equal([games.CategoryId, electronics.CategoryId], categories.Select(c => c.Id));
        }

        // A later month inherits the structure through normal carry-forward initialization.
        var december = await Month(client, budget.Id, "2026-12");
        Assert.Equal(["Video games", "Electronics"], december.Groups.Single(g => g.Id == WishlistRules.GroupId).Categories.Select(c => c.Name));

        // Stale writes are rejected with the current overview and change nothing.
        var stale = await client.PutAsJsonAsync($"{path}/buckets/{games.Id}", new WishlistBucketRequest(overview.Version - 1, "Stale"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("Video games", await stale.Content.ReadAsStringAsync());
        Assert.Equal("Video games", (await Month(client, budget.Id, "2026-10")).Groups.Single(g => g.Id == WishlistRules.GroupId).Categories[0].Name);
    }

    [Fact]
    public async Task Plan_assignments_drive_progress_and_reordering_changes_the_next_item()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "saver", "saver@example.test", "pro");
        using var client = await LoginAsync(app, "saver");
        var budget = await CreateBudgetAsync(client, "Saver");
        var path = $"/api/budgets/{budget.Id}/wishlist";
        var overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/enable", new WishlistVersionRequest(0)));
        overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/buckets", new WishlistBucketRequest(overview.Version, "Games")));
        var bucket = overview.Buckets.Single();
        Assert.Null(bucket.Progress);
        overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/buckets/{bucket.Id}/items",
            new WishlistItemRequest(overview.Version, "Racing game", "https://shop.example/racing", 59.99m, "Deluxe edition")));
        overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/buckets/{bucket.Id}/items",
            new WishlistItemRequest(overview.Version, "Console", null, 450m, null)));
        bucket = overview.Buckets.Single();
        Assert.Equal(59.99m, bucket.MinCost);
        Assert.Equal(450m, bucket.MaxCost);
        Assert.Equal("Racing game", bucket.Progress!.NextItemName);
        Assert.Equal(59.99m, bucket.Progress.Remaining);
        Assert.Equal(0, overview.ReadyCount);

        // Assign exactly the cost in Plan: the Plan DTO and the overview both report the item as purchasable.
        var saved = await Assign(client, budget.Id, "2026-10", bucket.CategoryId, 59.99m);
        var planProgress = Category(saved, bucket.CategoryId).WishlistProgress!;
        Assert.True(planProgress.CanPurchase);
        Assert.Equal(0m, planProgress.Remaining);
        overview = await Read<WishlistOverviewDto>(await client.GetAsync(path));
        Assert.True(overview.Buckets.Single().Progress!.CanPurchase);
        Assert.Equal(1, overview.ReadyCount);

        // Moving the console to the top makes it the next item, so the bucket is no longer ready.
        var console = overview.Buckets.Single().Items.Single(i => i.Name == "Console");
        var racing = overview.Buckets.Single().Items.Single(i => i.Name == "Racing game");
        overview = await Read<WishlistOverviewDto>(await client.PutAsJsonAsync($"{path}/buckets/{bucket.Id}/items/order",
            new WishlistItemOrderRequest(overview.Version, [console.Id, racing.Id])));
        Assert.Equal("Console", overview.Buckets.Single().Progress!.NextItemName);
        Assert.Equal(390.01m, overview.Buckets.Single().Progress!.Remaining);
        Assert.Equal(0, overview.ReadyCount);
        Assert.Equal("Console", Category(await Month(client, budget.Id, "2026-10"), bucket.CategoryId).WishlistProgress!.NextItemName);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{path}/buckets/{bucket.Id}/items/order",
            new WishlistItemOrderRequest(overview.Version, [console.Id]))).StatusCode);

        // Purchasing the next item keeps it in history and advances to the following item.
        overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/items/{console.Id}/purchase", new WishlistVersionRequest(overview.Version)));
        var updated = overview.Buckets.Single();
        Assert.Equal("Racing game", updated.Progress!.NextItemName);
        Assert.True(updated.Progress.CanPurchase);
        Assert.Equal("Console", Assert.Single(updated.Purchased).Name);
        Assert.NotNull(updated.Purchased[0].PurchasedAt);
        overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/items/{racing.Id}/remove", new WishlistVersionRequest(overview.Version)));
        Assert.Null(overview.Buckets.Single().Progress);
        Assert.Equal("Racing game", Assert.Single(overview.Buckets.Single().Removed).Name);
        overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/items/{racing.Id}/restore", new WishlistVersionRequest(overview.Version)));
        Assert.Equal("Racing game", overview.Buckets.Single().Progress!.NextItemName);
    }

    [Fact]
    public async Task Disabling_archives_the_group_without_moving_money_and_re_enabling_restores_it()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "toggler", "toggler@example.test", "plus");
        using var client = await LoginAsync(app, "toggler");
        var budget = await CreateBudgetAsync(client, "Toggle");
        var path = $"/api/budgets/{budget.Id}/wishlist";
        var overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/enable", new WishlistVersionRequest(0)));
        overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/buckets", new WishlistBucketRequest(overview.Version, "Travel")));
        var categoryId = overview.Buckets.Single().CategoryId;
        await Assign(client, budget.Id, "2026-10", categoryId, 120m);
        await Month(client, budget.Id, "2026-11");
        var before = await Month(client, budget.Id, "2026-11");
        Assert.Equal(120m, Category(before, categoryId).Available);

        overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/disable", new WishlistVersionRequest(overview.Version)));
        Assert.False(overview.IsEnabled);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{path}/buckets", new WishlistBucketRequest(overview.Version, "Blocked"))).StatusCode);
        var disabled = await Month(client, budget.Id, "2026-11");
        var group = disabled.Groups.Single(g => g.Id == WishlistRules.GroupId);
        Assert.True(group.IsArchived);
        Assert.Equal(120m, Category(disabled, categoryId).Available);
        Assert.Null(Category(disabled, categoryId).WishlistProgress);
        Assert.Equal(before.ReadyToAssign, disabled.ReadyToAssign);

        overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/enable", new WishlistVersionRequest(overview.Version)));
        var restored = await Month(client, budget.Id, "2026-11");
        Assert.False(restored.Groups.Single(g => g.Id == WishlistRules.GroupId).IsArchived);
        Assert.Equal(120m, Category(restored, categoryId).Available);
        Assert.Equal(before.ReadyToAssign, restored.ReadyToAssign);
        Assert.Equal(120m, overview.Buckets.Single().Available);

        // A bucket holding money cannot be deleted; once the money is moved out, delete archives its category.
        var bucketId = overview.Buckets.Single().Id;
        var blocked = await client.PostAsJsonAsync($"{path}/buckets/{bucketId}/delete", new WishlistVersionRequest(overview.Version));
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("Move this bucket's money", await blocked.Content.ReadAsStringAsync());
        await Assign(client, budget.Id, "2026-10", categoryId, 0m);
        overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/buckets/{bucketId}/delete", new WishlistVersionRequest(overview.Version)));
        Assert.Empty(overview.Buckets);
        var afterDelete = await Month(client, budget.Id, "2026-10");
        Assert.True(Category(afterDelete, categoryId).IsArchived);
    }

    [Fact]
    public async Task Item_validation_rejects_bad_links_and_amounts_without_partial_writes()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "validator", "validator@example.test", "plus");
        using var client = await LoginAsync(app, "validator");
        var budget = await CreateBudgetAsync(client, "Validate");
        var path = $"/api/budgets/{budget.Id}/wishlist";
        var overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/enable", new WishlistVersionRequest(0)));
        overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/buckets", new WishlistBucketRequest(overview.Version, "Small")));
        var bucketId = overview.Buckets.Single().Id;
        foreach (var invalid in new[]
        {
            new WishlistItemRequest(overview.Version, "Bad link", "javascript:alert(1)", 10m, null),
            new WishlistItemRequest(overview.Version, "Bad link", "ftp://files.example/x", 10m, null),
            new WishlistItemRequest(overview.Version, "Bad cost", null, 10.001m, null),
            new WishlistItemRequest(overview.Version, "Zero", null, 0m, null),
            new WishlistItemRequest(overview.Version, "", null, 10m, null),
        })
        {
            var response = await client.PostAsJsonAsync($"{path}/buckets/{bucketId}/items", invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"{path}/buckets/{Guid.NewGuid()}/items",
            new WishlistItemRequest(overview.Version, "Missing", null, 10m, null))).StatusCode);
        var unchanged = await Read<WishlistOverviewDto>(await client.GetAsync(path));
        Assert.Equal(overview.Version, unchanged.Version);
        Assert.Empty(unchanged.Buckets.Single().Items);
        await using var db = app.CreateDb();
        Assert.Equal(0, await db.WishlistItems.CountAsync());
    }

    [Fact]
    public async Task Plan_saves_cannot_rename_archive_move_or_delete_wishlist_categories()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "guard", "guard@example.test", "plus");
        using var client = await LoginAsync(app, "guard");
        var budget = await CreateBudgetAsync(client, "Guard");
        var path = $"/api/budgets/{budget.Id}/wishlist";
        var overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/enable", new WishlistVersionRequest(0)));
        overview = await Read<WishlistOverviewDto>(await client.PostAsJsonAsync($"{path}/buckets", new WishlistBucketRequest(overview.Version, "Gadgets")));
        var categoryId = overview.Buckets.Single().CategoryId;
        var month = await Month(client, budget.Id, "2026-10");

        async Task AssertRejected(BudgetUpdateDto update)
        {
            var response = await client.PutAsJsonAsync($"/api/budgets/{budget.Id}/months/2026-10", update);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        await AssertRejected(ToUpdate(month, c => c.Id == categoryId ? c with { Name = "Renamed" } : c));
        await AssertRejected(ToUpdate(month, c => c.Id == categoryId ? c with { IsArchived = true } : c));
        await AssertRejected(ToUpdate(month, c => c, groups => groups.Select(g => g.Id == WishlistRules.GroupId ? g with { IsArchived = true } : g).ToList()));
        await AssertRejected(ToUpdate(month, c => c, groups => groups.Select(g => g.Id == WishlistRules.GroupId ? g with { Name = "Wants" } : g).ToList()));
        // Deleting the category from the payload, or moving it into another group.
        await AssertRejected(ToUpdate(month, c => c, groups => groups.Select(g => g.Id == WishlistRules.GroupId ? g with { Categories = [] } : g).ToList()));
        await AssertRejected(ToUpdate(month, c => c, groups =>
        {
            var wishlistCategory = groups.Single(g => g.Id == WishlistRules.GroupId).Categories.Single();
            return groups.Select(g => g.Id == WishlistRules.GroupId ? g with { Categories = [] }
                : g.Id == "home" ? g with { Categories = [.. g.Categories, wishlistCategory with { SortOrder = 99 }] } : g).ToList();
        }));
        await AssertRejected(ToUpdate(month, c => c, groups => groups.Select(g => g.Id == WishlistRules.GroupId
            ? g with { Categories = [.. g.Categories, new CategoryUpdateDto($"wl-{Guid.NewGuid():D}", "Sneaky", 0, 0, null, false, 5, false)] } : g).ToList()));

        var unchanged = await Month(client, budget.Id, "2026-10");
        Assert.Equal(month.Version, unchanged.Version);
        // Assigning money and moving the whole group remain allowed.
        var reordered = ToUpdate(unchanged, c => c.Id == categoryId ? c with { Assigned = 25m } : c,
            groups => groups.Select(g => g.Id == WishlistRules.GroupId ? g with { SortOrder = -1 } : g).OrderBy(g => g.SortOrder)
                .Select((g, index) => g with { SortOrder = index }).ToList());
        var saved = await Read<BudgetDto>(await client.PutAsJsonAsync($"/api/budgets/{budget.Id}/months/2026-10", reordered));
        Assert.Equal(WishlistRules.GroupId, saved.Groups[0].Id);
        Assert.Equal(25m, Category(saved, categoryId).Available);
    }

    [Fact]
    public async Task Wishlist_data_is_isolated_per_budget_and_shared_with_editors()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "alice", "alice@example.test", "plus");
        await CreateUserAsync(app, "bob", "bob@example.test", "plus");
        using var alice = await LoginAsync(app, "alice");
        using var bob = await LoginAsync(app, "bob");
        var first = await CreateBudgetAsync(alice, "First");
        var second = await CreateBudgetAsync(alice, "Second");
        var overview = await Read<WishlistOverviewDto>(await alice.PostAsJsonAsync($"/api/budgets/{first.Id}/wishlist/enable", new WishlistVersionRequest(0)));
        overview = await Read<WishlistOverviewDto>(await alice.PostAsJsonAsync($"/api/budgets/{first.Id}/wishlist/buckets", new WishlistBucketRequest(overview.Version, "Books")));
        var bucketId = overview.Buckets.Single().Id;

        var other = await Read<WishlistOverviewDto>(await alice.GetAsync($"/api/budgets/{second.Id}/wishlist"));
        Assert.False(other.IsEnabled);
        Assert.Empty(other.Buckets);
        Assert.DoesNotContain((await Month(alice, second.Id, "2026-10")).Groups, g => g.Id == WishlistRules.GroupId);
        // A bucket from another budget cannot be addressed through this budget's routes.
        await alice.PostAsJsonAsync($"/api/budgets/{second.Id}/wishlist/enable", new WishlistVersionRequest(0));
        Assert.Equal(HttpStatusCode.NotFound, (await alice.PostAsJsonAsync($"/api/budgets/{second.Id}/wishlist/buckets/{bucketId}/items",
            new WishlistItemRequest(1, "Novel", null, 12m, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/budgets/{first.Id}/wishlist")).StatusCode);

        await using (var db = app.CreateDb())
        {
            var bobId = await db.Users.Where(u => u.UserName == "bob").Select(u => u.Id).SingleAsync();
            db.BudgetMembers.Add(new BudgetMemberEntity { BudgetId = first.Id, UserId = bobId, Role = "editor", InvitedByUserId = bobId });
            await db.SaveChangesAsync();
        }
        var shared = await Read<WishlistOverviewDto>(await bob.GetAsync($"/api/budgets/{first.Id}/wishlist"));
        Assert.Equal("Books", shared.Buckets.Single().Name);
        Assert.Equal(HttpStatusCode.OK, (await bob.PostAsJsonAsync($"/api/budgets/{first.Id}/wishlist/buckets/{bucketId}/items",
            new WishlistItemRequest(shared.Version, "Atlas", null, 30m, null))).StatusCode);

        // Deleting a budget removes its Wishlist rows with it.
        var current = (await alice.GetFromJsonAsync<OwnedBudgetListDto>("/api/budgets"))!.Budgets.Single(b => b.Id == first.Id);
        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/budgets/{first.Id}") { Content = JsonContent.Create(new DeleteOwnedBudgetRequest(current.Version)) };
        Assert.Equal(HttpStatusCode.NoContent, (await alice.SendAsync(delete)).StatusCode);
        await using var db2 = app.CreateDb();
        Assert.False(await db2.WishlistBuckets.AnyAsync(b => b.BudgetId == first.Id));
        Assert.False(await db2.WishlistItems.AnyAsync(i => i.BudgetId == first.Id));
        Assert.False(await db2.WishlistSettings.AnyAsync(s => s.BudgetId == first.Id));
        Assert.True(await db2.WishlistSettings.AnyAsync(s => s.BudgetId == second.Id));
    }

    private sealed record OwnedBudgetListDto(List<OwnedBudgetDto> Budgets);

    private static CategoryDto Category(BudgetDto budget, string id) => budget.Groups.SelectMany(g => g.Categories).Single(c => c.Id == id);

    private static async Task<BudgetDto> Month(HttpClient client, string budgetId, string month) =>
        await Read<BudgetDto>(await client.GetAsync($"/api/budgets/{budgetId}/months/{month}"));

    private static async Task<BudgetDto> Assign(HttpClient client, string budgetId, string month, string categoryId, decimal amount)
    {
        var current = await Month(client, budgetId, month);
        var update = ToUpdate(current, category => category.Id == categoryId ? category with { Assigned = amount } : category);
        return await Read<BudgetDto>(await client.PutAsJsonAsync($"/api/budgets/{budgetId}/months/{month}", update));
    }

    private static BudgetUpdateDto ToUpdate(BudgetDto budget, Func<CategoryUpdateDto, CategoryUpdateDto> change,
        Func<List<CategoryGroupUpdateDto>, List<CategoryGroupUpdateDto>>? groups = null)
    {
        var mapped = budget.Groups.Select(g => new CategoryGroupUpdateDto(g.Id, g.Name, g.SortOrder, g.IsArchived,
            g.Categories.Select(c => change(new CategoryUpdateDto(c.Id, c.Name, c.Assigned, c.Target, c.Icon, c.Snoozed, c.SortOrder, c.IsArchived))).ToList())).ToList();
        return new BudgetUpdateDto(budget.Month, budget.Note, budget.Version, budget.FundsAdded, groups?.Invoke(mapped) ?? mapped);
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
