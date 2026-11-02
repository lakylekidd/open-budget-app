using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenBudget.Api;
using OpenBudget.Api.Realtime;
using static OpenBudget.IntegrationTests.McpTestKit;

namespace OpenBudget.IntegrationTests;

// Budget-wide category definitions with per-month state, and deleting a category by reassigning its history.
public sealed class GlobalCategoriesApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string July = "2026-07", August = "2026-08", September = "2026-09", October = "2026-10", December = "2026-12";

    private sealed class Session(HttpClient client, string budgetId)
    {
        public HttpClient Client { get; } = client;
        public string BudgetId { get; } = budgetId;
        public string MonthPath(string month) => $"/api/budgets/{BudgetId}/months/{month}";
        public string CategoryPath(string categoryId) => $"/api/budgets/{BudgetId}/categories/{categoryId}";
        public Task<BudgetDto> Month(string month) => Read<BudgetDto>(Client.GetAsync(MonthPath(month)));
        public async Task<BudgetDto> Save(BudgetUpdateDto update) => await Read<BudgetDto>(Client.PutAsJsonAsync(MonthPath(update.Month), update));
        public async Task<BudgetDto> Edit(string month, Func<BudgetUpdateDto, BudgetUpdateDto> change) => await Save(change(Editable(await Month(month))));
        public async Task<BudgetDto> Assign(string month, string categoryId, decimal amount) =>
            await Edit(month, e => WithCategory(e, categoryId, c => c with { Assigned = amount }));
        public async Task<long> LedgerVersion() => (await Read<AccountsDto>(Client.GetAsync("/api/accounts"))).Version;
        public async Task<Guid> Account(decimal opening = 1000) =>
            (await Read<MutationDto>(Client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await LedgerVersion(), "Checking", new DateOnly(2026, 7, 1), opening)))).Id;
        public async Task<Guid> Spend(Guid account, DateOnly date, decimal amount, string? categoryId, List<SplitDto>? splits = null) =>
            (await Read<MutationDto>(Client.PostAsJsonAsync("/api/transactions",
                new TransactionRequest(Guid.NewGuid(), await LedgerVersion(), account, date, amount, "Synthetic Store", "test", categoryId, "uncleared", splits)))).Id;
        public Task<CategoryDeletionPreviewDto> Preview(string categoryId) => Read<CategoryDeletionPreviewDto>(Client.GetAsync(CategoryPath(categoryId) + "/deletion"));
        public async Task<HttpResponseMessage> Delete(string categoryId, string? target, long? version = null, Guid? commandId = null) =>
            await Client.PostAsJsonAsync(CategoryPath(categoryId) + "/delete", new DeleteCategoryRequest(commandId ?? Guid.NewGuid(), version ?? await LedgerVersion(), target));
    }

    private static async Task<T> Read<T>(Task<HttpResponseMessage> call)
    {
        using var response = await call;
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static BudgetUpdateDto Editable(BudgetDto b) => new(b.Month, b.Note, b.Version, b.FundsAdded, b.Groups.Select(g => new CategoryGroupUpdateDto(g.Id, g.Name, g.SortOrder, g.IsArchived,
        g.Categories.Select(c => new CategoryUpdateDto(c.Id, c.Name, c.Assigned, c.Target, c.Icon, c.Snoozed, c.SortOrder, c.IsArchived)).ToList())).ToList());

    private static BudgetUpdateDto WithCategory(BudgetUpdateDto e, string id, Func<CategoryUpdateDto, CategoryUpdateDto> change) =>
        e with { Groups = e.Groups.Select(g => g with { Categories = g.Categories.Select(c => c.Id == id ? change(c) : c).ToList() }).ToList() };

    private static BudgetUpdateDto AddCategory(BudgetUpdateDto e, string groupId, string id, string name) =>
        e with { Groups = e.Groups.Select(g => g.Id != groupId ? g : g with { Categories = [.. g.Categories, new CategoryUpdateDto(id, name, 0, 0, null, false, g.Categories.Count == 0 ? 0 : g.Categories.Max(c => c.SortOrder) + 1, false)] }).ToList() };

    private static CategoryDto Category(BudgetDto b, string id) => b.Groups.SelectMany(g => g.Categories).Single(c => c.Id == id);
    private static IEnumerable<string> Structure(BudgetDto b) => b.Groups.SelectMany(g => g.Categories.Select(c => $"{g.Id}/{g.Name}/{g.SortOrder}/{g.IsArchived}:{c.Id}/{c.Name}/{c.Icon}/{c.SortOrder}/{c.IsArchived}"));

    private async Task<(TestApp App, Session Session)> StartAsync(string user = "owner")
    {
        var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, user);
        var client = await LoginAsync(app, user);
        var budget = await CreateBudgetAsync(client, "Household");
        client.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={budget.Id}");
        return (app, new Session(client, budget.Id));
    }

    [Fact]
    public async Task Structure_changes_in_one_month_apply_to_every_past_existing_and_future_month_while_money_stays_per_month()
    {
        var (app, s) = await StartAsync();
        await using var _ = app;
        await s.Month(September); await s.Month(December);
        // Add, then rename/reorder/move/icon from October; the category starts with zero balances everywhere.
        await s.Edit(October, e => AddCategory(e, "home", "category-gym", "Gym"));
        await s.Assign(October, "category-gym", 40);
        await s.Edit(October, e =>
        {
            var gym = e.Groups.SelectMany(g => g.Categories).Single(c => c.Id == "category-gym");
            var moved = e with { Groups = e.Groups.Select(g => g.Id == "home" ? g with { Categories = g.Categories.Where(c => c.Id != "category-gym").ToList() }
                : g.Id == "life" ? g with { Name = "Fun", Categories = [gym with { Name = "Fitness", Icon = "F", SortOrder = 9 }, .. g.Categories] } : g).ToList() };
            return moved;
        });

        var structures = new List<IEnumerable<string>>();
        foreach (var month in new[] { July, August, September, October, "2026-11", December, "2027-03" })
        {
            var plan = await s.Month(month);
            structures.Add(Structure(plan).ToList());
            var gym = Category(plan, "category-gym");
            Assert.Equal("Fitness", gym.Name);
            Assert.Equal("Fun", plan.Groups.Single(g => g.Categories.Any(c => c.Id == "category-gym")).Name);
            Assert.Equal(month == October ? 40 : 0, gym.Assigned);
            Assert.Equal(string.CompareOrdinal(month, October) >= 0 ? 40 : 0, gym.Available);
        }
        Assert.All(structures, structure => Assert.Equal(structures[0], structure));

        // Archive and restore are budget-wide as well.
        await s.Edit(July, e => WithCategory(e, "coffee", c => c with { IsArchived = true }));
        Assert.True(Category(await s.Month(December), "coffee").IsArchived);
        await s.Edit(December, e => WithCategory(e, "coffee", c => c with { IsArchived = false }));
        Assert.False(Category(await s.Month(July), "coffee").IsArchived);

        // Snooze and the legacy target stay per month.
        await s.Edit(September, e => WithCategory(e, "rent", c => c with { Snoozed = true, Target = 25 }));
        Assert.True(Category(await s.Month(September), "rent").Snoozed);
        Assert.False(Category(await s.Month(August), "rent").Snoozed);
        Assert.Equal(0, Category(await s.Month(August), "rent").Target);
    }

    [Fact]
    public async Task A_structural_change_advances_every_month_version_so_a_stale_session_cannot_overwrite_it()
    {
        var (app, s) = await StartAsync();
        await using var _ = app;
        var staleSeptember = await s.Month(September);
        var octoberBefore = await s.Month(October);
        await s.Edit(October, e => WithCategory(e, "rent", c => c with { Name = "Housing" }));

        using var conflict = await s.Client.PutAsJsonAsync(s.MonthPath(September), WithCategory(Editable(staleSeptember), "groceries", c => c with { Assigned = 10 }));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var current = await s.Month(September);
        Assert.True(current.Version > staleSeptember.Version);
        Assert.Equal("Housing", Category(current, "rent").Name);
        Assert.Equal(0, Category(current, "groceries").Assigned);

        // An assignment-only save changes only its own month's version.
        var october = await s.Month(October);
        Assert.True(october.Version > octoberBefore.Version);
        var septemberVersion = (await s.Month(September)).Version;
        await s.Assign(October, "groceries", 15);
        Assert.Equal(septemberVersion, (await s.Month(September)).Version);
    }

    [Fact]
    public async Task A_structural_change_notifies_open_sessions_of_every_month()
    {
        var (app, s) = await StartAsync();
        await using var _ = app;
        await s.Month(September);
        using var subscription = app.Services.GetRequiredService<BudgetChangePublisher>().Subscribe(s.BudgetId, "observer")!;
        await s.Edit(October, e => WithCategory(e, "rent", c => c with { Name = "Housing" }));
        var events = new List<BudgetChangeEvent>();
        while (subscription.Channel.Reader.TryRead(out var change)) events.Add(change);
        // A budget-wide event (month = null) makes every open Plan month reload; the other month's new version is announced too.
        Assert.Contains(events, e => e.Kind == BudgetChangeKinds.Plan && e.Month is null);
        Assert.Contains(events, e => e.Kind == BudgetChangeKinds.Plan && e.Month == September && e.Version > 1);
    }

    [Fact]
    public async Task Starter_template_is_seeded_once_and_a_new_earlier_month_uses_the_budget_categories()
    {
        var (app, s) = await StartAsync();
        await using var _ = app;
        await s.Edit(October, e => AddCategory(WithCategory(e, "utilities", c => c with { Name = "Power" }), "home", "category-pets", "Pets"));
        var earlier = await s.Month("2025-01");
        Assert.Equal("Power", Category(earlier, "utilities").Name);
        Assert.Contains(earlier.Groups.SelectMany(g => g.Categories), c => c.Id == "category-pets");
        await using var db = app.CreateDb();
        Assert.True((await db.BudgetPlans.SingleAsync(p => p.Id == s.BudgetId)).CategoriesInitialized);
        Assert.Equal(1, await db.CategoryGroups.CountAsync(g => g.BudgetId == s.BudgetId && g.Id == "home"));
        Assert.Equal(16, await db.Categories.CountAsync(c => c.BudgetId == s.BudgetId));
    }

    [Fact]
    public async Task A_category_added_later_is_usable_for_transactions_in_any_month_and_labels_do_not_depend_on_the_date()
    {
        var (app, s) = await StartAsync();
        await using var _ = app;
        await s.Month(July);
        await s.Edit(October, e => AddCategory(e, "daily", "category-pets", "Pets"));
        var account = await s.Account();
        await s.Spend(account, new DateOnly(2026, 7, 10), -12, "category-pets");
        var register = await Read<RegisterDto>(s.Client.GetAsync($"/api/transactions?accountId={account}"));
        Assert.Equal("Everyday: Pets", register.Items.Single(t => t.CategoryId == "category-pets").CategoryName);
        Assert.Equal(-12, Category(await s.Month(July), "category-pets").Activity);

        // Archived categories are refused for new entries in every month.
        await s.Edit(July, e => WithCategory(e, "category-pets", c => c with { IsArchived = true }));
        using var refused = await s.Client.PostAsJsonAsync("/api/transactions",
            new TransactionRequest(Guid.NewGuid(), await s.LedgerVersion(), account, new DateOnly(2026, 12, 1), -3, null, null, "category-pets", "uncleared", null));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    [Fact]
    public async Task A_category_without_history_is_deleted_from_every_month_without_reassignment()
    {
        var (app, s) = await StartAsync();
        await using var _ = app;
        await s.Month(August);
        await s.Edit(October, e => AddCategory(e, "home", "category-empty", "Unused"));
        var preview = await s.Preview("category-empty");
        Assert.False(preview.RequiresReassignment);
        Assert.Equal(0, preview.TransactionCount);
        Assert.DoesNotContain(preview.ReassignmentOptions, o => o.Id == "category-empty" || o.Id.StartsWith("ccp-"));

        using var deleted = await s.Delete("category-empty", null);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        foreach (var month in new[] { August, October, December })
            Assert.DoesNotContain((await s.Month(month)).Groups.SelectMany(g => g.Categories), c => c.Id == "category-empty");
        await using var db = app.CreateDb();
        Assert.False(await db.CategoryMonths.AnyAsync(m => m.BudgetId == s.BudgetId && m.CategoryId == "category-empty"));
        Assert.False(await db.Categories.AnyAsync(c => c.BudgetId == s.BudgetId && c.Id == "category-empty"));
        Assert.True(await db.LedgerCommands.IgnoreQueryFilters().AnyAsync(c => c.BudgetId == s.BudgetId && c.Kind == "category.delete/category-empty"));
    }

    [Fact]
    public async Task Deleting_a_category_with_history_reassigns_transactions_splits_schedules_assignments_and_available_atomically()
    {
        var (app, s) = await StartAsync();
        await using var _ = app;
        var account = await s.Account(2000);
        await s.Month(August); await s.Month(December);
        await s.Assign(August, "coffee", 50); await s.Assign(August, "groceries", 100);
        await s.Assign(September, "coffee", 30); await s.Assign(October, "groceries", 20);
        await s.Spend(account, new DateOnly(2026, 8, 3), -20, "coffee");
        await s.Spend(account, new DateOnly(2026, 9, 3), -15, null, [new SplitDto("coffee", -10), new SplitDto("groceries", -5)]);
        await s.Spend(account, new DateOnly(2026, 10, 3), -70, "coffee");
        var deletedSpend = await s.Spend(account, new DateOnly(2026, 8, 5), -4, "coffee");
        await Read<MutationDto>(s.Client.PostAsJsonAsync($"/api/transactions/{deletedSpend}/delete", new DeleteTransactionRequest(Guid.NewGuid(), await s.LedgerVersion())));
        await Read<MutationDto>(s.Client.PostAsJsonAsync("/api/schedules", new ScheduleRequest(Guid.NewGuid(), await s.LedgerVersion(), account, new DateOnly(2026, 11, 20), -9,
            "Cafe", null, "coffee", null, "monthly")));
        await Read<CategoryTargetResultDto>(s.Client.PutAsJsonAsync($"{s.CategoryPath("coffee")}/target",
            new SaveCategoryTargetRequest(Guid.NewGuid(), 0, October, "monthly", 40m, "setAside")));

        var months = new[] { August, September, October, "2026-11", December };
        var before = new Dictionary<string, BudgetDto>();
        foreach (var month in months) before[month] = await s.Month(month);

        var preview = await s.Preview("coffee");
        Assert.True(preview.RequiresReassignment);
        Assert.Equal(4, preview.TransactionCount);
        Assert.Equal(1, preview.SplitLineCount);
        Assert.Equal(1, preview.ScheduleCount);
        Assert.Equal(2, preview.AssignedMonthCount);
        Assert.Equal(80, preview.AssignedTotal);
        Assert.Equal(-20, preview.RemainingAvailable);
        Assert.Equal(October, preview.RemainingAvailableMonth);
        Assert.True(preview.HasTarget);
        Assert.Contains(preview.ReassignmentOptions, o => o.Id == "groceries");
        Assert.DoesNotContain(preview.ReassignmentOptions, o => o.Id == "coffee");

        using (var missingTarget = await s.Delete("coffee", null)) Assert.Equal(HttpStatusCode.BadRequest, missingTarget.StatusCode);
        using (var self = await s.Delete("coffee", "coffee")) Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        using (var unknown = await s.Delete("coffee", "does-not-exist")) Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        using (var stale = await s.Delete("coffee", "groceries", version: preview.LedgerVersion - 1)) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var command = Guid.NewGuid();
        var result = await Read<MutationDto>(s.Delete("coffee", "groceries", preview.LedgerVersion, command));
        // Replaying the same command returns the original result and changes nothing more.
        var replay = await Read<MutationDto>(s.Delete("coffee", "groceries", preview.LedgerVersion, command));
        Assert.Equal(result, replay);

        foreach (var month in months)
        {
            var after = await s.Month(month);
            Assert.DoesNotContain(after.Groups.SelectMany(g => g.Categories), c => c.Id == "coffee");
            var groceries = Category(after, "groceries");
            var old = before[month];
            Assert.Equal(Category(old, "groceries").Assigned + Category(old, "coffee").Assigned, groceries.Assigned);
            Assert.Equal(Category(old, "groceries").Activity + Category(old, "coffee").Activity, groceries.Activity);
            // Coffee's October overspending (-20) was withheld from later Ready to Assign; Groceries now covers it.
            Assert.Equal(old.ReadyToAssign + (string.CompareOrdinal(month, October) > 0 ? 20 : 0), after.ReadyToAssign);
        }
        // Combined carry: the merged category keeps both histories, so October is no longer overspent.
        Assert.Equal(100 + 50 - 20, Category(await s.Month(August), "groceries").Available);
        Assert.Equal(130 + 30 - 15, Category(await s.Month(September), "groceries").Available);
        Assert.Equal(145 + 20 - 70, Category(await s.Month(October), "groceries").Available);

        await using var db = app.CreateDb();
        Assert.False(await db.Transactions.IgnoreQueryFilters().AnyAsync(t => t.BudgetId == s.BudgetId && t.CategoryId == "coffee"));
        // Three plain coffee entries (one soft-deleted, kept as history) now use Groceries.
        Assert.Equal(3, await db.Transactions.IgnoreQueryFilters().CountAsync(t => t.BudgetId == s.BudgetId && t.CategoryId == "groceries" && t.Memo == "test"));
        var split = await db.Transactions.IgnoreQueryFilters().Include(t => t.Splits).SingleAsync(t => t.BudgetId == s.BudgetId && t.Splits.Count > 0);
        Assert.All(split.Splits, line => Assert.Equal("groceries", line.CategoryId));
        var schedule = await db.Schedules.IgnoreQueryFilters().SingleAsync(x => x.BudgetId == s.BudgetId);
        Assert.Equal("groceries", JsonSerializer.Deserialize<ScheduleTemplateDto>(schedule.TemplateJson)!.CategoryId);
        Assert.False(await db.CategoryTargets.IgnoreQueryFilters().AnyAsync(t => t.BudgetId == s.BudgetId && t.CategoryId == "coffee"));
        Assert.False(await db.CategoryMonths.AnyAsync(m => m.BudgetId == s.BudgetId && m.CategoryId == "coffee"));
        var receipt = await db.LedgerCommands.IgnoreQueryFilters().SingleAsync(c => c.BudgetId == s.BudgetId && c.Kind == "category.delete/coffee");
        Assert.Contains("\"assignedByMonth\"", receipt.AfterJson);
        Assert.Contains("\"Id\":\"coffee\"", receipt.BeforeJson);
        Assert.Contains("\"targetRemoved\":true", receipt.AfterJson);
        var action = await db.PlanActions.SingleAsync(a => a.BudgetId == s.BudgetId && a.Kind == "reassigned");
        Assert.Equal(("Coffee & lunch", "Groceries", 80m), (action.FromCategoryName, action.ToCategoryName, action.Amount));
    }

    [Fact]
    public async Task A_failure_before_commit_rolls_back_the_whole_reassignment()
    {
        var (app, s) = await StartAsync();
        await using var _ = app;
        var account = await s.Account();
        await s.Assign(September, "coffee", 30);
        await s.Spend(account, new DateOnly(2026, 9, 2), -10, "coffee");
        var before = await s.Month(October);

        app.Failure.FailAuditSave = true;
        // The simulated failure happens after the reassignment rows were written, before the command commits.
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Delete("coffee", "groceries"));

        var after = await s.Month(October);
        Assert.Equal(Structure(before), Structure(after));
        Assert.Equal(20, Category(await s.Month(September), "coffee").Available);
        Assert.Equal(30, Category(await s.Month(September), "coffee").Assigned);
        await using var db = app.CreateDb();
        Assert.Equal(1, await db.Transactions.IgnoreQueryFilters().CountAsync(t => t.BudgetId == s.BudgetId && t.CategoryId == "coffee"));
        Assert.True(await db.Categories.AnyAsync(c => c.BudgetId == s.BudgetId && c.Id == "coffee"));
        Assert.False(await db.PlanActions.AnyAsync(a => a.BudgetId == s.BudgetId && a.Kind == "reassigned"));
    }

    [Fact]
    public async Task System_and_other_budget_categories_cannot_be_deleted_or_used_as_reassignment_targets()
    {
        var (app, s) = await StartAsync();
        await using var _ = app;
        var card = (await Read<MutationDto>(s.Client.PostAsJsonAsync("/api/accounts",
            new CreateAccountRequest(Guid.NewGuid(), await s.LedgerVersion(), "Card", new DateOnly(2026, 10, 1), 0, "creditCard")))).Id;
        var payment = CreditCardRules.PaymentCategoryId(card);
        Assert.Contains((await s.Month(October)).Groups.SelectMany(g => g.Categories), c => c.Id == payment);
        using (var system = await s.Delete(payment, "groceries")) Assert.Equal(HttpStatusCode.BadRequest, system.StatusCode);
        using (var systemTarget = await s.Delete("coffee", payment)) Assert.Equal(HttpStatusCode.BadRequest, systemTarget.StatusCode);
        Assert.DoesNotContain((await s.Preview("coffee")).ReassignmentOptions, o => o.Id == payment);
        await s.Edit(October, e => WithCategory(e, "dining", c => c with { IsArchived = true }));
        using (var archivedTarget = await s.Delete("coffee", "dining")) Assert.Equal(HttpStatusCode.BadRequest, archivedTarget.StatusCode);

        // Another user's budget is not found, for both the preview and the delete.
        await CreateUserAsync(app, "outsider");
        using var outsider = await LoginAsync(app, "outsider");
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(s.CategoryPath("coffee") + "/deletion")).StatusCode);
        using var denied = await outsider.PostAsJsonAsync(s.CategoryPath("coffee") + "/delete", new DeleteCategoryRequest(Guid.NewGuid(), 1, "groceries"));
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Contains((await s.Month(October)).Groups.SelectMany(g => g.Categories), c => c.Id == "coffee");
    }

    [Fact]
    public async Task Mcp_tools_preview_and_delete_a_category_with_confirmation()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "root", administrator: true);
        using var root = await LoginAsync(app, "root");
        var budgetId = (await CreateBudgetAsync(root, "Household")).Id;
        root.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={budgetId}");
        var s = new Session(root, budgetId);
        var account = await s.Account();
        await s.Spend(account, new DateOnly(2026, 10, 2), -8, "coffee");
        using var mcp = Mcp(app, (await CreateTokenAsync(root, "Admin")).Token);

        var preview = await mcp.CallAsync("categories_delete_preview", new { budgetId, categoryId = "coffee" });
        preview.AssertOk();
        Assert.Equal(1, preview.Structured().GetProperty("transactionCount").GetInt32());
        Assert.Equal("confirmation_required", (await mcp.CallAsync("categories_delete", new { budgetId, categoryId = "coffee", reassignToCategoryId = "groceries" })).ErrorCode());
        Assert.True((await mcp.CallAsync("categories_delete", new { budgetId, categoryId = "coffee", confirm = true })).IsError());
        (await mcp.CallAsync("categories_delete", new { budgetId, categoryId = "coffee", reassignToCategoryId = "groceries", confirm = true })).AssertOk();
        var month = await s.Month(October);
        Assert.DoesNotContain(month.Groups.SelectMany(g => g.Categories), c => c.Id == "coffee");
        Assert.Equal(-8, Category(month, "groceries").Activity);
    }
}
