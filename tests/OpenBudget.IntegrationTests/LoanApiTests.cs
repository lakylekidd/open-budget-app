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
    // Unpaired by default: pairing is the user's choice.
    private static readonly LoanTermsRequest Mortgage = new(3.47m, 1_789.49m);
    private static readonly ContractTermsRequest Phone = new(24, new DateOnly(2026, 1, 31), true, 50m, 20m, 599.99m, 6, 10m);
    private static LoanTermsRequest PairNew(LoanTermsRequest terms, string name, string? groupId = "home", string? newGroup = null) =>
        terms with { NewPairedCategory = new(name, groupId, newGroup) };

    [Fact]
    public async Task Every_loan_type_can_be_created_with_terms_and_a_paired_category_outside_ready_to_assign()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        await AddCash(client, 1_000);
        foreach (var type in LoanRules.LoanTypes)
        {
            var terms = PairNew(type == LoanRules.Contract ? new LoanTermsRequest(0, 0, Contract: Phone) : Mortgage, $"My {type}");
            var created = await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), $"My {type}", Date, -10_000m, type, terms) with
                { OpeningBalance = type == LoanRules.Contract ? 0 : -10_000m });
            var loan = await Get<LoanDto>(client, $"/api/accounts/{created.Id}/loan");
            Assert.Equal(type, loan.AccountType);
            Assert.Equal($"My {type}", loan.PairedCategory!.Name);
            Assert.Equal("Home", loan.PairedCategory.GroupName);
            Assert.False(loan.PairedCategory.IsAutomatic);
        }
        var budget = await Budget(client);
        Assert.Equal(1_000m, budget.ReadyToAssign);
        Assert.Equal(3 + 7, budget.Groups.Single(g => g.Id == "home").Categories.Count);
        Assert.DoesNotContain(budget.Groups, g => g.Id == LoanRules.PaymentGroupId);
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
        var create = new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Mortgage (400K @ 3.47%)", Date, -389_651.68m, "mortgage",
            PairNew(Mortgage, "Mortgage (400K @ 3.47%)", null, "Debts"));
        var created = await Post<MutationDto>(client, "/api/accounts", create);
        Assert.Equal(created, await Post<MutationDto>(client, "/api/accounts", create));
        var loanId = created.Id;

        var budget = await Budget(client);
        Assert.Equal(5_000m, budget.ReadyToAssign);
        var group = budget.Groups.Single(g => g.Name == "Debts");
        Assert.Equal("Mortgage (400K @ 3.47%)", Assert.Single(group.Categories).Name);
        var paired = group.Categories[0].Id;

        var loan = await Get<LoanDto>(client, $"/api/accounts/{loanId}/loan");
        Assert.Equal(paired, loan.PairedCategory!.Id);
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
        var unpair = new SaveLoanTermsRequest(Guid.NewGuid(), version, Mortgage);
        var unpaired = await Put<MutationDto>(client, $"/api/accounts/{loanId}/loan", unpair);
        Assert.Equal(unpaired, await Put<MutationDto>(client, $"/api/accounts/{loanId}/loan", unpair));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/accounts/{loanId}/loan", unpair with { Terms = Mortgage with { PairedCategoryId = paired } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/accounts/{loanId}/loan", unpair with { CommandId = Guid.NewGuid() })).StatusCode);
        budget = await Budget(client);
        Assert.Equal(1_421.02m, budget.ReadyToAssign);
        Assert.Equal(1_789.49m, Category(budget, paired).Available);
        Assert.Equal(3_210.51m, budget.ReadyToAssign + Category(budget, paired).Available);
        Assert.Null((await Get<LoanDto>(client, $"/api/accounts/{loanId}/loan")).PairedCategory);

        // Pairing an existing category moves the payment's spending there.
        await Put<MutationDto>(client, $"/api/accounts/{loanId}/loan", new SaveLoanTermsRequest(Guid.NewGuid(), await Version(client), Mortgage with { PairedCategoryId = "rent", InterestRate = 3.5m, EscrowAmount = 100m, MinimumPayment = 1_889.49m }));
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
            Mortgage with { PairedCategoryId = "missing" },
            Mortgage with { PairedCategoryId = CreditCardRules.PaymentCategoryId(Guid.NewGuid()) },
            Mortgage with { Contract = Phone }
        })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/accounts/{loanId}/loan", new SaveLoanTermsRequest(Guid.NewGuid(), await Version(client), invalid))).StatusCode);
        Assert.Equal(3.5m, (await Get<LoanDto>(client, $"/api/accounts/{loanId}/loan")).InterestRate);

        await Put<MutationDto>(client, $"/api/accounts/{loanId}/loan", new SaveLoanTermsRequest(Guid.NewGuid(), await Version(client), Mortgage with { PairedCategoryId = paired }));
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
        var loanId = (await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Car", Date, -9_000m, "autoLoan", Mortgage))).Id;
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
            new CreateAccountRequest(Guid.NewGuid(), before, "Category", Date, -100m, "mortgage", Mortgage with { PairedCategoryId = "missing" }),
            new CreateAccountRequest(Guid.NewGuid(), before, "Protected", Date, -100m, "mortgage", PairNew(Mortgage, "Mine", CreditCardRules.PaymentGroupId)),
            new CreateAccountRequest(Guid.NewGuid(), before, "Duplicate", Date, -100m, "mortgage", PairNew(Mortgage, " rent & MORTGAGE ")),
            new CreateAccountRequest(Guid.NewGuid(), before, "Both", Date, -100m, "mortgage", PairNew(Mortgage with { PairedCategoryId = "rent" }, "X")),
            new CreateAccountRequest(Guid.NewGuid(), before, "Device", Date, -100m, "mortgage", Mortgage with { SeparateDeviceCategory = true }),
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
    public async Task Recording_interest_replaces_the_estimate_adds_to_the_debt_and_zero_removes_it()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var created = await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Car Loan", Date, -5_000m, "autoLoan", Mortgage));
        var month = Date.ToString("yyyy-MM");
        async Task<LoanDto> Load() => await Get<LoanDto>(client, $"/api/accounts/{created.Id}/loan");
        Assert.Null((await Load()).Activity[0].Interest);

        await Post<MutationDto>(client, $"/api/accounts/{created.Id}/loan/interest", new LoanInterestRequest(Guid.NewGuid(), await Version(client), month, 4.17m));
        var recorded = (await Load()).Activity[0];
        Assert.Equal(-4.17m, recorded.Interest!.Amount);
        Assert.Equal(-5_004.17m, (await Load()).Balance);
        Assert.DoesNotContain(recorded.Other, item => item.Kind != "opening");

        await Post<MutationDto>(client, $"/api/accounts/{created.Id}/loan/interest", new LoanInterestRequest(Guid.NewGuid(), await Version(client), month, 5.25m));
        Assert.Equal(-5_005.25m, (await Load()).Balance);

        await Post<MutationDto>(client, $"/api/accounts/{created.Id}/loan/interest", new LoanInterestRequest(Guid.NewGuid(), await Version(client), month, 0m));
        Assert.Null((await Load()).Activity[0].Interest);
        Assert.Equal(-5_000m, (await Load()).Balance);

        var bad = await client.PostAsJsonAsync($"/api/accounts/{created.Id}/loan/interest", new LoanInterestRequest(Guid.NewGuid(), await Version(client), "2026-13", 1m));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, bad.StatusCode);
        var negative = await client.PostAsJsonAsync($"/api/accounts/{created.Id}/loan/interest", new LoanInterestRequest(Guid.NewGuid(), await Version(client), month, -1m));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, negative.StatusCode);
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
            PairNew(new(0, 0, Contract: Phone), "Phone")))).Id;
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

        var categoryId = loan.PairedCategory!.Id;
        await Assign(client, categoryId, 44.99m);
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), cash, id, Date, 44.99m, "Phone bill"));
        var budget = await Budget(client);
        Assert.Equal(455.01m, budget.ReadyToAssign);
        Assert.Equal(0, Category(budget, categoryId).Available);
        Assert.Equal(-630.09m, Balance(await Accounts(client), id));

        await Put<MutationDto>(client, $"/api/accounts/{id}/loan", new SaveLoanTermsRequest(Guid.NewGuid(), await Version(client),
            new(0, 0, PairedCategoryId: categoryId, Contract: Phone with { DiscountMonths = 0, DiscountedServiceCost = null, Months = 12 })));
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

    [Fact]
    public async Task Creating_a_loan_with_a_new_category_in_a_new_group_is_one_atomic_replay_safe_command()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var cash = await AddCash(client, 2_000);
        await Get<BudgetDto>(client, "/api/budget/2026-11"); // a later initialized month must receive the category too
        var create = new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Mortgage", Date, -100_000m, "mortgage", PairNew(Mortgage, "Mortgage", null, " Debts "));
        var created = await Post<MutationDto>(client, "/api/accounts", create);
        Assert.Equal(created, await Post<MutationDto>(client, "/api/accounts", create));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/accounts", create with { Name = "Other" })).StatusCode);

        var loan = await Get<LoanDto>(client, $"/api/accounts/{created.Id}/loan");
        var categoryId = loan.PairedCategory!.Id;
        Assert.Equal("Debts", loan.PairedCategory.GroupName);
        foreach (var month in new[] { "2026-10", "2026-11" })
        {
            var group = Assert.Single((await Get<BudgetDto>(client, $"/api/budget/{month}")).Groups, g => g.Name == "Debts");
            Assert.Equal(categoryId, Assert.Single(group.Categories).Id);
            Assert.Equal(0, group.Categories[0].Available);
        }
        Assert.Contains(categoryId, (await Get<AuditPageDto>(client, $"/api/ledger/audit?resourceId={created.Id}")).Items.Single(i => i.Kind == "account.create").AfterJson);
        var pairings = await Get<LoanPairingsDto>(client, "/api/loan-pairings");
        Assert.Equal(new LoanPairingDto(created.Id, "Mortgage", categoryId, "loan"), Assert.Single(pairings.Items));

        // A failure after the new group/category was staged (the device slot reuses a paired category) writes nothing.
        var version = await Version(client);
        var groupsBefore = await GroupCount(app);
        foreach (var invalid in new[]
        {
            new CreateAccountRequest(Guid.NewGuid(), version, "Phone", Date, 0, "contract", PairNew(new(0, 0, Contract: Phone), "Phone", null, "Phones")
                with { SeparateDeviceCategory = true, DevicePairedCategoryId = categoryId }),
            new CreateAccountRequest(Guid.NewGuid(), version, "Car", Date, -5_000m, "autoLoan", Mortgage with { PairedCategoryId = categoryId }),
            new CreateAccountRequest(Guid.NewGuid(), version, "Car", Date, -5_000m, "autoLoan", PairNew(Mortgage, "mortgage", null, "debts")),
            new CreateAccountRequest(Guid.NewGuid(), version, "Car", Date, -5_000m, "autoLoan", PairNew(Mortgage, "Car", "missing-group"))
        })
        {
            var response = await client.PostAsJsonAsync("/api/accounts", invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("error", await response.Content.ReadAsStringAsync());
        }
        Assert.Contains("already paired", await (await client.PostAsJsonAsync("/api/accounts",
            new CreateAccountRequest(Guid.NewGuid(), version, "Car", Date, -5_000m, "autoLoan", Mortgage with { PairedCategoryId = categoryId }))).Content.ReadAsStringAsync());
        Assert.Equal(version, await Version(client));
        Assert.Equal(groupsBefore, await GroupCount(app));
        Assert.Equal(2, (await Accounts(client)).Accounts.Count);
        Assert.DoesNotContain((await Budget(client)).Groups, g => g.Name == "Phones");

        // Adding to an existing group reuses it; the payment spends from the new category and the invariant holds.
        var car = await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Car", Date, -5_000m, "autoLoan",
            PairNew(Mortgage, "Car loan", (await Budget(client)).Groups.Single(g => g.Name == "Debts").Id)));
        var carCategory = (await Get<LoanDto>(client, $"/api/accounts/{car.Id}/loan")).PairedCategory!;
        Assert.Equal(("Car loan", "Debts"), (carCategory.Name, carCategory.GroupName));
        await Assign(client, carCategory.Id, 300m);
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), cash, car.Id, Date, 300m, "Car"));
        Assert.Equal(0, Category(await Budget(client), carCategory.Id).Available);
        await AssertCashInvariant(client);
    }

    [Fact]
    public async Task Pairing_existing_or_skipping_controls_where_payments_come_from_and_pairs_are_one_to_one()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var cash = await AddCash(client, 3_000);
        var rentLoan = (await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Mortgage", Date, -50_000m, "mortgage",
            Mortgage with { PairedCategoryId = "rent" }))).Id;
        var skipped = (await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Student", Date, -8_000m, "studentLoan", Mortgage))).Id;
        Assert.Equal("rent", (await Get<LoanDto>(client, $"/api/accounts/{rentLoan}/loan")).PairedCategory!.Id);
        Assert.Null((await Get<LoanDto>(client, $"/api/accounts/{skipped}/loan")).PairedCategory);
        Assert.DoesNotContain((await Budget(client)).Groups, g => g.Id == LoanRules.PaymentGroupId);

        await Assign(client, "rent", 500m);
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), cash, rentLoan, Date, 500m, "Mortgage"));
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), cash, skipped, Date, 200m, "Student"));
        var budget = await Budget(client);
        Assert.Equal(0, Category(budget, "rent").Available);
        Assert.Equal(2_300m, budget.ReadyToAssign); // 3,000 - 500 assigned - 200 unpaired payment
        await AssertCashInvariant(client);

        // One-to-one: the skipped loan cannot take "rent"; archived and protected categories are rejected without writes.
        var version = await Version(client);
        var duplicate = await client.PutAsJsonAsync($"/api/accounts/{skipped}/loan", new SaveLoanTermsRequest(Guid.NewGuid(), version, Mortgage with { PairedCategoryId = "rent" }));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("already paired with another loan", await duplicate.Content.ReadAsStringAsync());
        await Archive(client, "gifts");
        version = await Version(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/accounts/{skipped}/loan", new SaveLoanTermsRequest(Guid.NewGuid(), version, Mortgage with { PairedCategoryId = "gifts" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/accounts/{skipped}/loan", new SaveLoanTermsRequest(Guid.NewGuid(), version, Mortgage with { PairedCategoryId = LoanRules.AutoCategoryId(rentLoan) }))).StatusCode);
        Assert.Equal(version, await Version(client));
        Assert.Null((await Get<LoanDto>(client, $"/api/accounts/{skipped}/loan")).PairedCategory);
    }

    [Fact]
    public async Task Edit_account_pairs_unpairs_and_repairs_as_versioned_audited_commands()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var cash = await AddCash(client, 1_000);
        var id = (await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Car", Date, -9_000m, "autoLoan", Mortgage))).Id;
        UpdateAccountRequest Edit(long version, LoanTermsRequest terms) => new(Guid.NewGuid(), version, "Car", false, "autoLoan", Loan: terms);

        // Pair a brand new category in an existing group from Edit Account.
        var pair = Edit(await Version(client), PairNew(Mortgage, "Car payment", "daily"));
        var paired = await Put<MutationDto>(client, $"/api/accounts/{id}", pair);
        Assert.Equal(paired, await Put<MutationDto>(client, $"/api/accounts/{id}", pair));
        var category = (await Get<LoanDto>(client, $"/api/accounts/{id}/loan")).PairedCategory!;
        Assert.Equal(("Car payment", "Everyday"), (category.Name, category.GroupName));
        Assert.Single((await Budget(client)).Groups.Single(g => g.Id == "daily").Categories, c => c.Name == "Car payment");

        // Stale versions conflict and write nothing.
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/accounts/{id}", Edit(paired.Version - 1, Mortgage))).StatusCode);
        Assert.Equal(category.Id, (await Get<LoanDto>(client, $"/api/accounts/{id}/loan")).PairedCategory!.Id);

        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), cash, id, Date, 100m, "Car"));
        Assert.Equal(-100m, Category(await Budget(client), category.Id).Activity);

        // Unpair: the category stays (with its history), payments move to Ready to Assign.
        await Put<MutationDto>(client, $"/api/accounts/{id}", Edit(await Version(client), Mortgage));
        var budget = await Budget(client);
        Assert.Equal(0, Category(budget, category.Id).Activity);
        Assert.Equal(900m, budget.ReadyToAssign);
        Assert.Empty((await Get<LoanPairingsDto>(client, "/api/loan-pairings")).Items);
        await AssertCashInvariant(client);

        // Re-pair the same (now unpaired) category, then switch to another existing one.
        await Put<MutationDto>(client, $"/api/accounts/{id}", Edit(await Version(client), Mortgage with { PairedCategoryId = category.Id }));
        Assert.Equal(-100m, Category(await Budget(client), category.Id).Activity);
        await Put<MutationDto>(client, $"/api/accounts/{id}", Edit(await Version(client), Mortgage with { PairedCategoryId = "transport" }));
        budget = await Budget(client);
        Assert.Equal(-100m, Category(budget, "transport").Activity);
        Assert.Equal(0, Category(budget, category.Id).Activity);
        await AssertCashInvariant(client);

        var audit = await Get<AuditPageDto>(client, $"/api/ledger/audit?resourceId={id}&pageSize=50");
        Assert.Equal(4, audit.Items.Count(item => item.Kind == $"account.update/{id}"));
        Assert.Contains(audit.Items, item => item.Kind == $"account.update/{id}" && item.AfterJson.Contains("Car payment"));
    }

    [Fact]
    public async Task Contract_with_a_separate_device_category_splits_each_payment_by_the_months_schedule()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var cash = await AddCash(client, 500);
        var terms = new LoanTermsRequest(0, 0, Contract: Phone, NewPairedCategory: new("Phone plan", null, "Phone"), SeparateDeviceCategory: true,
            NewDevicePairedCategory: new("Phone device", null, "Phone"));
        var id = (await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Phone", Date, 0, "contract", terms))).Id;
        var loan = await Get<LoanDto>(client, $"/api/accounts/{id}/loan");
        Assert.True(loan.SeparateDeviceCategory);
        var plan = loan.PairedCategory!.Id;
        var device = loan.DevicePairedCategory!.Id;
        var phoneGroup = Assert.Single((await Budget(client)).Groups, g => g.Name == "Phone");
        Assert.Equal(["Phone plan", "Phone device"], phoneGroup.Categories.OrderBy(c => c.SortOrder).Select(c => c.Name));
        Assert.Equal(2, (await Get<LoanPairingsDto>(client, "/api/loan-pairings")).Items.Count);

        // October is scheduled as contract 20.00 + device 24.99, so a 44.99 payment clears both exactly.
        await Assign(client, plan, 20m);
        await Assign(client, device, 24.99m);
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), cash, id, Date, 44.99m, "Phone bill"));
        var budget = await Budget(client);
        Assert.Equal((0m, 0m), (Category(budget, plan).Available, Category(budget, device).Available));
        // A further 50.00 splits 22.22 contract / 27.78 device (contract share truncated to cents, remainder to the device).
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), cash, id, Date, 50m, "Extra"));
        budget = await Budget(client);
        Assert.Equal(-22.22m, Category(budget, plan).Available);
        Assert.Equal(-27.78m, Category(budget, device).Available);
        Assert.Equal(455.01m, budget.ReadyToAssign);
        await AssertCashInvariant(client);
        loan = await Get<LoanDto>(client, $"/api/accounts/{id}/loan");
        Assert.Equal(-27.78m, loan.DevicePairedCategory!.Available);

        // The device category cannot be paired with another loan; leaving the device slot unpaired sends its share to Ready to Assign.
        var car = (await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Car", Date, -1_000m, "autoLoan", Mortgage))).Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/accounts/{car}/loan", new SaveLoanTermsRequest(Guid.NewGuid(), await Version(client), Mortgage with { PairedCategoryId = device }))).StatusCode);
        await Put<MutationDto>(client, $"/api/accounts/{id}/loan", new SaveLoanTermsRequest(Guid.NewGuid(), await Version(client), new(0, 0, PairedCategoryId: plan, Contract: Phone, SeparateDeviceCategory: true)));
        budget = await Budget(client);
        Assert.Equal(-22.22m, Category(budget, plan).Available);
        Assert.Equal(24.99m, Category(budget, device).Available);
        Assert.Equal(455.01m - 24.99m - 27.78m, budget.ReadyToAssign);
        await AssertCashInvariant(client);

        // One category for both: the whole payment spends from the contract category.
        await Put<MutationDto>(client, $"/api/accounts/{id}/loan", new SaveLoanTermsRequest(Guid.NewGuid(), await Version(client), new(0, 0, PairedCategoryId: plan, Contract: Phone)));
        budget = await Budget(client);
        Assert.Equal(20m - 94.99m, Category(budget, plan).Available);
        Assert.Null((await Get<LoanDto>(client, $"/api/accounts/{id}/loan")).DevicePairedCategory);
        await AssertCashInvariant(client);
    }

    [Fact]
    public async Task Legacy_automatic_loan_categories_keep_their_pairing_and_money()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var cash = await AddCash(client, 1_000);
        var id = (await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), "Old loan", Date, -2_000m, "personalLoan", Mortgage))).Id;
        await using (var db = app.CreateDb())
        {
            // A loan created before optional pairing: auto-paired with its own loan-{id} category.
            var row = await db.LoanTerms.IgnoreQueryFilters().SingleAsync(t => t.AccountId == id);
            row.PairedCategoryId = LoanRules.AutoCategoryId(id);
            await db.SaveChangesAsync();
        }
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), cash, id, Date, 150m, "Payment"));
        var budget = await Budget(client);
        var legacy = Category(budget, LoanRules.AutoCategoryId(id));
        Assert.Equal(-150m, legacy.Activity);
        Assert.Equal("Loans", budget.Groups.Single(g => g.Id == LoanRules.PaymentGroupId).Name);
        await AssertCashInvariant(client);

        // Unrelated edits keep the legacy pairing; it shows as automatic and can still be unpaired and re-paired.
        await Put<MutationDto>(client, $"/api/accounts/{id}", new UpdateAccountRequest(Guid.NewGuid(), await Version(client), "Old loan", false,
            Loan: Mortgage with { PairedCategoryId = LoanRules.AutoCategoryId(id), MinimumPayment = 300m }));
        var loan = await Get<LoanDto>(client, $"/api/accounts/{id}/loan");
        Assert.True(loan.PairedCategory!.IsAutomatic);
        Assert.Equal(-150m, Category(await Budget(client), LoanRules.AutoCategoryId(id)).Activity);
    }

    private static async Task AssertCashInvariant(HttpClient client)
    {
        var accounts = await Accounts(client);
        var cash = accounts.Accounts.Where(a => !LoanRules.IsLoan(a.AccountType) && !CreditCardRules.IsCredit(a.AccountType)).Sum(a => a.Balance);
        var budget = await Budget(client);
        Assert.Equal(cash, budget.ReadyToAssign + budget.Groups.SelectMany(g => g.Categories).Sum(c => c.Available));
    }

    private static async Task<int> GroupCount(TestApp app)
    {
        await using var db = app.CreateDb();
        return await db.CategoryGroups.IgnoreQueryFilters().CountAsync() + await db.Categories.IgnoreQueryFilters().CountAsync();
    }

    private static async Task Archive(HttpClient client, string categoryId)
    {
        var budget = await Budget(client);
        var request = new BudgetUpdateDto(budget.Month, budget.Note, budget.Version, budget.FundsAdded, budget.Groups.Select(g => new CategoryGroupUpdateDto(g.Id, g.Name, g.SortOrder, g.IsArchived,
            g.Categories.Select(c => new CategoryUpdateDto(c.Id, c.Name, c.Assigned, c.Target, c.Icon, c.Snoozed, c.SortOrder, c.IsArchived || c.Id == categoryId)).ToList())).ToList());
        await Put<BudgetDto>(client, "/api/budget/2026-10", request);
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
