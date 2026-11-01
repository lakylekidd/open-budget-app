using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenBudget.Api.Mcp;
using static OpenBudget.IntegrationTests.McpTestKit;

namespace OpenBudget.IntegrationTests;

/// <summary>Issue #15: explicit no-expiration agent tokens and renewal (secret rotation).</summary>
public sealed class AgentTokenRenewalApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string TokensPath = "/api/auth/agent-tokens";

    [Fact]
    public async Task No_expiration_requires_explicit_opt_in_and_authenticates_past_the_default_expiry_until_revoked()
    {
        await using var app = await fixture.CreateAppAsync();
        await CreateUserAsync(app, "owner");
        var owner = await LoginAsync(app, "owner");
        var budget = await CreateBudgetAsync(owner, "Household");
        var createdAt = app.Clock.UtcNow;

        // Omitting the lifetime keeps the dated default; contradictory input is rejected without writes.
        var dated = await CreateAsync(owner, new CreateAgentTokenRequest("Dated", "Reporting", [budget.Id], null));
        Assert.Equal(createdAt.AddDays(AgentTokenService.DefaultExpiryDays), dated.Details.ExpiresAt);
        var both = await owner.PostAsJsonAsync(TokensPath, new CreateAgentTokenRequest("Both", "Reporting", [budget.Id], 30, true));
        Assert.Equal(HttpStatusCode.BadRequest, both.StatusCode);
        var explicitFalse = await CreateAsync(owner, new CreateAgentTokenRequest("Explicit false", "Reporting", [budget.Id], null, false));
        Assert.NotNull(explicitFalse.Details.ExpiresAt);

        var forever = await CreateAsync(owner, new CreateAgentTokenRequest("Forever", "Reporting", [budget.Id], null, true));
        Assert.Null(forever.Details.ExpiresAt);
        Assert.Equal("active", forever.Details.Status);
        await using (var db = app.CreateDb()) Assert.Equal(3, await db.Set<AgentTokenEntity>().CountAsync());

        // Well past the maximum dated expiry: dated tokens are rejected, the explicit no-expiry token still works.
        app.Clock.UtcNow = createdAt.AddDays(AgentTokenService.MaxExpiryDays + 30);
        using var datedMcp = Mcp(app, dated.Token);
        using (var expired = await datedMcp.PostRawAsync("tools/list", null)) Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        using var foreverMcp = Mcp(app, forever.Token);
        (await foreverMcp.CallAsync("budgets_list")).AssertOk();
        // Cookie sessions last 12 hours, so sign in again at the new server time.
        owner.Dispose();
        owner = await LoginAsync(app, "owner");
        var listing = (await owner.GetFromJsonAsync<List<AgentTokenDto>>(TokensPath))!;
        Assert.Equal("expired", listing.Single(x => x.Id == dated.Details.Id).Status);
        Assert.Equal("active", listing.Single(x => x.Id == forever.Details.Id).Status);

        // Revocation still applies to non-expiring tokens.
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"{TokensPath}/{forever.Details.Id}/revoke", null)).StatusCode);
        using (var revoked = await foreverMcp.PostRawAsync("tools/list", null)) Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);

        await using (var db = app.CreateDb())
        {
            var actions = await db.AdminAuditLogs.Select(x => x.Action).ToListAsync();
            Assert.Equal(2, actions.Count(x => x == "agent-token.create.reporting"));
            Assert.Contains("agent-token.create.reporting.no-expiry", actions);
            Assert.Contains("agent-token.revoke", actions);
        }
        owner.Dispose();
    }

    [Fact]
    public async Task Renew_rotates_the_secret_for_the_owner_keeps_scope_and_never_persists_or_logs_plaintext()
    {
        await using var app = await fixture.CreateAppAsync();
        var logs = new CapturingLoggerProvider();
        await using var logged = app.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs).SetMinimumLevel(LogLevel.Trace)));
        await CreateUserAsync(logged, "owner");
        await CreateUserAsync(logged, "other");
        var owner = await LoginAsync(logged, "owner");
        using var other = await LoginAsync(logged, "other");
        var budget = await CreateBudgetAsync(owner, "Household");
        var secrets = new List<string>();

        var original = await CreateAsync(owner, new CreateAgentTokenRequest("Planner", "Budget", [budget.Id], 10));
        secrets.Add(original.Token);
        var id = original.Details.Id;
        using var originalMcp = Mcp(logged, original.Token);
        (await originalMcp.CallAsync("budgets_list")).AssertOk();

        // Only the owner can renew; another user's attempt does not change the token.
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"{TokensPath}/{id}/renew", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync($"{TokensPath}/{Guid.NewGuid()}/renew", null)).StatusCode);
        // Invalid lifetimes are rejected with no partial write.
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"{TokensPath}/{id}/renew", new RenewAgentTokenRequest(400))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"{TokensPath}/{id}/renew", new RenewAgentTokenRequest(5, true))).StatusCode);
        (await originalMcp.CallAsync("budgets_list")).AssertOk();

        // Renew without a body: new secret, same record and scope, old secret rejected immediately, dated mode kept.
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(1);
        var renewedResponse = await owner.PostAsync($"{TokensPath}/{id}/renew", null);
        Assert.Equal(HttpStatusCode.OK, renewedResponse.StatusCode);
        Assert.Equal("no-store", renewedResponse.Headers.CacheControl?.ToString());
        var renewed = (await renewedResponse.Content.ReadFromJsonAsync<CreatedAgentTokenDto>())!;
        secrets.Add(renewed.Token);
        Assert.NotEqual(original.Token, renewed.Token);
        Assert.Matches("^obk_[A-Za-z0-9_-]{43}$", renewed.Token);
        Assert.Equal(id, renewed.Details.Id);
        Assert.Equal(renewed.Token[..12], renewed.Details.Prefix);
        Assert.Equal("Budget", renewed.Details.Permission);
        Assert.Equal(budget.Id, Assert.Single(renewed.Details.Budgets).Id);
        Assert.Equal(original.Details.CreatedAt, renewed.Details.CreatedAt);
        Assert.Equal(app.Clock.UtcNow, renewed.Details.RenewedAt);
        Assert.Equal(app.Clock.UtcNow.AddDays(AgentTokenService.DefaultExpiryDays), renewed.Details.ExpiresAt);
        using (var old = await originalMcp.PostRawAsync("tools/list", null)) Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        using var renewedMcp = Mcp(logged, renewed.Token);
        (await renewedMcp.CallAsync("plan_get_month", new { budgetId = budget.Id, month = "2026-10" })).AssertOk();

        // Explicit switch to no expiration, then a body-less renewal keeps that mode.
        var forever = await RenewAsync(owner, id, new RenewAgentTokenRequest(NeverExpires: true));
        secrets.Add(forever.Token);
        Assert.Null(forever.Details.ExpiresAt);
        var stillForever = await RenewAsync(owner, id, null);
        secrets.Add(stillForever.Token);
        Assert.Null(stillForever.Details.ExpiresAt);

        // An expired dated token can be renewed back into service.
        var shortLived = await CreateAsync(owner, new CreateAgentTokenRequest("Short", "Reporting", [budget.Id], 1));
        secrets.Add(shortLived.Token);
        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(2);
        owner.Dispose();
        owner = await LoginAsync(logged, "owner");
        var revived = await RenewAsync(owner, shortLived.Details.Id, new RenewAgentTokenRequest(7));
        secrets.Add(revived.Token);
        Assert.Equal(app.Clock.UtcNow.AddDays(7), revived.Details.ExpiresAt);
        using (var revivedMcp = Mcp(logged, revived.Token)) (await revivedMcp.CallAsync("budgets_list")).AssertOk();

        // Concurrent renewals: whatever interleaving happens, exactly one returned secret is valid afterwards.
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => owner.PostAsync($"{TokensPath}/{id}/renew", null)));
        Assert.All(concurrent, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        var concurrentSecrets = new List<string>();
        foreach (var response in concurrent.Where(x => x.StatusCode == HttpStatusCode.OK))
            concurrentSecrets.Add((await response.Content.ReadFromJsonAsync<CreatedAgentTokenDto>())!.Token);
        secrets.AddRange(concurrentSecrets);
        var valid = 0;
        foreach (var secret in concurrentSecrets.Append(stillForever.Token))
        {
            using var client = Mcp(logged, secret);
            using var probe = await client.PostRawAsync("tools/list", null);
            if (probe.StatusCode == HttpStatusCode.OK) valid++;
        }
        Assert.Equal(1, valid);

        // Revoked tokens cannot be renewed back to life.
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"{TokensPath}/{id}/revoke", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync($"{TokensPath}/{id}/renew", null)).StatusCode);
        foreach (var secret in concurrentSecrets)
        {
            using var client = Mcp(logged, secret);
            using var probe = await client.PostRawAsync("tools/list", null);
            Assert.Equal(HttpStatusCode.Unauthorized, probe.StatusCode);
        }

        await using (var db = app.CreateDb())
        {
            var actions = await db.AdminAuditLogs.Select(x => x.Action).ToListAsync();
            Assert.Contains("agent-token.renew", actions);
            // Two sequential no-expiry renewals plus one per successful concurrent renewal; failed (409) attempts write nothing.
            Assert.Equal(2 + concurrentSecrets.Count, actions.Count(x => x == "agent-token.renew.no-expiry"));
            Assert.Contains("agent-token.revoke", actions);
            var stored = await db.Set<AgentTokenEntity>().AsNoTracking().ToListAsync();
            var persisted = string.Join("|", stored.Select(x => $"{x.Name}|{x.Prefix}|{x.TokenHash}").Concat(actions));
            Assert.All(secrets, secret => Assert.DoesNotContain(secret, persisted));
        }
        owner.Dispose();
        Assert.NotEmpty(logs.Messages);
        Assert.All(secrets, secret => Assert.DoesNotContain(logs.Messages, message => message.Contains(secret, StringComparison.Ordinal)));
    }

    private static async Task<CreatedAgentTokenDto> CreateAsync(HttpClient client, CreateAgentTokenRequest request)
    {
        var response = await client.PostAsJsonAsync(TokensPath, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedAgentTokenDto>())!;
    }

    private static async Task<CreatedAgentTokenDto> RenewAsync(HttpClient client, Guid id, RenewAgentTokenRequest? request)
    {
        var response = request is null ? await client.PostAsync($"{TokensPath}/{id}/renew", null) : await client.PostAsJsonAsync($"{TokensPath}/{id}/renew", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedAgentTokenDto>())!;
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Logger(Messages);
        public void Dispose() { }

        private sealed class Logger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                // Include structured state values, not only the rendered message.
                var values = state is IEnumerable<KeyValuePair<string, object?>> pairs ? string.Join(";", pairs.Select(x => $"{x.Key}={x.Value}")) : "";
                messages.Enqueue($"{formatter(state, exception)}|{values}|{exception}");
            }
        }
    }
}
