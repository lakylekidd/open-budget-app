using System.Text.RegularExpressions;
using OpenBudget.Api;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("budgetdatabase")
    ?? builder.Configuration.GetConnectionString("BudgetDatabase")
    ?? "Host=localhost;Port=5432;Database=budgetboard;Username=budgetboard;Password=budgetboard_dev";
builder.Services.AddDbContext<BudgetDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddOpenBudgetIdentity(builder.Environment);
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<LongTermPlanningService>();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<LedgerGate>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
if (!builder.Environment.IsEnvironment("Testing")) builder.Services.AddHostedService<SchedulePostingWorker>();
builder.Services.AddScoped<RegisterViewService>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://127.0.0.1:5173", "http://localhost:5173")
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) ||
        HttpMethods.IsOptions(context.Request.Method) || HttpMethods.IsTrace(context.Request.Method) ||
        !context.Request.Headers.TryGetValue("Origin", out var origin) || string.IsNullOrEmpty(origin))
    {
        await next(context);
        return;
    }
    if (!Uri.TryCreate(origin.ToString(), UriKind.Absolute, out var source) ||
        !string.Equals(source.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(source.Scheme, context.Request.Scheme, StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next(context);
});
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (LedgerException error)
    {
        context.Response.StatusCode = error.StatusCode;
        await context.Response.WriteAsJsonAsync(new { error = error.Message }, context.RequestAborted);
    }
    catch (DbUpdateConcurrencyException)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { error = "Data changed in another session. Reload and retry." }, context.RequestAborted);
    }
});
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<BudgetDbContext>().Database.MigrateAsync();
    if (!app.Environment.IsEnvironment("Testing"))
        await AuthenticationSetup.EnsureBootstrapAdministratorAsync(scope.ServiceProvider, app.Configuration, app.Environment);
}

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
}).AllowAnonymous();

app.MapAuthenticationEndpoints();

