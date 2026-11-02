using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

// The budget-wide category structure (groups and categories) shared by every Plan month. Definitions are global; money
// (assigned, activity, available, credit overspending) and per-month choices (snooze, legacy target) live in one
// CategoryMonthEntity per category and month. A month that has no state row for a category treats it as all zero, so a
// category added later appears in every past and future month without copying. See docs/global-categories.md.
public sealed class CategoryCatalog
{
    private readonly BudgetDbContext? db;
    private readonly List<CategoryGroupEntity> groups;
    private readonly List<CategoryEntity> categories;
    private readonly List<BudgetEntity> months = [];

    public CategoryCatalog(string budgetId, IEnumerable<CategoryGroupEntity> groups, IEnumerable<CategoryEntity> categories, BudgetDbContext? db = null)
    {
        BudgetId = budgetId;
        this.groups = groups.ToList();
        this.categories = categories.ToList();
        this.db = db;
    }

    public string BudgetId { get; }
    public IReadOnlyList<CategoryGroupEntity> Groups => groups;
    public IReadOnlyList<CategoryEntity> Categories => categories;
    public IReadOnlyList<BudgetEntity> Months => months;

    public CategoryGroupEntity? FindGroup(string id) => groups.FirstOrDefault(g => g.Id == id);
    public CategoryEntity? Find(string id) => categories.FirstOrDefault(c => c.Id == id);

    // Loads (or reuses) the tracked catalog of one budget for this DbContext, including definitions added but not saved.
    public static async Task<CategoryCatalog> LoadAsync(BudgetDbContext db, string budgetId, CancellationToken ct)
    {
        if (db.Catalogs.TryGetValue(budgetId, out var cached) && cached.IsTracked()) return cached;
        await db.CategoryGroups.Where(g => g.BudgetId == budgetId).LoadAsync(ct);
        await db.Categories.Where(c => c.BudgetId == budgetId).LoadAsync(ct);
        var catalog = new CategoryCatalog(budgetId,
            db.CategoryGroups.Local.Where(g => g.BudgetId == budgetId),
            db.Categories.Local.Where(c => c.BudgetId == budgetId), db);
        db.Catalogs[budgetId] = catalog;
        return catalog;
    }

    private bool IsTracked() => db is not null &&
        groups.All(g => db.Entry(g).State != EntityState.Detached) && categories.All(c => db.Entry(c).State != EntityState.Detached);

    public CategoryGroupEntity AddGroup(string id, string name, int sortOrder, bool isArchived = false)
    {
        if (FindGroup(id) is not null) throw new InvalidOperationException($"Category group {id} already exists.");
        var group = new CategoryGroupEntity { BudgetId = BudgetId, Id = id, Name = name, SortOrder = sortOrder, IsArchived = isArchived };
        groups.Add(group);
        db?.CategoryGroups.Add(group);
        return group;
    }

    public CategoryEntity AddCategory(string id, string groupId, string name, int sortOrder, string? icon = null, bool isArchived = false)
    {
        if (Find(id) is not null) throw new InvalidOperationException($"Category {id} already exists.");
        if (FindGroup(groupId) is null) throw new InvalidOperationException($"Category group {groupId} does not exist.");
        var category = new CategoryEntity { BudgetId = BudgetId, Id = id, GroupId = groupId, Name = name, Icon = icon, SortOrder = sortOrder, IsArchived = isArchived };
        categories.Add(category);
        db?.Categories.Add(category);
        foreach (var month in months) EnsureState(month, id);
        return category;
    }

    // Removes a category definition and its state in every attached month. The caller must have moved its money and
    // references first and must load every month of the budget so no state row remains.
    public void RemoveCategory(CategoryEntity category)
    {
        foreach (var month in months)
        {
            var state = month.State(category.Id);
            if (state is null) continue;
            month.CategoryStates.Remove(state);
            if (db is not null && db.Entry(state).State != EntityState.Detached) db.CategoryMonths.Remove(state);
            month.CategoryViews.Remove(category.Id);
        }
        categories.Remove(category);
        if (db is not null && db.Entry(category).State != EntityState.Detached) db.Categories.Remove(category);
    }

    public void RemoveGroup(CategoryGroupEntity group)
    {
        if (categories.Any(c => c.GroupId == group.Id)) throw new InvalidOperationException("Only an empty category group can be removed.");
        groups.Remove(group);
        foreach (var month in months) month.GroupViews.Remove(group.Id);
        if (db is not null && db.Entry(group).State != EntityState.Detached) db.CategoryGroups.Remove(group);
    }

    // Composes a month with this catalog and gives it a state row for every category.
    public void Attach(BudgetEntity month)
    {
        if (month.BudgetId != BudgetId) throw new InvalidOperationException("A month can only use its own budget's categories.");
        if (!ReferenceEquals(month.Catalog, this))
        {
            month.Catalog = this;
            month.CategoryViews.Clear();
            month.GroupViews.Clear();
        }
        if (!months.Contains(month)) months.Add(month);
        foreach (var category in categories) EnsureState(month, category.Id);
    }

