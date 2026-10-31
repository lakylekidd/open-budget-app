using System.Text.Json;
using System.Text.RegularExpressions;
using OpenBudget.Api.Mcp;
using OpenBudget.Api.Realtime;
using static OpenBudget.Api.Mcp.S;

namespace OpenBudget.UnitTests;

public class AgentTokenSecretsTests
{
    [Fact]
    public void Generated_tokens_have_prefix_256_bits_and_store_only_a_sha256_hash()
    {
        var token = AgentTokenSecrets.Generate();
        Assert.Matches("^obk_[A-Za-z0-9_-]{43}$", token.Plaintext);
        Assert.Equal(32, Convert.FromBase64String(token.Plaintext[4..].Replace('-', '+').Replace('_', '/') + "=").Length);
        Assert.Equal(token.Plaintext[..12], token.DisplayPrefix);
        Assert.Matches("^[0-9a-f]{64}$", token.Hash);
        Assert.DoesNotContain(token.Plaintext[4..], token.Hash);
        Assert.Equal(AgentTokenSecrets.Hash(token.Plaintext), token.Hash);
    }

    [Fact]
    public void Tokens_are_unique()
    {
        var tokens = Enumerable.Range(0, 200).Select(_ => AgentTokenSecrets.Generate().Plaintext).ToHashSet();
        Assert.Equal(200, tokens.Count);
    }

