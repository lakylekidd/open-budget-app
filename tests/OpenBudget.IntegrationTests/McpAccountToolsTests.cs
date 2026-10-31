using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenBudget.Api;
using static OpenBudget.IntegrationTests.McpTestKit;

namespace OpenBudget.IntegrationTests;

// Account creation through MCP must produce exactly the account type the web app would create (regression: a mortgage
// requested by an agent was created as a line of credit because the tool only exposed cash and credit types).
public sealed class McpAccountToolsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Today = "2026-10-15";

    [Fact]
    public async Task Creating_a_mortgage_via_mcp_yields_a_mortgage_loan_account_with_its_terms()
    {
        await using var app = await fixture.CreateAppAsync();
        var (mcp, budgetId, root) = await AdminAsync(app);
        using (root)
        using (mcp)
        {
            var created = await mcp.CallAsync("accounts_create", new
            {
                budgetId, name = "Home loan", accountType = "mortgage", date = Today,
                loan = new { balanceOwed = 250000m, interestRate = 4.125m, minimumPayment = 1450m, escrowAmount = 250m }
            });
            created.AssertOk();
            var id = created.Structured().GetProperty("id").GetGuid();

            var account = (await AccountsAsync(mcp, budgetId)).Single(a => a.Id == id);
            Assert.Equal("mortgage", account.AccountType);
            Assert.Equal(-250000m, account.Balance);

            var loan = await mcp.CallAsync("loans_get", new { budgetId, accountId = id });
            loan.AssertOk();
            var details = loan.Structured();
            Assert.Equal("mortgage", details.GetProperty("accountType").GetString());
            Assert.Equal(4.125m, details.GetProperty("interestRate").GetDecimal());
            Assert.Equal(1450m, details.GetProperty("minimumPayment").GetDecimal());
            Assert.Equal(250m, details.GetProperty("escrowAmount").GetDecimal());
            Assert.Equal(250000m, details.GetProperty("originalBalance").GetDecimal());

            await using var db = app.CreateDb();
            Assert.False(await db.Set<CreditPayoffTargetEntity>().IgnoreQueryFilters().AnyAsync());
            Assert.Equal(1, await db.LoanTerms.IgnoreQueryFilters().CountAsync(t => t.AccountId == id));
        }
    }

    [Theory]
    [InlineData("checking", "cash")]
    [InlineData("savings", "cash")]
    [InlineData("cash", "cash")]
    [InlineData("creditCard", "credit")]
    [InlineData("lineOfCredit", "credit")]
    [InlineData("mortgage", "loan")]
    [InlineData("autoLoan", "loan")]
    [InlineData("studentLoan", "loan")]
    [InlineData("personalLoan", "loan")]
    [InlineData("medicalDebt", "loan")]
    [InlineData("otherDebt", "loan")]
    [InlineData("contract", "loan")]
    public async Task Every_account_type_the_web_app_offers_is_created_as_that_type(string accountType, string family)
    {
        await using var app = await fixture.CreateAppAsync();
        var (mcp, budgetId, root) = await AdminAsync(app);
        using (root)
        using (mcp)
        {
            object arguments = (accountType, family) switch
            {
                ("contract", _) => new { budgetId, name = "Phone", accountType, date = Today, loan = new { contract = new { months = 24, startDate = "2026-10-01", includesDevice = true, monthlyServiceCost = 20m, deviceTotal = 480m } } },
                (_, "loan") => new { budgetId, name = accountType, accountType, date = Today, loan = new { balanceOwed = 9000m, interestRate = 6.5m, minimumPayment = 300m } },
                (_, "credit") => new { budgetId, name = accountType, accountType, date = Today, openingBalance = -120m },
                _ => (object)new { budgetId, name = accountType, accountType, date = Today, openingBalance = 75m },
            };
            var created = await mcp.CallAsync("accounts_create", arguments);
            created.AssertOk();
            var id = created.Structured().GetProperty("id").GetGuid();
            var account = (await AccountsAsync(mcp, budgetId)).Single(a => a.Id == id);
            Assert.Equal(accountType, account.AccountType);
            Assert.Equal(family, LoanRules.Family(account.AccountType));
            var expected = (accountType, family) switch { ("contract", _) => -480m - 20m * 24, (_, "loan") => -9000m, (_, "credit") => -120m, _ => 75m };
            // A contract's opening balance is its remaining commitment on the opening date (items due on/after it).
            if (accountType != "contract") Assert.Equal(expected, account.Balance);
            else Assert.True(account.Balance < 0);
        }
    }

    [Fact]
    public async Task Missing_unknown_or_inconsistent_account_types_are_rejected_without_writing()
    {
        await using var app = await fixture.CreateAppAsync();
        var (mcp, budgetId, root) = await AdminAsync(app);
        using (root)
        using (mcp)
        {
            var missing = await mcp.CallAsync("accounts_create", new { budgetId, name = "House", date = Today });
            Assert.Equal("invalid_arguments", missing.ErrorCode());
            var missingMessage = missing.Structured().GetProperty("error").GetProperty("message").GetString()!;
            Assert.Contains("accountType is required", missingMessage);
            Assert.Contains("mortgage", missingMessage);
            Assert.Contains("lineOfCredit", missingMessage);

            var unknown = await mcp.CallAsync("accounts_create", new { budgetId, name = "House", accountType = "homeLoan", date = Today });
            Assert.Equal("invalid_arguments", unknown.ErrorCode());
            Assert.Contains("mortgage, autoLoan, studentLoan, personalLoan, medicalDebt, otherDebt, contract", unknown.Structured().GetProperty("error").GetProperty("message").GetString());

            // A loan type without loan details, or with a cash-style opening balance, never falls back to another type.
            var noTerms = await mcp.CallAsync("accounts_create", new { budgetId, name = "House", accountType = "mortgage", date = Today });
            Assert.Equal("invalid_request", noTerms.ErrorCode());
            Assert.Contains("loan.balanceOwed", noTerms.Structured().GetProperty("error").GetProperty("message").GetString());
            var opening = await mcp.CallAsync("accounts_create", new { budgetId, name = "House", accountType = "mortgage", date = Today, openingBalance = -1000m,
                loan = new { balanceOwed = 1000m, interestRate = 3m, minimumPayment = 100m } });
            Assert.Equal("invalid_request", opening.ErrorCode());
            var partial = await mcp.CallAsync("accounts_create", new { budgetId, name = "House", accountType = "mortgage", date = Today, loan = new { balanceOwed = 1000m } });
            Assert.Contains("loan.interestRate, loan.minimumPayment", partial.Structured().GetProperty("error").GetProperty("message").GetString());
            var creditWithLoan = await mcp.CallAsync("accounts_create", new { budgetId, name = "Card", accountType = "lineOfCredit", date = Today,
                loan = new { interestRate = 3m, minimumPayment = 100m } });
            Assert.Equal("invalid_request", creditWithLoan.ErrorCode());
            // Service-level rule (escrow below payment) surfaces as a rejected request too.
            var escrow = await mcp.CallAsync("accounts_create", new { budgetId, name = "House", accountType = "mortgage", date = Today,
                loan = new { balanceOwed = 1000m, interestRate = 3m, minimumPayment = 100m, escrowAmount = 100m } });
            Assert.True(escrow.IsError());

            Assert.Empty(await AccountsAsync(mcp, budgetId));
            await using var db = app.CreateDb();
            Assert.False(await db.LoanTerms.IgnoreQueryFilters().AnyAsync());
        }
    }

    [Fact]
    public async Task Loan_tools_update_terms_pair_categories_confirm_balance_simulate_and_replay()
    {
        await using var app = await fixture.CreateAppAsync();
        var (mcp, budgetId, root) = await AdminAsync(app);
        using (root)
        using (mcp)
        {
            var commandId = Guid.NewGuid();
            var version = (await mcp.CallAsync("accounts_list", new { budgetId })).Structured().GetProperty("version").GetInt64();
            // Exact replay needs the same arguments including the expected ledger version (the receipt fingerprint covers it).
            var args = new
            {
                budgetId, name = "Car", accountType = "autoLoan", date = Today, commandId, version,
                loan = new { balanceOwed = 12000m, interestRate = 5m, minimumPayment = 400m, newPairedCategory = new { name = "Car payment", newGroupName = "Debt" } }
            };
            var created = await mcp.CallAsync("accounts_create", args);
            created.AssertOk();
            var id = created.Structured().GetProperty("id").GetGuid();
            // Exact replay returns the original receipt instead of creating a second account.
            var replay = await mcp.CallAsync("accounts_create", args);
            replay.AssertOk();
            Assert.Equal(id, replay.Structured().GetProperty("id").GetGuid());
            Assert.Single(await AccountsAsync(mcp, budgetId));

            var pairings = await mcp.CallAsync("loans_pairings_list", new { budgetId });
            pairings.AssertOk();
            var pairing = Assert.Single(pairings.Structured().GetProperty("items").EnumerateArray());
            Assert.Equal(id, pairing.GetProperty("accountId").GetGuid());
            var categoryId = pairing.GetProperty("categoryId").GetString();

            var saved = await mcp.CallAsync("loans_save_terms", new { budgetId, accountId = id, loan = new { interestRate = 4.5m, minimumPayment = 450m, pairedCategoryId = categoryId } });
            saved.AssertOk();
            var details = (await mcp.CallAsync("loans_get", new { budgetId, accountId = id })).Structured();
            Assert.Equal(4.5m, details.GetProperty("interestRate").GetDecimal());
            Assert.Equal(categoryId, details.GetProperty("pairedCategory").GetProperty("id").GetString());

            // accounts_update can change the type within the loan family and keep terms valid.
            (await mcp.CallAsync("accounts_update", new { budgetId, accountId = id, accountType = "personalLoan" })).AssertOk();
            Assert.Equal("personalLoan", (await mcp.CallAsync("loans_get", new { budgetId, accountId = id })).Structured().GetProperty("accountType").GetString());
            var family = await mcp.CallAsync("accounts_update", new { budgetId, accountId = id, accountType = "checking" });
            Assert.True(family.IsError());

            var simulation = await mcp.CallAsync("loans_simulate", new { budgetId, accountId = id, payment = 600m });
            simulation.AssertOk();
            Assert.Equal(600m, simulation.Structured().GetProperty("projection").GetProperty("monthlyPayment").GetDecimal());
            Assert.Equal("invalid_request", (await mcp.CallAsync("loans_simulate", new { budgetId, accountId = id, payment = 600m, payoffMonth = "2030-01" })).ErrorCode());

            (await mcp.CallAsync("loans_confirm_balance", new { budgetId, accountId = id, balance = -11800m })).AssertOk();
            Assert.Equal(-11800m, (await AccountsAsync(mcp, budgetId)).Single().Balance);

            // Loan tools reject non-loan accounts.
            var checking = await mcp.CallAsync("accounts_create", new { budgetId, name = "Checking", accountType = "checking", date = Today, openingBalance = 10m });
            var checkingId = checking.Structured().GetProperty("id").GetGuid();
            Assert.True((await mcp.CallAsync("loans_get", new { budgetId, accountId = checkingId })).IsError());
            Assert.Equal("invalid_request", (await mcp.CallAsync("loans_save_terms", new { budgetId, accountId = checkingId, loan = new { interestRate = 1m, minimumPayment = 10m } })).ErrorCode());
        }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<List<AccountDto>> AccountsAsync(McpTestClient mcp, string budgetId)
    {
        var listed = await mcp.CallAsync("accounts_list", new { budgetId });
        listed.AssertOk();
        return listed.Structured().GetProperty("accounts").Deserialize<List<AccountDto>>(Json)!;
    }

    private static async Task<(McpTestClient Mcp, string BudgetId, HttpClient Root)> AdminAsync(TestApp app)
    {
        app.Clock.UtcNow = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        await CreateUserAsync(app, "root", administrator: true);
        var root = await LoginAsync(app, "root");
        var budget = await CreateBudgetAsync(root, "Household");
        var mcp = Mcp(app, (await CreateTokenAsync(root, "Admin")).Token);
        return (mcp, budget.Id, root);
    }
}
