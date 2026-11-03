using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenBudget.Api;
using Testcontainers.PostgreSql;

namespace OpenBudget.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public Task InitializeAsync() => container.StartAsync();
    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    public async Task<TestApp> CreateAppAsync()
    {
        // Each scenario gets a fresh database in a disposable container, never the Aspire volume.
        var database = "test_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
        await command.ExecuteNonQueryAsync();
        return new TestApp(new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = database }.ConnectionString);
    }
}

public sealed class TestApp(string connectionString) : WebApplicationFactory<Program>
{
    private static readonly IServiceProvider identitySchemaServices = new ServiceCollection()
        .Configure<IdentityOptions>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
        .BuildServiceProvider();
    public string ConnectionString { get; } = connectionString;
    public FailureInjector Failure { get; } = new();
    public TestClock Clock { get; } = new();
    public TestAccountEmailSender Email { get; } = new();
    public FakeEnableBankingClient EnableBanking { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EnableBanking:ApplicationId"] = "integration-test-app",
            ["EnableBanking:PrivateKeyPath"] = "/not-read-by-fake-client.pem",
            ["EnableBanking:RedirectUrl"] = "https://budget.example.test/api/banking/callback"
        }));
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BudgetDbContext>>();
            services.RemoveAll<BudgetDbContext>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IAccountEmailSender>();
            services.AddSingleton<IAccountEmailSender>(Email);
            services.RemoveAll<IEnableBankingClient>();
            services.AddSingleton<IEnableBankingClient>(EnableBanking);
            services.AddDbContext<BudgetDbContext>(options => options.UseNpgsql(ConnectionString).AddInterceptors(Failure));
        });
    }
    public BudgetDbContext CreateDb() => new(new DbContextOptionsBuilder<BudgetDbContext>()
        .UseApplicationServiceProvider(identitySchemaServices)
        .UseNpgsql(ConnectionString)
        .Options);
}

public sealed class FakeEnableBankingClient : IEnableBankingClient
{
    public List<EnableTransaction> Transactions { get; set; } = [];
    public List<string> StartedStates { get; } = [];
    public Task<IReadOnlyList<EnableBank>> GetBanksAsync(string country, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EnableBank>>([new EnableBank("Test Bank", country, null, null, 60 * 60 * 24 * 90)]);
    public Task<string> StartAuthorizationAsync(string country, string bankName, string state, string redirectUrl, string psuId, DateTimeOffset validUntil, CancellationToken ct)
    {
        StartedStates.Add(state);
        return Task.FromResult($"https://provider.example.test/authorize?state={Uri.EscapeDataString(state)}");
    }
    public Task<EnableSession> CreateSessionAsync(string code, CancellationToken ct) => Task.FromResult(new EnableSession(
        "session-" + code, DateTimeOffset.UtcNow.AddDays(60), [
            new EnableAccount("provider-account-1", "Checking", "EUR", "identity-1"),
            new EnableAccount("provider-account-2", "Savings", "EUR", "identity-2")
        ]));
    public Task<EnableTransactionPage> GetTransactionsAsync(string sessionId, string accountUid, DateOnly dateFrom, string? continuationKey, CancellationToken ct) =>
        Task.FromResult(new EnableTransactionPage(Transactions, null));
    public Task RevokeSessionAsync(string sessionId, CancellationToken ct) => Task.CompletedTask;
}

public sealed class TestAccountEmailSender : IAccountEmailSender
{
    public bool IsConfigured { get; set; } = true;
    public bool FailSending { get; set; }
    public List<(string To, string Subject, AccountEmailContent Content)> Messages { get; } = [];
    public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken) => Task.FromResult(IsConfigured);
    public Task SendAsync(string to, string subject, AccountEmailContent content, CancellationToken cancellationToken)
    {
        if (FailSending) throw new InvalidOperationException("Simulated email delivery failure.");
        Messages.Add((to, subject, content));
        return Task.CompletedTask;
    }
}

public sealed class FailureInjector : SaveChangesInterceptor
{
    public bool FailAuditSave { get; set; }
    public bool FailBudgetAfterSave { get; set; }
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (FailBudgetAfterSave) { FailBudgetAfterSave=false; throw new InvalidOperationException("Simulated failure after month rows were written, before commit."); }
        return ValueTask.FromResult(result);
    }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (FailAuditSave && eventData.Context!.ChangeTracker.Entries<LedgerCommandEntity>().Any(e => e.State == EntityState.Added))
        {
            FailAuditSave = false;
            throw new InvalidOperationException("Simulated failure after ledger rows were written, before command commit.");
        }
        return ValueTask.FromResult(result);
    }
}

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026,10,21,12,0,0,TimeSpan.Zero);
    public TimeZoneInfo Zone { get; set; } = TimeZoneInfo.Utc;
    public override DateTimeOffset GetUtcNow() => UtcNow;
    public override TimeZoneInfo LocalTimeZone => Zone;
}
