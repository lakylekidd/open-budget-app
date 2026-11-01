using System.Net.Http.Json;
using OpenBudget.Api;

namespace OpenBudget.IntegrationTests;

// The test clock is 21 October 2026.
public sealed class PaymentPresentationApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateOnly Date = new(2026, 10, 21);

    [Fact]
    public async Task Transfers_between_cash_accounts_read_To_and_From()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var checking = await Create(client, "Checking", 500);
        var savings = await Create(client, "Savings", 0);
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), checking, savings, Date, 100m, "Rainy day"));

        var from = Assert.Single(await Register(client, checking), t => t.TransferId is not null);
        Assert.Equal("To Savings", from.Payee);
        Assert.False(from.IsPayment);
        Assert.Null(from.CategoryName);
        Assert.Equal("From Checking", Assert.Single(await Register(client, savings), t => t.TransferId is not null).Payee);
    }

    [Fact]
    public async Task Paying_a_credit_card_from_cash_reads_Payment_with_the_cards_payment_category()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var checking = await Create(client, "Checking", 500);
        var card = await Create(client, "Mastercard", -200, "creditCard");
        var before = await Budget(client);
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), checking, card, Date, 50m, ""));

        var cash = Assert.Single(await Register(client, checking), t => t.TransferId is not null);
        Assert.Equal("Payment: Mastercard", cash.Payee);
        Assert.True(cash.IsPayment);
        Assert.Equal("Credit Card Payments: Mastercard", cash.CategoryName);
        // Presentation only: the stored leg has no category and Ready to Assign is unaffected by the labelling.
        Assert.Null(cash.CategoryId);
        Assert.Equal(before.ReadyToAssign, (await Budget(client)).ReadyToAssign);
        // The card-side leg stays an ordinary transfer.
        var cardLeg = Assert.Single(await Register(client, card), t => t.TransferId is not null);
        Assert.False(cardLeg.IsPayment);
        Assert.Equal("From Checking", cardLeg.Payee == "" ? "From Checking" : cardLeg.Payee);
    }

    [Fact]
    public async Task Paying_a_loan_from_cash_uses_the_paired_category_and_none_when_unpaired()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var checking = await Create(client, "Checking", 5_000);
        var paired = await Create(client, "Car Loan", -5_000, "autoLoan", new LoanTermsRequest(4m, 300m) { NewPairedCategory = new("Car Loan", null, "Debts") });
        var unpaired = await Create(client, "Student Loan", -1_000, "studentLoan", new LoanTermsRequest(2m, 100m));
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), checking, paired, Date, 300m, ""));
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), checking, unpaired, Date, 100m, ""));

        var payments = (await Register(client, checking)).Where(t => t.TransferId is not null).ToList();
        var car = Assert.Single(payments, t => t.Payee == "Payment: Car Loan");
        Assert.Equal("Debts: Car Loan", car.CategoryName);
        var student = Assert.Single(payments, t => t.Payee == "Payment: Student Loan");
        Assert.True(student.IsPayment);
        Assert.Null(student.CategoryName);
    }

    [Fact]
    public async Task Scheduled_payments_and_the_register_agree()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var checking = await Create(client, "Checking", 500);
        var card = await Create(client, "Mastercard", -200, "creditCard");
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), checking, card, Date.AddMonths(1), 25m, "", "monthly"));

        var schedules = await Get<SchedulesDto>(client, $"/api/schedules?accountId={checking}");
        var item = Assert.Single(schedules.Items).Transaction;
        Assert.Equal("Payment: Mastercard", item.Payee);
        Assert.True(item.IsPayment);
        Assert.Equal("Credit Card Payments: Mastercard", item.CategoryName);
    }

    [Fact]
    public async Task Changing_a_loan_payments_category_blanks_the_payee_and_spends_from_that_category()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var checking = await Create(client, "Checking", 5_000);
        var loan = await Create(client, "Car Loan", -5_000, "autoLoan", new LoanTermsRequest(4m, 300m) { NewPairedCategory = new("Car Loan", null, "Debts") });
        var other = (await Budget(client)).Groups.SelectMany(g => g.Categories).First(c => c.Name != "Car Loan" && !c.Id.StartsWith("ccp-")).Id;
        var before = await Budget(client);
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), checking, loan, Date, 300m, "", CategoryId: other));

        var leg = Assert.Single(await Register(client, checking), t => t.TransferId is not null);
        Assert.Equal("", leg.Payee);
        Assert.False(leg.IsPayment);
        Assert.Equal(other, leg.CategoryId);
        var after = await Budget(client);
        Assert.Equal(-300m, after.Groups.SelectMany(g => g.Categories).Single(c => c.Id == other).Activity);
        Assert.Equal(before.ReadyToAssign, after.ReadyToAssign);
        Assert.Equal(-4_700m, (await Get<AccountsDto>(client, "/api/accounts")).Accounts.Single(a => a.Id == loan).Balance);
    }

    [Fact]
    public async Task Tracking_transfers_need_a_category_on_the_budget_side_and_never_touch_the_budget_themselves()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var checking = await Create(client, "Checking", 1_000);
        var invest = await Create(client, "Investments", 0, "trackingAsset");
        var category = (await Budget(client)).Groups.SelectMany(g => g.Categories).First(c => !c.Id.StartsWith("ccp-")).Id;
        var rta = (await Budget(client)).ReadyToAssign;

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), checking, invest, Date, 200m, ""))).StatusCode);
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), checking, invest, Date, 200m, "", CategoryId: category));

        var cash = Assert.Single(await Register(client, checking), t => t.TransferId is not null);
        Assert.Equal("To Investments", cash.Payee);
        Assert.Equal(category, cash.CategoryId);
        var tracked = Assert.Single(await Register(client, invest), t => t.TransferId is not null);
        Assert.Null(tracked.CategoryId);
        Assert.Equal("From Checking", tracked.Payee);
        Assert.Equal(-200m, (await Budget(client)).Groups.SelectMany(g => g.Categories).Single(c => c.Id == category).Activity);
        Assert.Equal(rta, (await Budget(client)).ReadyToAssign);
        // Money coming back from the tracking account is categorized the same way on the budget side.
        await Post<MutationDto>(client, "/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), invest, checking, Date, 50m, "", CategoryId: category));
        Assert.Equal(-150m, (await Budget(client)).Groups.SelectMany(g => g.Categories).Single(c => c.Id == category).Activity);
        // Tracking accounts never raise "needs a category" on their own rows.
        Assert.Equal(0, (await Get<AccountsDto>(client, "/api/accounts")).Accounts.Single(a => a.Id == invest).NeedsCategoryCount);
    }

    [Fact]
    public async Task Cash_to_cash_transfers_reject_a_category()
    {
        await using var app = await fixture.CreateAppAsync(); var client = app.CreateClient();
        var checking = await Create(client, "Checking", 500);
        var savings = await Create(client, "Savings", 0);
        var category = (await Budget(client)).Groups.SelectMany(g => g.Categories).First().Id;
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/transfers", new TransferRequest(Guid.NewGuid(), await Version(client), checking, savings, Date, 10m, "", CategoryId: category))).StatusCode);
    }

    private static async Task<List<TransactionDto>> Register(HttpClient client, Guid accountId) => (await Get<RegisterDto>(client, $"/api/transactions?accountId={accountId}")).Items;
    private static Task<BudgetDto> Budget(HttpClient client) => Get<BudgetDto>(client, "/api/budget/2026-10");
    private static async Task<long> Version(HttpClient client) => (await Get<AccountsDto>(client, "/api/accounts")).Version;
    private static async Task<Guid> Create(HttpClient client, string name, decimal balance, string type = "checking", LoanTermsRequest? loan = null) =>
        (await Post<MutationDto>(client, "/api/accounts", new CreateAccountRequest(Guid.NewGuid(), await Version(client), name, Date, balance, type, loan))).Id;
    private static async Task<T> Get<T>(HttpClient client, string path) => await Read<T>(await client.GetAsync(path));
    private static async Task<T> Post<T>(HttpClient client, string path, object request) => await Read<T>(await client.PostAsJsonAsync(path, request));
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
