using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using OpenBudget.Api;
using OpenBudget.Tests;
using static OpenBudget.IntegrationTests.McpTestKit;

namespace OpenBudget.IntegrationTests;

// UI-parity MCP tools: Wishlist, Plan structure ordering and transaction images, exercised through /mcp against PostgreSQL.
public sealed class McpParityToolsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Month = "2026-10";

    [Fact]
    public async Task Wishlist_tools_manage_buckets_and_items_with_versions_confirmation_and_entitlement()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "root", administrator: true);
        await GrantPlusAsync(app, "root");
        using var root = await LoginAsync(app, "root");
        var budgetId = (await CreateBudgetAsync(root, "Household")).Id;
        using var mcp = Mcp(app, (await CreateTokenAsync(root, "Admin")).Token);

        (await mcp.CallAsync("wishlist_set_enabled", new { budgetId, enabled = true })).AssertOk();
        var bucket = await mcp.CallAsync("wishlist_buckets_create", new { budgetId, name = "Garden" });
        bucket.AssertOk();
        var bucketId = bucket.Structured().GetProperty("buckets")[0].GetProperty("id").GetGuid();
        var second = (await mcp.CallAsync("wishlist_buckets_create", new { budgetId, name = "Tech" })).Structured();
        var secondId = second.GetProperty("buckets").EnumerateArray().Single(b => b.GetProperty("name").GetString() == "Tech").GetProperty("id").GetGuid();
        (await mcp.CallAsync("wishlist_buckets_rename", new { budgetId, bucketId, name = "Outdoor" })).AssertOk();
        (await mcp.CallAsync("wishlist_buckets_reorder", new { budgetId, bucketIds = new[] { secondId, bucketId } })).AssertOk();

        var mower = await mcp.CallAsync("wishlist_items_create", new { budgetId, bucketId, name = "Mower", cost = 220m, url = "https://example.test/mower" });
        mower.AssertOk();
        (await mcp.CallAsync("wishlist_items_create", new { budgetId, bucketId, name = "Hose", cost = 30m })).AssertOk();
        var items = Bucket(await mcp.CallAsync("wishlist_get", new { budgetId }), bucketId).GetProperty("items");
        var mowerId = items[0].GetProperty("id").GetGuid();
        var hoseId = items[1].GetProperty("id").GetGuid();
        (await mcp.CallAsync("wishlist_items_reorder", new { budgetId, bucketId, itemIds = new[] { hoseId, mowerId } })).AssertOk();
        (await mcp.CallAsync("wishlist_items_update", new { budgetId, itemId = mowerId, name = "Robot mower", cost = 900m })).AssertOk();

        // Removing needs confirmation; purchased/restored do not.
        Assert.Equal("confirmation_required", (await mcp.CallAsync("wishlist_items_set_status", new { budgetId, itemId = hoseId, status = "removed" })).ErrorCode());
        (await mcp.CallAsync("wishlist_items_set_status", new { budgetId, itemId = hoseId, status = "removed", confirm = true })).AssertOk();
        (await mcp.CallAsync("wishlist_items_set_status", new { budgetId, itemId = hoseId, status = "active" })).AssertOk();
        (await mcp.CallAsync("wishlist_items_set_status", new { budgetId, itemId = mowerId, status = "purchased" })).AssertOk();

        var overview = await mcp.CallAsync("wishlist_get", new { budgetId, month = Month });
        overview.AssertOk();
        var garden = Bucket(overview, bucketId);
        Assert.Equal("Outdoor", garden.GetProperty("name").GetString());
        Assert.Equal(1, garden.GetProperty("sortOrder").GetInt32());
        Assert.Equal("Hose", Assert.Single(garden.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal("Robot mower", Assert.Single(garden.GetProperty("purchased").EnumerateArray()).GetProperty("name").GetString());

        // Stale version: conflict, nothing written.
        var version = overview.Structured().GetProperty("version").GetInt64();
        var stale = await mcp.CallAsync("wishlist_buckets_create", new { budgetId, name = "Stale", version = version - 1 });
        Assert.Equal("conflict", stale.ErrorCode());

        // Delete is destructive and archives the Plan category.
        Assert.Equal("confirmation_required", (await mcp.CallAsync("wishlist_buckets_delete", new { budgetId, bucketId = secondId })).ErrorCode());
        (await mcp.CallAsync("wishlist_buckets_delete", new { budgetId, bucketId = secondId, confirm = true })).AssertOk();
        Assert.Equal("confirmation_required", (await mcp.CallAsync("wishlist_set_enabled", new { budgetId, enabled = false })).ErrorCode());
        Assert.Single((await mcp.CallAsync("wishlist_get", new { budgetId })).Structured().GetProperty("buckets").EnumerateArray());

        // Without the entitlement the endpoint policy denies the call.
        await CreateUserAsync(app, "free", administrator: true);
        using var free = await LoginAsync(app, "free");
        var freeBudget = (await CreateBudgetAsync(free, "Free")).Id;
        using var freeMcp = Mcp(app, (await CreateTokenAsync(free, "Admin")).Token);
        Assert.Equal("forbidden", (await freeMcp.CallAsync("wishlist_get", new { budgetId = freeBudget })).ErrorCode());
    }

    [Fact]
    public async Task Structure_tools_reorder_groups_and_move_categories_between_groups()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "root", administrator: true);
        using var root = await LoginAsync(app, "root");
        var budgetId = (await CreateBudgetAsync(root, "Household")).Id;
        using var mcp = Mcp(app, (await CreateTokenAsync(root, "Admin")).Token);

        var before = (await mcp.CallAsync("plan_get_month", new { budgetId, month = Month })).Structured().GetProperty("groups");
        var groups = before.EnumerateArray().OrderBy(g => g.GetProperty("sortOrder").GetInt32()).Select(g => g.GetProperty("id").GetString()!).ToList();
        Assert.True(groups.Count >= 2);
        var last = groups[^1];
        (await mcp.CallAsync("category_groups_move", new { budgetId, month = Month, groupId = last, position = 0 })).AssertOk();

        var source = before.EnumerateArray().First(g => g.GetProperty("categories").GetArrayLength() > 0 && g.GetProperty("id").GetString() != last);
        var categoryId = source.GetProperty("categories")[0].GetProperty("id").GetString()!;
        (await mcp.CallAsync("categories_move", new { budgetId, month = Month, categoryId, groupId = last, position = 0 })).AssertOk();

        var after = (await mcp.CallAsync("plan_get_month", new { budgetId, month = Month })).Structured().GetProperty("groups");
        var first = after.EnumerateArray().OrderBy(g => g.GetProperty("sortOrder").GetInt32()).First();
        Assert.Equal(last, first.GetProperty("id").GetString());
        Assert.Equal(categoryId, first.GetProperty("categories").EnumerateArray().OrderBy(c => c.GetProperty("sortOrder").GetInt32()).First().GetProperty("id").GetString());
        Assert.DoesNotContain(after.EnumerateArray().Single(g => g.GetProperty("id").GetString() == source.GetProperty("id").GetString()).GetProperty("categories").EnumerateArray(),
            c => c.GetProperty("id").GetString() == categoryId);
        // Later months follow the moved structure.
        var next = (await mcp.CallAsync("plan_get_month", new { budgetId, month = "2026-11" })).Structured().GetProperty("groups");
        Assert.Equal(last, next.EnumerateArray().OrderBy(g => g.GetProperty("sortOrder").GetInt32()).First().GetProperty("id").GetString());

        Assert.Equal("not_found", (await mcp.CallAsync("categories_move", new { budgetId, month = Month, categoryId = "nope", groupId = last, position = 0 })).ErrorCode());
    }

    [Fact]
    public async Task Image_tools_list_and_delete_receipts_with_confirmation()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "root", administrator: true);
        using var root = await LoginAsync(app, "root");
        var budgetId = (await CreateBudgetAsync(root, "Household")).Id;
        root.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={budgetId}");
        using var mcp = Mcp(app, (await CreateTokenAsync(root, "Admin")).Token);
        var account = (await mcp.CallAsync("accounts_create", new { budgetId, name = "Checking", accountType = "checking", date = "2026-10-01", openingBalance = 100m })).Structured().GetProperty("id").GetGuid();
        var transaction = (await mcp.CallAsync("transactions_create", new { budgetId, accountId = account, date = "2026-10-02", amount = -5m, payee = "Cafe", categoryId = "groceries" })).Structured().GetProperty("id").GetGuid();

        var form = new MultipartFormDataContent { { new StringContent(Guid.NewGuid().ToString()), "commandId" } };
        var file = new ByteArrayContent(TestImages.Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "files", "receipt.png");
        Assert.True((await root.PostAsync($"/api/transactions/{transaction}/images", form)).IsSuccessStatusCode);

        var listed = await mcp.CallAsync("transaction_images_list", new { budgetId, transactionId = transaction });
        listed.AssertOk();
        var imageId = Assert.Single(listed.Structured().GetProperty("images").EnumerateArray()).GetProperty("id").GetGuid();
        Assert.Equal("confirmation_required", (await mcp.CallAsync("transaction_images_delete", new { budgetId, imageId })).ErrorCode());
        (await mcp.CallAsync("transaction_images_delete", new { budgetId, imageId, confirm = true })).AssertOk();
        Assert.Empty((await mcp.CallAsync("transaction_images_list", new { budgetId, transactionId = transaction })).Structured().GetProperty("images").EnumerateArray());
    }

    private static System.Text.Json.JsonElement Bucket(System.Text.Json.JsonElement result, Guid bucketId)
    {
        result.AssertOk();
        return result.Structured().GetProperty("buckets").EnumerateArray().Single(b => b.GetProperty("id").GetGuid() == bucketId);
    }

    private static async Task GrantPlusAsync(TestApp app, string userName)
    {
        await using var db = app.CreateDb();
        var userId = await db.Users.Where(u => u.UserName == userName).Select(u => u.Id).SingleAsync();
        db.UserMemberships.Add(new UserMembershipEntity { UserId = userId, MembershipPlanId = "plus" });
        await db.SaveChangesAsync();
    }
}
