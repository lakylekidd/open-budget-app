using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

// Read-only bridge from Long-Term Planning into the monthly Plan DTO. A category's planned value for a month is
// its effective planned budget override: the latest override at or before that month (an override applies from
// its month onward until another override replaces it). Categories without any such override get no value.
public static class LongTermPlanningPlanValues
{
    public static Dictionary<string, decimal> EffectiveOverrides(
        IEnumerable<LongTermPlanningCategoryEntity> entries, string month) => entries
        .Where(x => x.PlannedBudgetOverride.HasValue && string.CompareOrdinal(x.Month, month) <= 0)
        .GroupBy(x => x.CategoryId, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.MaxBy(x => x.Month, StringComparer.Ordinal)!.PlannedBudgetOverride!.Value,
            StringComparer.Ordinal);

    public static async Task<BudgetDto> AttachPlannedAsync(BudgetDbContext db, BudgetEntity month, BudgetDto dto, CancellationToken ct)
    {
        // One no-tracking query per month DTO, explicitly scoped to the month's budget; never writes planning data.
        var entries = await db.LongTermPlanningCategories.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.BudgetId == month.BudgetId && x.PlannedBudgetOverride != null && string.Compare(x.Month, month.Month) <= 0)
            .Select(x => new LongTermPlanningCategoryEntity
            {
                BudgetId = x.BudgetId, Month = x.Month, CategoryId = x.CategoryId, PlannedBudgetOverride = x.PlannedBudgetOverride
            })
            .ToListAsync(ct);
        if (entries.Count == 0) return dto;
        var planned = EffectiveOverrides(entries, month.Month);
        return dto with
        {
            Groups = dto.Groups.Select(g => g with
            {
                Categories = g.Categories.Select(c => planned.TryGetValue(c.Id, out var value) ? c with { Planned = value } : c).ToList()
            }).ToList()
        };
    }
}
