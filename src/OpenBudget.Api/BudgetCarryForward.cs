namespace OpenBudget.Api;

// Propagate changes, rather than copying assignments or overwriting preserved opening baselines.
public static class BudgetCarryForward
{
    public sealed record Changes(decimal ReadyToAssignDelta, IReadOnlyDictionary<string, decimal> AvailableDeltas)
    {
        public bool HasChanges => ReadyToAssignDelta != 0 || AvailableDeltas.Values.Any(delta => delta != 0);
    }

    public static decimal OpeningAvailable(decimal available) => Math.Max(available, 0);

    // Negative Available reduces the next month's Ready to Assign, except credit overspending, which is card debt.
    public static decimal UncoveredOverspending(decimal available, decimal creditOverspending) => Math.Min(available + creditOverspending, 0);

    public static decimal OpeningReadyToAssign(BudgetEntity source) => decimal.Round(
        BudgetService.CalculateReadyToAssign(source) + source.CategoryStates.Sum(state => UncoveredOverspending(state.Available, state.CreditOverspending)),
        2,
        MidpointRounding.ToEven);

    public static Changes CalculateChanges(
        decimal previousReadyToAssign,
        decimal currentReadyToAssign,
        IReadOnlyDictionary<string, decimal> previousAvailable,
        IEnumerable<CategoryMonthEntity> currentCategories,
        IReadOnlyDictionary<string, decimal>? previousCreditOverspending = null)
    {
        var availableDeltas = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var readyToAssignDelta = currentReadyToAssign - previousReadyToAssign;

        foreach (var category in currentCategories)
        {
            var oldAvailable = previousAvailable.GetValueOrDefault(category.CategoryId);
            availableDeltas[category.CategoryId] = OpeningAvailable(category.Available) - OpeningAvailable(oldAvailable);
            readyToAssignDelta += UncoveredOverspending(category.Available, category.CreditOverspending)
                - UncoveredOverspending(oldAvailable, previousCreditOverspending?.GetValueOrDefault(category.CategoryId) ?? 0);
        }

        return new Changes(decimal.Round(readyToAssignDelta, 2, MidpointRounding.ToEven), availableDeltas);
    }

    // Categories are budget-wide, so every month can hold every category: a month without a state row for a category
    // (an all-zero state) gets one when carried money reaches it.
    public static bool Apply(BudgetEntity month, decimal readyDelta, IReadOnlyDictionary<string, decimal> availableDeltas)
    {
        var changed = readyDelta != 0;
        month.OpeningReadyToAssign += readyDelta;
        foreach (var (id, delta) in availableDeltas)
        {
            if (delta == 0) continue;
            var state = month.State(id);
            if (state is null)
            {
                if (month.Catalog is { } catalog) state = catalog.EnsureState(month, id);
                else month.CategoryStates.Add(state = new CategoryMonthEntity { BudgetId = month.BudgetId, BudgetMonth = month.Month, CategoryId = id });
            }
            state.OpeningAvailable += delta;
            changed = true;
        }
        return changed;
    }

    // Snapshot helpers used before a month changes, keyed by category ID.
    public static Dictionary<string, decimal> AvailableById(BudgetEntity month) =>
        month.CategoryStates.ToDictionary(c => c.CategoryId, c => c.Available, StringComparer.Ordinal);

    public static Dictionary<string, decimal> CreditOverspendingById(BudgetEntity month) =>
        month.CategoryStates.ToDictionary(c => c.CategoryId, c => c.CreditOverspending, StringComparer.Ordinal);
}
