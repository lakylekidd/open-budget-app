using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenBudget.Api;

public sealed class BudgetDbContextFactory : IDesignTimeDbContextFactory<BudgetDbContext>
{
    public BudgetDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__BudgetDatabase")
            ?? "Host=localhost;Port=5432;Database=budgetboard;Username=budgetboard;Password=budgetboard_dev";
        var options = new DbContextOptionsBuilder<BudgetDbContext>().UseNpgsql(connectionString).Options;
        return new BudgetDbContext(options);
    }
}
