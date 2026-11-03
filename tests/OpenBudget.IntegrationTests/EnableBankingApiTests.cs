using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenBudget.Api;

namespace OpenBudget.IntegrationTests;

public sealed class EnableBankingApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Supports_multiple_sessions_per_bank_and_stages_a_suggested_match_until_verified()
    {
        await using var app = await fixture.CreateAppAsync();
        await McpTestKit.CreateUserAsync(app, "banking-user");
        using var client = await McpTestKit.LoginAsync(app, "banking-user");
        var budget = await McpTestKit.CreateBudgetAsync(client, "Banking");
        client.DefaultRequestHeaders.Add("Cookie", $"openbudget.activeBudgetId={budget.Id}");

        var initialAccounts = await client.GetFromJsonAsync<AccountsDto>("/api/accounts");
        var accountResult = await client.PostAsJsonAsync("/api/accounts",
            new CreateAccountRequest(Guid.NewGuid(), initialAccounts!.Version, "Checking", new DateOnly(2026, 10, 21), 0, "checking"));
        Assert.True(accountResult.IsSuccessStatusCode, await accountResult.Content.ReadAsStringAsync());
        var accounts = await client.GetFromJsonAsync<AccountsDto>("/api/accounts");
        var account = Assert.Single(accounts!.Accounts);
        var date = new DateOnly(2026, 10, 20);
        var existing = await client.PostAsJsonAsync("/api/transactions", new TransactionRequest(Guid.NewGuid(), accounts.Version,
            account.Id, date, -18.40m, "Albert Heijn", "Groceries", null, "uncleared", null));
        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);

        var start = await client.PostAsJsonAsync("/api/banking/connections/start", new StartBankConnectionRequest("NL", "Test Bank", 30));
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var state = app.EnableBanking.StartedStates.Last();
        using var callbackClient = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var callback = await callbackClient.GetAsync($"/api/banking/callback?state={Uri.EscapeDataString(state)}&code=first");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        var replay = await callbackClient.GetAsync($"/api/banking/callback?state={Uri.EscapeDataString(state)}&code=replay");
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);

        var secondStart = await client.PostAsJsonAsync("/api/banking/connections/start", new StartBankConnectionRequest("NL", "Test Bank", 60));
        Assert.Equal(HttpStatusCode.OK, secondStart.StatusCode);
        var secondState = app.EnableBanking.StartedStates.Last();
        Assert.Equal(HttpStatusCode.Redirect, (await callbackClient.GetAsync($"/api/banking/callback?state={Uri.EscapeDataString(secondState)}&code=second")).StatusCode);

        var overview = await client.GetFromJsonAsync<BankOverviewDto>("/api/banking/overview");
        Assert.Equal(2, overview!.Connections.Count);
        var first = overview.Connections[0];
        Assert.Equal("Test Bank", first.BankName);
        Assert.Equal(2, first.Accounts.Count);
        var linkedAccount = first.Accounts[0];
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/banking/accounts/{linkedAccount.Id}/map", new MapBankAccountRequest(account.Id))).StatusCode);

        await using (var setupDb = app.CreateDb())
        {
            var connection = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(setupDb.BankConnections, row => row.Id == first.Id);
            connection.AccessValidUntil = app.Clock.UtcNow.AddMinutes(-1);
            await setupDb.SaveChangesAsync();
        }
        await using (var scope = app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<BankConnectionsService>().SyncDueAsync(CancellationToken.None);
        var expired = await client.GetFromJsonAsync<BankOverviewDto>("/api/banking/overview");
        Assert.Equal("reauthorization_required", Assert.Single(expired!.Connections, row => row.Id == first.Id).Status);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/banking/connections/{first.Id}/reauthorize", null)).StatusCode);
        var reconnectState = app.EnableBanking.StartedStates.Last();
        Assert.Equal(HttpStatusCode.Redirect, (await callbackClient.GetAsync($"/api/banking/callback?state={Uri.EscapeDataString(reconnectState)}&code=renewed")).StatusCode);
        var reconnected = await client.GetFromJsonAsync<BankOverviewDto>("/api/banking/overview");
        var restored = Assert.Single(reconnected!.Connections, row => row.Id == first.Id);
        Assert.Equal("active", restored.Status);
        Assert.Equal(account.Id, Assert.Single(restored.Accounts, row => row.Id == linkedAccount.Id).AccountId);

        app.EnableBanking.Transactions = [
            new EnableTransaction("txn-1", "reference-1", "booked", date, -18.40m, "EUR", "ALBERTHEIJN NL", "Groceries"),
            new EnableTransaction("txn-2", "reference-2", "booked", date.AddDays(-1), -12m, "EUR", "Albert Heijn Leiden", "Groceries")
        ];
        await using (var scope = app.Services.CreateAsyncScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<BankConnectionsService>().SyncDueAsync(CancellationToken.None));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/banking/connections/{first.Id}/sync", null)).StatusCode);
        var imports = await client.GetFromJsonAsync<BankImportDto[]>("/api/banking/imports");
        Assert.Equal(2, imports!.Length);
        var import = Assert.Single(imports, row => row.Amount == -18.40m);
        Assert.NotNull(import.Candidate);
        Assert.Equal("Albert Heijn", import.Candidate.Payee);
        var payeeSuggestion = Assert.Single(imports, row => row.Amount == -12m);
        Assert.Equal("Albert Heijn", payeeSuggestion.SuggestedPayee);
        var register = await client.GetFromJsonAsync<RegisterDto>($"/api/transactions?accountId={account.Id}&page=1&pageSize=50");
        Assert.True(Assert.Single(register!.Items).BankMatchToVerify);
        var before = await client.GetFromJsonAsync<AccountsDto>("/api/accounts");
        Assert.Equal(-18.40m, Assert.Single(before!.Accounts).Balance);

        var verified = await client.PostAsJsonAsync($"/api/banking/imports/{import.Id}/review",
            new ReviewBankImportRequest("verify-match", import.Candidate.Id));
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        var after = await client.GetFromJsonAsync<AccountsDto>("/api/accounts");
        Assert.Equal(-18.40m, Assert.Single(after!.Accounts).Balance);
        var accepted = await client.PostAsJsonAsync($"/api/banking/imports/{payeeSuggestion.Id}/review",
            new ReviewBankImportRequest("accept", UseSuggestedPayee: true));
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        var finalAccounts = await client.GetFromJsonAsync<AccountsDto>("/api/accounts");
        Assert.Equal(-30.40m, Assert.Single(finalAccounts!.Accounts).Balance);
        var finalRegister = await client.GetFromJsonAsync<RegisterDto>($"/api/transactions?accountId={account.Id}&page=1&pageSize=50");
        Assert.Contains(finalRegister!.Items, entry => entry.Payee == "Albert Heijn" && entry.Amount == -12m);
        using var db = app.CreateDb();
        Assert.Equal("matched", (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.BankImports, row => row.ProviderTransactionId == "txn-1")).ReviewStatus);
    }
}
