using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenBudget.Api;

namespace OpenBudget.IntegrationTests;

// The GlobalCategories migration against a database seeded with legacy per-month categories: several budgets and months,
// a month created from the starter template whose names, groups and order differ from the user's (the bug that motivated
// the change), a category that exists only in one middle month, and money in every state column.
public sealed class GlobalCategoriesMigrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string PreviousMigration = "20261031210930_AccountSortOrder";

    private const string LegacySeed = """
        INSERT INTO "BudgetPlans" ("Id", "OwnerUserId", "Name", "Currency", "IsArchived", "CreatedAt", "Version") VALUES
            ('mig-a', NULL, 'Synthetic A', 'EUR', false, '2026-01-01T00:00:00Z', 1),
            ('mig-b', NULL, 'Synthetic B', 'EUR', false, '2026-01-01T00:00:00Z', 1),
            ('mig-c', NULL, 'Synthetic C', 'EUR', false, '2026-01-01T00:00:00Z', 1);
        INSERT INTO "Budgets" ("Month", "BudgetId", "Note", "OpeningReadyToAssign", "FundsAdded", "LedgerIncome", "Version") VALUES
            ('2026-06', 'mig-a', '', 0, 0, 0, 1),
            ('2026-08', 'mig-a', '', 500, 0, 0, 3),
            ('2026-09', 'mig-a', '', 420, 0, 0, 2),
            ('2026-10', 'mig-a', 'October', 400, 0, 0, 5),
            ('2026-10', 'mig-b', '', 0, 0, 0, 1),
            ('2026-11', 'mig-b', '', 0, 0, 0, 1);
        -- mig-a 2026-06 was created from the starter template (template names and order).
        INSERT INTO "CategoryGroups" ("BudgetId", "BudgetMonth", "Id", "Name", "SortOrder", "IsArchived") VALUES
            ('mig-a', '2026-06', 'home', 'Home', 0, false),
            ('mig-a', '2026-06', 'daily', 'Everyday', 1, false),
            ('mig-a', '2026-08', 'home', 'Home', 0, false),
            ('mig-a', '2026-08', 'daily', 'Daily stuff', 1, false),
            ('mig-a', '2026-08', 'bills', 'Bills', 2, false),
            ('mig-a', '2026-09', 'home', 'Home', 0, false),
            ('mig-a', '2026-09', 'daily', 'Daily stuff', 1, false),
            ('mig-a', '2026-09', 'bills', 'Bills', 2, false),
            ('mig-a', '2026-10', 'bills', 'Fixed bills', 0, false),
            ('mig-a', '2026-10', 'home', 'Home', 1, false),
            ('mig-a', '2026-10', 'daily', 'Daily stuff', 2, false),
            ('mig-b', '2026-10', 'home', 'Wohnen', 0, false),
            ('mig-b', '2026-11', 'home', 'Wohnen', 0, false);
        INSERT INTO "Categories" ("BudgetId", "BudgetMonth", "Id", "GroupId", "Name", "OpeningAvailable", "Assigned", "LegacyActivity", "Activity", "CreditOverspending", "Target", "Icon", "Snoozed", "SortOrder", "IsArchived") VALUES
            ('mig-a', '2026-06', 'rent', 'home', 'Rent & mortgage', 0, 0, 0, 0, 0, 0, 'h', false, 0, false),
            ('mig-a', '2026-06', 'utilities', 'home', 'Electricity & water', 0, 0, 0, 0, 0, 0, 'u', false, 1, false),
            ('mig-a', '2026-06', 'coffee', 'daily', 'Coffee & lunch', 0, 0, 0, 0, 0, 0, NULL, false, 0, false),
            ('mig-a', '2026-08', 'rent', 'home', 'Rent', 10.50, 800, -5.25, -800, 0, 800, 'h', false, 0, false),
            ('mig-a', '2026-08', 'utilities', 'bills', 'Power', 0, 60, 0, -45.10, 0, 0, 'u', true, 0, false),
            ('mig-a', '2026-08', 'coffee', 'daily', 'Coffee', 0, 20, 0, -35, 12.40, 0, NULL, false, 0, false),
            ('mig-a', '2026-09', 'rent', 'home', 'Rent', 5.25, 800, 0, -800, 0, 800, 'h', false, 0, false),
            ('mig-a', '2026-09', 'utilities', 'bills', 'Power', 14.90, 50, 0, -60, 0, 0, 'u', false, 0, false),
            ('mig-a', '2026-09', 'coffee', 'daily', 'Coffee', 0, 0, 0, 0, 0, 0, NULL, false, 0, true),
            ('mig-a', '2026-09', 'category-gym', 'daily', 'Gym', 0, 30, 0, -30, 0, 0, 'g', false, 1, false),
            ('mig-a', '2026-10', 'rent', 'home', 'Rent', 5.25, 820, 0, 0, 0, 820, 'h', false, 0, false),
            ('mig-a', '2026-10', 'utilities', 'bills', 'Electricity', 4.90, 55, 0, 0, 0, 0, 'e', false, 0, false),
            ('mig-a', '2026-10', 'coffee', 'daily', 'Coffee', 0, 0, 0, 0, 0, 0, NULL, false, 0, true),
            ('mig-b', '2026-10', 'rent', 'home', 'Miete', 0, 900, 0, -900, 0, 0, NULL, false, 0, false),
            ('mig-b', '2026-11', 'rent', 'home', 'Miete', 0, 900, 0, 0, 0, 0, NULL, false, 0, false);
        """;

    private const string LegacyStateQuery = """
        SELECT "BudgetId", "BudgetMonth", "Id", "OpeningAvailable", "Assigned", "LegacyActivity", "Activity", "CreditOverspending", "Target", "Snoozed"
        FROM {0} ORDER BY 1, 2, 3
        """;

    private static async Task<List<string>> RowsAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
            rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture))));
        return rows;
    }

    [Fact]
    public async Task Upgrade_dedupes_definitions_by_latest_month_and_preserves_every_month_value()
    {
        await using var app = await fixture.CreateAppAsync();
        List<string> legacyStates;
        await using (var db = app.CreateDb())
        {
            await db.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await db.Database.ExecuteSqlRawAsync(LegacySeed);
            legacyStates = await RowsAsync(app.ConnectionString, string.Format(LegacyStateQuery, "\"Categories\""));
            await db.Database.MigrateAsync();
        }

        // Every per-month row survived unchanged.
        var migrated = await RowsAsync(app.ConnectionString, string.Format(LegacyStateQuery.Replace("\"Id\"", "\"CategoryId\""), "\"CategoryMonths\""));
        Assert.Equal(legacyStates, migrated);
        Assert.Equal(15, migrated.Count);
        // The pre-migration rows are kept for audit.
        Assert.Equal(legacyStates, await RowsAsync(app.ConnectionString, string.Format(LegacyStateQuery, "\"LegacyMonthCategories\"")));

        await using (var db = app.CreateDb())
        {
            var categories = await db.Categories.Where(c => c.BudgetId == "mig-a").ToDictionaryAsync(c => c.Id);
            var groups = await db.CategoryGroups.Where(g => g.BudgetId == "mig-a").ToDictionaryAsync(g => g.Id);
            // Latest month wins: October renamed "utilities" to Electricity in Fixed bills; the June template names are gone.
            Assert.Equal(("Electricity", "bills", "e"), (categories["utilities"].Name, categories["utilities"].GroupId, categories["utilities"].Icon));
            Assert.Equal(("Rent", "home"), (categories["rent"].Name, categories["rent"].GroupId));
            Assert.True(categories["coffee"].IsArchived);
            // A category only present in September keeps its September definition.
            Assert.Equal(("Gym", "daily", false), (categories["category-gym"].Name, categories["category-gym"].GroupId, categories["category-gym"].IsArchived));
            Assert.Equal(("Fixed bills", 0), (groups["bills"].Name, groups["bills"].SortOrder));
            Assert.Equal(("Daily stuff", 2), (groups["daily"].Name, groups["daily"].SortOrder));
            Assert.Equal(new[] { 0, 1 }, categories.Values.Where(c => c.GroupId == "daily").Select(c => c.SortOrder).Order());
            // Budgets stay isolated even with the same category IDs.
            Assert.Equal("Miete", (await db.Categories.SingleAsync(c => c.BudgetId == "mig-b" && c.Id == "rent")).Name);
            Assert.Equal("Wohnen", (await db.CategoryGroups.SingleAsync(g => g.BudgetId == "mig-b")).Name);
            var flags = await db.BudgetPlans.Where(p => p.Id.StartsWith("mig-")).ToDictionaryAsync(p => p.Id, p => p.CategoriesInitialized);
            Assert.Equal(new Dictionary<string, bool> { ["mig-a"] = true, ["mig-b"] = true, ["mig-c"] = false }, flags);
        }

        // The template-seeded June now shows the user's categories with its own (zero) money, and the September-only
        // category appears in every month.
        await using (var db = app.CreateDb())
        {
            var budgets = new BudgetService(db, new LedgerProjection(db));
            var june = BudgetDto.From(await budgets.GetMonthAsync("mig-a", "2026-06", CancellationToken.None));
            var october = BudgetDto.From(await budgets.GetMonthAsync("mig-a", "2026-10", CancellationToken.None));
            Assert.Equal(june.Groups.SelectMany(g => g.Categories.Select(c => $"{g.Name}:{c.Id}:{c.Name}")), october.Groups.SelectMany(g => g.Categories.Select(c => $"{g.Name}:{c.Id}:{c.Name}")));
            Assert.Contains(june.Groups.SelectMany(g => g.Categories), c => c.Id == "category-gym" && c.Assigned == 0);
            Assert.Equal("Electricity", june.Groups.SelectMany(g => g.Categories).Single(c => c.Id == "utilities").Name);
            var august = BudgetDto.From(await budgets.GetMonthAsync("mig-a", "2026-08", CancellationToken.None));
            var rent = august.Groups.SelectMany(g => g.Categories).Single(c => c.Id == "rent");
            Assert.Equal((800m, -800m, 10.50m), (rent.Assigned, rent.Activity, rent.Available));
            Assert.Equal(12.40m, august.Groups.SelectMany(g => g.Categories).Single(c => c.Id == "coffee").CreditOverspending);
            // A new month for the never-initialized budget gets the starter template exactly once.
            var first = await budgets.GetMonthAsync("mig-c", "2026-10", CancellationToken.None);
            Assert.Equal(15, first.Groups.Sum(g => g.Categories.Count));
        }
    }

    [Fact]
    public async Task Downgrade_rebuilds_per_month_rows_and_upgrading_again_gives_the_same_result()
    {
        await using var app = await fixture.CreateAppAsync();
        await using var db = app.CreateDb();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await db.Database.ExecuteSqlRawAsync(LegacySeed);
        var legacy = await RowsAsync(app.ConnectionString, string.Format(LegacyStateQuery, "\"Categories\""));
        await db.Database.MigrateAsync();
        var upgraded = await RowsAsync(app.ConnectionString, string.Format(LegacyStateQuery.Replace("\"Id\"", "\"CategoryId\""), "\"CategoryMonths\""));

        await migrator.MigrateAsync(PreviousMigration);
        // Every month now holds every budget category; months that had a row keep its exact values, others are zero.
        var rebuilt = await RowsAsync(app.ConnectionString, string.Format(LegacyStateQuery, "\"Categories\""));
        Assert.Equal(4 * 4 + 2 * 1, rebuilt.Count);
        Assert.All(legacy, row => Assert.Contains(row, rebuilt));
        Assert.All(rebuilt.Except(legacy), row => Assert.EndsWith("|0.00|0.00|0.00|0.00|0.00|0.00|False", row));
        var names = await RowsAsync(app.ConnectionString, """SELECT DISTINCT "Name" FROM "Categories" WHERE "BudgetId" = 'mig-a' AND "Id" = 'utilities'""");
        Assert.Equal(new[] { "Electricity" }, names);

        await db.Database.MigrateAsync();
        var again = await RowsAsync(app.ConnectionString, string.Format(LegacyStateQuery.Replace("\"Id\"", "\"CategoryId\""), "\"CategoryMonths\""));
        Assert.All(upgraded, row => Assert.Contains(row, again));
        Assert.Equal(rebuilt.Count, again.Count);
    }
}
