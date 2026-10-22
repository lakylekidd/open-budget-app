using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public sealed class LongTermPlanningTests
{
    private static LongTermPlanningCategoryEntity Value(string month, decimal? budgetOverride = null, decimal expenses = 0m) => new()
    {
        Month = month,
        CategoryId = "groceries",
        PlannedBudgetOverride = budgetOverride,
        PlannedExpenses = expenses
    };

    [Fact]
    public void Effective_month_overrides_propagate_until_the_next_override()
    {
        var entries = new[]
        {
            Value("2026-01", 500m, 400m),
            Value("2026-06", 600m),
            Value("2026-09", 700m)
        };

        var result = LongTermPlanningCalculations.Calculate(2026, "groceries", entries);

        Assert.Equal(500m, result[0].PlannedBudget);
        Assert.Equal(100m, result[0].PlannedAvailable);
        Assert.Equal(500m, result[4].PlannedBudget);
        Assert.Equal(600m, result[5].PlannedBudget);
        Assert.Equal(600m, result[7].PlannedBudget);
        Assert.Equal(700m, result[8].PlannedBudget);
        Assert.Equal(700m, result[11].PlannedBudget);
        Assert.Equal(600m, result[8].PlannedBudgetBeforeMonth);
    }

    [Fact]
    public void Removing_an_override_makes_the_month_inherit_the_previous_effective_value()
    {
        var entries = new[]
        {
            Value("2026-01", 500m),
            Value("2026-06", null),
            Value("2026-09", 700m)
        };

        var result = LongTermPlanningCalculations.Calculate(2026, "groceries", entries);

        Assert.Equal(500m, result[5].PlannedBudget);
        Assert.Null(result[5].PlannedBudgetOverride);
        Assert.Equal(500m, result[7].PlannedBudget);
        Assert.Equal(700m, result[8].PlannedBudget);
    }

    [Fact]
    public void Planned_available_carries_across_the_december_january_boundary_with_cent_precision()
    {
        var entries = new[]
        {
            Value("2025-12", 300.25m, 25.10m),
            Value("2026-01", null, 100.05m)
        };

        var result = LongTermPlanningCalculations.Calculate(2026, "groceries", entries);

        Assert.Equal(300.25m, result[0].PlannedBudget);
        Assert.Equal(100.05m, result[0].PlannedExpenses);
        Assert.Equal(475.35m, result[0].PlannedAvailable);
    }

    [Fact]
    public void No_saved_values_produce_a_zero_forecast_without_sample_data()
    {
        var result = LongTermPlanningCalculations.Calculate(2026, "groceries", Array.Empty<LongTermPlanningCategoryEntity>());

        Assert.Equal(12, result.Count);
        Assert.All(result, month =>
        {
            Assert.Null(month.PlannedBudgetOverride);
            Assert.Equal(0m, month.PlannedBudget);
            Assert.Equal(0m, month.PlannedExpenses);
            Assert.Equal(0m, month.PlannedAvailable);
        });
    }
}
