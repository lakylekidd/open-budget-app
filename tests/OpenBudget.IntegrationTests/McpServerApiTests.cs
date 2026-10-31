using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenBudget.Api;
using OpenBudget.Api.Mcp;
using static OpenBudget.IntegrationTests.McpTestKit;

namespace OpenBudget.IntegrationTests;

public sealed class McpServerApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Month = "2026-10";

    [Fact]
    public async Task Initialize_and_tools_list_are_filtered_by_token_permission()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "owner", administrator: true);
        using var owner = await LoginAsync(app, "owner");
        var budget = await CreateBudgetAsync(owner, "Household");

        var admin = await CreateTokenAsync(owner, "Admin");
        var planner = await CreateTokenAsync(owner, "Budget", budget.Id);
        var reader = await CreateTokenAsync(owner, "Reporting", budget.Id);
        Assert.StartsWith("obk_", admin.Token);

        using var adminMcp = Mcp(app, admin.Token);
        var init = await adminMcp.InitializeAsync();
        Assert.Equal("openbudget", init.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.True(init.GetProperty("result").GetProperty("capabilities").TryGetProperty("tools", out _));

        var adminTools = await adminMcp.ListToolsAsync();
        Assert.Equal(McpToolCatalog.All.Count, adminTools.GetArrayLength());
        var deleteTool = adminTools.EnumerateArray().Single(x => x.GetProperty("name").GetString() == "transactions_delete");
        Assert.True(deleteTool.GetProperty("annotations").GetProperty("destructiveHint").GetBoolean());
        Assert.False(deleteTool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
        Assert.Contains("confirm", deleteTool.GetProperty("inputSchema").GetProperty("properties").EnumerateObject().Select(x => x.Name));
        Assert.Equal("Admin", deleteTool.GetProperty("_meta").GetProperty("openbudget/permissions")[0].GetString());

        using var plannerMcp = Mcp(app, planner.Token);
        var plannerTools = await plannerMcp.ListToolNamesAsync();
        Assert.Equal(["budgets_list", "category_targets_get", "plan_get_month", "plan_move_money", "plan_recent_actions", "plan_set_assigned"], plannerTools.Order().ToList());
        using var readerMcp = Mcp(app, reader.Token);
        var readerTools = await readerMcp.ListToolsAsync();
        Assert.All(readerTools.EnumerateArray(), tool => Assert.True(tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean()));
        Assert.Contains(readerTools.EnumerateArray(), tool => tool.GetProperty("name").GetString() == "transactions_list");
        Assert.DoesNotContain(readerTools.EnumerateArray(), tool => tool.GetProperty("name").GetString() is "plan_set_assigned" or "sharing_get" or "users_list");
    }

    [Fact]
    public async Task Agent_tokens_are_created_once_listed_without_secrets_revoked_and_expire_immediately()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "owner");
        await CreateUserAsync(app, "other");
        using var owner = await LoginAsync(app, "owner");
        using var other = await LoginAsync(app, "other");
        var budget = await CreateBudgetAsync(owner, "Household");
        var foreign = await CreateBudgetAsync(other, "Not yours");

        // Non-administrators cannot mint Admin tokens; budgets must be the creator's own or shared with them.
        var adminAttempt = await owner.PostAsJsonAsync("/api/auth/agent-tokens", new CreateAgentTokenRequest("root", "Admin", [], null));
        Assert.Equal(HttpStatusCode.Forbidden, adminAttempt.StatusCode);
        var foreignAttempt = await owner.PostAsJsonAsync("/api/auth/agent-tokens", new CreateAgentTokenRequest("peek", "Reporting", [foreign.Id], null));
        var unknownAttempt = await owner.PostAsJsonAsync("/api/auth/agent-tokens", new CreateAgentTokenRequest("peek", "Reporting", ["does-not-exist"], null));
        Assert.Equal(HttpStatusCode.BadRequest, foreignAttempt.StatusCode);
        Assert.Equal(await unknownAttempt.Content.ReadAsStringAsync(), await foreignAttempt.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/auth/agent-tokens", new CreateAgentTokenRequest("x", "Budget", [], null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/auth/agent-tokens", new CreateAgentTokenRequest("x", "Budget", [budget.Id], 366))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/auth/agent-tokens", new CreateAgentTokenRequest("", "Budget", [budget.Id], null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/auth/agent-tokens", new CreateAgentTokenRequest("x", "Owner", [budget.Id], null))).StatusCode);

        var created = await owner.PostAsJsonAsync("/api/auth/agent-tokens", new CreateAgentTokenRequest("Laptop agent", "Reporting", [budget.Id], null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("no-store", created.Headers.CacheControl?.ToString());
        var token = (await created.Content.ReadFromJsonAsync<CreatedAgentTokenDto>())!;
        Assert.Matches("^obk_[A-Za-z0-9_-]{43}$", token.Token);
        Assert.Equal(token.Token[..12], token.Details.Prefix);
        Assert.Equal(app.Clock.UtcNow.AddDays(90), token.Details.ExpiresAt);
        Assert.Equal("Household", Assert.Single(token.Details.Budgets).Name);

        var listing = await owner.GetStringAsync("/api/auth/agent-tokens");
        Assert.DoesNotContain(token.Token, listing);
        Assert.DoesNotContain(AgentTokenSecrets.Hash(token.Token), listing);
        Assert.Equal("[]", await other.GetStringAsync("/api/auth/agent-tokens"));
        await using (var db = app.CreateDb())
        {
            var stored = await db.Set<AgentTokenEntity>().SingleAsync();
            Assert.Equal(AgentTokenSecrets.Hash(token.Token), stored.TokenHash);
            Assert.NotEqual(token.Token, stored.TokenHash);
        }

        using var mcp = Mcp(app, token.Token);
        (await mcp.CallAsync("budgets_list")).AssertOk();
        await using (var db = app.CreateDb()) Assert.NotNull((await db.Set<AgentTokenEntity>().SingleAsync()).LastUsedAt);

        // Revocation by another user is a 404; by the owner it is immediate.
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/auth/agent-tokens/{token.Details.Id}/revoke", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/auth/agent-tokens/{token.Details.Id}/revoke", null)).StatusCode);
        using (var revoked = await mcp.PostRawAsync("tools/list", null)) Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        Assert.Contains("\"status\":\"revoked\"", await owner.GetStringAsync("/api/auth/agent-tokens"));

        // Expiry uses server time.
        var shortLived = await owner.PostAsJsonAsync("/api/auth/agent-tokens", new CreateAgentTokenRequest("Short", "Budget", [budget.Id], 1));
        var shortToken = (await shortLived.Content.ReadFromJsonAsync<CreatedAgentTokenDto>())!;
        using var shortMcp = Mcp(app, shortToken.Token);
        (await shortMcp.CallAsync("plan_get_month", new { budgetId = budget.Id, month = Month })).AssertOk();
        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(1).AddSeconds(1);
        using (var expired = await shortMcp.PostRawAsync("tools/list", null)) Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    [Fact]
    public async Task Mcp_accepts_only_bearer_tokens_and_denies_suspended_or_demoted_creators()
    {
        await using var app = await fixture.CreateAppAsync();
        var adminId = await CreateUserAsync(app, "root", administrator: true);
        var memberId = await CreateUserAsync(app, "member");
        using var root = await LoginAsync(app, "root");
        using var member = await LoginAsync(app, "member");
        var budget = await CreateBudgetAsync(member, "Member budget");
        var adminToken = await CreateTokenAsync(root, "Admin");
        var memberToken = await CreateTokenAsync(member, "Reporting", budget.Id);

        // A valid session cookie is not accepted on /mcp, and the token is not accepted on the cookie-only REST API.
        var cookieOnly = await member.PostAsync("/mcp", new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, cookieOnly.StatusCode);
        Assert.Contains("Bearer", cookieOnly.Headers.WwwAuthenticate.ToString());
        using var bearerRest = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        bearerRest.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await bearerRest.GetAsync("/api/auth/agent-tokens")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await bearerRest.GetAsync($"/api/budgets/{budget.Id}/events")).StatusCode);

        using var anonymous = Mcp(app, null);
        using (var none = await anonymous.PostRawAsync("tools/list", null)) Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);
        using (var malformed = await anonymous.PostRawAsync("tools/list", null, "not-a-token")) Assert.Equal(HttpStatusCode.Unauthorized, malformed.StatusCode);
        using (var forged = await anonymous.PostRawAsync("tools/list", null, AgentTokenSecrets.Generate().Plaintext)) Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);

        using var memberMcp = Mcp(app, memberToken.Token);
        (await memberMcp.CallAsync("budgets_list")).AssertOk();
        Assert.Equal(HttpStatusCode.NoContent, (await root.PostAsync($"/api/admin/users/{memberId}/suspend", null)).StatusCode);
        using (var suspended = await memberMcp.PostRawAsync("tools/list", null)) Assert.Equal(HttpStatusCode.Unauthorized, suspended.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await root.PostAsync($"/api/admin/users/{memberId}/reactivate", null)).StatusCode);
        (await memberMcp.CallAsync("budgets_list")).AssertOk();

        using var adminMcp = Mcp(app, adminToken.Token);
        var users = await adminMcp.CallAsync("users_list");
        users.AssertOk();
        Assert.Equal(2, users.Structured().GetProperty("items").GetArrayLength());
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<OpenBudgetUser>>();
            await manager.RemoveFromRoleAsync((await manager.FindByIdAsync(adminId))!, AuthenticationSetup.AdministratorRole);
        }
        using (var demoted = await adminMcp.PostRawAsync("tools/list", null)) Assert.Equal(HttpStatusCode.Unauthorized, demoted.StatusCode);
    }

    [Fact]
    public async Task Budget_token_changes_assignments_only_within_scope_and_surfaces_conflicts()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "owner");
        await CreateUserAsync(app, "other");
        using var owner = await LoginAsync(app, "owner");
        using var other = await LoginAsync(app, "other");
        var budget = await CreateBudgetAsync(owner, "Household");
        var otherBudget = await OwnSecondBudgetAsync(app, owner, "owner", "Side budget");
        var foreign = await CreateBudgetAsync(other, "Foreign");
        var token = await CreateTokenAsync(owner, "Budget", budget.Id);
        using var mcp = Mcp(app, token.Token);

        var month = await mcp.CallAsync("plan_get_month", new { budgetId = budget.Id, month = Month });
        month.AssertOk();
        var version = month.Structured().GetProperty("version").GetInt64();

        var set = await mcp.CallAsync("plan_set_assigned", new { budgetId = budget.Id, month = Month, categoryId = "groceries", assigned = 125.50m });
        set.AssertOk();
        Assert.Equal(125.50m, set.Structured().GetProperty("categories")[0].GetProperty("assigned").GetDecimal());
        var reloaded = await owner.GetFromJsonAsync<BudgetDto>($"/api/budgets/{budget.Id}/months/{Month}");
        Assert.Equal(125.50m, reloaded!.Groups.SelectMany(g => g.Categories).Single(c => c.Id == "groceries").Assigned);

        var move = await mcp.CallAsync("plan_move_money", new { budgetId = budget.Id, month = Month, fromCategoryId = "groceries", toCategoryId = "dining", amount = 20.25m });
        move.AssertOk();
        reloaded = await owner.GetFromJsonAsync<BudgetDto>($"/api/budgets/{budget.Id}/months/{Month}");
        Assert.Equal(105.25m, reloaded!.Groups.SelectMany(g => g.Categories).Single(c => c.Id == "groceries").Assigned);
        Assert.Equal(20.25m, reloaded.Groups.SelectMany(g => g.Categories).Single(c => c.Id == "dining").Assigned);
        var actions = await owner.GetStringAsync($"/api/budgets/{budget.Id}/plan-actions?kind=moved");
        Assert.Contains("Dining out", actions);

        // Stale caller version: the server's 409 is surfaced with the current month, and nothing changes.
        var stale = await mcp.CallAsync("plan_set_assigned", new { budgetId = budget.Id, month = Month, categoryId = "groceries", assigned = 1m, version });
        Assert.True(stale.IsError());
        Assert.Equal("conflict", stale.ErrorCode());
        Assert.True(stale.Structured().GetProperty("error").GetProperty("current").GetProperty("version").GetInt64() > version);
        reloaded = await owner.GetFromJsonAsync<BudgetDto>($"/api/budgets/{budget.Id}/months/{Month}");
        Assert.Equal(105.25m, reloaded!.Groups.SelectMany(g => g.Categories).Single(c => c.Id == "groceries").Assigned);

        // Out-of-scope, foreign and unknown budgets are indistinguishable.
        var outOfScope = await mcp.CallAsync("plan_get_month", new { budgetId = otherBudget.Id, month = Month });
        var foreignResult = await mcp.CallAsync("plan_get_month", new { budgetId = foreign.Id, month = Month });
        var unknown = await mcp.CallAsync("plan_get_month", new { budgetId = "no-such-budget", month = Month });
        Assert.Equal("not_found", outOfScope.ErrorCode());
        Assert.Equal(unknown.Structured().GetRawText(), outOfScope.Structured().GetRawText());
        Assert.Equal(unknown.Structured().GetRawText(), foreignResult.Structured().GetRawText());
        var listed = (await mcp.CallAsync("budgets_list")).Structured().GetProperty("budgets");
        Assert.Equal(budget.Id, Assert.Single(listed.EnumerateArray()).GetProperty("id").GetString());

        // A Budget token cannot write the ledger, even though its creator could.
        var transaction = await mcp.CallRawAsync("transactions_create", new { budgetId = budget.Id, accountId = Guid.NewGuid(), date = "2026-10-05", amount = -10m });
        Assert.Equal(-32602, transaction.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Contains("Unknown tool", transaction.GetProperty("error").GetProperty("message").GetString());

        // Invalid arguments are rejected before any service runs.
        var invalid = await mcp.CallAsync("plan_set_assigned", new { budgetId = budget.Id, month = "2026-13", categoryId = "groceries", assigned = 1.234m });
        Assert.Equal("invalid_arguments", invalid.ErrorCode());
        var extra = await mcp.CallAsync("plan_set_assigned", new { budgetId = budget.Id, month = Month, categoryId = "groceries", assigned = 1m, note = "x" });
        Assert.Equal("invalid_arguments", extra.ErrorCode());

        // Losing access to a shared budget denies the token immediately.
        await using (var db = app.CreateDb())
        {
            var plan = await db.BudgetPlans.SingleAsync(x => x.Id == budget.Id);
            plan.IsArchived = true;
            await db.SaveChangesAsync();
        }
        Assert.Equal("not_found", (await mcp.CallAsync("plan_get_month", new { budgetId = budget.Id, month = Month })).ErrorCode());
    }

    [Fact]
    public async Task Reporting_and_budget_tokens_cannot_call_any_write_tool()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "root", administrator: true);
        using var root = await LoginAsync(app, "root");
        var budget = await CreateBudgetAsync(root, "Household");
        using var adminMcp = Mcp(app, (await CreateTokenAsync(root, "Admin")).Token);
        var account = await adminMcp.CallAsync("accounts_create", new { budgetId = budget.Id, name = "Checking", accountType = "checking", date = "2026-10-01", openingBalance = 500m });
        account.AssertOk();
        var reader = await CreateTokenAsync(root, "Reporting", budget.Id);
        var planner = await CreateTokenAsync(root, "Budget", budget.Id);
        using var readerMcp = Mcp(app, reader.Token);
        using var plannerMcp = Mcp(app, planner.Token);
        var before = await SnapshotAsync(adminMcp, budget.Id);

        var writeTools = McpToolCatalog.All.Where(x => !x.ReadOnly).ToList();
        Assert.True(writeTools.Count > 40);
        foreach (var tool in writeTools)
        {
            var arguments = new Dictionary<string, object?> { ["budgetId"] = budget.Id, ["confirm"] = true };
            var response = await readerMcp.CallRawAsync(tool.Name, arguments);
            Assert.True(response.TryGetProperty("error", out var error), $"{tool.Name} was callable by a Reporting token");
            Assert.Contains("Unknown tool", error.GetProperty("message").GetString());
            if (tool.Name is "plan_set_assigned" or "plan_move_money") continue;
            var plannerResponse = await plannerMcp.CallRawAsync(tool.Name, arguments);
            Assert.True(plannerResponse.TryGetProperty("error", out _), $"{tool.Name} was callable by a Budget token");
        }
        Assert.Equal(before, await SnapshotAsync(adminMcp, budget.Id));

        // Every readable area works for Reporting.
        foreach (var (name, args) in new (string, object)[]
        {
            ("accounts_list", new { budgetId = budget.Id }), ("transactions_list", new { budgetId = budget.Id, pageSize = 10 }),
            ("payees_list", new { budgetId = budget.Id }), ("schedules_list", new { budgetId = budget.Id }), ("flags_list", new { budgetId = budget.Id }),
            ("long_term_planning_get_year", new { budgetId = budget.Id, year = 2026 }), ("ledger_audit_list", new { budgetId = budget.Id }),
            ("category_targets_get", new { budgetId = budget.Id, categoryId = "groceries", month = Month }), ("plan_recent_actions", new { budgetId = budget.Id })
        })
            (await readerMcp.CallAsync(name, args)).AssertOk();

        await using var db = app.CreateDb();
        var denied = await db.Set<AgentAuditLogEntity>().Where(x => x.Outcome == "denied").CountAsync();
        Assert.True(denied >= writeTools.Count);
    }

    [Fact]
    public async Task Admin_token_covers_every_area_with_confirmation_and_ledger_conflicts()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "root", administrator: true);
        using var root = await LoginAsync(app, "root");
        var budget = await CreateBudgetAsync(root, "Household");
        using var mcp = Mcp(app, (await CreateTokenAsync(root, "Admin")).Token);
        var b = budget.Id;

        var checking = await mcp.CallAsync("accounts_create", new { budgetId = b, name = "Checking", accountType = "checking", date = "2026-10-01", openingBalance = 1000m });
        checking.AssertOk();
        var checkingId = checking.Structured().GetProperty("id").GetString()!;
        var card = await mcp.CallAsync("accounts_create", new { budgetId = b, name = "Card", accountType = "creditCard", date = "2026-10-01", openingBalance = -200m });
        card.AssertOk();
        var cardId = card.Structured().GetProperty("id").GetString()!;
        var savings = await mcp.CallAsync("accounts_create", new { budgetId = b, name = "Savings", accountType = "savings", date = "2026-10-01" });
        var savingsId = savings.Structured().GetProperty("id").GetString()!;

        var spend = await mcp.CallAsync("transactions_create", new { budgetId = b, accountId = checkingId, date = "2026-10-05", amount = -42.10m, payee = "Grocer", categoryId = "groceries" });
        spend.AssertOk();
        var spendId = spend.Structured().GetProperty("id").GetString()!;
        var ledgerVersion = spend.Structured().GetProperty("version").GetInt64();
        (await mcp.CallAsync("transactions_update", new { budgetId = b, transactionId = spendId, accountId = checkingId, date = "2026-10-06", amount = -40m, payee = "Grocer", categoryId = "groceries" })).AssertOk();
        (await mcp.CallAsync("transactions_set_status", new { budgetId = b, transactionId = spendId, status = "cleared" })).AssertOk();
        (await mcp.CallAsync("transactions_set_flag", new { budgetId = b, transactionId = spendId, color = "red" })).AssertOk();
        (await mcp.CallAsync("transfers_create", new { budgetId = b, fromAccountId = checkingId, toAccountId = savingsId, date = "2026-10-07", amount = 100m })).AssertOk();
        (await mcp.CallAsync("accounts_update", new { budgetId = b, accountId = savingsId, name = "Rainy day", isFavorite = true })).AssertOk();
        (await mcp.CallAsync("accounts_reorder", new { budgetId = b, accountIds = new[] { savingsId, cardId, checkingId } })).AssertOk();
        var reordered = (await mcp.CallAsync("accounts_list", new { budgetId = b })).Structured().GetProperty("accounts").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray();
        Assert.Equal([savingsId, cardId, checkingId], reordered);
        (await mcp.CallAsync("flags_rename", new { budgetId = b, flags = new[] { "red", "orange", "yellow", "green", "blue", "purple" }.Select(color => new { color, name = color == "red" ? "Review" : color }).ToArray() })).AssertOk();
        (await mcp.CallAsync("credit_payoff_target_save", new { budgetId = b, accountId = cardId, kind = "monthlyAmount", monthlyAmount = 50m })).AssertOk();
        (await mcp.CallAsync("credit_payoff_target_get", new { budgetId = b, accountId = cardId, month = Month })).AssertOk();
        (await mcp.CallAsync("accounts_credit_summary", new { budgetId = b, accountId = cardId, month = Month })).AssertOk();
        var schedule = await mcp.CallAsync("schedules_create", new { budgetId = b, accountId = checkingId, date = "2026-11-01", amount = -800m, payee = "Landlord", categoryId = "rent", repeat = "monthly" });
        schedule.AssertOk();
        Assert.Equal(1, (await mcp.CallAsync("schedules_list", new { budgetId = b })).Structured().GetProperty("total").GetInt32());
        (await mcp.CallAsync("category_targets_save", new { budgetId = b, categoryId = "rent", month = Month, kind = "monthly", amount = 800m, behavior = "setAside" })).AssertOk();
        (await mcp.CallAsync("categories_create", new { budgetId = b, month = Month, groupId = "home", name = "Insurance" })).AssertOk();
        (await mcp.CallAsync("plan_set_note", new { budgetId = b, month = Month, note = "Agent reviewed" })).AssertOk();
        (await mcp.CallAsync("long_term_planning_set_override", new { budgetId = b, month = "2027-03", categoryId = "rent", amount = 850m })).AssertOk();
        (await mcp.CallAsync("sharing_get", new { budgetId = b })).AssertOk();
        (await mcp.CallAsync("admin_dashboard_get")).AssertOk();
        (await mcp.CallAsync("admin_settings_registration_mode_set", new { mode = "ApprovalRequired" })).AssertOk();
        Assert.Equal("ApprovalRequired", (await mcp.CallAsync("admin_settings_registration_mode_get")).Structured().GetProperty("mode").GetString());
        var email = await mcp.CallAsync("admin_settings_email_get");
        email.AssertOk();
        Assert.False(email.Structured().TryGetProperty("password", out _));
        (await mcp.CallAsync("membership_plans_list")).AssertOk();
        var renamed = await mcp.CallAsync("budgets_rename", new { budgetId = b, name = "Family" });
        renamed.AssertOk();

        // Reports reflect the writes.
        var month = await root.GetFromJsonAsync<BudgetDto>($"/api/budgets/{b}/months/{Month}");
        Assert.Equal(-40m, month!.Groups.SelectMany(g => g.Categories).Single(c => c.Id == "groceries").Activity);
        Assert.Contains(month.Groups.Single(g => g.Id == "home").Categories, c => c.Name == "Insurance");
        Assert.NotNull(month.Groups.SelectMany(g => g.Categories).Single(c => c.Id == "rent").CategoryTarget);
        Assert.Equal("Agent reviewed", month.Note);

        // Stale ledger version: conflict, no write.
        var stale = await mcp.CallAsync("transactions_create", new { budgetId = b, accountId = checkingId, date = "2026-10-08", amount = -1m, version = ledgerVersion });
        Assert.Equal("conflict", stale.ErrorCode());

        // Destructive tools refuse without confirm and leave data in place.
        var unconfirmed = await mcp.CallAsync("transactions_delete", new { budgetId = b, transactionId = spendId });
        Assert.Equal("confirmation_required", unconfirmed.ErrorCode());
        var listing = await mcp.CallAsync("transactions_list", new { budgetId = b, accountId = checkingId });
        Assert.Contains(listing.Structured().GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetString() == spendId);
        Assert.Equal("confirmation_required", (await mcp.CallAsync("transactions_bulk_update", new { budgetId = b, ids = new[] { spendId }, action = "delete" })).ErrorCode());
        Assert.Equal("confirmation_required", (await mcp.CallAsync("budgets_delete", new { budgetId = b })).ErrorCode());
        (await mcp.CallAsync("transactions_bulk_update", new { budgetId = b, ids = new[] { spendId }, action = "memo", memo = "checked" })).AssertOk();
        (await mcp.CallAsync("transactions_delete", new { budgetId = b, transactionId = spendId, confirm = true })).AssertOk();
        listing = await mcp.CallAsync("transactions_list", new { budgetId = b, accountId = checkingId });
        Assert.DoesNotContain(listing.Structured().GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetString() == spendId);

        // Admin budget lifecycle with confirmation.
        await RaiseBudgetLimitAsync(app, "root");
        var created = await mcp.CallAsync("budgets_create", new { name = "Scratch" });
        created.AssertOk();
        var scratch = created.Structured().GetProperty("id").GetString()!;
        (await mcp.CallAsync("budgets_archive", new { budgetId = scratch, confirm = true })).AssertOk();
        (await mcp.CallAsync("budgets_restore", new { budgetId = scratch })).AssertOk();
        (await mcp.CallAsync("budgets_delete", new { budgetId = scratch, confirm = true })).AssertOk();
        await using var db = app.CreateDb();
        Assert.False(await db.BudgetPlans.AnyAsync(x => x.Id == scratch));
    }

    [Fact]
    public async Task Audit_rows_record_each_call_without_arguments_and_are_visible_only_to_the_owner()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "owner");
        await CreateUserAsync(app, "other");
        using var owner = await LoginAsync(app, "owner");
        using var other = await LoginAsync(app, "other");
        var budget = await CreateBudgetAsync(owner, "Household");
        var token = await CreateTokenAsync(owner, "Budget", budget.Id);
        using var mcp = Mcp(app, token.Token);
        (await mcp.CallAsync("plan_set_assigned", new { budgetId = budget.Id, month = Month, categoryId = "groceries", assigned = 987.65m })).AssertOk();
        Assert.True((await mcp.CallAsync("plan_get_month", new { budgetId = "missing", month = Month })).IsError());
        await mcp.CallRawAsync("accounts_create", new { budgetId = budget.Id });

        await using (var db = app.CreateDb())
        {
            var rows = await db.Set<AgentAuditLogEntity>().OrderBy(x => x.CreatedAt).ThenBy(x => x.Tool).ToListAsync();
            Assert.Equal(3, rows.Count);
            Assert.All(rows, row => Assert.Equal(token.Details.Id, row.TokenId));
            var write = rows.Single(x => x.Tool == "plan_set_assigned");
            Assert.True(write.Success);
            Assert.Equal(budget.Id, write.BudgetId);
            Assert.Equal("not_found", rows.Single(x => x.Tool == "plan_get_month").Outcome);
            Assert.Equal("denied", rows.Single(x => x.Tool == "accounts_create").Outcome);
            var serialized = JsonSerializer.Serialize(rows);
            Assert.DoesNotContain("987", serialized);
            Assert.DoesNotContain(token.Token, serialized);
            Assert.DoesNotContain("groceries", serialized);
        }

        var activity = await owner.GetFromJsonAsync<List<AgentActivityDto>>($"/api/auth/agent-tokens/{token.Details.Id}/activity");
        Assert.Equal(3, activity!.Count);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/auth/agent-tokens/{token.Details.Id}/activity")).StatusCode);
    }

    [Fact]
    public async Task Events_stream_notifies_authorised_sessions_after_an_mcp_write_without_financial_values()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "owner");
        await CreateUserAsync(app, "stranger");
        using var owner = await LoginAsync(app, "owner");
        using var stranger = await LoginAsync(app, "stranger");
        var budget = await CreateBudgetAsync(owner, "Household");
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/budgets/{budget.Id}/events")).StatusCode);
        using var anonymous = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/budgets/{budget.Id}/events")).StatusCode);
        // Ensure the month exists before subscribing so the only event is the agent's write.
        await owner.GetAsync($"/api/budgets/{budget.Id}/months/{Month}");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/budgets/{budget.Id}/events");
        using var response = await owner.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        Assert.Equal("retry: 5000", await reader.ReadLineAsync());

        var token = await CreateTokenAsync(owner, "Budget", budget.Id);
        using var mcp = Mcp(app, token.Token);
        (await mcp.CallAsync("plan_set_assigned", new { budgetId = budget.Id, month = Month, categoryId = "groceries", assigned = 314.15m })).AssertOk();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        string? data = null;
        while (data is null)
        {
            var line = await reader.ReadLineAsync(timeout.Token);
            Assert.NotNull(line);
            if (line.StartsWith("data: ")) data = line[6..];
        }
        var change = JsonDocument.Parse(data).RootElement;
        Assert.Equal(budget.Id, change.GetProperty("budgetId").GetString());
        Assert.Equal("plan", change.GetProperty("kind").GetString());
        Assert.Equal(Month, change.GetProperty("month").GetString());
        Assert.True(change.GetProperty("version").GetInt64() > 1);
        Assert.DoesNotContain("314", data);
        Assert.Equal(["at", "budgetId", "kind", "month", "version"], change.EnumerateObject().Select(x => x.Name).Order().ToList());
    }

    [Fact]
    public async Task Mcp_is_rate_limited_per_token()
    {
        await using var app = await fixture.CreateAppAsync();
        await using var limited = app.WithWebHostBuilder(builder => builder.UseSetting("Mcp:RateLimit:PermitLimit", "3"));
        await CreateUserAsync(limited, "owner");
        using var owner = await LoginAsync(limited, "owner");
        var budget = await CreateBudgetAsync(owner, "Household");
        var first = await CreateTokenAsync(owner, "Reporting", budget.Id);
        var second = await CreateTokenAsync(owner, "Reporting", budget.Id);
        using var mcp = Mcp(limited, first.Token);
        for (var i = 0; i < 3; i++)
            using (var ok = await mcp.PostRawAsync("tools/list", null)) Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        using (var limitedResponse = await mcp.PostRawAsync("tools/list", null))
        {
            Assert.Equal((HttpStatusCode)429, limitedResponse.StatusCode);
            Assert.True(limitedResponse.Headers.Contains("Retry-After"));
        }
        using var otherMcp = Mcp(limited, second.Token);
        using (var separate = await otherMcp.PostRawAsync("tools/list", null)) Assert.Equal(HttpStatusCode.OK, separate.StatusCode);
    }

    private static async Task<OwnedBudgetDto> OwnSecondBudgetAsync(TestApp app, HttpClient client, string userName, string name)
    {
        await RaiseBudgetLimitAsync(app, userName);
        return await CreateBudgetAsync(client, name);
    }

    private static async Task RaiseBudgetLimitAsync(TestApp app, string userName)
    {
        await using var db = app.CreateDb();
        var userId = await db.Users.Where(x => x.UserName == userName).Select(x => x.Id).SingleAsync();
        var membership = await db.UserMemberships.SingleOrDefaultAsync(x => x.UserId == userId);
        if (membership is null) db.UserMemberships.Add(new UserMembershipEntity { UserId = userId, MembershipPlanId = "free", BudgetLimitOverride = 5 });
        else membership.BudgetLimitOverride = 5;
        await db.SaveChangesAsync();
    }

    private static async Task<string> SnapshotAsync(McpTestClient admin, string budgetId)
    {
        var month = await admin.CallAsync("plan_get_month", new { budgetId, month = Month });
        var accounts = await admin.CallAsync("accounts_list", new { budgetId });
        var schedules = await admin.CallAsync("schedules_list", new { budgetId });
        var budgets = await admin.CallAsync("budgets_list", new { includeArchived = true });
        return string.Join("|", new[] { month, accounts, schedules, budgets }.Select(x => x.Structured().GetRawText()));
    }
}
