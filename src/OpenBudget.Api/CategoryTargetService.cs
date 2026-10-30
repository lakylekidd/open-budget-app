using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

// Category targets for regular Plan categories. Each budget/category has at most one target; writes run under the
// budget's LedgerGate, use the target's own optimistic version and record an append-only command receipt (replay and
// audit, visible in the ledger audit feed) without advancing the ledger revision, because targets never change money.
public sealed class CategoryTargetService(BudgetDbContext db, BudgetService budgets, TimeProvider clock)
{
    private const int MaxCategoryIdLength = 100;

    public async Task<CategoryTargetCommandResult> GetAsync(string budgetId, string categoryId, string month, CancellationToken ct)
    {
        if (!IsCategoryId(categoryId)) return CategoryTargetCommandResult.Fail("Category ID is invalid.");
        var (_, row, error) = await LoadCategoryAsync(budgetId, categoryId, month, requireActive: false, ct);
        if (error is not null) return error;
        var target = await db.CategoryTargets.AsNoTracking().SingleOrDefaultAsync(x => x.CategoryId == categoryId, ct);
        return CategoryTargetCommandResult.Success(await ToResultAsync(budgetId, categoryId, month, target, row!.Value, ct));
    }

    public async Task<CategoryTargetCommandResult> SaveAsync(string budgetId, string categoryId, SaveCategoryTargetRequest request, CancellationToken ct)
    {
        if (!IsCategoryId(categoryId)) return CategoryTargetCommandResult.Fail("Category ID is invalid.");
        var validation = CategoryTargetRules.Validate(request);
        if (validation is not null) return CategoryTargetCommandResult.Fail(validation);
        if (CategoryTargetRules.IsPastDueOneOff(request, Today())) return CategoryTargetCommandResult.Fail("A target that does not repeat cannot be due in the past.");

        var kind = $"categoryTarget.save/{categoryId}";
        var replay = await ReplayAsync(request.CommandId, kind, request, budgetId, categoryId, request.Month, ct);
        if (replay is not null) return replay;

        var (_, row, error) = await LoadCategoryAsync(budgetId, categoryId, request.Month, requireActive: true, ct);
        if (error is not null) return error;
        var target = await db.CategoryTargets.SingleOrDefaultAsync(x => x.CategoryId == categoryId, ct);
        if ((target?.Version ?? 0) != request.Version)
            return CategoryTargetCommandResult.Conflict(await ToResultAsync(budgetId, categoryId, request.Month, target, row!.Value, ct));

        var before = target?.ToDto();
        var now = clock.GetUtcNow();
        if (target is null)
        {
            target = new CategoryTargetEntity { CategoryId = categoryId, StartMonth = request.Month, Version = 0, CreatedAt = now };
            db.CategoryTargets.Add(target);
        }
        else if (target.Kind != request.Kind || target.Behavior != request.Behavior || target.DueDate != request.DueDate
            || target.RepeatEvery != request.RepeatEvery || target.RepeatUnit != request.RepeatUnit)
        {
            // A different schedule starts a new funding cycle; changing only the amount keeps earlier contributions.
            target.StartMonth = request.Month;
        }
        target.Kind = request.Kind;
        target.Amount = request.Amount;
        target.Behavior = request.Behavior;
        target.Weekday = request.Weekday;
        target.DayOfMonth = request.DayOfMonth is 0 ? null : request.DayOfMonth;
        target.DueDate = request.DueDate;
        target.RepeatEvery = request.RepeatEvery;
        target.RepeatUnit = request.RepeatUnit;
        target.Version++;
        target.UpdatedAt = now;
        await AddReceiptAsync(request.CommandId, kind, request, target.Id, before, target.ToDto(), now, ct);
        await db.SaveChangesAsync(ct);
        return CategoryTargetCommandResult.Success(await ToResultAsync(budgetId, categoryId, request.Month, target, row!.Value, ct));
    }

