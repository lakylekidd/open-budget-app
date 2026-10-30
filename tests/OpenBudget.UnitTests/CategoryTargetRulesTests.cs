using OpenBudget.Api;

namespace OpenBudget.UnitTests;

// October 2026 starts on a Thursday: it has five Thursdays, Fridays and Saturdays and four of every other weekday.
public sealed class CategoryTargetRulesTests
{
    private static CategoryTargetDefinition Target(string kind, decimal amount, string behavior = CategoryTargetRules.SetAside, string start = "2026-10",
        int? weekday = null, int? day = null, DateOnly? due = null, int? every = null, string? unit = null) =>
        new(kind, amount, behavior, start, weekday, day, due, every, unit);

    [Fact]
    public void Weekly_target_multiplies_the_amount_by_the_five_fridays_in_october()
    {
        var progress = CategoryTargetRules.Calculate(Target("weekly", 25m, weekday: 5), "2026-10", assigned: 50m, available: 50m);

        Assert.Equal(125m, progress.Needed);
        Assert.Equal(5, progress.Segments);
        Assert.Equal(75m, progress.MoreNeeded);
        Assert.Equal(50m, progress.Funded);
        Assert.Equal(0.4m, progress.FundedFraction);
        Assert.Equal("underfunded", progress.Status);
        Assert.Equal("Every Friday", progress.Cadence);
    }

    [Fact]
    public void Weekly_target_uses_four_segments_for_a_weekday_that_occurs_four_times()
    {
        var progress = CategoryTargetRules.Calculate(Target("weekly", 25m, weekday: 0), "2026-10", assigned: 0, available: 0);

        Assert.Equal(100m, progress.Needed);
        Assert.Equal(4, progress.Segments);
        Assert.Equal(100m, progress.MoreNeeded);
        Assert.Equal(0m, progress.FundedFraction);
    }

    [Fact]
    public void Weekly_target_is_funded_once_assigned_covers_every_occurrence()
    {
        var progress = CategoryTargetRules.Calculate(Target("weekly", 25m, weekday: 0), "2026-10", assigned: 120m, available: 120m);

        Assert.Equal(0m, progress.MoreNeeded);
        Assert.Equal(100m, progress.Funded);
        Assert.Equal(1m, progress.FundedFraction);
        Assert.Equal("funded", progress.Status);
    }

    [Fact]
    public void Monthly_set_aside_needs_the_full_amount_each_month_regardless_of_carried_money()
    {
        var progress = CategoryTargetRules.Calculate(Target("monthly", 100m), "2026-10", assigned: 30m, available: 230m);

        Assert.Equal(100m, progress.Needed);
        Assert.Equal(70m, progress.MoreNeeded);
        Assert.Equal(0.3m, progress.FundedFraction);
        Assert.Equal(new DateOnly(2026, 10, 31), progress.NextDueDate);
        Assert.Equal("Every month by the last day", progress.Cadence);
    }

    [Fact]
    public void Monthly_due_day_is_clamped_to_short_months()
    {
        var progress = CategoryTargetRules.Calculate(Target("monthly", 100m, day: 31), "2027-02", assigned: 0, available: 0);

        Assert.Equal(new DateOnly(2027, 2, 28), progress.NextDueDate);
        Assert.Equal("Every month by the 31st", progress.Cadence);
    }

    [Fact]
    public void Monthly_refill_tops_available_up_to_the_amount()
    {
        // Opening + activity = 80 - 20 = 60, so another 90 brings Available to 150; 20 is already assigned.
        var progress = CategoryTargetRules.Calculate(Target("monthly", 150m, CategoryTargetRules.Refill), "2026-10", assigned: 20m, available: 80m);

        Assert.Equal(90m, progress.Needed);
        Assert.Equal(70m, progress.MoreNeeded);
        Assert.Equal(0.2222m, progress.FundedFraction);
    }

