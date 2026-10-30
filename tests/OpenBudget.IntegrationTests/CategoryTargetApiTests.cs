using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenBudget.Api;

namespace OpenBudget.IntegrationTests;

// The test clock is 21 October 2026. October 2026 has five Fridays; November 2026 has four.
public sealed class CategoryTargetApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Targets_can_be_created_replayed_edited_and_deleted_with_month_progress_and_version_checks()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "targets", "targets@example.test");
        using var client = await LoginAsync(app, "targets");
        var budget = await CreateBudgetAsync(client, "Targets");
        client.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={budget.Id}");
        var path = $"/api/budgets/{budget.Id}/categories/groceries/target";

        var empty = await Read<CategoryTargetResultDto>(await client.GetAsync($"{path}/2026-10"));
        Assert.Null(empty.Target);
        Assert.Null(Category(await Month(client, budget.Id, "2026-10"), "groceries").CategoryTarget);

        var create = new SaveCategoryTargetRequest(Guid.NewGuid(), 0, "2026-10", "monthly", 100m, "setAside");
        var created = await Read<CategoryTargetResultDto>(await client.PutAsJsonAsync(path, create));
        Assert.Equal(1, created.Target!.Version);
        Assert.Equal("2026-10", created.Target.StartMonth);
        Assert.Equal(100m, created.Progress!.Needed);
        Assert.Equal(100m, created.Progress.MoreNeeded);

        // An exact replay of a lost response returns the stored state without a second write.
        var replayed = await Read<CategoryTargetResultDto>(await client.PutAsJsonAsync(path, create));
        Assert.Equal(1, replayed.Target!.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path, create with { Amount = 120m })).StatusCode);

        await Assign(client, budget.Id, "2026-10", "groceries", 30m);
        var groceries = Category(await Month(client, budget.Id, "2026-10"), "groceries");
        Assert.Equal("monthly", groceries.CategoryTarget!.Kind);
        Assert.Equal(70m, groceries.TargetProgress!.MoreNeeded);
        Assert.Equal(0.3m, groceries.TargetProgress.FundedFraction);
        Assert.Equal("underfunded", groceries.TargetProgress.Status);

        var stale = await client.PutAsJsonAsync(path, create with { CommandId = Guid.NewGuid(), Amount = 50m });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var current = (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("current");
        Assert.Equal(1, current.GetProperty("target").GetProperty("version").GetInt64());

        var weekly = await Read<CategoryTargetResultDto>(await client.PutAsJsonAsync(path,
            new SaveCategoryTargetRequest(Guid.NewGuid(), 1, "2026-10", "weekly", 25m, "setAside", Weekday: 5)));
        Assert.Equal(2, weekly.Target!.Version);
        Assert.Equal(125m, weekly.Progress!.Needed);
        Assert.Equal(5, weekly.Progress.Segments);
        Assert.Equal(95m, weekly.Progress.MoreNeeded);

        var november = Category(await Month(client, budget.Id, "2026-11"), "groceries");
        Assert.Equal(4, november.TargetProgress!.Segments);
        Assert.Equal(100m, november.TargetProgress.Needed);

        var audit = await client.GetFromJsonAsync<AuditPageDto>("/api/ledger/audit?page=1&pageSize=25");
        Assert.Contains(audit!.Items, item => item.Kind == "categoryTarget.save/groceries");

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{path}/delete", new DeleteCategoryTargetRequest(Guid.NewGuid(), 1, "2026-10"))).StatusCode);
        var deleted = await Read<CategoryTargetResultDto>(await client.PostAsJsonAsync($"{path}/delete", new DeleteCategoryTargetRequest(Guid.NewGuid(), 2, "2026-10")));
        Assert.Null(deleted.Target);
        Assert.Null(Category(await Month(client, budget.Id, "2026-10"), "groceries").TargetProgress);
        await using var db = app.CreateDb();
        Assert.Empty(await db.CategoryTargets.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Yearly_set_aside_targets_subtract_earlier_assignments_in_later_months()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "yearly", "yearly@example.test");
        using var client = await LoginAsync(app, "yearly");
        var budget = await CreateBudgetAsync(client, "Yearly");
        var path = $"/api/budgets/{budget.Id}/categories/annual/target";

        var created = await Read<CategoryTargetResultDto>(await client.PutAsJsonAsync(path,
            new SaveCategoryTargetRequest(Guid.NewGuid(), 0, "2026-10", "yearly", 1200m, "setAside", DueDate: new DateOnly(2027, 3, 15))));
        Assert.Equal(200m, created.Progress!.Needed); // October..March = 6 months
        Assert.Equal(6, created.Progress.MonthsRemaining);

        await Assign(client, budget.Id, "2026-10", "annual", 100m);
        var november = Category(await Month(client, budget.Id, "2026-11"), "annual");
        Assert.Equal(220m, november.TargetProgress!.Needed); // (1200 - 100) / 5
        Assert.Equal(new DateOnly(2027, 3, 15), november.TargetProgress.NextDueDate);
    }

    [Fact]
    public async Task Invalid_payment_hidden_and_missing_category_targets_are_rejected_without_writes()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "invalid", "invalid@example.test");
        using var client = await LoginAsync(app, "invalid");
        var budget = await CreateBudgetAsync(client, "Validation");
        client.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={budget.Id}");
        var path = $"/api/budgets/{budget.Id}/categories/groceries/target";
        var valid = new SaveCategoryTargetRequest(Guid.NewGuid(), 0, "2026-10", "monthly", 100m, "setAside");

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, valid with { Amount = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, valid with { Amount = 1.234m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, valid with { Kind = "weekly", Weekday = 9 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, valid with { DayOfMonth = 32 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, valid with { Kind = "custom", DueDate = new DateOnly(2026, 10, 1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, valid with { Kind = "custom", DueDate = new DateOnly(2027, 1, 1), RepeatEvery = 100, RepeatUnit = "day" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, valid with { Kind = "yearly", DueDate = new DateOnly(1899, 12, 31) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/budgets/{budget.Id}/categories/missing/target", valid)).StatusCode);

        var accounts = await client.GetFromJsonAsync<AccountsDto>("/api/accounts");
        var card = await Read<MutationDto>(await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(
            Guid.NewGuid(), accounts!.Version, "Visa", new DateOnly(2026, 10, 1), -50m, "creditCard")));
        var payment = await client.PutAsJsonAsync($"/api/budgets/{budget.Id}/categories/{CreditCardRules.PaymentCategoryId(card.Id)}/target", valid);
        Assert.Equal(HttpStatusCode.BadRequest, payment.StatusCode);
        Assert.Contains("payment", await payment.Content.ReadAsStringAsync());

        var month = await Month(client, budget.Id, "2026-10");
        var hidden = ToUpdate(month, category => category.Id == "coffee" ? category with { IsArchived = true } : category);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/budgets/{budget.Id}/months/2026-10", hidden)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/budgets/{budget.Id}/categories/coffee/target", valid)).StatusCode);

        // A rejected write leaves no target or receipt and does not initialize the requested month.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, valid with { Month = "2027-05", Amount = -1 })).StatusCode);
        await using var db = app.CreateDb();
        Assert.Empty(await db.CategoryTargets.IgnoreQueryFilters().ToListAsync());
        Assert.DoesNotContain(await db.LedgerCommands.IgnoreQueryFilters().Select(x => x.Kind).ToListAsync(), kind => kind.StartsWith("categoryTarget"));
        Assert.False(await db.Budgets.AnyAsync(x => x.BudgetId == budget.Id && x.Month == "2027-05"));
    }

    [Fact]
    public async Task Targets_are_isolated_between_budgets()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "targetalice", "targetalice@example.test");
        await CreateUserAsync(app, "targetbob", "targetbob@example.test");
        using var alice = await LoginAsync(app, "targetalice");
        using var bob = await LoginAsync(app, "targetbob");
        var aliceBudget = await CreateBudgetAsync(alice, "Alice targets");
        var bobBudget = await CreateBudgetAsync(bob, "Bob targets");
        var request = new SaveCategoryTargetRequest(Guid.NewGuid(), 0, "2026-10", "monthly", 80m, "refill", DayOfMonth: 15);

        Assert.Equal(HttpStatusCode.OK, (await alice.PutAsJsonAsync($"/api/budgets/{aliceBudget.Id}/categories/rent/target", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/budgets/{aliceBudget.Id}/categories/rent/target/2026-10")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/budgets/{aliceBudget.Id}/categories/rent/target",
            request with { CommandId = Guid.NewGuid(), Version = 1 })).StatusCode);

        Assert.Null(Category(await Month(bob, bobBudget.Id, "2026-10"), "rent").CategoryTarget);
        var aliceRent = Category(await Month(alice, aliceBudget.Id, "2026-10"), "rent");
        Assert.Equal("refill", aliceRent.CategoryTarget!.Behavior);
        Assert.Equal(15, aliceRent.CategoryTarget.DayOfMonth);
        Assert.Equal(80m, aliceRent.TargetProgress!.MoreNeeded);

        // Bob can create the same logical category's target in his own budget independently.
        Assert.Equal(HttpStatusCode.OK, (await bob.PutAsJsonAsync($"/api/budgets/{bobBudget.Id}/categories/rent/target",
            request with { CommandId = Guid.NewGuid(), Amount = 10m })).StatusCode);
        Assert.Equal(80m, Category(await Month(alice, aliceBudget.Id, "2026-10"), "rent").CategoryTarget!.Amount);
    }

    private static CategoryDto Category(BudgetDto budget, string id) => budget.Groups.SelectMany(g => g.Categories).Single(c => c.Id == id);

    private static async Task<BudgetDto> Month(HttpClient client, string budgetId, string month) =>
        (await client.GetFromJsonAsync<BudgetDto>($"/api/budgets/{budgetId}/months/{month}"))!;

    private static async Task Assign(HttpClient client, string budgetId, string month, string categoryId, decimal amount)
    {
        var current = await Month(client, budgetId, month);
        var update = ToUpdate(current, category => category.Id == categoryId ? category with { Assigned = amount } : category);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/budgets/{budgetId}/months/{month}", update)).StatusCode);
    }

    private static BudgetUpdateDto ToUpdate(BudgetDto budget, Func<CategoryUpdateDto, CategoryUpdateDto> change) => new(
        budget.Month, budget.Note, budget.Version, budget.FundsAdded,
        budget.Groups.Select(g => new CategoryGroupUpdateDto(g.Id, g.Name, g.SortOrder, g.IsArchived,
            g.Categories.Select(c => change(new CategoryUpdateDto(c.Id, c.Name, c.Assigned, c.Target, c.Icon, c.Snoozed, c.SortOrder, c.IsArchived))).ToList())).ToList());

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