    public CategoryMonthEntity EnsureState(BudgetEntity month, string categoryId)
    {
        var state = month.State(categoryId);
        if (state is not null) return state;
        state = new CategoryMonthEntity { BudgetId = month.BudgetId, BudgetMonth = month.Month, CategoryId = categoryId };
        month.CategoryStates.Add(state);
        // Months read without tracking get an in-memory zero state only.
        if (db is not null && db.Entry(month).State != EntityState.Detached) db.CategoryMonths.Add(state);
        return state;
    }

    // Groups and categories in display order, each category paired with this month's state.
    public IReadOnlyList<PlanCategoryGroup> Compose(BudgetEntity month)
    {
        var byGroup = categories.GroupBy(c => c.GroupId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.OrderBy(c => c.SortOrder).ThenBy(c => c.Id, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        var result = new List<PlanCategoryGroup>(groups.Count);
        foreach (var group in groups.OrderBy(g => g.SortOrder).ThenBy(g => g.Id, StringComparer.Ordinal))
        {
            if (!month.GroupViews.TryGetValue(group.Id, out var view) || !ReferenceEquals(view.Definition, group))
                month.GroupViews[group.Id] = view = new PlanCategoryGroup(group);
            view.Categories = (byGroup.GetValueOrDefault(group.Id) ?? []).Select(category => View(month, category)).ToList();
            result.Add(view);
        }
        return result;
    }

    private PlanCategory View(BudgetEntity month, CategoryEntity category)
    {
        var state = EnsureState(month, category.Id);
        if (month.CategoryViews.TryGetValue(category.Id, out var view) && ReferenceEquals(view.Definition, category) && ReferenceEquals(view.State, state)) return view;
        return month.CategoryViews[category.Id] = new PlanCategory(category, state);
    }
}

// A category group as shown in one Plan month. Name, order and archive state are budget-wide.
public sealed class PlanCategoryGroup(CategoryGroupEntity definition)
{
    public CategoryGroupEntity Definition { get; } = definition;
    public string Id => Definition.Id;
    public string Name { get => Definition.Name; set => Definition.Name = value; }
    public int SortOrder { get => Definition.SortOrder; set => Definition.SortOrder = value; }
    public bool IsArchived { get => Definition.IsArchived; set => Definition.IsArchived = value; }
    public IReadOnlyList<PlanCategory> Categories { get; internal set; } = [];
}

// A category in one Plan month: the budget-wide definition plus that month's state.
public sealed class PlanCategory(CategoryEntity definition, CategoryMonthEntity state)
{
    public CategoryEntity Definition { get; } = definition;
    public CategoryMonthEntity State { get; } = state;
    public string Id => Definition.Id;
    public string BudgetMonth => State.BudgetMonth;
    public string GroupId { get => Definition.GroupId; set => Definition.GroupId = value; }
    public string Name { get => Definition.Name; set => Definition.Name = value; }
    public string? Icon { get => Definition.Icon; set => Definition.Icon = value; }
    public int SortOrder { get => Definition.SortOrder; set => Definition.SortOrder = value; }
    public bool IsArchived { get => Definition.IsArchived; set => Definition.IsArchived = value; }
    public decimal OpeningAvailable { get => State.OpeningAvailable; set => State.OpeningAvailable = value; }
    public decimal Assigned { get => State.Assigned; set => State.Assigned = value; }
    public decimal LegacyActivity { get => State.LegacyActivity; set => State.LegacyActivity = value; }
    public decimal Activity { get => State.Activity; set => State.Activity = value; }
    public decimal CreditOverspending { get => State.CreditOverspending; set => State.CreditOverspending = value; }
    public decimal Target { get => State.Target; set => State.Target = value; }
    public bool Snoozed { get => State.Snoozed; set => State.Snoozed = value; }
    public decimal Available => State.Available;
}

public static class PlanMonthQueries
{
    // Loads months with their state rows and composes each with its budget's category catalog.
    public static async Task<List<BudgetEntity>> LoadPlanMonthsAsync(this BudgetDbContext db, IQueryable<BudgetEntity> query, CancellationToken ct)
    {
        var months = await query.Include(b => b.CategoryStates).ToListAsync(ct);
        foreach (var budgetId in months.Select(m => m.BudgetId).Distinct(StringComparer.Ordinal).ToList())
        {
            var catalog = await CategoryCatalog.LoadAsync(db, budgetId, ct);
            foreach (var month in months.Where(m => m.BudgetId == budgetId)) catalog.Attach(month);
        }
        return months;
    }

    public static async Task<BudgetEntity?> LoadPlanMonthAsync(this BudgetDbContext db, IQueryable<BudgetEntity> query, CancellationToken ct) =>
        (await db.LoadPlanMonthsAsync(query, ct)).SingleOrDefault();
}