    public async Task<CategoryTargetCommandResult> DeleteAsync(string budgetId, string categoryId, DeleteCategoryTargetRequest request, CancellationToken ct)
    {
        if (!IsCategoryId(categoryId)) return CategoryTargetCommandResult.Fail("Category ID is invalid.");
        if (request.CommandId == Guid.Empty || request.Version < 1) return CategoryTargetCommandResult.Fail("Command ID and target version are required.");
        if (!CategoryTargetRules.TryParseMonth(request.Month, out _)) return CategoryTargetCommandResult.Fail("Month must use YYYY-MM format.");

        var kind = $"categoryTarget.delete/{categoryId}";
        var replay = await ReplayAsync(request.CommandId, kind, request, budgetId, categoryId, request.Month, ct);
        if (replay is not null) return replay;

        var (_, row, error) = await LoadCategoryAsync(budgetId, categoryId, request.Month, requireActive: false, ct);
        if (error is not null) return error;
        var target = await db.CategoryTargets.SingleOrDefaultAsync(x => x.CategoryId == categoryId, ct);
        if (target is null || target.Version != request.Version)
            return CategoryTargetCommandResult.Conflict(await ToResultAsync(budgetId, categoryId, request.Month, target, row!.Value, ct));

        await AddReceiptAsync(request.CommandId, kind, request, target.Id, target.ToDto(), null, clock.GetUtcNow(), ct);
        db.CategoryTargets.Remove(target);
        await db.SaveChangesAsync(ct);
        return CategoryTargetCommandResult.Success(new CategoryTargetResultDto(categoryId, request.Month, null, null));
    }

    private async Task<(BudgetEntity? Month, (CategoryGroupEntity Group, CategoryEntity Category)? Row, CategoryTargetCommandResult? Error)> LoadCategoryAsync(
        string budgetId, string categoryId, string month, bool requireActive, CancellationToken ct)
    {
        if (!CategoryTargetRules.TryParseMonth(month, out _)) return (null, null, CategoryTargetCommandResult.Fail("Month must use YYYY-MM format."));
        var budget = await budgets.GetMonthAsync(budgetId, month, ct);
        var group = budget.Groups.FirstOrDefault(g => g.Categories.Any(c => c.Id == categoryId));
        var category = group?.Categories.Single(c => c.Id == categoryId);
        if (group is null || category is null) return (budget, null, CategoryTargetCommandResult.Fail("Category not found in this month.", 404));
        if (CategoryTargetRules.IsPaymentCategory(category.Id, group.Id))
            return (budget, null, CategoryTargetCommandResult.Fail("Credit card payment categories use debt payoff targets."));
        if (requireActive && (category.IsArchived || group.IsArchived))
            return (budget, null, CategoryTargetCommandResult.Fail("Hidden categories cannot have targets. Restore the category first."));
        return (budget, (group, category), null);
    }

    private async Task<CategoryTargetResultDto> ToResultAsync(string budgetId, string categoryId, string month, CategoryTargetEntity? target,
        (CategoryGroupEntity Group, CategoryEntity Category) row, CancellationToken ct)
    {
        if (target is null) return new CategoryTargetResultDto(categoryId, month, null, null);
        var active = !row.Category.IsArchived && !row.Group.IsArchived;
        if (!active) return new CategoryTargetResultDto(categoryId, month, target.ToDto(), null);
        var history = await budgets.LoadTargetHistoryAsync(budgetId, month, [target], ct);
        return new CategoryTargetResultDto(categoryId, month, target.ToDto(), BudgetService.CalculateTarget(target, month, row.Category, history));
    }

    // Exact command replay returns the current target state; reusing a command ID for a different request is a conflict.
    private async Task<CategoryTargetCommandResult?> ReplayAsync<T>(Guid commandId, string kind, T payload, string budgetId, string categoryId, string month, CancellationToken ct)
    {
        var previous = await db.LedgerCommands.AsNoTracking().SingleOrDefaultAsync(c => c.Id == commandId, ct);
        if (previous is null) return null;
        if (previous.Fingerprint != Fingerprint(kind, payload))
            return CategoryTargetCommandResult.Fail("This command ID was already used for a different request.", 409);
        return await GetAsync(budgetId, categoryId, month, ct);
    }

