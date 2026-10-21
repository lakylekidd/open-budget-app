using System.Text.RegularExpressions;
using OpenBudget.Api;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("budgetdatabase")
    ?? builder.Configuration.GetConnectionString("BudgetDatabase")
    ?? "Host=localhost;Port=5432;Database=budgetboard;Username=budgetboard;Password=budgetboard_dev";
builder.Services.AddDbContext<BudgetDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://127.0.0.1:5173", "http://localhost:5173")
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<BudgetDbContext>().Database.MigrateAsync();

app.MapGet("/health", async (BudgetDbContext db, CancellationToken cancellationToken) =>
{
    try
    {
        return await db.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "ok", database = "connected" })
            : Results.Problem("Database is unavailable", statusCode: 503);
    }
    catch (Npgsql.NpgsqlException)
    {
        return Results.Problem("Database is unavailable", statusCode: 503);
    }
});

app.MapGet("/api/budget/{month}", async (string month, BudgetDbContext db, CancellationToken cancellationToken) =>
{
    if (!IsMonth(month)) return Results.BadRequest(new { error = "Month must use YYYY-MM format." });
    var budget = await db.Budgets.Include(x => x.Groups).ThenInclude(x => x.Categories)
        .SingleOrDefaultAsync(x => x.Month == month, cancellationToken);
    if (budget is null)
    {
        budget = DemoBudget.Create(month);
        db.Budgets.Add(budget);
        await db.SaveChangesAsync(cancellationToken);
    }
    return Results.Ok(BudgetDto.From(budget));
});

app.MapPut("/api/budget/{month}", async (string month, BudgetDto dto, BudgetDbContext db, CancellationToken cancellationToken) =>
{
    if (!IsMonth(month)) return Results.BadRequest(new { error = "Month must use YYYY-MM format." });
    if (dto.Month != month) return Results.BadRequest(new { error = "Route month must match payload month." });
    if (dto.Note?.Length > 1000 || dto.Groups.Count > 100 || dto.Groups.Any(g => string.IsNullOrWhiteSpace(g.Id) || g.Id.Length > 100 || string.IsNullOrWhiteSpace(g.Name) || g.Name.Length > 120 || g.Categories.Count > 500))
        return Results.BadRequest(new { error = "Budget contains invalid group or category data." });
    var categoryDtos = dto.Groups.SelectMany(g => g.Categories).ToList();
    if (dto.Groups.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count() != dto.Groups.Count ||
        dto.Groups.SelectMany(g => g.Categories.Select(c => (GroupId: g.Id, CategoryId: c.Id))).Distinct().Count() != categoryDtos.Count)
        return Results.BadRequest(new { error = "Group and category IDs must be unique." });
    if (dto.Groups.SelectMany(g => g.Categories).Any(c => string.IsNullOrWhiteSpace(c.Id) || c.Id.Length > 100 || string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 160 || c.Target < 0))
        return Results.BadRequest(new { error = "Category data is invalid. IDs and names are required, and targets cannot be negative." });

    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    var budget = await db.Budgets.Include(x => x.Groups).ThenInclude(x => x.Categories)
        .SingleOrDefaultAsync(x => x.Month == month, cancellationToken);
    if (budget is null)
    {
        budget = new BudgetEntity { Month = month };
        db.Budgets.Add(budget);
    }
    else
    {
        db.CategoryGroups.RemoveRange(budget.Groups);
        await db.SaveChangesAsync(cancellationToken);
        budget.Groups = [];
    }
    budget.Note = dto.Note ?? "";
    budget.Groups.Clear();
    foreach (var groupDto in dto.Groups)
    {
        var group = new CategoryGroupEntity { BudgetMonth = month, Id = groupDto.Id, Name = groupDto.Name.Trim(), SortOrder = groupDto.SortOrder };
        foreach (var categoryDto in groupDto.Categories)
            group.Categories.Add(new CategoryEntity
            {
                BudgetMonth = month, GroupId = groupDto.Id, Id = categoryDto.Id, Name = categoryDto.Name.Trim(), Assigned = categoryDto.Assigned,
                Activity = categoryDto.Activity, Available = categoryDto.Available, Target = categoryDto.Target,
                Icon = categoryDto.Icon, Snoozed = categoryDto.Snoozed, SortOrder = categoryDto.SortOrder
            });
        budget.Groups.Add(group);
    }
    await db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return Results.Ok(BudgetDto.From(budget));
});

app.Run();

static bool IsMonth(string month) => Regex.IsMatch(month, @"^\d{4}-(0[1-9]|1[0-2])$");