    [Fact]
    public void Monthly_refill_needs_nothing_when_carried_money_already_reaches_the_amount()
    {
        var progress = CategoryTargetRules.Calculate(Target("monthly", 150m, CategoryTargetRules.Refill), "2026-10", assigned: 0, available: 175m);

        Assert.Equal(0m, progress.Needed);
        Assert.Equal(0m, progress.MoreNeeded);
        Assert.Equal(1m, progress.FundedFraction);
        Assert.Equal("funded", progress.Status);
    }

    [Fact]
    public void Yearly_set_aside_spreads_the_amount_over_the_months_left_and_rounds_up_to_the_cent()
    {
        // October 2026 through March 2027 is six months: 1000 / 6 = 166.666... -> 166.67.
        var progress = CategoryTargetRules.Calculate(Target("yearly", 1000m, due: new DateOnly(2027, 3, 15)), "2026-10", assigned: 0, available: 0);

        Assert.Equal(166.67m, progress.Needed);
        Assert.Equal(6, progress.MonthsRemaining);
        Assert.Equal(new DateOnly(2027, 3, 15), progress.NextDueDate);
        Assert.Equal("Every year by 15-03-2027", progress.Cadence);
    }

    [Fact]
    public void Yearly_set_aside_subtracts_amounts_assigned_earlier_in_the_cycle()
    {
        // 1000 - (166.67 + 100) = 733.33 over December..March (4 months) = 183.3325 -> 183.34.
        var history = new Dictionary<string, decimal> { ["2026-10"] = 166.67m, ["2026-11"] = 100m, ["2026-09"] = 999m };
        var progress = CategoryTargetRules.Calculate(Target("yearly", 1000m, due: new DateOnly(2027, 3, 15)), "2026-12", assigned: 50m, available: 316.67m, history);

        Assert.Equal(183.34m, progress.Needed);
        Assert.Equal(133.34m, progress.MoreNeeded);
        Assert.Equal(4, progress.MonthsRemaining);
    }

    [Fact]
    public void Yearly_target_starts_the_next_cycle_after_its_due_date()
    {
        // April 2027 begins the cycle due 15-03-2028: twelve months, so 1000 / 12 = 83.333... -> 83.34.
        var history = new Dictionary<string, decimal> { ["2026-10"] = 500m, ["2027-03"] = 500m };
        var progress = CategoryTargetRules.Calculate(Target("yearly", 1000m, due: new DateOnly(2027, 3, 15)), "2027-04", assigned: 0, available: 0, history);

        Assert.Equal(83.34m, progress.Needed);
        Assert.Equal(12, progress.MonthsRemaining);
        Assert.Equal(new DateOnly(2028, 3, 15), progress.NextDueDate);
        Assert.False(progress.IsPastDue);
    }

    [Fact]
    public void Yearly_refill_spreads_the_shortfall_below_the_target_balance()
    {
        // Available before this month's assignment is 300; (1200 - 300) / 6 months = 150.
        var progress = CategoryTargetRules.Calculate(Target("yearly", 1200m, CategoryTargetRules.Refill, due: new DateOnly(2027, 3, 1)), "2026-10", assigned: 0, available: 300m);

        Assert.Equal(150m, progress.Needed);
        Assert.Equal(150m, progress.MoreNeeded);
    }

    [Fact]
    public void Custom_one_off_target_spreads_until_due_then_asks_for_the_unfunded_remainder()
    {
        var target = Target("custom", 500m, due: new DateOnly(2026, 12, 20));

        var october = CategoryTargetRules.Calculate(target, "2026-10", assigned: 0, available: 0);
        Assert.Equal(166.67m, october.Needed);
        Assert.Equal(3, october.MonthsRemaining);
        Assert.Equal("By 20-12-2026", october.Cadence);

        var history = new Dictionary<string, decimal> { ["2026-10"] = 166.67m, ["2026-11"] = 166.67m, ["2026-12"] = 100m };
        var late = CategoryTargetRules.Calculate(target, "2027-01", assigned: 0, available: 433.34m, history);
        Assert.True(late.IsPastDue);
        Assert.Equal(66.66m, late.Needed);
        Assert.Null(late.MonthsRemaining);

        var paid = CategoryTargetRules.Calculate(target, "2027-01", assigned: 0, available: 0, new Dictionary<string, decimal> { ["2026-10"] = 500m });
        Assert.Equal(0m, paid.Needed);
        Assert.Equal("funded", paid.Status);
    }

