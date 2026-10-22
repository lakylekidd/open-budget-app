using OpenBudget.Api;
namespace OpenBudget.UnitTests;
public sealed class RecurrenceTests
{
    [Theory]
    [InlineData("daily", "2026-12-31", 1, "2027-01-01")]
    [InlineData("weekly", "2026-12-29", 1, "2027-01-05")]
    [InlineData("everyOtherWeek", "2026-12-29", 1, "2027-01-12")]
    [InlineData("every4Weeks", "2026-12-29", 1, "2027-01-26")]
    [InlineData("monthly", "2026-01-31", 1, "2026-02-28")]
    [InlineData("monthly", "2026-01-31", 2, "2026-03-31")]
    [InlineData("everyOtherMonth", "2026-12-31", 1, "2027-02-28")]
    [InlineData("every3Months", "2026-11-30", 1, "2027-02-28")]
    [InlineData("every4Months", "2026-10-31", 1, "2027-02-28")]
    [InlineData("twiceAYear", "2026-08-31", 1, "2027-02-28")]
    [InlineData("yearly", "2024-02-29", 1, "2025-02-28")]
    [InlineData("yearly", "2024-02-29", 4, "2028-02-29")]
    [InlineData("everyOtherYear", "2024-02-29", 2, "2028-02-29")]
    [InlineData("twiceAMonth", "2026-01-10", 1, "2026-01-25")]
    [InlineData("twiceAMonth", "2026-01-10", 2, "2026-02-10")]
    [InlineData("twiceAMonth", "2026-01-31", 1, "2026-02-16")]
    [InlineData("twiceAMonth", "2026-01-31", 2, "2026-02-28")]
    public void Anchored_dates_do_not_drift(string repeat, string anchor, int index, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), RecurrenceRules.Occurrence(DateOnly.Parse(anchor), repeat, index));
    [Fact]
    public void Exhaustion_and_identity_are_deterministic()
    {
        Assert.Null(RecurrenceRules.Occurrence(new(2026,10,21), "never", 1));
        Assert.Null(RecurrenceRules.Occurrence(new(9998,12,31), "daily", 1));
        Assert.Throws<LedgerException>(() => RecurrenceRules.Occurrence(new(2026,10,21), "unknown", 1));
        var id = Guid.NewGuid(); Assert.Equal(RecurrenceRules.OccurrenceId(id,4), RecurrenceRules.OccurrenceId(id,4));
        Assert.NotEqual(RecurrenceRules.OccurrenceId(id,4), RecurrenceRules.OccurrenceId(id,5));
    }
}
