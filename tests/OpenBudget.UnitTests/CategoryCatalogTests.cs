using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public sealed class CategoryCatalogTests
{
    private static (CategoryCatalog Catalog, BudgetEntity August, BudgetEntity September) TwoMonths()
    {
        var catalog = new CategoryCatalog("b1", [], []);
        catalog.AddGroup("home", "Home", 1);
        catalog.AddGroup("daily", "Daily", 0);
        catalog.AddCategory("rent", "home", "Rent", 0);
        catalog.AddCategory("food", "daily", "Food", 1);
        catalog.AddCategory("coffee", "daily", "Coffee", 0);
        var august = new BudgetEntity { BudgetId = "b1", Month = "2026-08" };
        var september = new BudgetEntity { BudgetId = "b1", Month = "2026-09" };
        catalog.Attach(august); catalog.Attach(september);
        return (catalog, august, september);
    }

    [Fact]
    public void Every_month_shows_the_same_structure_in_order_with_its_own_state()
    {
        var (_, august, september) = TwoMonths();
        august.State("food")!.Assigned = 40;
        Assert.Equal(["daily:coffee", "daily:food", "home:rent"], august.Groups.SelectMany(g => g.Categories.Select(c => $"{g.Id}:{c.Id}")));
        Assert.Equal(august.Groups.SelectMany(g => g.Categories.Select(c => c.Id)), september.Groups.SelectMany(g => g.Categories.Select(c => c.Id)));
        Assert.Equal(40, august.Groups.SelectMany(g => g.Categories).Single(c => c.Id == "food").Assigned);
        Assert.Equal(0, september.Groups.SelectMany(g => g.Categories).Single(c => c.Id == "food").Assigned);
    }

    [Fact]
    public void Renaming_or_moving_through_one_month_changes_every_month()
    {
        var (_, august, september) = TwoMonths();
        var coffee = august.Groups.SelectMany(g => g.Categories).Single(c => c.Id == "coffee");
        coffee.Name = "Cafe"; coffee.GroupId = "home"; coffee.IsArchived = true;
        var other = september.Groups.Single(g => g.Id == "home").Categories.Single(c => c.Id == "coffee");
        Assert.Equal(("Cafe", true), (other.Name, other.IsArchived));
        Assert.Same(coffee, august.Groups.Single(g => g.Id == "home").Categories.Single(c => c.Id == "coffee"));
    }

    [Fact]
    public void A_new_category_gets_a_zero_state_in_every_attached_month_and_later_attached_months()
    {
        var (catalog, august, september) = TwoMonths();
        catalog.AddCategory("gym", "home", "Gym", 1);
        Assert.All(new[] { august, september }, month => Assert.Equal(0, month.State("gym")!.Available));
        var later = new BudgetEntity { BudgetId = "b1", Month = "2026-12" };
        catalog.Attach(later);
        Assert.Equal(4, later.CategoryStates.Count);
        Assert.Throws<InvalidOperationException>(() => catalog.AddCategory("gym", "home", "Again", 2));
        Assert.Throws<InvalidOperationException>(() => catalog.AddCategory("other", "missing", "Other", 0));
        Assert.Throws<InvalidOperationException>(() => catalog.Attach(new BudgetEntity { BudgetId = "b2", Month = "2026-08" }));
    }

    [Fact]
    public void Removing_a_category_removes_its_state_from_every_month_and_only_empty_groups_can_be_removed()
    {
        var (catalog, august, september) = TwoMonths();
        catalog.RemoveCategory(catalog.Find("rent")!);
        Assert.Null(august.State("rent")); Assert.Null(september.State("rent"));
        Assert.Throws<InvalidOperationException>(() => catalog.RemoveGroup(catalog.FindGroup("daily")!));
        catalog.RemoveGroup(catalog.FindGroup("home")!);
        Assert.Equal(["daily"], august.Groups.Select(g => g.Id));
    }
}

public sealed class CategoryDeletionRulesTests
{
    [Theory]
    [InlineData("ccp-6f9619ff-8b86-d011-b42d-00cf4fc964ff", true)]
    [InlineData("loan-6f9619ff-8b86-d011-b42d-00cf4fc964ff", true)]
    [InlineData("wl-6f9619ff-8b86-d011-b42d-00cf4fc964ff", true)]
    [InlineData("groceries", false)]
    [InlineData("category-6f9619ff-8b86-d011-b42d-00cf4fc964ff", false)]
    public void System_categories_are_recognised(string id, bool system) => Assert.Equal(system, CategoryDeletionRules.IsSystemCategory(id));

    [Fact]
    public void Reassignment_options_exclude_the_deleted_archived_and_system_categories()
    {
        var catalog = new CategoryCatalog("b1", [], []);
        catalog.AddGroup(CreditCardRules.PaymentGroupId, CreditCardRules.PaymentGroupName, 0);
        catalog.AddGroup("daily", "Daily", 1);
        catalog.AddGroup("old", "Old", 2, isArchived: true);
        catalog.AddCategory("ccp-6f9619ff-8b86-d011-b42d-00cf4fc964ff", CreditCardRules.PaymentGroupId, "Card", 0);
        catalog.AddCategory("food", "daily", "Food", 0);
        catalog.AddCategory("coffee", "daily", "Coffee", 1);
        catalog.AddCategory("hidden", "daily", "Hidden", 2, isArchived: true);
        catalog.AddCategory("in-archived-group", "old", "Old", 0);
        Assert.Equal([new CategoryOptionDto("food", "Food", "daily", "Daily")], CategoryDeletionRules.ReassignmentOptions(catalog, "coffee"));
    }

    [Fact]
    public void Merging_a_month_moves_assignments_and_baselines_and_zeroes_the_deleted_state()
    {
        var deleted = new CategoryMonthEntity { CategoryId = "coffee", Assigned = 30, OpeningAvailable = 5, LegacyActivity = -2, Activity = -7 };
        var target = new CategoryMonthEntity { CategoryId = "food", Assigned = 10, OpeningAvailable = 1, LegacyActivity = -1 };
        Assert.Equal(30, CategoryDeletionRules.MergeMonth(deleted, target));
        Assert.Equal((40m, 6m, -3m), (target.Assigned, target.OpeningAvailable, target.LegacyActivity));
        Assert.Equal((0m, 0m, 0m), (deleted.Assigned, deleted.OpeningAvailable, deleted.LegacyActivity));
    }

    [Fact]
    public void Schedule_templates_are_reassigned_for_the_category_and_split_lines()
    {
        var template = new ScheduleTemplateDto(-20, "Cafe", null, "coffee", [new SplitDto("coffee", -5), new SplitDto("food", -15)]);
        Assert.True(CategoryDeletionRules.References(template, "coffee"));
        var moved = CategoryDeletionRules.Reassign(template, "coffee", "food");
        Assert.Equal("food", moved.CategoryId);
        Assert.All(moved.Splits!, split => Assert.Equal("food", split.CategoryId));
        Assert.False(CategoryDeletionRules.References(moved, "coffee"));
        Assert.False(CategoryDeletionRules.References(null, "coffee"));
    }

    [Fact]
    public void Audit_resource_ids_are_deterministic_per_budget_and_category()
    {
        Assert.Equal(CategoryDeletionRules.ResourceId("b1", "coffee"), CategoryDeletionRules.ResourceId("b1", "coffee"));
        Assert.NotEqual(CategoryDeletionRules.ResourceId("b1", "coffee"), CategoryDeletionRules.ResourceId("b2", "coffee"));
    }
}
