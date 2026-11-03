using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace OpenBudget.Api;

public sealed class BudgetDbContextFactory : IDesignTimeDbContextFactory<BudgetDbContext>
{
    public BudgetDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__BudgetDatabase")
            ?? "Host=localhost;Port=5432;Database=budgetboard;Username=budgetboard;Password=budgetboard_dev";
        var identityServices = new ServiceCollection()
            .Configure<IdentityOptions>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .BuildServiceProvider();
        var options = new DbContextOptionsBuilder<BudgetDbContext>()
            .UseApplicationServiceProvider(identityServices)
            .UseNpgsql(connectionString)
            .Options;
        return new BudgetDbContext(options);
    }
}
