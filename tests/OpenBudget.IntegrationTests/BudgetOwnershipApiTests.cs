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
    public async Task Owner_can_invite_collaborators_and_recent_plan_actions_include_the_actor()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "manager", "manager@example.test");
        await CreateUserAsync(app, "planner", "planner@example.test");
        await CreateUserAsync(app, "outsider", "outsider@example.test");
        using var manager = await LoginAsync(app, "manager");
        using var planner = await LoginAsync(app, "planner");
        using var outsider = await LoginAsync(app, "outsider");
        var budget = await CreateBudgetAsync(manager, "Shared home");

        var invite = await manager.PostAsJsonAsync($"/api/budgets/{budget.Id}/invitations", new InviteBudgetMemberRequest("planner@example.test"));
        Assert.Equal(HttpStatusCode.Accepted, invite.StatusCode);
        var sharing = await manager.GetFromJsonAsync<BudgetSharingDto>($"/api/budgets/{budget.Id}/sharing");
        Assert.True(sharing!.CanManage); Assert.Equal(3, sharing.InvitesRemaining);
        Assert.Equal("planner@example.test", Assert.Single(sharing.Members).Email);

        using var listing = await planner.GetFromJsonAsync<JsonDocument>("/api/budgets");
        var shared = Assert.Single(listing!.RootElement.GetProperty("budgets").EnumerateArray());
        Assert.Equal(budget.Id, shared.GetProperty("id").GetString());
        Assert.Equal("editor", shared.GetProperty("accessRole").GetString());
        planner.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={budget.Id}");
        Assert.Equal(HttpStatusCode.OK, (await planner.GetAsync("/api/accounts")).StatusCode);
        var month = await planner.GetFromJsonAsync<BudgetDto>($"/api/budgets/{budget.Id}/months/2026-10");
        var update = new BudgetUpdateDto(month!.Month, month.Note, month.Version, month.FundsAdded,
            month.Groups.Select(group => new CategoryGroupUpdateDto(group.Id, group.Name, group.SortOrder, group.IsArchived,
                group.Categories.Select(category => new CategoryUpdateDto(category.Id, category.Name,
                    category.Id == "groceries" ? 75m : category.Assigned, category.Target, category.Icon, category.Snoozed, category.SortOrder, category.IsArchived)).ToList())).ToList());
        Assert.Equal(HttpStatusCode.OK, (await planner.PutAsJsonAsync($"/api/budgets/{budget.Id}/months/2026-10", update)).StatusCode);

        var actions = await manager.GetFromJsonAsync<List<PlanActionDto>>($"/api/budgets/{budget.Id}/plan-actions");
        var action = Assert.Single(actions!);
        Assert.Equal("assigned", action.Kind); Assert.Equal(75, action.Amount); Assert.Equal("planner", action.ActorName); Assert.Equal("Groceries", action.ToCategoryName);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/budgets/{budget.Id}/months/2026-10")).StatusCode);
        Assert.False((await planner.GetFromJsonAsync<BudgetSharingDto>($"/api/budgets/{budget.Id}/sharing"))!.CanManage);
        Assert.Equal(HttpStatusCode.NotFound, (await planner.PostAsJsonAsync($"/api/budgets/{budget.Id}/invitations", new InviteBudgetMemberRequest("friend@example.test"))).StatusCode);

        Assert.Equal(HttpStatusCode.Accepted, (await manager.PostAsJsonAsync($"/api/budgets/{budget.Id}/invitations", new InviteBudgetMemberRequest("later@example.test"))).StatusCode);
        await CreateUserAsync(app, "later", "later@example.test");
        using var later = await LoginAsync(app, "later");
        using var accepted = await later.GetFromJsonAsync<JsonDocument>("/api/budgets");
        Assert.Contains(accepted!.RootElement.GetProperty("budgets").EnumerateArray(), item => item.GetProperty("id").GetString() == budget.Id);

        var plannerId = sharing.Members.Single().Id;
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"/api/budgets/{budget.Id}/members/{plannerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await planner.GetAsync($"/api/budgets/{budget.Id}/months/2026-10")).StatusCode);
    }

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

    [Fact]
    public async Task Owned_budget_cookie_scopes_ledger_writes_and_rejects_another_users_budget()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "ledgeralice", "ledgeralice@example.test");
        await CreateUserAsync(app, "ledgerbob", "ledgerbob@example.test");
        using var alice = await LoginAsync(app, "ledgeralice");
        using var bob = await LoginAsync(app, "ledgerbob");
        var aliceBudget = await CreateBudgetAsync(alice, "Alice ledger");
        var bobBudget = await CreateBudgetAsync(bob, "Bob ledger");
        alice.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={aliceBudget.Id}");
        bob.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={bobBudget.Id}");

        var aliceState = await alice.GetFromJsonAsync<AccountsDto>("/api/accounts");
        var aliceCreated = await alice.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(
            Guid.NewGuid(), aliceState!.Version, "Alice checking", new DateOnly(2026, 10, 21), 40m));
        Assert.Equal(HttpStatusCode.OK, aliceCreated.StatusCode);
        var aliceAccount = (await aliceCreated.Content.ReadFromJsonAsync<MutationDto>())!;
        var bobState = await bob.GetFromJsonAsync<AccountsDto>("/api/accounts");
        var bobCreated = await bob.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(
            Guid.NewGuid(), bobState!.Version, "Bob checking", new DateOnly(2026, 10, 21), 60m));
        Assert.Equal(HttpStatusCode.OK, bobCreated.StatusCode);
        var bobAccount = (await bobCreated.Content.ReadFromJsonAsync<MutationDto>())!;

        var aliceAccounts = await alice.GetFromJsonAsync<AccountsDto>("/api/accounts");
        var bobAccounts = await bob.GetFromJsonAsync<AccountsDto>("/api/accounts");
        Assert.Equal("Alice checking", Assert.Single(aliceAccounts!.Accounts).Name);
        Assert.Equal("Bob checking", Assert.Single(bobAccounts!.Accounts).Name);

        var aliceSchedule = await alice.PostAsJsonAsync("/api/schedules", new ScheduleRequest(Guid.NewGuid(), aliceAccounts.Version,
            aliceAccount.Id, new DateOnly(2026, 10, 21), -5m, "Alice rent", "Due", "groceries", null));
        Assert.Equal(HttpStatusCode.OK, aliceSchedule.StatusCode);
        var bobSchedule = await bob.PostAsJsonAsync("/api/schedules", new ScheduleRequest(Guid.NewGuid(), bobAccounts.Version,
            bobAccount.Id, new DateOnly(2026, 10, 21), -7m, "Bob rent", "Due", "groceries", null));
        Assert.Equal(HttpStatusCode.OK, bobSchedule.StatusCode);
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SchedulePostingService>().ProcessAllBudgetsAsync(CancellationToken.None);
        var aliceRegister = await alice.GetFromJsonAsync<RegisterDto>($"/api/transactions?accountId={aliceAccount.Id}");
        var bobRegister = await bob.GetFromJsonAsync<RegisterDto>($"/api/transactions?accountId={bobAccount.Id}");
        Assert.Contains(aliceRegister!.Items, transaction => transaction.Payee == "Alice rent" && !transaction.IsApproved);
        Assert.Contains(bobRegister!.Items, transaction => transaction.Payee == "Bob rent" && !transaction.IsApproved);

        var alicePlanning = await alice.GetFromJsonAsync<LongTermPlanningDto>("/api/long-term-planning/2026");
        var alicePlanWrite = await alice.PutAsJsonAsync("/api/long-term-planning/2026", new LongTermPlanningUpdateDto(
            alicePlanning!.Version, "2026-11", "plannedBudgetOverride", "groceries", 400m));
        Assert.Equal(HttpStatusCode.OK, alicePlanWrite.StatusCode);
        var bobPlanning = await bob.GetFromJsonAsync<LongTermPlanningDto>("/api/long-term-planning/2026");
        var bobPlanWrite = await bob.PutAsJsonAsync("/api/long-term-planning/2026", new LongTermPlanningUpdateDto(
            bobPlanning!.Version, "2026-11", "plannedBudgetOverride", "groceries", 700m));
        Assert.Equal(HttpStatusCode.OK, bobPlanWrite.StatusCode);
        alicePlanning = await alice.GetFromJsonAsync<LongTermPlanningDto>("/api/long-term-planning/2026");
        bobPlanning = await bob.GetFromJsonAsync<LongTermPlanningDto>("/api/long-term-planning/2026");
        Assert.Equal(400m, alicePlanning!.Groups.SelectMany(group => group.Categories).Single(category => category.Id == "groceries").Months.Single(month => month.Month == "2026-11").PlannedBudgetOverride);
        Assert.Equal(700m, bobPlanning!.Groups.SelectMany(group => group.Categories).Single(category => category.Id == "groceries").Months.Single(month => month.Month == "2026-11").PlannedBudgetOverride);
        var alicePlanMonth = await alice.GetFromJsonAsync<BudgetDto>($"/api/budgets/{aliceBudget.Id}/months/2026-12");
        var bobPlanMonth = await bob.GetFromJsonAsync<BudgetDto>($"/api/budgets/{bobBudget.Id}/months/2026-12");
        Assert.Equal(400m, alicePlanMonth!.Groups.SelectMany(group => group.Categories).Single(category => category.Id == "groceries").Planned);
        Assert.Equal(700m, bobPlanMonth!.Groups.SelectMany(group => group.Categories).Single(category => category.Id == "groceries").Planned);

        bob.DefaultRequestHeaders.Remove("Cookie");
        bob.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={aliceBudget.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync("/api/accounts")).StatusCode);
    }

    [Fact]
    public async Task Account_delete_is_scoped_to_the_selected_owned_budget()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "deletealice", "deletealice@example.test");
        await CreateUserAsync(app, "deletebob", "deletebob@example.test");
        using var alice = await LoginAsync(app, "deletealice");
        using var bob = await LoginAsync(app, "deletebob");
        var aliceBudget = await CreateBudgetAsync(alice, "Alice accounts");
        var bobBudget = await CreateBudgetAsync(bob, "Bob accounts");
        alice.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={aliceBudget.Id}");
        bob.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={bobBudget.Id}");
        var aliceState = (await alice.GetFromJsonAsync<AccountsDto>("/api/accounts"))!;
        var created = await alice.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(Guid.NewGuid(), aliceState.Version, "Alice empty", new DateOnly(2026, 10, 21), 0m));
        var aliceAccount = (await created.Content.ReadFromJsonAsync<MutationDto>())!;

        // Bob's own budget does not contain Alice's account; selecting Alice's budget is forbidden. Neither writes anything.
        var bobState = (await bob.GetFromJsonAsync<AccountsDto>("/api/accounts"))!;
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync($"/api/accounts/{aliceAccount.Id}/delete", new DeleteAccountRequest(Guid.NewGuid(), bobState.Version))).StatusCode);
        bob.DefaultRequestHeaders.Remove("Cookie");
        bob.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={aliceBudget.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PostAsJsonAsync($"/api/accounts/{aliceAccount.Id}/delete", new DeleteAccountRequest(Guid.NewGuid(), aliceAccount.Version))).StatusCode);
        var aliceAccounts = (await alice.GetFromJsonAsync<AccountsDto>("/api/accounts"))!;
        Assert.Equal(aliceAccount.Version, aliceAccounts.Version);
        Assert.Equal("Alice empty", Assert.Single(aliceAccounts.Accounts).Name);

        var deleted = await alice.PostAsJsonAsync($"/api/accounts/{aliceAccount.Id}/delete", new DeleteAccountRequest(Guid.NewGuid(), aliceAccounts.Version));
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Empty((await alice.GetFromJsonAsync<AccountsDto>("/api/accounts"))!.Accounts);
    }

    [Fact]
    public async Task Owner_can_permanently_delete_a_budget_and_its_plan_only_with_current_version()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "deleter", "deleter@example.test");
        await CreateUserAsync(app, "another", "another@example.test");
        using var owner = await LoginAsync(app, "deleter");
        using var other = await LoginAsync(app, "another");
        var budget = await CreateBudgetAsync(owner, "Temporary");
        using var month = await owner.GetAsync($"/api/budgets/{budget.Id}/months/2026-10");
        Assert.Equal(HttpStatusCode.OK, month.StatusCode);
        owner.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={budget.Id}");
        var ledger = await owner.GetFromJsonAsync<AccountsDto>("/api/accounts");
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(
            Guid.NewGuid(), ledger!.Version, "Temporary checking", new DateOnly(2026, 10, 21), 25m))).StatusCode);

        using var foreignRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/budgets/{budget.Id}")
        {
            Content = JsonContent.Create(new DeleteOwnedBudgetRequest(budget.Version))
        };
        Assert.Equal(HttpStatusCode.NotFound, (await other.SendAsync(foreignRequest)).StatusCode);
        using var staleRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/budgets/{budget.Id}")
        {
            Content = JsonContent.Create(new DeleteOwnedBudgetRequest(budget.Version + 1))
        };
        Assert.Equal(HttpStatusCode.Conflict, (await owner.SendAsync(staleRequest)).StatusCode);
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/budgets/{budget.Id}")
        {
            Content = JsonContent.Create(new DeleteOwnedBudgetRequest(budget.Version))
        };
        Assert.Equal(HttpStatusCode.NoContent, (await owner.SendAsync(deleteRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/budgets/{budget.Id}/months/2026-10")).StatusCode);

        await using var db = app.CreateDb();
        Assert.False(await db.BudgetPlans.AnyAsync(x => x.Id == budget.Id));
        Assert.False(await db.Budgets.AnyAsync(x => x.BudgetId == budget.Id));
        Assert.False(await db.CategoryGroups.AnyAsync(x => x.BudgetId == budget.Id));
        Assert.False(await db.Categories.AnyAsync(x => x.BudgetId == budget.Id));
        Assert.False(await db.Accounts.IgnoreQueryFilters().AnyAsync(x => x.BudgetId == budget.Id));
        Assert.False(await db.Transactions.IgnoreQueryFilters().AnyAsync(x => x.BudgetId == budget.Id));
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
