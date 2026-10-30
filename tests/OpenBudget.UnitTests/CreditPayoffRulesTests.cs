using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public sealed class CreditPayoffRulesTests
{
    [Fact]
    public void Pay_by_date_spreads_the_remaining_debt_over_the_months_left_and_rounds_up_to_the_cent()
    {
        var progress = CreditPayoffRules.Calculate("payByDate", "2027-03", null, "2026-10", -2674.47m, 500.40m, 0);
        Assert.Equal(2674.47m, progress.NeededForPayoff);
        Assert.Equal(500.40m, progress.AvailableForPayment);
        Assert.Equal(2174.07m, progress.ToGo);
        Assert.Equal(18, progress.Percent);
        Assert.Equal(6, progress.MonthsRemaining);
        Assert.Equal(362.35m, progress.AssignThisMonth);
        Assert.Equal("behind", progress.Status);
        Assert.False(progress.IsPastDue);
    }

    [Fact]
    public void Pay_by_date_subtracts_money_already_assigned_this_month()
    {
        var partly = CreditPayoffRules.Calculate("payByDate", "2027-03", null, "2026-10", -2674.47m, 700.40m, 200);
        Assert.Equal(162.35m, partly.AssignThisMonth);
        var met = CreditPayoffRules.Calculate("payByDate", "2027-03", null, "2026-10", -2674.47m, 862.75m, 362.35m);
        Assert.Equal(0, met.AssignThisMonth);
        Assert.Equal("onTrack", met.Status);
        Assert.Equal(32, met.Percent);
    }

    [Fact]
    public void Pay_by_date_in_the_target_month_or_after_it_requires_the_whole_remaining_debt()
    {
        var due = CreditPayoffRules.Calculate("payByDate", "2026-10", null, "2026-10", -1000, 250, 0);
        Assert.Equal(1, due.MonthsRemaining);
        Assert.Equal(750, due.AssignThisMonth);
        Assert.False(due.IsPastDue);
        var late = CreditPayoffRules.Calculate("payByDate", "2026-08", null, "2026-10", -1000, 250, 0);
        Assert.True(late.IsPastDue);
        Assert.Equal(1, late.MonthsRemaining);
        Assert.Equal(750, late.AssignThisMonth);
    }

    [Fact]
    public void Funded_or_paid_off_cards_need_nothing_more()
    {
        var funded = CreditPayoffRules.Calculate("payByDate", "2027-03", null, "2026-10", -400, 450, 0);
        Assert.Equal(0, funded.ToGo);
        Assert.Equal(0, funded.AssignThisMonth);
        Assert.Equal(100, funded.Percent);
        Assert.Equal("funded", funded.Status);
        var paid = CreditPayoffRules.Calculate("payByDate", "2026-08", null, "2026-10", 25, 0, 0);
        Assert.Equal(0, paid.NeededForPayoff);
        Assert.Equal(100, paid.Percent);
        Assert.False(paid.IsPastDue);
    }

    [Fact]
    public void Negative_available_counts_as_no_money_for_the_payment()
    {
        var progress = CreditPayoffRules.Calculate("payByDate", "2026-11", null, "2026-10", -100, -20, 0);
        Assert.Equal(100, progress.ToGo);
        Assert.Equal(0, progress.Percent);
        Assert.Equal(50, progress.AssignThisMonth);
    }

    [Fact]
    public void Monthly_amount_asks_for_the_amount_less_this_months_assignment_capped_at_the_debt()
    {
        var start = CreditPayoffRules.Calculate("monthlyAmount", null, 300, "2026-10", -2674.47m, 500.40m, 0);
        Assert.Equal(300, start.AssignThisMonth);
        Assert.Equal(0, start.Percent);
        Assert.Null(start.MonthsRemaining);
        var partly = CreditPayoffRules.Calculate("monthlyAmount", null, 300, "2026-10", -2674.47m, 620.40m, 120);
        Assert.Equal(180, partly.AssignThisMonth);
        Assert.Equal(40, partly.Percent);
        var met = CreditPayoffRules.Calculate("monthlyAmount", null, 300, "2026-10", -2674.47m, 800.40m, 300);
        Assert.Equal(0, met.AssignThisMonth);
        Assert.Equal(100, met.Percent);
        Assert.Equal("onTrack", met.Status);
        var nearlyPaid = CreditPayoffRules.Calculate("monthlyAmount", null, 300, "2026-10", -550, 500, 0);
        Assert.Equal(50, nearlyPaid.AssignThisMonth);
        Assert.Equal(0, nearlyPaid.Percent);
    }

    [Theory]
    [InlineData("payByDate", "2026-10", null, null)]
    [InlineData("payByDate", "2030-12", null, null)]
    [InlineData("payByDate", "2026-09", null, "Target month cannot be in the past.")]
    [InlineData("payByDate", "2026-13", null, "Target month must use YYYY-MM format.")]
    [InlineData("payByDate", null, null, "Target month must use YYYY-MM format.")]
    [InlineData("payByDate", "2026-12", "10", "A pay-by-date target does not take a monthly amount.")]
    [InlineData("payByDate", "2077-01", null, "Target month must be within 50 years.")]
    [InlineData("monthlyAmount", null, "125.50", null)]
    [InlineData("monthlyAmount", null, "0", "Monthly amount must be positive with at most two decimal places.")]
    [InlineData("monthlyAmount", null, "-5", "Monthly amount must be positive with at most two decimal places.")]
    [InlineData("monthlyAmount", null, "10.005", "Monthly amount must be positive with at most two decimal places.")]
    [InlineData("monthlyAmount", null, null, "Monthly amount must be positive with at most two decimal places.")]
    [InlineData("monthlyAmount", "2026-12", "10", "A monthly amount target does not take a target month.")]
    [InlineData("minimumPayment", null, null, "Target type must be payByDate or monthlyAmount.")]
    public void Validation_rejects_past_months_bad_months_and_imprecise_amounts(string kind, string? month, string? amount, string? error) =>
        Assert.Equal(error, CreditPayoffRules.Validate(kind, month, amount is null ? null : decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), "2026-10"));
}