    private async Task AddReceiptAsync<T>(Guid commandId, string kind, T payload, Guid resourceId, object? before, object? after, DateTimeOffset now, CancellationToken ct)
    {
        var revision = (await db.LedgerStates.AsNoTracking().SingleAsync(ct)).Version;
        db.LedgerCommands.Add(new LedgerCommandEntity
        {
            Id = commandId, ResourceId = resourceId, Kind = kind, Fingerprint = Fingerprint(kind, payload), Version = revision,
            BeforeJson = JsonSerializer.Serialize(before), AfterJson = JsonSerializer.Serialize(after), CreatedAt = now
        });
    }

    private static string Fingerprint<T>(string kind, T payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kind + JsonSerializer.Serialize(payload))));

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    private static bool IsCategoryId(string categoryId) => !string.IsNullOrWhiteSpace(categoryId) && categoryId.Length <= MaxCategoryIdLength;

    public static void MapEndpoints(WebApplication app)
    {
        var budgets = app.MapGroup("/api/budgets/{id}/categories/{categoryId}/target").RequireAuthorization();
        budgets.MapGet("/{month}", async (string id, string categoryId, string month, ClaimsPrincipal principal, BudgetOwnershipService ownership,
            BudgetRequestContext budgetContext, CategoryTargetService service, LedgerGate gate, CancellationToken ct) =>
        {
            if (!IsMonth(month)) return Results.BadRequest(new { error = "Month must use YYYY-MM format." });
            if (await AuthorizeAsync(principal, ownership, budgetContext, id, ct) is { } denied) return denied;
            return ToHttp(await gate.RunAsync(() => service.GetAsync(id, categoryId, month, ct), ct, Committed));
        });
        budgets.MapPut("", async (string id, string categoryId, SaveCategoryTargetRequest request, ClaimsPrincipal principal, BudgetOwnershipService ownership,
            BudgetRequestContext budgetContext, CategoryTargetService service, LedgerGate gate, CancellationToken ct) =>
        {
            if (await AuthorizeAsync(principal, ownership, budgetContext, id, ct) is { } denied) return denied;
            return ToHttp(await gate.RunAsync(() => service.SaveAsync(id, categoryId, request, ct), ct, Committed));
        });
        budgets.MapPost("/delete", async (string id, string categoryId, DeleteCategoryTargetRequest request, ClaimsPrincipal principal, BudgetOwnershipService ownership,
            BudgetRequestContext budgetContext, CategoryTargetService service, LedgerGate gate, CancellationToken ct) =>
        {
            if (await AuthorizeAsync(principal, ownership, budgetContext, id, ct) is { } denied) return denied;
            return ToHttp(await gate.RunAsync(() => service.DeleteAsync(id, categoryId, request, ct), ct, Committed));
        });
    }

    // Same access rule as the owned-budget Plan month routes: owners and joined editors of an active budget.
    private static async Task<IResult?> AuthorizeAsync(ClaimsPrincipal principal, BudgetOwnershipService ownership, BudgetRequestContext budgetContext, string budgetId, CancellationToken ct)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();
        if (!await ownership.CanAccessBudgetAsync(userId, budgetId, ct)) return Results.NotFound();
        budgetContext.BudgetId = budgetId;
        return null;
    }

    // Validation and conflict results roll back the gate transaction, including a month initialized by the request.
    private static bool Committed(CategoryTargetCommandResult result) => result.Error is null;

    private static IResult ToHttp(CategoryTargetCommandResult result) => result.StatusCode switch
    {
        200 => Results.Ok(result.Value),
        409 => Results.Conflict(new { error = result.Error, current = result.Value }),
        404 => Results.NotFound(new { error = result.Error }),
        _ => Results.BadRequest(new { error = result.Error })
    };

    private static bool IsMonth(string month) => Regex.IsMatch(month, @"^\d{4}-(0[1-9]|1[0-2])$") && CategoryTargetRules.TryParseMonth(month, out _);
}
