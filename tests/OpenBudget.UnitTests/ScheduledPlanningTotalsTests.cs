using System.Text.Json;
using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public sealed class ScheduledPlanningTotalsTests
{
    [Fact]
    public void Recurrence_and_split_expenses_flow_across_the_year_boundary()
    {
        var rule = new ScheduleEntity
        {
            Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), AnchorDate = new DateOnly(2026, 12, 31),
            NextDate = new DateOnly(2026, 12, 31), Repeat = "monthly",
            TemplateJson = JsonSerializer.Serialize(new ScheduleTemplateDto(-150m, "Bills", null, null,
                [new SplitDto("rent", -100m), new SplitDto("utilities", -50m)]))
        };

        var totals = ScheduledPlanningTotals.ForYear(2027, [rule], [], out var byCategory);

        Assert.Equal(150m, totals["2026-12"].Expenses);
        Assert.Equal(150m, totals["2027-01"].Expenses);
        Assert.Equal(100m, byCategory[("2027-01", "rent")]);
        Assert.Equal(50m, byCategory[("2027-01", "utilities")]);
    }

    [Fact]
    public void Pending_override_changes_one_occurrence_and_count_stops_future_months()
    {
        var rule = new ScheduleEntity
        {
            Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), AnchorDate = new DateOnly(2027, 1, 1),
            NextDate = new DateOnly(2027, 1, 1), Repeat = "monthly", Count = 2,
            TemplateJson = JsonSerializer.Serialize(new ScheduleTemplateDto(500m, "Income", null, null, []))
        };
        var occurrence = new ScheduleOccurrenceEntity
        {
            ScheduleId = rule.Id, Sequence = 0, Disposition = "pending",
            OverrideJson = JsonSerializer.Serialize(new ScheduleTemplateDto(600m, "Income", null, null, []))
        };

        var totals = ScheduledPlanningTotals.ForYear(2027, [rule], [occurrence], out _);

        Assert.Equal(600m, totals["2027-01"].Income);
        Assert.Equal(500m, totals["2027-02"].Income);
        Assert.False(totals.ContainsKey("2027-03"));
    }
}
