using System.Text.RegularExpressions;
using OpenBudget.Api;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("budgetdatabase")
    ?? builder.Configuration.GetConnectionString("BudgetDatabase")
    ?? "Host=localhost;Port=5432;Database=budgetboard;Username=budgetboard;Password=budgetboard_dev";
builder.Services.AddDbContext<BudgetDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<BudgetService>();
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

app.MapGet("/api/budget/{month}", async (string month, BudgetService budgets, CancellationToken cancellationToken) =>
{
    if (!IsMonth(month)) return Results.BadRequest(new { error = "Month must use YYYY-MM format." });
    var budget = await budgets.GetMonthAsync(month, cancellationToken);
    return Results.Ok(BudgetDto.From(budget));
});

app.MapGet("/api/budget", async (BudgetService budgets, CancellationToken cancellationToken) =>
    Results.Ok(await budgets.GetPlanAsync(cancellationToken)));

app.MapPut("/api/budget", async (BudgetPlanUpdateDto request, BudgetService budgets, CancellationToken cancellationToken) =>
{
    var result = await budgets.UpdatePlanAsync(request, cancellationToken);
    if (result.IsConflict)
        return Results.Conflict(new { error = "This budget changed in another session.", current = result.Plan });
    if (result.Error is not null) return Results.BadRequest(new { error = result.Error });
    return Results.Ok(result.Plan);
});

app.MapPut("/api/budget/{month}", async (string month, BudgetUpdateDto request, BudgetService budgets, CancellationToken cancellationToken) =>
{
    if (!IsMonth(month)) return Results.BadRequest(new { error = "Month must use YYYY-MM format." });
    if (request.Month != month) return Results.BadRequest(new { error = "Route month must match payload month." });

    var result = await budgets.UpdateMonthAsync(request, cancellationToken);
    if (result.IsConflict)
        return Results.Conflict(new { error = "This month changed in another session.", current = result.Budget });
    if (result.Error is not null) return Results.BadRequest(new { error = result.Error });
    return Results.Ok(result.Budget);
});

app.Run();

static bool IsMonth(string month) => Regex.IsMatch(month, @"^\d{4}-(0[1-9]|1[0-2])$");
