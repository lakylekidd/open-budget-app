using System.Globalization;

namespace OpenBudget.Api;

// A debt payoff target belongs to one credit account's payment category. It never changes ledger or Plan amounts;
// its progress is recalculated for whichever Plan month is being viewed.
//   Needed for Payoff   = max(-card balance through the end of the viewed month, 0)
//   Available           = the payment category's Available in that month (negative values count as 0)
//   To Go               = max(Needed - Available, 0)
//   payByDate           : months remaining = viewed month .. target month inclusive (1 when the target month has passed)
//                         assign this month = max(0, ceil-to-cent((Needed - (Available - Assigned)) / months) - Assigned)
//                         percent = floor(min(Available / Needed, 1) * 100), 100 when Needed is 0
//   monthlyAmount       : month target = min(amount, Assigned + To Go)
//                         assign this month = max(month target - Assigned, 0)
//                         percent = floor(min(Assigned / month target, 1) * 100), 100 when the month target is 0
public static class CreditPayoffRules
{
    public const string PayByDate = "payByDate";
    public const string MonthlyAmount = "monthlyAmount";
    public const int MaxYearsAhead = 50;

    public static string? Validate(string? kind, string? targetMonth, decimal? monthlyAmount, string currentMonth)
    {
        if (kind == PayByDate)
        {
            if (monthlyAmount is not null) return "A pay-by-date target does not take a monthly amount.";
            if (!TryParseMonth(targetMonth, out var target)) return "Target month must use YYYY-MM format.";
            var current = ParseMonth(currentMonth);
            if (target < current) return "Target month cannot be in the past.";
            if (target > current.AddYears(MaxYearsAhead)) return $"Target month must be within {MaxYearsAhead} years.";
            return null;
        }
        if (kind == MonthlyAmount)
        {
            if (targetMonth is not null) return "A monthly amount target does not take a target month.";
            if (monthlyAmount is not { } amount || amount <= 0 || !LedgerRules.IsMoney(amount)) return "Monthly amount must be positive with at most two decimal places.";
            return null;
        }
        return "Target type must be payByDate or monthlyAmount.";
    }

    public static CreditPayoffProgressDto Calculate(string kind, string? targetMonth, decimal? monthlyAmount, string viewedMonth,
        decimal balanceThroughMonth, decimal available, decimal assigned)
    {
        var needed = Math.Max(-balanceThroughMonth, 0);
        var funded = Math.Max(available, 0);
        var toGo = Math.Max(needed - funded, 0);
        int? monthsRemaining = null;
        var pastDue = false;
        decimal assign;
        int percent;
        if (kind == PayByDate)
        {
            var months = MonthsBetween(ParseMonth(viewedMonth), ParseMonth(targetMonth!)) + 1;
            pastDue = months < 1;
            monthsRemaining = Math.Max(months, 1);
            var fundedBeforeThisMonth = funded - assigned;
            assign = Math.Max(CeilingCents((needed - fundedBeforeThisMonth) / monthsRemaining.Value) - assigned, 0);
            assign = Math.Min(assign, toGo);
            percent = needed == 0 ? 100 : Percent(funded / needed);
        }
        else
        {
            var monthTarget = Math.Max(Math.Min(monthlyAmount!.Value, assigned + toGo), 0);
            assign = Math.Min(Math.Max(monthTarget - assigned, 0), toGo);
            percent = monthTarget == 0 ? 100 : Percent(Math.Max(assigned, 0) / monthTarget);
        }
        var status = toGo == 0 ? "funded" : assign == 0 ? "onTrack" : "behind";
        return new(needed, available, assigned, toGo, percent, assign, monthsRemaining, pastDue && toGo > 0, status);
    }

    public static bool TryParseMonth(string? value, out DateOnly month)
    {
        month = default;
        return value is { Length: 7 } && DateOnly.TryParseExact($"{value}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out month)
            && month.Year is >= 1900 and <= 9998;
    }

    private static DateOnly ParseMonth(string value) =>
        TryParseMonth(value, out var month) ? month : throw new LedgerException("Month must use YYYY-MM format.");

    private static int MonthsBetween(DateOnly from, DateOnly to) => (to.Year - from.Year) * 12 + to.Month - from.Month;
    private static decimal CeilingCents(decimal value) => Math.Ceiling(value * 100) / 100;
    private static int Percent(decimal ratio) => (int)Math.Floor(Math.Clamp(ratio, 0, 1) * 100);
}

public sealed class CreditPayoffTargetEntity
{
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public Guid AccountId { get; set; }
    public string Kind { get; set; } = CreditPayoffRules.PayByDate;
    public string? TargetMonth { get; set; }
    public decimal? MonthlyAmount { get; set; }
    // The card debt when the target was last saved; shown as the "Pay off" headline.
    public decimal PayoffAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