    [Fact]
    public void Verification_accepts_only_the_exact_token()
    {
        var token = AgentTokenSecrets.Generate();
        Assert.True(AgentTokenSecrets.Verify(token.Plaintext, token.Hash));
        var tampered = token.Plaintext[..^1] + (token.Plaintext[^1] == 'A' ? 'B' : 'A');
        Assert.False(AgentTokenSecrets.Verify(tampered, token.Hash));
        Assert.False(AgentTokenSecrets.Verify(token.Plaintext.ToUpperInvariant(), token.Hash));
        Assert.False(AgentTokenSecrets.Verify(token.Plaintext, token.Hash[..63]));
        Assert.False(AgentTokenSecrets.Verify(AgentTokenSecrets.Generate().Plaintext, token.Hash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("obk_")]
    [InlineData("ghp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("obk_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("obk_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("obk_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa=a")]
    [InlineData("obk_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa a")]
    public void Malformed_tokens_are_rejected_before_any_lookup(string? token) => Assert.False(AgentTokenSecrets.HasValidShape(token));

    [Theory]
    [InlineData("Bearer abc", "abc")]
    [InlineData("bearer  abc ", "abc")]
    [InlineData("Basic abc", null)]
    [InlineData("Bearer ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Bearer_header_parsing(string? header, string? expected) => Assert.Equal(expected, AgentTokenSecrets.ReadBearer(header));

    [Fact]
    public void Oversized_authorization_headers_are_ignored() => Assert.Null(AgentTokenSecrets.ReadBearer("Bearer " + new string('a', 600)));
}

public class AgentTokenLifetimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Omitted_lifetime_defaults_to_a_dated_expiry_and_never_infers_no_expiration()
    {
        Assert.True(AgentTokenLifetime.TryResolve(null, null, currentlyNeverExpires: false, Now, out var expiresAt, out _));
        Assert.Equal(Now.AddDays(AgentTokenService.DefaultExpiryDays), expiresAt);
        Assert.True(AgentTokenLifetime.TryResolve(null, false, currentlyNeverExpires: true, Now, out expiresAt, out _));
        Assert.Equal(Now.AddDays(AgentTokenService.DefaultExpiryDays), expiresAt);
    }

    [Fact]
    public void Only_explicit_never_expires_produces_no_expiration_and_renewal_keeps_the_mode()
    {
        Assert.True(AgentTokenLifetime.TryResolve(null, true, currentlyNeverExpires: false, Now, out var expiresAt, out _));
        Assert.Null(expiresAt);
        Assert.True(AgentTokenLifetime.TryResolve(null, null, currentlyNeverExpires: true, Now, out expiresAt, out _));
        Assert.Null(expiresAt);
        Assert.True(AgentTokenLifetime.TryResolve(30, null, currentlyNeverExpires: true, Now, out expiresAt, out _));
        Assert.Equal(Now.AddDays(30), expiresAt);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(366, null)]
    [InlineData(-1, false)]
    [InlineData(30, true)]
    public void Invalid_or_contradictory_lifetimes_are_rejected(int days, bool? neverExpires)
    {
        Assert.False(AgentTokenLifetime.TryResolve(days, neverExpires, currentlyNeverExpires: false, Now, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Null_expiry_stays_active_but_revoked_and_expired_tokens_do_not()
    {
        Assert.True(AgentTokenLifetime.IsActive(new AgentTokenEntity { ExpiresAt = null }, Now.AddYears(50)));
        Assert.False(AgentTokenLifetime.IsActive(new AgentTokenEntity { ExpiresAt = null, RevokedAt = Now }, Now));
        Assert.False(AgentTokenLifetime.IsActive(new AgentTokenEntity { ExpiresAt = Now }, Now));
        Assert.True(AgentTokenLifetime.IsActive(new AgentTokenEntity { ExpiresAt = Now.AddTicks(1) }, Now));
        Assert.Equal("expired", AgentTokenLifetime.Status(new AgentTokenEntity { ExpiresAt = Now }, Now));
        Assert.Equal("revoked", AgentTokenLifetime.Status(new AgentTokenEntity { ExpiresAt = null, RevokedAt = Now }, Now));
        Assert.Equal("active", AgentTokenLifetime.Status(new AgentTokenEntity { ExpiresAt = null }, Now));
    }

    [Fact]
    public void Audit_actions_record_the_expiry_mode()
    {
        Assert.Equal("agent-token.create.budget", AgentTokenLifetime.AuditAction("agent-token.create.budget", Now));
        Assert.Equal("agent-token.create.budget.no-expiry", AgentTokenLifetime.AuditAction("agent-token.create.budget", null));
        Assert.Equal("agent-token.renew.no-expiry", AgentTokenLifetime.AuditAction("agent-token.renew", null));
    }
}

public class McpPermissionMatrixTests
{
    [Fact]
    public void Reporting_tokens_only_get_read_only_tools()
    {
        var tools = McpToolCatalog.For(AgentPermission.Reporting).ToList();
        Assert.NotEmpty(tools);
        Assert.All(tools, tool => Assert.True(tool.ReadOnly, tool.Name));
        Assert.All(McpToolCatalog.All.Where(x => !x.ReadOnly), tool => Assert.False(McpPermissions.CanUse(AgentPermission.Reporting, tool.Permissions), tool.Name));
    }

    [Fact]
    public void Budget_tokens_get_plan_reads_and_assignment_writes_only()
    {
        var names = McpToolCatalog.For(AgentPermission.Budget).Select(x => x.Name).Order().ToList();
        Assert.Equal(["budgets_list", "category_targets_get", "plan_get_month", "plan_move_money", "plan_recent_actions", "plan_set_assigned"], names);
        Assert.All(McpToolCatalog.For(AgentPermission.Budget), tool => Assert.False(tool.Destructive, tool.Name));
    }

    [Fact]
    public void Admin_tokens_get_every_tool_and_admin_only_tools_are_not_budget_scoped_to_other_levels()
    {
        Assert.Equal(McpToolCatalog.All.Count, McpToolCatalog.For(AgentPermission.Admin).Count());
        foreach (var name in new[] { "users_list", "users_suspend", "admin_settings_email_update", "budgets_delete", "sharing_invite", "transactions_create" })
        {
            var tool = McpToolCatalog.Find(name)!;
            Assert.Equal([AgentPermission.Admin], tool.Permissions.ToArray());
        }
    }

    [Theory]
    [InlineData(AgentPermission.Reporting, "b1", true)]
    [InlineData(AgentPermission.Reporting, "b2", false)]
    [InlineData(AgentPermission.Budget, "b1", true)]
    [InlineData(AgentPermission.Budget, "B1", false)]
    [InlineData(AgentPermission.Admin, "anything", true)]
    public void Budget_scope_is_exact_for_scoped_tokens(AgentPermission level, string budgetId, bool expected) =>
        Assert.Equal(expected, McpPermissions.BudgetInTokenScope(level, ["b1"], budgetId));

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Budget", true)]
    [InlineData("Reporting", true)]
    [InlineData("admin", false)]
    [InlineData("2", false)]
    [InlineData("Owner", false)]
    [InlineData(null, false)]
    public void Permission_parsing_is_strict(string? value, bool ok) => Assert.Equal(ok, McpPermissions.TryParse(value, out _));

    [Fact]
    public void Catalog_names_hints_and_confirmation_are_consistent()
    {
        Assert.True(McpToolCatalog.All.Count >= 70);
        Assert.Equal(McpToolCatalog.All.Count, McpToolCatalog.All.Select(x => x.Name).Distinct().Count());
        foreach (var tool in McpToolCatalog.All)
        {
            Assert.Matches("^[a-z]+(_[a-z]+)+$", tool.Name);
            Assert.True(tool.Name.Length <= 64);
            Assert.False(tool.ReadOnly && tool.Destructive, tool.Name);
            Assert.Contains("Requires a", tool.Description);
            var properties = tool.InputSchema["properties"]!.AsObject();
            Assert.Equal(tool.Destructive, properties.ContainsKey("confirm"));
            if (tool.BudgetScoped) Assert.True(properties.ContainsKey("budgetId"), tool.Name);
            Assert.False(tool.InputSchema["additionalProperties"]!.GetValue<bool>());
            var empty = JsonSerializer.SerializeToElement(new Dictionary<string, object>());
            if (tool.Destructive && tool.NeedsConfirmation is null) Assert.True(tool.RequiresConfirm(empty), tool.Name);
        }
        foreach (var name in new[] { "transactions_delete", "budgets_delete", "budgets_archive", "users_suspend", "sharing_remove_member", "sharing_cancel_invitation", "category_targets_delete", "accounts_delete" })
            Assert.True(McpToolCatalog.Find(name)!.RequiresConfirm(JsonSerializer.SerializeToElement(new { })), name);
        var review = McpToolCatalog.Find("transactions_review")!;
        Assert.False(review.RequiresConfirm(JsonSerializer.SerializeToElement(new { action = "approve" })));
        Assert.True(review.RequiresConfirm(JsonSerializer.SerializeToElement(new { action = "reject" })));
    }
}

public class McpArgumentValidatorTests
{
    private static readonly System.Text.Json.Nodes.JsonObject Schema = Obj(
        R("budgetId", BudgetId()), R("month", Month()), R("amount", Money("Amount.", -100m, 100m)),
        O("count", Int("Count.", 1, 10)), O("kind", Enum("Kind.", "a", "b")), O("id", Uuid("Id.")), O("date", Date("Date.")),
        O("flag", Bool("Flag.")), O("note", Nullable(Str("Note.", 5))),
        O("items", Array("Items.", Obj(R("name", Str("Name.", 3, 1))), 2, 1)));

    private static string? Validate(object value) => McpArgumentValidator.Validate(Schema, JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Valid_arguments_pass()
    {
        Assert.Null(Validate(new { budgetId = "b_1-x", month = "2026-10", amount = -12.34m, count = 3, kind = "a", id = Guid.NewGuid(), date = "2026-02-28", flag = true, note = (string?)null, items = new[] { new { name = "abc" } } }));
    }

    [Theory]
    [InlineData("""{"month":"2026-10","amount":1}""", "arguments.budgetId is required.")]
    [InlineData("""{"budgetId":"b","month":"2026-13","amount":1}""", "arguments.month has an invalid format.")]
    [InlineData("""{"budgetId":"../x","month":"2026-10","amount":1}""", "arguments.budgetId has an invalid format.")]
    [InlineData("""{"budgetId":"b","month":"2026-10","amount":1.001}""", "arguments.amount must have at most two decimal places.")]
    [InlineData("""{"budgetId":"b","month":"2026-10","amount":"1"}""", "arguments.amount must be a number.")]
    [InlineData("""{"budgetId":"b","month":"2026-10","amount":101}""", "arguments.amount is above the allowed maximum.")]
    [InlineData("""{"budgetId":"b","month":"2026-10","amount":1,"count":0}""", "arguments.count must be at least 1.")]
    [InlineData("""{"budgetId":"b","month":"2026-10","amount":1,"count":1.5}""", "arguments.count must be an integer.")]
    [InlineData("""{"budgetId":"b","month":"2026-10","amount":1,"kind":"c"}""", "arguments.kind must be one of: a, b.")]
    [InlineData("""{"budgetId":"b","month":"2026-10","amount":1,"id":"nope"}""", "arguments.id must be at least 36 characters.")]
    [InlineData("""{"budgetId":"b","month":"2026-10","amount":1,"date":"2026-02-30"}""", "arguments.date must be a valid date.")]
    [InlineData("""{"budgetId":"b","month":"2026-10","amount":1,"flag":"true"}""", "arguments.flag must be true or false.")]
    [InlineData("""{"budgetId":"b","month":"2026-10","amount":1,"note":"toolong"}""", "arguments.note must be at most 5 characters.")]
    [InlineData("""{"budgetId":"b","month":"2026-10","amount":1,"note":"a\u0000"}""", "arguments.note contains control characters.")]
    [InlineData("""{"budgetId":"b","month":"2026-10","amount":1,"items":[]}""", "arguments.items must contain at least 1 items.")]
    [InlineData("""{"budgetId":"b","month":"2026-10","amount":1,"items":[{"name":"abcd"}]}""", "arguments.items[0].name must be at most 3 characters.")]
    [InlineData("""{"budgetId":"b","month":"2026-10","amount":1,"items":[{"name":"a","x":1}]}""", "arguments.items[0] has an unknown property 'x'.")]
    [InlineData("""{"budgetId":"b","month":"2026-10","amount":1,"extra":1}""", "arguments has an unknown property 'extra'.")]
    [InlineData("""{"budgetId":null,"month":"2026-10","amount":1}""", "arguments.budgetId must not be null.")]
    [InlineData("""[]""", "arguments must be an object.")]
    public void Invalid_arguments_are_rejected_with_a_path_but_no_values(string json, string expected)
    {
        var error = McpArgumentValidator.Validate(Schema, JsonDocument.Parse(json).RootElement);
        Assert.Equal(expected, error);
    }

    [Fact]
    public void Missing_required_enum_arguments_list_the_valid_values() =>
        Assert.Equal("arguments.kind is required; use one of: a, b.",
            McpArgumentValidator.Validate(Obj(R("kind", Enum("Kind.", "a", "b"))), JsonDocument.Parse("{}").RootElement));

    [Fact]
    public void Account_tools_expose_every_account_type_the_web_app_offers()
    {
        string[] webAppTypes = ["checking", "savings", "cash", "creditCard", "lineOfCredit", "mortgage", "autoLoan", "studentLoan", "personalLoan", "medicalDebt", "otherDebt", "contract"];
        foreach (var name in new[] { "accounts_create", "accounts_update" })
        {
            var properties = McpToolCatalog.Find(name)!.InputSchema["properties"]!.AsObject();
            Assert.Equal(webAppTypes, properties["accountType"]!["enum"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
            Assert.True(properties.ContainsKey("loan"), name);
        }
        var create = McpToolCatalog.Find("accounts_create")!;
        Assert.Contains("accountType", create.InputSchema["required"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Contains("mortgage", create.Description);
    }

    [Fact]
    public void Every_catalog_schema_rejects_unknown_properties()
    {
        foreach (var tool in McpToolCatalog.All)
        {
            var error = McpArgumentValidator.Validate(tool.InputSchema, JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["unexpected_property"] = 1 }));
            Assert.NotNull(error);
        }
    }

    [Theory]
    [InlineData("default", true)]
    [InlineData("a1b2_c-3", true)]
    [InlineData("", false)]
    [InlineData("../etc", false)]
    [InlineData("a b", false)]
    public void Budget_ids_are_validated(string id, bool expected) => Assert.Equal(expected, McpBudgetIds.IsValid(id));
}

public class BudgetChangeCollapseTests
{
    [Fact]
    public void Events_collapse_per_budget_kind_and_month_keeping_the_highest_version()
    {
        var at = DateTimeOffset.UnixEpoch;
        var events = BudgetChangeInterceptor.Collapse([
            new("b", "plan", "2026-10", 3, at), new("b", "plan", "2026-10", null, at), new("b", "plan", "2026-10", 4, at),
            new("b", "ledger", null, 9, at), new("b", "ledger", null, null, at), new("c", "plan", "2026-10", 1, at)
        ]);
        Assert.Equal(3, events.Count);
        Assert.Equal(4, events.Single(x => x.BudgetId == "b" && x.Kind == "plan").Version);
        Assert.Equal(9, events.Single(x => x.Kind == "ledger").Version);
    }

    [Fact]
    public void Large_recalculations_become_one_budget_wide_plan_event()
    {
        var at = DateTimeOffset.UnixEpoch;
        var months = Enumerable.Range(0, 30).Select(i => new BudgetChangeEvent("b", "plan", $"20{20 + i / 12}-{i % 12 + 1:D2}", i, at));
        var plan = Assert.Single(BudgetChangeInterceptor.Collapse(months));
        Assert.Null(plan.Month);
        Assert.Null(plan.Version);
    }

    [Fact]
    public void Event_payload_has_no_financial_fields() =>
        Assert.Equal(["At", "BudgetId", "Kind", "Month", "Version"], typeof(BudgetChangeEvent).GetProperties().Select(x => x.Name).Order().ToArray());
}
