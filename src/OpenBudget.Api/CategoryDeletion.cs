using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed record CategoryOptionDto(string Id, string Name, string GroupId, string GroupName);

// What deleting a category would change. RequiresReassignment is true when the category has any history (money in any
// month, transactions or split lines including deleted ones, schedules, payee defaults, loan pairings or planning values);
// those then move to the chosen category. A target cannot be merged and is removed (kept in the audit receipt).
public sealed record CategoryDeletionPreviewDto(
    string CategoryId, string Name, string GroupName, bool RequiresReassignment,
    int TransactionCount, int SplitLineCount, int ScheduleCount, int AssignedMonthCount, decimal AssignedTotal,
    decimal RemainingAvailable, string? RemainingAvailableMonth, bool HasTarget, int PayeeDefaultCount, int LoanPairingCount,
    int PlanningValueCount, long LedgerVersion, IReadOnlyList<CategoryOptionDto> ReassignmentOptions);

public sealed record DeleteCategoryRequest(Guid CommandId, long Version, string? ReassignToCategoryId);

public static class CategoryDeletionRules
{
    // Credit card payment, legacy loan and Wishlist categories are created and removed with their account or bucket.
    public static bool IsSystemCategory(string categoryId) =>
        CreditCardRules.IsPaymentCategory(categoryId) || categoryId.StartsWith(LoanRules.CategoryPrefix, StringComparison.Ordinal) || WishlistRules.IsWishlistCategory(categoryId);

    public static bool IsSystemGroup(string groupId) =>
        groupId == CreditCardRules.PaymentGroupId || groupId == LoanRules.PaymentGroupId || groupId == WishlistRules.GroupId;

    // History can be reassigned only to an active, user-managed category other than the deleted one.
    public static IReadOnlyList<CategoryOptionDto> ReassignmentOptions(CategoryCatalog catalog, string deletedCategoryId)
    {
        var groups = catalog.Groups.ToDictionary(g => g.Id, StringComparer.Ordinal);
        return catalog.Categories
            .Where(c => c.Id != deletedCategoryId && !c.IsArchived && !IsSystemCategory(c.Id) && groups.TryGetValue(c.GroupId, out var g) && !g.IsArchived && !IsSystemGroup(g.Id))
            .OrderBy(c => groups[c.GroupId].SortOrder).ThenBy(c => c.SortOrder).ThenBy(c => c.Id, StringComparer.Ordinal)
            .Select(c => new CategoryOptionDto(c.Id, c.Name, c.GroupId, groups[c.GroupId].Name)).ToList();
    }

    public static bool HasMoney(CategoryMonthEntity state) =>
        state.Assigned != 0 || state.Activity != 0 || state.LegacyActivity != 0 || state.OpeningAvailable != 0 || state.CreditOverspending != 0;

    // Moves one month's state of the deleted category into the target category. Opening available here is only the
    // non-carried baseline: the carry from the previous month is already moved by carry-forward deltas.
    public static decimal MergeMonth(CategoryMonthEntity deleted, CategoryMonthEntity target)
    {
        var assigned = deleted.Assigned;
        target.Assigned += deleted.Assigned;
        target.OpeningAvailable += deleted.OpeningAvailable;
        target.LegacyActivity += deleted.LegacyActivity;
        deleted.Assigned = 0;
        deleted.OpeningAvailable = 0;
        deleted.LegacyActivity = 0;
        return assigned;
    }

    public static ScheduleTemplateDto Reassign(ScheduleTemplateDto template, string from, string to) => template with
    {
        CategoryId = template.CategoryId == from ? to : template.CategoryId,
        Splits = template.Splits?.Select(s => s.CategoryId == from ? s with { CategoryId = to } : s).ToList()
    };

    public static bool References(ScheduleTemplateDto? template, string categoryId) =>
        template is not null && (template.CategoryId == categoryId || template.Splits?.Any(s => s.CategoryId == categoryId) == true);

    // A deterministic resource ID for the audit receipt of a category (ledger receipts are keyed by UUID).
    public static Guid ResourceId(string budgetId, string categoryId) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"category/{budgetId}/{categoryId}")).AsSpan(0, 16));
}

