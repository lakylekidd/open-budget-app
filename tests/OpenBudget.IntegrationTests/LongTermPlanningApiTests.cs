using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OpenBudget.Api;

namespace OpenBudget.IntegrationTests;

public sealed class LongTermPlanningApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static Task<T> Get<T>(HttpClient client, string path) => Read<T>(client.GetAsync(path));

    private static async Task<T> Read<T>(Task<HttpResponseMessage> pending)
    {
        using var response = await pending;
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<T> Put<T>(HttpClient client, string path, object body) =>
        await Read<T>(client.PutAsJsonAsync(path, body));

    private static Task<LongTermPlanningDto> Year(HttpClient client, int year) =>
        Get<LongTermPlanningDto>(client, $"/api/long-term-planning/{year}");

    private static Task<HttpResponseMessage> Update(HttpClient client, int year, LongTermPlanningDto plan,
        string month, string field, string? categoryId, decimal? amount) =>
        client.PutAsJsonAsync($"/api/long-term-planning/{year}",
            new LongTermPlanningUpdateDto(plan.Version, month, field, categoryId, amount));

    private static LongTermPlanningCategoryMonthDto CategoryMonth(LongTermPlanningDto plan, string categoryId, string month) =>
        plan.Groups.SelectMany(x => x.Categories).Single(x => x.Id == categoryId).Months.Single(x => x.Month == month);

    [Fact]
    public async Task Scheduled_income_and_expenses_from_all_accounts_drive_read_only_forecasts()
    {
        await using var app = await fixture.CreateAppAsync();
        app.Clock.UtcNow = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);
        var client = app.CreateClient();
        async Task<long> Version() => (await Get<AccountsDto>(client, "/api/accounts")).Version;
        async Task<Guid> Account(string name) => (await Read<MutationDto>(client.PostAsJsonAsync("/api/accounts",
            new CreateAccountRequest(Guid.NewGuid(), await Version(), name, new DateOnly(2026, 5, 15), 0m)))).Id;
        var main = await Account("Main");
        var other = await Account("Other");
        async Task Schedule(Guid account, decimal amount, string? category = null, Guid? transfer = null, bool paused = false) =>
            _ = await Read<MutationDto>(client.PostAsJsonAsync("/api/schedules", new ScheduleRequest(
                Guid.NewGuid(), await Version(), account, new DateOnly(2026, 6, 1), amount,
                "Forecast", null, category, [], "monthly", transfer, paused)));
        await Schedule(main, 2000m);
        await Schedule(other, 500m);
        await Schedule(main, -800m, "rent");
        await Schedule(main, -100m, transfer: other);
        await Schedule(other, 300m, paused: true);

        var plan = await Year(client, 2026);
        var juneActivity = await Get<LongTermPlanningActivityDto>(client, "/api/long-term-planning/2026/activity/2026-06");
        var julyActivity = await Get<LongTermPlanningActivityDto>(client, "/api/long-term-planning/2026/activity/2026-07");
        Assert.Equal(3, juneActivity.Scheduled.Count);
        Assert.Equal(3, julyActivity.Scheduled.Count);
        Assert.Contains(julyActivity.Scheduled, entry => entry.Amount == 2000m && entry.Date == new DateOnly(2026, 7, 1));
        Assert.Contains(julyActivity.Scheduled, entry => entry.Amount == -800m && entry.CategoryId == "rent");
        Assert.DoesNotContain(julyActivity.Scheduled, entry => entry.TransferId is not null || entry.Amount == -100m || entry.Amount == 300m);
        Assert.NotEqual(juneActivity.Scheduled.Single(entry => entry.Amount == -800m).Id,
            julyActivity.Scheduled.Single(entry => entry.Amount == -800m).Id);
        using var invalidActivity = await client.GetAsync("/api/long-term-planning/2026/activity/2026-13");
        Assert.Equal(HttpStatusCode.BadRequest, invalidActivity.StatusCode);
        foreach (var month in new[] { "2026-06", "2026-07" })
        {
            var summary = plan.Months.Single(x => x.Month == month);
            Assert.Equal(2500m, summary.ExpectedIncome);
            Assert.Equal(800m, summary.ExpectedExpenses);
            Assert.Equal(1700m, summary.Remaining);
            Assert.Equal(2500m, summary.RemainingToBeAssigned);
            Assert.Equal(800m, CategoryMonth(plan, "rent", month).PlannedExpenses);
        }
        plan = await Read<LongTermPlanningDto>(Update(client, 2026, plan, "2026-06", "plannedBudgetOverride", "rent", 900m));
        Assert.Equal(1600m, plan.Months.Single(x => x.Month == "2026-06").RemainingToBeAssigned);
        using var incomeEdit = await Update(client, 2026, plan, "2026-06", "expectedIncome", null, 3000m);
        using var expenseEdit = await Update(client, 2026, plan, "2026-06", "plannedExpenses", "rent", 900m);
        Assert.Equal(HttpStatusCode.BadRequest, incomeEdit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, expenseEdit.StatusCode);
    }

    [Fact]
    public async Task Annual_read_does_not_initialize_months_and_planning_edits_persist_without_changing_actual_budgets()
    {
        await using var app = await fixture.CreateAppAsync();
        app.Clock.UtcNow = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);
        var client = app.CreateClient();
        var actual = await Get<BudgetDto>(client, "/api/budget/2026-05");
        var budgetBefore = await Get<BudgetDto>(client, "/api/budget/2026-05");
        var editable = new BudgetUpdateDto(actual.Month, actual.Note, actual.Version, actual.FundsAdded,
            actual.Groups.Select(group => new CategoryGroupUpdateDto(group.Id, group.Name, group.SortOrder, group.IsArchived,
                group.Categories.Select(category => new CategoryUpdateDto(category.Id, category.Name,
                    category.Id == "utilities" ? 150m : category.Assigned, category.Target, category.Icon,
                    category.Snoozed, category.SortOrder, category.IsArchived)).ToList())).ToList());
        await Put<BudgetDto>(client, "/api/budget/2026-05", editable);
        var budgetAfterAssignment = await Get<BudgetDto>(client, "/api/budget/2026-05");

        var initial = await Year(client, 2026);
        Assert.Equal(12, initial.Months.Count);
        Assert.False(initial.HasPlanningData);
        Assert.Equal(150m, CategoryMonth(initial, "utilities", "2026-05").ActualAssigned);
        Assert.Equal(150m, CategoryMonth(initial, "utilities", "2026-05").ActualAvailable);
        await using (var db = app.CreateDb()) Assert.Equal(1, await db.Budgets.CountAsync());

        var saved = initial;
        saved = await Read<LongTermPlanningDto>(Update(client, 2026, saved, "2026-06", "plannedBudgetOverride", "groceries", 600m));
        saved = await Read<LongTermPlanningDto>(Update(client, 2026, saved, "2026-09", "plannedBudgetOverride", "groceries", 700m));
        saved = await Read<LongTermPlanningDto>(Update(client, 2026, saved, "2026-06", "plannedBudgetOverride", "groceries", 650m));

        Assert.Equal(650m, CategoryMonth(saved, "groceries", "2026-06").PlannedBudget);
        Assert.Equal(650m, CategoryMonth(saved, "groceries", "2026-08").PlannedBudget);
        Assert.Equal(700m, CategoryMonth(saved, "groceries", "2026-09").PlannedBudget);
        Assert.Equal(650m, CategoryMonth(saved, "groceries", "2026-06").PlannedAvailable);
        Assert.Equal(0m, saved.Months.Single(x => x.Month == "2026-06").Remaining);

        saved = await Read<LongTermPlanningDto>(Update(client, 2026, saved, "2026-09", "plannedBudgetOverride", "groceries", null));
        Assert.Null(CategoryMonth(saved, "groceries", "2026-09").PlannedBudgetOverride);
        Assert.Equal(650m, CategoryMonth(saved, "groceries", "2026-09").PlannedBudget);

        var nextYear = await Year(client, 2027);
        Assert.Equal(650m, CategoryMonth(nextYear, "groceries", "2027-01").PlannedBudget);
        Assert.Equal(5_200m, CategoryMonth(nextYear, "groceries", "2027-01").PlannedAvailable);

        await using var restarted = new TestApp(app.ConnectionString);
        var reloaded = await Year(restarted.CreateClient(), 2026);
        Assert.Equal(saved.Version, reloaded.Version);
        Assert.Equal(0m, reloaded.Months.Single(x => x.Month == "2026-06").ExpectedIncome);
        Assert.Equal(650m, CategoryMonth(reloaded, "groceries", "2026-09").PlannedBudget);
        Assert.Equivalent(budgetAfterAssignment, await Get<BudgetDto>(client, "/api/budget/2026-05"), strict: true);
        Assert.NotEqual(budgetBefore.Version, budgetAfterAssignment.Version);
    }

    [Fact]
    public async Task Planned_available_and_actual_available_carry_across_year_boundary_without_creating_actual_months()
    {
        await using var app = await fixture.CreateAppAsync();
        var client = app.CreateClient();
        var december = await Get<BudgetDto>(client, "/api/budget/2026-12");
        var edit = new BudgetUpdateDto(december.Month, december.Note, december.Version, december.FundsAdded,
            december.Groups.Select(group => new CategoryGroupUpdateDto(group.Id, group.Name, group.SortOrder, group.IsArchived,
                group.Categories.Select(category => new CategoryUpdateDto(category.Id, category.Name,
                    category.Id == "utilities" ? 50m : category.Assigned, category.Target, category.Icon,
                    category.Snoozed, category.SortOrder, category.IsArchived)).ToList())).ToList());
        await Put<BudgetDto>(client, "/api/budget/2026-12", edit);

        var plan = await Year(client, 2026);
        plan = await Read<LongTermPlanningDto>(Update(client, 2026, plan, "2026-12", "plannedBudgetOverride", "utilities", 300.25m));
        var nextYear = await Year(client, 2027);

        var january = CategoryMonth(nextYear, "utilities", "2027-01");
        Assert.Equal(600.50m, january.PlannedAvailable);
        Assert.Null(january.ActualAssigned);
        Assert.Null(january.ActualExpenses);
        Assert.Equal(50m, january.ActualAvailable);
        await using (var db = app.CreateDb()) Assert.Equal(1, await db.Budgets.CountAsync());
    }

    [Fact]
    public async Task Past_months_invalid_money_and_stale_writes_are_rejected_without_partial_planning_changes()
    {
        await using var app = await fixture.CreateAppAsync();
        var client = app.CreateClient();
        await Get<BudgetDto>(client, "/api/budget/2026-10");
        var initial = await Year(client, 2026);

        using var past = await Update(client, 2026, initial, "2026-09", "plannedBudgetOverride", "groceries", 500m);
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        using var invalid = await Update(client, 2026, initial, "2026-11", "plannedExpenses", "groceries", -1m);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var saved = await Read<LongTermPlanningDto>(Update(client, 2026, initial, "2026-11", "plannedBudgetOverride", "groceries", 2000m));
        using var stale = await Update(client, 2026, initial, "2026-11", "plannedBudgetOverride", "groceries", 3000m);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var conflict = await stale.Content.ReadFromJsonAsync<PlanningConflictBody>();
        Assert.Equal(2000m, CategoryMonth(conflict!.Current, "groceries", "2026-11").PlannedBudget);
        Assert.Equal(2000m, CategoryMonth(await Year(client, 2026), "groceries", "2026-11").PlannedBudget);
        Assert.True(saved.HasPlanningData);
        await using (var db = app.CreateDb())
        {
            Assert.Equal(2000m, Assert.Single(await db.LongTermPlanningCategories.ToListAsync()).PlannedBudgetOverride);
            Assert.Empty(await db.LongTermPlanningIncomes.ToListAsync());
        }
    }

    [Fact]
    public async Task Plan_month_exposes_effective_planned_overrides_without_writing_planning_data()
    {
        await using var app = await fixture.CreateAppAsync();
        app.Clock.UtcNow = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);
        var client = app.CreateClient();
        static decimal? Planned(BudgetDto month, string categoryId) =>
            month.Groups.SelectMany(x => x.Categories).Single(x => x.Id == categoryId).Planned;

        var may = await Get<BudgetDto>(client, "/api/budget/2026-05");
        Assert.All(may.Groups.SelectMany(x => x.Categories), category => Assert.Null(category.Planned));

        var plan = await Year(client, 2026);
        plan = await Read<LongTermPlanningDto>(Update(client, 2026, plan, "2026-06", "plannedBudgetOverride", "groceries", 600m));
        plan = await Read<LongTermPlanningDto>(Update(client, 2026, plan, "2026-09", "plannedBudgetOverride", "groceries", 700.50m));
        plan = await Read<LongTermPlanningDto>(Update(client, 2026, plan, "2026-07", "plannedBudgetOverride", "utilities", 0m));

        int planningRows;
        await using (var db = app.CreateDb()) planningRows = await db.LongTermPlanningCategories.CountAsync();

        Assert.Null(Planned(await Get<BudgetDto>(client, "/api/budget/2026-05"), "groceries"));
        Assert.Equal(600m, Planned(await Get<BudgetDto>(client, "/api/budget/2026-06"), "groceries"));
        var august = await Get<BudgetDto>(client, "/api/budget/2026-08");
        Assert.Equal(600m, Planned(august, "groceries"));
        Assert.Equal(0m, Planned(august, "utilities"));
        Assert.Null(Planned(august, "rent"));
        Assert.Equal(700.50m, Planned(await Get<BudgetDto>(client, "/api/budget/2026-09"), "groceries"));
        Assert.Equal(700.50m, Planned(await Get<BudgetDto>(client, "/api/budget/2027-02"), "groceries"));
        Assert.Equivalent(august, await Get<BudgetDto>(client, "/api/budget/2026-08"), strict: true);

        var reloaded = await Year(client, 2026);
        Assert.Equal(plan.Version, reloaded.Version);
        await using (var db = app.CreateDb())
            Assert.Equal(planningRows, await db.LongTermPlanningCategories.CountAsync());
    }

    private sealed record PlanningConflictBody(string Error, LongTermPlanningDto Current);
}
