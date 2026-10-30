using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenBudget.Api;

namespace OpenBudget.IntegrationTests;

// The test clock is 21 October 2026.
public sealed class LoanApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateOnly Date = new(2026, 10, 21);
    private static readonly LoanTermsRequest Mortgage = new(3.47m, 1_789.49m, CreatePairedCategory: true);
    private static readonly ContractTermsRequest Phone = new(24, new DateOnly(2026, 1, 31), true, 50m, 20m, 599.99m, 6, 10m);

    [Fact]
    public async Task Every_loan_type_can_be_created_with_terms_and_a_paired_category_outside_ready_to_assign()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        await AddCash(client, 1_000);
        foreach (var type in LoanRules.LoanTypes)
        {
            var terms = type == LoanRules.Contract ? new LoanTermsRequest(0, 0, CreatePairedCategory: true, Contract: Phone) : Mortgage;
            var created = await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), $"My {type}", Date, -10_000m, type, terms) with
                { OpeningBalance = type == LoanRules.Contract ? 0 : -10_000m });
            var loan = await Get<LoanDto>(client, $"/api/accounts/{created.Id}/loan");
            Assert.Equal(type, loan.AccountType);
            Assert.Equal(LoanRules.AutoCategoryId(created.Id), loan.PairedCategory!.Id);
            Assert.Equal("Loans", loan.PairedCategory.GroupName);
        }
        var budget = await Budget(client);
        Assert.Equal(1_000m, budget.ReadyToAssign);
        Assert.Equal(7, budget.Groups.Single(g => g.Id == LoanRules.PaymentGroupId).Categories.Count);
        var accounts = await Accounts(client);
        Assert.Equal(8, accounts.Accounts.Count);
        Assert.All(accounts.Accounts.Where(a => LoanRules.IsLoan(a.AccountType)), a => Assert.True(a.Balance < 0));
        await using var db = app.CreateDb();
        Assert.Equal(7, await db.LoanTerms.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Loan_terms_are_versioned_replay_safe_and_payments_spend_from_the_paired_category()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var cash = await AddCash(client, 5_000);
        var create = new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Mortgage (400K @ 3.47%)", Date, -389_651.68m, "mortgage", Mortgage);
        var created = await Post<MutationDto>(client, "/api/accounts", create);
        Assert.Equal(created, await Post<MutationDto>(client, "/api/accounts", create));
        var loanId = created.Id;
        var paired = LoanRules.AutoCategoryId(loanId);

        var budget = await Budget(client);
        Assert.Equal(5_000m, budget.ReadyToAssign);
        var group = budget.Groups.Single(g => g.Id == LoanRules.PaymentGroupId);
        Assert.Equal("Mortgage (400K @ 3.47%)", Assert.Single(group.Categories).Name);

        var loan = await Get<LoanDto>(client, $"/api/accounts/{loanId}/loan");
        Assert.Equal(-389_651.68m, loan.Balance);
        Assert.Equal(389_651.68m, loan.OriginalBalance);
        Assert.Equal(100.0m, loan.PercentRemaining);
        Assert.Equal(344, loan.Projection.PaymentsRemaining);
        Assert.Equal("2055-06", loan.Projection.PayoffMonth);
        Assert.Equal(225_929.45m, loan.Projection.InterestRemaining);
        Assert.Equal(615_581.13m, loan.Projection.RemainingToPay);
        Assert.Equal(1_126.74m, loan.EstimatedInterestThisMonth);
        var october = Assert.Single(loan.Activity);
        Assert.Equal(1_126.74m, october.EstimatedInterest);
        Assert.Null(loan.BalanceConfirmedAt);

        await Assign(client, paired, 1_789.49m);
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), cash, loanId, Date, 1_789.49m, "October payment"));
        budget = await Budget(client);
        Assert.Equal(3_210.51m, budget.ReadyToAssign);
        Assert.Equal(-1_789.49m, Category(budget, paired).Activity);
        Assert.Equal(0, Category(budget, paired).Available);
        Assert.Equal(3_210.51m, Balance(await Accounts(client), cash));
        Assert.Equal(-387_862.19m, Balance(await Accounts(client), loanId));
        loan = await Get<LoanDto>(client, $"/api/accounts/{loanId}/loan");
        var payment = Assert.Single(Assert.Single(loan.Activity).Payments);
        Assert.Equal(1_789.49m, payment.Amount);
        Assert.Equal("Checking", payment.AccountName);
        Assert.Equal(0, loan.PairedCategory!.Available);

        // Loan-only activity such as interest changes the debt but never Ready to Assign, and needs no category.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/transactions", await Entry(client, loanId, -1_126.74m, "groceries"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/transactions/import", await Entry(client, loanId, -10m))).StatusCode);
        await Post<MutationDto>(client, "/api/transactions", await Entry(client, loanId, -1_126.74m, payee: "Interest"));
        Assert.Equal(3_210.51m, (await Budget(client)).ReadyToAssign);
        Assert.Equal(0, (await Accounts(client)).Accounts.Single(a => a.Id == loanId).NeedsCategoryCount);
        Assert.Empty((await Get<RegisterDto>(client, "/api/transactions?categoryId=uncategorized")).Items);

        // Unpairing keeps the category but sends payments to Ready to Assign, so cash still equals RTA + Available.
        var version = await Version(client);
        var unpair = new SaveLoanTermsRequest(Guid.NewGuid(), version, Mortgage with { CreatePairedCategory = false });
        var unpaired = await Put<MutationDto>(client, $"/api/accounts/{loanId}/loan", unpair);
        Assert.Equal(unpaired, await Put<MutationDto>(client, $"/api/accounts/{loanId}/loan", unpair));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/accounts/{loanId}/loan", unpair with { Terms = Mortgage })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/accounts/{loanId}/loan", unpair with { CommandId = Guid.NewGuid() })).StatusCode);
        budget = await Budget(client);
        Assert.Equal(1_421.02m, budget.ReadyToAssign);
        Assert.Equal(1_789.49m, Category(budget, paired).Available);
        Assert.Equal(3_210.51m, budget.ReadyToAssign + Category(budget, paired).Available);
        Assert.Null((await Get<LoanDto>(client, $"/api/accounts/{loanId}/loan")).PairedCategory);

        // Pairing an existing category moves the payment's spending there.
        await Put<MutationDto>(client, $"/api/accounts/{loanId}/loan", new SaveLoanTermsRequest(Guid.NewGuid(), await Version(client), Mortgage with { CreatePairedCategory = false, PairedCategoryId = "rent", InterestRate = 3.5m, EscrowAmount = 100m, MinimumPayment = 1_889.49m }));
        budget = await Budget(client);
        Assert.Equal(-1_789.49m, Category(budget, "rent").Activity);
        Assert.Equal(3_210.51m, budget.ReadyToAssign);
        loan = await Get<LoanDto>(client, $"/api/accounts/{loanId}/loan");
        Assert.Equal("rent", loan.PairedCategory!.Id);
        Assert.Equal(3.5m, loan.InterestRate);
        Assert.Equal(100m, loan.EscrowAmount);

        foreach (var invalid in new[]
        {
            Mortgage with { InterestRate = 100.5m }, Mortgage with { MinimumPayment = 0 }, Mortgage with { EscrowAmount = 2_000m },
            Mortgage with { CreatePairedCategory = false, PairedCategoryId = "missing" },
            Mortgage with { CreatePairedCategory = false, PairedCategoryId = CreditCardRules.PaymentCategoryId(Guid.NewGuid()) },
            Mortgage with { Contract = Phone }
        })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/accounts/{loanId}/loan", new SaveLoanTermsRequest(Guid.NewGuid(), await Version(client), invalid))).StatusCode);
        Assert.Equal(3.5m, (await Get<LoanDto>(client, $"/api/accounts/{loanId}/loan")).InterestRate);

        await Put<MutationDto>(client, $"/api/accounts/{loanId}/loan", new SaveLoanTermsRequest(Guid.NewGuid(), await Version(client), Mortgage));
        Assert.Equal(paired, (await Get<LoanDto>(client, $"/api/accounts/{loanId}/loan")).PairedCategory!.Id);
        Assert.Equal(-1_789.49m, Category(await Budget(client), paired).Activity);

        var audit = await Get<AuditPageDto>(client, $"/api/ledger/audit?resourceId={loanId}&pageSize=50");
        Assert.Equal(3, audit.Items.Count(item => item.Kind == $"loan.terms/{loanId}"));
        Assert.Contains(audit.Items, item => item.Kind == "account.create");
    }

    [Fact]
    public async Task Account_families_cannot_switch_and_invalid_loans_write_nothing()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var cash = await AddCash(client, 100);
        var loanId = (await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Car", Date, -9_000m, "autoLoan", Mortgage with { CreatePairedCategory = false }))).Id;
        var contractId = (await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Phone", Date, 0, "contract", new(0, 0, Contract: Phone)))).Id;
        Assert.Equal(-675.08m, Balance(await Accounts(client), contractId));

        async Task<HttpStatusCode> Update(Guid id, string name, string type) =>
            (await client.PutAsJsonAsync($"/api/accounts/{id}", new UpdateAccountRequest(Guid.NewGuid(), await Version(client), name, false, type))).StatusCode;
        Assert.Equal(HttpStatusCode.BadRequest, await Update(loanId, "Car", "checking"));
        Assert.Equal(HttpStatusCode.BadRequest, await Update(loanId, "Car", "creditCard"));
        Assert.Equal(HttpStatusCode.BadRequest, await Update(cash, "Checking", "mortgage"));
        Assert.Equal(HttpStatusCode.BadRequest, await Update(loanId, "Car", "contract"));
        Assert.Equal(HttpStatusCode.BadRequest, await Update(contractId, "Phone", "mortgage"));
        Assert.Equal(HttpStatusCode.OK, await Update(loanId, "Car", "personalLoan"));
        Assert.Equal("personalLoan", (await Accounts(client)).Accounts.Single(a => a.Id == loanId).AccountType);

        var before = await Version(client);
        foreach (var invalid in new[]
        {
            new CreateAccountRequest(Guid.NewGuid(), before, "No terms", Date, -100m, "mortgage"),
            new CreateAccountRequest(Guid.NewGuid(), before, "Positive", Date, 100m, "mortgage", Mortgage),
            new CreateAccountRequest(Guid.NewGuid(), before, "Rate", Date, -100m, "mortgage", Mortgage with { InterestRate = 3.4705m }),
            new CreateAccountRequest(Guid.NewGuid(), before, "Category", Date, -100m, "mortgage", Mortgage with { CreatePairedCategory = false, PairedCategoryId = "missing" }),
            new CreateAccountRequest(Guid.NewGuid(), before, "Card", Date, -100m, "creditCard", Mortgage),
            new CreateAccountRequest(Guid.NewGuid(), before, "Contract", Date, 0, "contract", new(1m, 0, Contract: Phone)),
            new CreateAccountRequest(Guid.NewGuid(), before, "Contract", Date, -5m, "contract", new(0, 0, Contract: Phone)),
            new CreateAccountRequest(Guid.NewGuid(), before, "Contract", Date, 0, "contract", new(0, 0, Contract: Phone with { Months = 61 })),
            new CreateAccountRequest(Guid.NewGuid(), before, "Unknown", Date, 0, "boat")
        })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/accounts", invalid)).StatusCode);
        Assert.Equal(before, await Version(client));
        Assert.Equal(3, (await Accounts(client)).Accounts.Count);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/accounts/{Guid.NewGuid()}/loan")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/accounts/{cash}/loan")).StatusCode);
    }

    [Fact]
    public async Task Balance_confirmation_and_correction_are_ledger_commands()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        await AddCash(client, 100);
        var loanId = (await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Student", Date, -20_000m, "studentLoan", Mortgage))).Id;
        var yes = new LoanBalanceRequest(Guid.NewGuid(), await Version(client));
        var confirmed = await Post<MutationDto>(client, $"/api/accounts/{loanId}/loan/balance", yes);
        Assert.Equal(confirmed, await Post<MutationDto>(client, $"/api/accounts/{loanId}/loan/balance", yes));
        var loan = await Get<LoanDto>(client, $"/api/accounts/{loanId}/loan");
        Assert.Equal(app.Clock.UtcNow, loan.BalanceConfirmedAt);
        Assert.Equal(-20_000m, loan.Balance);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/accounts/{loanId}/loan/balance", new LoanBalanceRequest(Guid.NewGuid(), confirmed.Version - 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/accounts/{loanId}/loan/balance", new LoanBalanceRequest(Guid.NewGuid(), await Version(client), -1.001m))).StatusCode);

        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(2);
        await Post<MutationDto>(client, $"/api/accounts/{loanId}/loan/balance", new LoanBalanceRequest(Guid.NewGuid(), await Version(client), -19_950.25m));
        loan = await Get<LoanDto>(client, $"/api/accounts/{loanId}/loan");
        Assert.Equal(-19_950.25m, loan.Balance);
        Assert.Equal(app.Clock.UtcNow, loan.BalanceConfirmedAt);
        var adjustment = Assert.Single(Assert.Single(loan.Activity).Other, item => item.Kind == "transaction");
        Assert.Equal(49.75m, adjustment.Amount);
        Assert.Equal(100m, (await Budget(client)).ReadyToAssign);

        // The edit form's working balance uses the same adjustment transaction.
        await Put<MutationDto>(client, $"/api/accounts/{loanId}", new UpdateAccountRequest(Guid.NewGuid(), await Version(client), "Student", false, WorkingBalance: -19_900m,
            Loan: Mortgage with { MinimumPayment = 250m }));
        loan = await Get<LoanDto>(client, $"/api/accounts/{loanId}/loan");
        Assert.Equal(-19_900m, loan.Balance);
        Assert.Equal(250m, loan.MinimumPayment);
        Assert.Equal(100m, (await Budget(client)).ReadyToAssign);
    }

    [Fact]
    public async Task Simulation_is_read_only_and_solves_payments_and_payoff_dates()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        await AddCash(client, 100);
        var loanId = (await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Mortgage", Date, -389_651.68m, "mortgage", Mortgage))).Id;
        var version = await Version(client);
        var path = $"/api/accounts/{loanId}/loan/simulation";

        var minimum = await Get<LoanSimulationDto>(client, path);
        Assert.Equal(344, minimum.Projection.PaymentsRemaining);
        Assert.Equal(345, minimum.Projection.Track.Count);
        var byDate = await Get<LoanSimulationDto>(client, $"{path}?payoffMonth=2046-10");
        Assert.Equal(2_253.83m, byDate.Projection.MonthlyPayment);
        Assert.Equal(240, byDate.Projection.PaymentsRemaining);
        Assert.Equal("2046-10", byDate.Projection.PayoffMonth);
        var extra = await Get<LoanSimulationDto>(client, $"{path}?payment=1789.49&extra=10000");
        Assert.Equal(330, extra.Projection.PaymentsRemaining);
        Assert.Equal(209_462.96m, extra.Projection.InterestRemaining);
        var never = await Get<LoanSimulationDto>(client, $"{path}?payment=1000");
        Assert.True(never.Projection.NeverPaidOff);
        Assert.Null(never.Projection.PayoffMonth);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{path}?payment=1000&payoffMonth=2046-10")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{path}?payoffMonth=2026-10")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{path}?payment=-1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{path}?extra=0.001")).StatusCode);
        Assert.Equal(version, await Version(client));
        Assert.Equal(-389_651.68m, Balance(await Accounts(client), loanId));
    }

    [Fact]
    public async Task Contract_accounts_report_their_schedule_and_remaining_commitment()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var cash = await AddCash(client, 500);
        var id = (await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Phone", Date, 0, "contract",
            new(0, 0, CreatePairedCategory: true, Contract: Phone)))).Id;
        var loan = await Get<LoanDto>(client, $"/api/accounts/{id}/loan");
        Assert.Equal(-675.08m, loan.Balance);
        Assert.Equal(1_019.99m, loan.OriginalBalance);
        var contract = loan.Contract!;
        Assert.Equal(1_069.99m, contract.TotalCost);
        Assert.Equal(375.08m, contract.RemainingDevice);
        Assert.Equal(300m, contract.RemainingService);
        Assert.Equal(15, contract.RemainingMonths);
        Assert.Equal(new DateOnly(2027, 12, 31), contract.EndDate);
        Assert.Equal(24, contract.Schedule.Count);
        Assert.Equal(6, contract.Schedule.Count(item => item.IsDiscounted));
        Assert.Equal(9, contract.Schedule.Count(item => item.IsPast));
        Assert.Equal(44.99m, loan.Projection.MonthlyPayment);
        Assert.Equal("2027-12", loan.Projection.PayoffMonth);
        Assert.Equal(0, loan.InterestRate);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/accounts/{id}/loan/simulation")).StatusCode);

        await Assign(client, LoanRules.AutoCategoryId(id), 44.99m);
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), cash, id, Date, 44.99m, "Phone bill"));
        var budget = await Budget(client);
        Assert.Equal(455.01m, budget.ReadyToAssign);
        Assert.Equal(0, Category(budget, LoanRules.AutoCategoryId(id)).Available);
        Assert.Equal(-630.09m, Balance(await Accounts(client), id));

        await Put<MutationDto>(client, $"/api/accounts/{id}/loan", new SaveLoanTermsRequest(Guid.NewGuid(), await Version(client),
            new(0, 0, CreatePairedCategory: true, Contract: Phone with { DiscountMonths = 0, DiscountedServiceCost = null, Months = 12 })));
        loan = await Get<LoanDto>(client, $"/api/accounts/{id}/loan");
        Assert.Equal(12, loan.Contract!.Months);
        Assert.Equal(0, loan.Contract.DiscountMonths);
    }

    [Fact]
    public async Task Loans_are_isolated_between_budgets()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "owner", "owner@example.test");
        await CreateUserAsync(app, "other", "other@example.test");
        using var owner = await LoginAsync(app, "owner");
        using var other = await LoginAsync(app, "other");
        var budget = await CreateBudgetAsync(owner, "Owner");
        var otherBudget = await CreateBudgetAsync(other, "Other");
        owner.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={budget.Id}");
        other.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={otherBudget.Id}");
        var loanId = (await Post<MutationDto>(owner, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(owner), "Mortgage", Date, -1_000m, "mortgage", Mortgage))).Id;

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/accounts/{loanId}/loan")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/accounts/{loanId}/loan", new SaveLoanTermsRequest(Guid.NewGuid(), await Version(other), Mortgage))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/accounts/{loanId}/loan/balance", new LoanBalanceRequest(Guid.NewGuid(), await Version(other)))).StatusCode);
        Assert.Empty((await Accounts(other)).Accounts);
        Assert.Single((await Accounts(owner)).Accounts);
        await using var db = app.CreateDb();
        Assert.Equal(budget.Id, (await db.LoanTerms.IgnoreQueryFilters().SingleAsync()).BudgetId);
    }

    private static decimal Balance(AccountsDto accounts, Guid id) => accounts.Accounts.Single(a => a.Id == id).Balance;
    private static CategoryDto Category(BudgetDto budget, string id) => budget.Groups.SelectMany(g => g.Categories).Single(c => c.Id == id);
    private static Task<BudgetDto> Budget(HttpClient client) => Get<BudgetDto>(client, "/api/budget/2026-10");
    private static Task<AccountsDto> Accounts(HttpClient client) => Get<AccountsDto>(client, "/api/accounts");
    private static async Task<long> Version(HttpClient client) => (await Accounts(client)).Version;
    private static async Task<Guid> AddCash(HttpClient client, decimal balance) =>
        (await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Checking", Date, balance))).Id;
    private static async Task<TransactionRequest> Entry(HttpClient client, Guid accountId, decimal amount, string? category = null, string? payee = null) =>
        new(Guid.NewGuid(), await Version(client), accountId, Date, amount, payee, "", category, "cleared", []);

    private static async Task Assign(HttpClient client, string categoryId, decimal amount)
    {
        var budget = await Budget(client);
        var request = new BudgetUpdateDto(budget.Month, budget.Note, budget.Version, budget.FundsAdded, budget.Groups.Select(g => new CategoryGroupUpdateDto(g.Id, g.Name, g.SortOrder, g.IsArchived,
            g.Categories.Select(c => new CategoryUpdateDto(c.Id, c.Name, c.Id == categoryId ? amount : c.Assigned, c.Target, c.Icon, c.Snoozed, c.SortOrder, c.IsArchived)).ToList())).ToList());
        await Put<BudgetDto>(client, "/api/budget/2026-10", request);
    }

    private static async Task<T> Get<T>(HttpClient client, string path) => await Read<T>(await client.GetAsync(path));
    private static async Task<T> Post<T>(HttpClient client, string path, object request) => await Read<T>(await client.PostAsJsonAsync(path, request));
    private static async Task<T> Put<T>(HttpClient client, string path, object request) => await Read<T>(await client.PutAsJsonAsync(path, request));
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