// Deleting a category with history is a ledger command (command ID, ledger version, replay receipt and audit) because it
// recategorizes transactions. Everything happens in one gated database transaction; any failure rolls all of it back.
public sealed partial class LedgerService
{
    public async Task<CategoryDeletionPreviewDto> GetCategoryDeletionPreviewAsync(string categoryId, CancellationToken ct)
    {
        var catalog = await CategoryCatalog.LoadAsync(db, db.ActiveBudgetId, ct);
        var category = catalog.Find(categoryId) ?? throw new LedgerException("Category not found.", 404);
        if (CategoryDeletionRules.IsSystemCategory(categoryId)) throw new LedgerException("This category is managed by the app and cannot be deleted.");
        var usage = await LoadCategoryUsageAsync(categoryId, ct);
        var states = await db.CategoryMonths.AsNoTracking().Where(s => s.BudgetId == db.ActiveBudgetId && s.CategoryId == categoryId).ToListAsync(ct);
        // The remaining available amount is the category's balance in the current month (or the nearest existing month).
        var currentMonth = LoanRules.Month(DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
        var latest = states.Where(s => string.CompareOrdinal(s.BudgetMonth, currentMonth) <= 0).OrderByDescending(s => s.BudgetMonth, StringComparer.Ordinal).FirstOrDefault()
            ?? states.OrderBy(s => s.BudgetMonth, StringComparer.Ordinal).FirstOrDefault();
        var requires = usage.RequiresReassignment || states.Any(CategoryDeletionRules.HasMoney);
        var version = await db.LedgerStates.Select(x => x.Version).SingleAsync(ct);
        return new CategoryDeletionPreviewDto(categoryId, category.Name, catalog.FindGroup(category.GroupId)?.Name ?? "", requires,
            usage.Transactions, usage.SplitLines, usage.Schedules, states.Count(s => s.Assigned != 0), states.Sum(s => s.Assigned),
            latest?.Available ?? 0, latest?.BudgetMonth, usage.HasTarget, usage.Payees, usage.Loans, usage.PlanningValues, version,
            CategoryDeletionRules.ReassignmentOptions(catalog, categoryId));
    }

    public Task<MutationDto> DeleteCategoryAsync(string categoryId, DeleteCategoryRequest request, string actorUserId, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, $"category.delete/{categoryId}", request, async () =>
        {
            var budgetId = db.ActiveBudgetId;
            var catalog = await CategoryCatalog.LoadAsync(db, budgetId, ct);
            var category = catalog.Find(categoryId) ?? throw new LedgerException("Category not found.", 404);
            if (CategoryDeletionRules.IsSystemCategory(categoryId)) throw new LedgerException("This category is managed by the app and cannot be deleted.");
            var groupName = catalog.FindGroup(category.GroupId)?.Name ?? "";
            var targetId = string.IsNullOrWhiteSpace(request.ReassignToCategoryId) ? null : request.ReassignToCategoryId.Trim();
            CategoryEntity? target = null;
            if (targetId is not null)
            {
                if (targetId == categoryId) throw new LedgerException("Choose a different category to reassign to.");
                if (CategoryDeletionRules.ReassignmentOptions(catalog, categoryId).All(o => o.Id != targetId))
                    throw new LedgerException("Choose an active category to reassign to.");
                target = catalog.Find(targetId)!;
            }

            var usage = await LoadCategoryUsageAsync(categoryId, ct);
            var hasMoney = await db.CategoryMonths.AnyAsync(s => s.BudgetId == budgetId && s.CategoryId == categoryId &&
                (s.Assigned != 0 || s.Activity != 0 || s.LegacyActivity != 0 || s.OpeningAvailable != 0 || s.CreditOverspending != 0), ct);
            if ((usage.RequiresReassignment || hasMoney) && target is null)
                throw new LedgerException("This category has history. Choose a category to reassign it to before deleting.");

            var categoryTarget = await db.CategoryTargets.SingleOrDefaultAsync(t => t.CategoryId == categoryId, ct);
            var before = new
            {
                category = new { category.Id, category.Name, category.GroupId, groupName, category.Icon, category.SortOrder, category.IsArchived },
                target = categoryTarget?.ToDto()
            };
            var moved = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
            if (target is not null)
            {
                foreach (var entry in await db.Transactions.Where(t => t.CategoryId == categoryId).ToListAsync(ct)) entry.CategoryId = target.Id;
                foreach (var entry in await db.Transactions.Include(t => t.Splits).Where(t => t.Splits.Any(s => s.CategoryId == categoryId)).ToListAsync(ct))
                    foreach (var split in entry.Splits.Where(s => s.CategoryId == categoryId)) split.CategoryId = target.Id;
                foreach (var schedule in await db.Schedules.ToListAsync(ct))
                {
                    var template = JsonSerializer.Deserialize<ScheduleTemplateDto>(schedule.TemplateJson);
                    if (CategoryDeletionRules.References(template, categoryId))
                        schedule.TemplateJson = JsonSerializer.Serialize(CategoryDeletionRules.Reassign(template!, categoryId, target.Id));
                }
                foreach (var occurrence in await db.ScheduleOccurrences.Where(o => o.OverrideJson != null).ToListAsync(ct))
                {
                    var template = JsonSerializer.Deserialize<ScheduleTemplateDto>(occurrence.OverrideJson!);
                    if (CategoryDeletionRules.References(template, categoryId))
                        occurrence.OverrideJson = JsonSerializer.Serialize(CategoryDeletionRules.Reassign(template!, categoryId, target.Id));
                }
                foreach (var payee in await db.Payees.Where(p => p.DefaultCategoryId == categoryId).ToListAsync(ct)) payee.DefaultCategoryId = target.Id;
                foreach (var terms in await db.LoanTerms.Where(t => t.PairedCategoryId == categoryId || t.DevicePairedCategoryId == categoryId).ToListAsync(ct))
                {
                    if (terms.PairedCategoryId == categoryId) terms.PairedCategoryId = target.Id;
                    if (terms.DevicePairedCategoryId == categoryId) terms.DevicePairedCategoryId = target.Id;
                }
                await MergePlanningValuesAsync(categoryId, target.Id, ct);
            }
            else
            {
                db.LongTermPlanningCategories.RemoveRange(await db.LongTermPlanningCategories.Where(x => x.CategoryId == categoryId).ToListAsync(ct));
            }
            if (categoryTarget is not null) db.CategoryTargets.Remove(categoryTarget);
            await db.SaveChangesAsync(ct);

            // Move each month's assigned amount and baselines into the target, oldest month first, and let the projection
            // recalculate activity, credit card flows and carry-forward for every month in the same pass.
            await projection.RecalculateAsync(budgetId, ct, target is null ? null : month =>
            {
                var from = catalog.EnsureState(month, categoryId);
                var into = catalog.EnsureState(month, target.Id);
                var assigned = CategoryDeletionRules.MergeMonth(from, into);
                if (assigned != 0) moved[month.Month] = assigned;
            });
            var months = catalog.Months.Where(m => m.BudgetId == budgetId).ToList();
            if (months.Select(m => m.State(categoryId)).OfType<CategoryMonthEntity>().Any(CategoryDeletionRules.HasMoney))
                throw new LedgerException("The category's money could not be fully reassigned. Nothing was changed.", 409);
            // Every month is loaded by the projection, so this removes every state row of the category.
            var remaining = await db.CategoryMonths.Where(s => s.BudgetId == budgetId && s.CategoryId == categoryId).ToListAsync(ct);
            catalog.RemoveCategory(category);
            foreach (var state in remaining.Where(s => db.Entry(s).State != EntityState.Deleted)) db.CategoryMonths.Remove(state);
            foreach (var month in months) month.Version++;

            if (target is not null)
            {
                db.PlanActions.Add(new PlanActionEntity
                {
                    BudgetId = budgetId, Month = LoanRules.Month(DateOnly.FromDateTime(clock.GetLocalNow().DateTime)), ActorUserId = actorUserId, Kind = "reassigned",
                    Amount = moved.Values.Sum(), FromCategoryName = category.Name, ToCategoryName = target.Name, CreatedAt = clock.GetUtcNow()
                });
            }
            return new Change(CategoryDeletionRules.ResourceId(budgetId, categoryId), before, new
            {
                deleted = true,
                reassignedTo = target is null ? null : new { target.Id, target.Name },
                transactions = usage.Transactions, splitLines = usage.SplitLines, schedules = usage.Schedules,
                assignedByMonth = moved, payeeDefaults = usage.Payees, loanPairings = usage.Loans, planningValues = usage.PlanningValues,
                targetRemoved = categoryTarget is not null
            });
        }, ct);

    private sealed record CategoryUsage(int Transactions, int SplitLines, int Schedules, int Payees, int Loans, int PlanningValues, bool HasTarget)
    {
        public bool RequiresReassignment => Transactions > 0 || SplitLines > 0 || Schedules > 0 || Payees > 0 || Loans > 0 || PlanningValues > 0;
    }

    // Counts every reference to a category in the active budget, including soft-deleted transactions (kept as history).
    private async Task<CategoryUsage> LoadCategoryUsageAsync(string categoryId, CancellationToken ct)
    {
        var transactions = await db.Transactions.CountAsync(t => t.CategoryId == categoryId || t.Splits.Any(s => s.CategoryId == categoryId), ct);
        var splitLines = await db.Transactions.SelectMany(t => t.Splits).CountAsync(s => s.CategoryId == categoryId, ct);
        var templates = await db.Schedules.Select(s => s.TemplateJson).ToListAsync(ct);
        var overrides = await db.ScheduleOccurrences.Where(o => o.OverrideJson != null).Select(o => o.OverrideJson!).ToListAsync(ct);
        var schedules = templates.Concat(overrides).Count(json => CategoryDeletionRules.References(JsonSerializer.Deserialize<ScheduleTemplateDto>(json), categoryId));
        var payees = await db.Payees.CountAsync(p => p.DefaultCategoryId == categoryId, ct);
        var loans = await db.LoanTerms.CountAsync(t => t.PairedCategoryId == categoryId || t.DevicePairedCategoryId == categoryId, ct);
        var planning = await db.LongTermPlanningCategories.CountAsync(x => x.CategoryId == categoryId, ct);
        var hasTarget = await db.CategoryTargets.AnyAsync(t => t.CategoryId == categoryId, ct);
        return new CategoryUsage(transactions, splitLines, schedules, payees, loans, planning, hasTarget);
    }

    // Long-term planning values move to the target; where both have a value for a month they are added together.
    private async Task MergePlanningValuesAsync(string categoryId, string targetId, CancellationToken ct)
    {
        var rows = await db.LongTermPlanningCategories.Where(x => x.CategoryId == categoryId || x.CategoryId == targetId).ToListAsync(ct);
        foreach (var row in rows.Where(x => x.CategoryId == categoryId).ToList())
        {
            var existing = rows.SingleOrDefault(x => x.CategoryId == targetId && x.Month == row.Month);
            if (existing is null)
            {
                db.LongTermPlanningCategories.Add(new LongTermPlanningCategoryEntity
                {
                    Month = row.Month, CategoryId = targetId, PlannedBudgetOverride = row.PlannedBudgetOverride, PlannedExpenses = row.PlannedExpenses
                });
            }
            else
            {
                existing.PlannedExpenses += row.PlannedExpenses;
                existing.PlannedBudgetOverride = existing.PlannedBudgetOverride is null && row.PlannedBudgetOverride is null
                    ? null : (existing.PlannedBudgetOverride ?? 0) + (row.PlannedBudgetOverride ?? 0);
            }
            db.LongTermPlanningCategories.Remove(row);
        }
    }

    public static void MapCategoryDeletionEndpoints(WebApplication app)
    {
        var categories = app.MapGroup("/api/budgets/{id}/categories/{categoryId}").RequireAuthorization();
        categories.MapGet("/deletion", async (string id, string categoryId, ClaimsPrincipal principal, BudgetOwnershipService ownership,
            BudgetRequestContext budgetContext, LedgerService ledger, LedgerGate gate, CancellationToken ct) =>
        {
            if (!IsCategoryId(categoryId)) return Results.BadRequest(new { error = "Category ID is invalid." });
            if (await AuthorizeBudgetAsync(principal, ownership, budgetContext, id, ct) is { } denied) return denied;
            return Results.Ok(await gate.RunAsync(() => ledger.GetCategoryDeletionPreviewAsync(categoryId, ct), ct));
        });
        categories.MapPost("/delete", async (string id, string categoryId, DeleteCategoryRequest request, ClaimsPrincipal principal, BudgetOwnershipService ownership,
            BudgetRequestContext budgetContext, LedgerService ledger, CancellationToken ct) =>
        {
            if (!IsCategoryId(categoryId)) return Results.BadRequest(new { error = "Category ID is invalid." });
            if (request.ReassignToCategoryId is { Length: > 100 }) return Results.BadRequest(new { error = "Category ID is invalid." });
            if (await AuthorizeBudgetAsync(principal, ownership, budgetContext, id, ct) is { } denied) return denied;
            return Results.Ok(await ledger.DeleteCategoryAsync(categoryId, request, principal.FindFirstValue(ClaimTypes.NameIdentifier)!, ct));
        });
    }

    private static bool IsCategoryId(string categoryId) => !string.IsNullOrWhiteSpace(categoryId) && categoryId.Length <= 100;

    // Same access rule as the owned-budget Plan month routes: owners and joined editors of an active budget.
    private static async Task<IResult?> AuthorizeBudgetAsync(ClaimsPrincipal principal, BudgetOwnershipService ownership, BudgetRequestContext budgetContext, string budgetId, CancellationToken ct)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();
        if (!await ownership.CanAccessBudgetAsync(userId, budgetId, ct)) return Results.NotFound();
        budgetContext.BudgetId = budgetId;
        return null;
    }
}