    [Fact]
    public void Custom_fill_up_target_uses_the_available_balance()
    {
        // Fill up to 600 by 20-12-2026 with 150 already available: (600 - 150) / 3 = 150.
        var progress = CategoryTargetRules.Calculate(Target("custom", 600m, CategoryTargetRules.Refill, due: new DateOnly(2026, 12, 20)), "2026-10", assigned: 50m, available: 200m);

        Assert.Equal(150m, progress.Needed);
        Assert.Equal(100m, progress.MoreNeeded);
    }

    [Fact]
    public void Custom_target_repeating_every_two_weeks_counts_due_dates_in_the_month()
    {
        var target = Target("custom", 40m, due: new DateOnly(2026, 10, 2), every: 2, unit: "week");

        var october = CategoryTargetRules.Calculate(target, "2026-10", assigned: 0, available: 0);
        Assert.Equal(120m, october.Needed); // 2, 16 and 30 October
        Assert.Equal(3, october.Segments);
        Assert.Equal(new DateOnly(2026, 10, 2), october.NextDueDate);
        Assert.Equal("Every 2 weeks, next 02-10-2026", october.Cadence);

        var november = CategoryTargetRules.Calculate(target, "2026-11", assigned: 0, available: 0);
        Assert.Equal(80m, november.Needed); // 13 and 27 November
        Assert.Equal(new DateOnly(2026, 11, 13), november.NextDueDate);
    }

    [Fact]
    public void Custom_target_repeating_monthly_rolls_its_due_date_forward()
    {
        var target = Target("custom", 300m, CategoryTargetRules.Refill, due: new DateOnly(2026, 11, 15), every: 1, unit: "month");

        var october = CategoryTargetRules.Calculate(target, "2026-10", assigned: 0, available: 100m);
        Assert.Equal(100m, october.Needed); // (300 - 100) over October and November

        var december = CategoryTargetRules.Calculate(target, "2026-12", assigned: 0, available: 120m);
        Assert.Equal(180m, december.Needed);
        Assert.Equal(new DateOnly(2026, 12, 15), december.NextDueDate);
        Assert.Equal("Every month by 15-12-2026", december.Cadence);
    }

    [Fact]
    public void Custom_target_repeating_every_two_years_resets_its_cycle()
    {
        var target = Target("custom", 2400m, due: new DateOnly(2026, 12, 1), every: 2, unit: "year");

        var progress = CategoryTargetRules.Calculate(target, "2027-01", assigned: 0, available: 0, new Dictionary<string, decimal> { ["2026-11"] = 2400m });

        Assert.Equal(new DateOnly(2028, 12, 1), progress.NextDueDate);
        Assert.Equal(24, progress.MonthsRemaining);
        Assert.Equal(100m, progress.Needed);
        Assert.Equal("Every 2 years by 01-12-2028", progress.Cadence);
    }

    [Fact]
    public void Negative_assignment_counts_as_nothing_funded()
    {
        var progress = CategoryTargetRules.Calculate(Target("monthly", 100m), "2026-10", assigned: -20m, available: 0);

        Assert.Equal(0m, progress.Funded);
        Assert.Equal(100m, progress.MoreNeeded);
        Assert.Equal(0m, progress.FundedFraction);
    }

