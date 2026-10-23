namespace OpenBudget.Api;

// Propagate changes, rather than copying assignments or overwriting preserved opening baselines.
public static class BudgetCarryForward
{
    public static bool Apply(BudgetEntity month, decimal readyDelta, IReadOnlyDictionary<string, decimal> availableDeltas, BudgetEntity source)
    {
        var changed = readyDelta != 0;
        month.OpeningReadyToAssign += readyDelta;
        foreach (var (id, delta) in availableDeltas)
        {
            if (delta == 0) continue;
            var category = month.Groups.SelectMany(g => g.Categories).SingleOrDefault(c => c.Id == id);
            if (category is null)
            {
                // A category funded/spent in a backdated month must not disappear across an
                // already initialized month that predates that category's creation.
                var sourceGroup = source.Groups.Single(g => g.Categories.Any(c => c.Id == id));
                var original = sourceGroup.Categories.Single(c => c.Id == id);
                var group = month.Groups.SingleOrDefault(g => g.Id == sourceGroup.Id);
                if (group is null)
                {
                    group = new CategoryGroupEntity { BudgetId = month.BudgetId, BudgetMonth = month.Month, Id = sourceGroup.Id, Name = sourceGroup.Name,
                        IsArchived = sourceGroup.IsArchived, SortOrder = month.Groups.Count == 0 ? 0 : month.Groups.Max(g => g.SortOrder) + 1 };
                    month.Groups.Add(group);
                }
                category = new CategoryEntity { BudgetId = month.BudgetId, BudgetMonth = month.Month, GroupId = group.Id, Id = original.Id, Name = original.Name,
                    Target = original.Target, Icon = original.Icon, Snoozed = original.Snoozed, IsArchived = original.IsArchived,
                    SortOrder = group.Categories.Count == 0 ? 0 : group.Categories.Max(c => c.SortOrder) + 1 };
                group.Categories.Add(category);
            }
            category.OpeningAvailable += delta;
            changed = true;
        }
        return changed;
    }
}