app.MapGet("/api/budget/{month}", async (string month, BudgetService budgets, LedgerGate gate, CancellationToken cancellationToken) =>
{
    if (!IsMonth(month)) return Results.BadRequest(new { error = "Month must use YYYY-MM format." });
    return await gate.RunAsync(async () => Results.Ok(BudgetDto.From(await budgets.GetMonthAsync(month, cancellationToken))), cancellationToken);
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

app.MapPut("/api/budget/{month}", async (string month, BudgetUpdateDto request, BudgetService budgets, LedgerGate gate, CancellationToken cancellationToken) =>
{
    if (!IsMonth(month)) return Results.BadRequest(new { error = "Month must use YYYY-MM format." });
    if (request.Month != month) return Results.BadRequest(new { error = "Route month must match payload month." });

    var result = await gate.RunAsync(() => budgets.UpdateMonthAsync(request, cancellationToken), cancellationToken,
        result => result.Error is null && !result.IsConflict);
    if (result.IsConflict)
        return Results.Conflict(new { error = "This month changed in another session.", current = result.Budget });
    if (result.Error is not null) return Results.BadRequest(new { error = result.Error });
    return Results.Ok(result.Budget);
});

app.MapGet("/api/long-term-planning/{year:int}", async (int year, LongTermPlanningService planning, LedgerGate gate, CancellationToken cancellationToken) =>
{
    if (year is < 1 or > 9999) return Results.BadRequest(new { error = "Year must be between 0001 and 9999." });
    return await gate.RunAsync(async () => Results.Ok(await planning.GetYearAsync(year, cancellationToken)), cancellationToken);
});

app.MapGet("/api/long-term-planning/{year:int}/activity/{month}", async (int year, string month, LongTermPlanningService planning, LedgerGate gate, CancellationToken cancellationToken) =>
{
    if (year is < 1 or > 9999 || !DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var date) || date.Year != year || month != $"{year:D4}-{date.Month:D2}")
        return Results.BadRequest(new { error = "Month must use YYYY-MM format and belong to the selected year." });
    return await gate.RunAsync(async () => Results.Ok(await planning.GetActivityAsync(year, month, cancellationToken)), cancellationToken);
});

app.MapPut("/api/long-term-planning/{year:int}", async (int year, LongTermPlanningUpdateDto request, LongTermPlanningService planning, LedgerGate gate, CancellationToken cancellationToken) =>
{
    var result = await gate.RunAsync(() => planning.UpdateAsync(year, request, cancellationToken), cancellationToken,
        value => value.Error is null && !value.IsConflict);
    if (result.IsConflict)
        return Results.Conflict(new { error = "Long-term planning changed in another session.", current = result.Plan });
    if (result.Error is not null) return Results.BadRequest(new { error = result.Error });
    return Results.Ok(result.Plan);
});

app.MapGet("/api/accounts", async (LedgerService ledger, LedgerGate gate, CancellationToken ct) => {
    if(!app.Environment.IsEnvironment("Testing"))await ledger.PostDueSchedulesAsync(ct);
    return await gate.RunAsync(() => ledger.GetAccountsAsync(ct), ct);
});
app.MapGet("/api/register-views/{scope}", (string scope, RegisterViewService views, LedgerGate gate, CancellationToken ct) =>
    gate.RunAsync(() => views.GetAsync(scope, ct), ct));
app.MapPut("/api/register-views/{scope}", (string scope, RegisterViewDto request, RegisterViewService views, LedgerGate gate, CancellationToken ct) =>
    gate.RunAsync(() => views.SaveAsync(scope, request, ct), ct));
app.MapPost("/api/accounts", (CreateAccountRequest request, LedgerService ledger, CancellationToken ct) => ledger.CreateAccountAsync(request, ct));
app.MapPut("/api/accounts/{id:guid}", (Guid id, UpdateAccountRequest request, LedgerService ledger, CancellationToken ct) => ledger.UpdateAccountAsync(id, request, ct));
app.MapPost("/api/accounts/{id:guid}/reconcile", (Guid id, ReconcileRequest request, LedgerService ledger, CancellationToken ct) => ledger.ReconcileAsync(id, request, ct));
app.MapGet("/api/transactions", async (Guid? accountId, Guid? transferId, string? search, DateOnly? from, DateOnly? to, string? categoryId, string? status,
    int? page, int? pageSize, bool? includeReconciled, bool? needsAttention, Guid? payeeId, LedgerService ledger, LedgerGate gate, CancellationToken ct) =>
    { if(!app.Environment.IsEnvironment("Testing"))await ledger.PostDueSchedulesAsync(ct);
      return await gate.RunAsync(() => ledger.GetRegisterAsync(accountId, transferId, search, from, to, categoryId, status, page ?? 1, pageSize ?? 50, ct, includeReconciled ?? true, needsAttention ?? false, payeeId), ct); });
app.MapGet("/api/flags", (LedgerService ledger, LedgerGate gate, CancellationToken ct) => gate.RunAsync(()=>ledger.GetFlagsAsync(ct),ct));
app.MapPut("/api/flags", (RenameFlagsRequest request, LedgerService ledger, CancellationToken ct) => ledger.RenameFlagsAsync(request,ct));
app.MapPost("/api/transactions/{id:guid}/flag", (Guid id, SetFlagRequest request, LedgerService ledger, CancellationToken ct) => ledger.SetFlagAsync(id,false,request,ct));
app.MapPost("/api/schedules/{id:guid}/flag", (Guid id, SetFlagRequest request, LedgerService ledger, CancellationToken ct) => ledger.SetFlagAsync(id,true,request,ct));
app.MapGet("/api/schedules", async (Guid? accountId, DateOnly? from, DateOnly? to, int? page, int? pageSize, LedgerService ledger, LedgerGate gate, CancellationToken ct) =>
    { if(!app.Environment.IsEnvironment("Testing"))await ledger.PostDueSchedulesAsync(ct);
      return await gate.RunAsync(() => ledger.GetSchedulesAsync(accountId, from, to, page ?? 1, pageSize ?? 25, ct), ct); });
app.MapPost("/api/schedules/process-due", (LedgerService ledger, CancellationToken ct) => ledger.PostDueSchedulesAsync(ct));
app.MapPost("/api/transactions/{id:guid}/review", (Guid id, ReviewTransactionRequest request, LedgerService ledger, CancellationToken ct) => ledger.ReviewTransactionAsync(id,request,ct));
app.MapPost("/api/schedules", (ScheduleRequest request, LedgerService ledger, CancellationToken ct) => ledger.SaveScheduleAsync(null, request, ct));
app.MapPut("/api/schedules/{id:guid}", (Guid id, ScheduleRequest request, LedgerService ledger, CancellationToken ct) => ledger.SaveScheduleAsync(id, request, ct));
app.MapPost("/api/schedules/{id:guid}/actions", (Guid id, ScheduleActionRequest request, LedgerService ledger, CancellationToken ct) => ledger.ScheduleActionAsync(id, request, ct));
app.MapPost("/api/transactions", (TransactionRequest request, LedgerService ledger, CancellationToken ct) => ledger.SaveTransactionAsync(null, request, ct));
app.MapPost("/api/transactions/selection", (TransactionSelectionRequest request, LedgerService ledger, CancellationToken ct) => ledger.ApplySelectionAsync(request, ct));
app.MapPut("/api/transactions/{id:guid}", (Guid id, TransactionRequest request, LedgerService ledger, CancellationToken ct) => ledger.SaveTransactionAsync(id, request, ct));
app.MapPost("/api/transactions/{id:guid}/delete", (Guid id, DeleteTransactionRequest request, LedgerService ledger, CancellationToken ct) => ledger.DeleteTransactionAsync(id, request, ct));
app.MapPost("/api/transactions/{id:guid}/status", (Guid id, StatusRequest request, LedgerService ledger, CancellationToken ct) => ledger.SetStatusAsync(id, request, ct));
app.MapPost("/api/transfers", (TransferRequest request, LedgerService ledger, CancellationToken ct) => ledger.SaveTransferAsync(null, request, ct));
app.MapPut("/api/transfers/{id:guid}", (Guid id, TransferRequest request, LedgerService ledger, CancellationToken ct) => ledger.SaveTransferAsync(id, request, ct));
app.MapGet("/api/ledger/audit", (Guid? resourceId, int? page, int? pageSize, LedgerService ledger, CancellationToken ct) =>
    ledger.GetAuditAsync(resourceId, page ?? 1, pageSize ?? 50, ct));
app.MapGet("/api/payees", (LedgerService ledger, CancellationToken ct) => ledger.GetPayeesAsync(ct));
app.MapGet("/api/payees/manage", (LedgerService ledger, LedgerGate gate, CancellationToken ct) => gate.RunAsync(() => ledger.GetManagedPayeesAsync(ct), ct));
app.MapPut("/api/payees/manage", (SavePayeesRequest request, LedgerService ledger, CancellationToken ct) => ledger.SavePayeesAsync(request, ct));
app.MapPost("/api/transactions/import", (TransactionRequest request, LedgerService ledger, CancellationToken ct) => ledger.SaveTransactionAsync(null, request, ct, imported: true));

app.Run();

static bool IsMonth(string month) => Regex.IsMatch(month, @"^\d{4}-(0[1-9]|1[0-2])$") && DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", out _);

public partial class Program { }