    public static TheoryData<SaveCategoryTargetRequest, string> InvalidRequests => new()
    {
        { Request() with { Amount = 0 }, "greater than zero" },
        { Request() with { Amount = 10.005m }, "two decimal places" },
        { Request() with { Kind = "daily" }, "Target type" },
        { Request() with { Behavior = "keep" }, "set aside" },
        { Request() with { Month = "2026-13" }, "YYYY-MM" },
        { Request() with { Kind = "weekly", Weekday = 7 }, "weekday" },
        { Request() with { Kind = "weekly", Weekday = 1, Behavior = "refill" }, "Weekly targets" },
        { Request() with { DayOfMonth = 32 }, "Day of month" },
        { Request() with { Weekday = 1 }, "Only weekly" },
        { Request() with { DueDate = new DateOnly(2027, 1, 1) }, "Only yearly and custom" },
        { Request() with { Kind = "yearly" }, "Choose a date" },
        { Request() with { Kind = "custom", DueDate = new DateOnly(9999, 1, 1) }, "between 1900 and 9998" },
        { Request() with { Kind = "custom", DueDate = new DateOnly(2027, 1, 1), RepeatEvery = 100, RepeatUnit = "day" }, "between 1 and 99" },
        { Request() with { Kind = "custom", DueDate = new DateOnly(2027, 1, 1), RepeatEvery = 2 }, "both an interval and a unit" },
        { Request() with { Kind = "custom", DueDate = new DateOnly(2027, 1, 1), RepeatEvery = 2, RepeatUnit = "fortnight" }, "Repeat unit" },
        { Request() with { RepeatEvery = 1, RepeatUnit = "month" }, "Only custom" },
        { Request() with { CommandId = Guid.Empty }, "Command ID" },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void Invalid_requests_are_rejected(SaveCategoryTargetRequest request, string message)
    {
        Assert.Contains(message, CategoryTargetRules.Validate(request));
    }

    [Fact]
    public void Valid_requests_for_every_type_pass_validation()
    {
        Assert.Null(CategoryTargetRules.Validate(Request()));
        Assert.Null(CategoryTargetRules.Validate(Request() with { Kind = "weekly", Weekday = 6 }));
        Assert.Null(CategoryTargetRules.Validate(Request() with { DayOfMonth = 15, Behavior = "refill" }));
        Assert.Null(CategoryTargetRules.Validate(Request() with { Kind = "yearly", DueDate = new DateOnly(2027, 3, 1) }));
        Assert.Null(CategoryTargetRules.Validate(Request() with { Kind = "custom", DueDate = new DateOnly(2027, 3, 1), RepeatEvery = 99, RepeatUnit = "year" }));
    }

    [Fact]
    public void Non_repeating_custom_targets_cannot_be_due_in_the_past()
    {
        var today = new DateOnly(2026, 10, 21);
        Assert.True(CategoryTargetRules.IsPastDueOneOff(Request() with { Kind = "custom", DueDate = new DateOnly(2026, 10, 20) }, today));
        Assert.False(CategoryTargetRules.IsPastDueOneOff(Request() with { Kind = "custom", DueDate = new DateOnly(2026, 10, 21) }, today));
        Assert.False(CategoryTargetRules.IsPastDueOneOff(Request() with { Kind = "custom", DueDate = new DateOnly(2026, 1, 1), RepeatEvery = 1, RepeatUnit = "month" }, today));
    }

    [Fact]
    public void Payment_categories_are_recognized_by_group_or_id()
    {
        Assert.True(CategoryTargetRules.IsPaymentCategory("ccp-1", "home"));
        Assert.True(CategoryTargetRules.IsPaymentCategory("anything", CreditCardRules.PaymentGroupId));
        Assert.False(CategoryTargetRules.IsPaymentCategory("groceries", "daily"));
    }

    private static SaveCategoryTargetRequest Request() => new(Guid.NewGuid(), 0, "2026-10", "monthly", 100m, "setAside");
}
