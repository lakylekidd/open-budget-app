using System.Globalization;

namespace OpenBudget.Api;

// Category targets for regular (non credit-card-payment) Plan categories. A target never changes ledger or Plan
// amounts: what a category needs in a viewed month is recalculated from the target definition and that month's
// Assigned/Available values (plus earlier months' Assigned values of the current cycle for "set aside" date targets).
//
// Notation: M = viewed month, A = target amount, funded = max(Assigned in M, 0),
//           before = Available - Assigned (opening balance plus this month's activity).
//   weekly               needed = A x occurrences of the weekday in M; one bar segment per occurrence.
//   monthly  setAside    needed = A.
//   monthly  refill      needed = max(A - before, 0) (top Available up to A).
//   yearly               a dated target that repeats every 12 months from its date.
//   dated    setAside    remaining = A - Assigned in earlier months of the current cycle,
//                        needed = max(ceil-to-cent(remaining / monthsRemaining), 0), monthsRemaining counts M..due month.
//   dated    refill      needed = max(ceil-to-cent((A - before) / monthsRemaining), 0).
//   custom repeat N month/year: dated target whose due date advances by N months/years after it passes.
//   custom repeat N day/week:   needed = A x due occurrences in M (minus before for fill up); a month without an
//                        occurrence spreads A towards the next occurrence like a dated target.
//   non-repeating after the due date: the whole remaining amount (0 once funded).
//   moreNeeded = max(needed - funded, 0); fundedFraction = min(funded / needed, 1), 1 when nothing is needed.
// A cycle starts the month after the previous due date and never before the month the target was created.
public static class CategoryTargetRules
{
    public const string Weekly = "weekly";
    public const string Monthly = "monthly";
    public const string Yearly = "yearly";
    public const string Custom = "custom";
    public const string SetAside = "setAside";
    public const string Refill = "refill";
    public static readonly string[] Kinds = [Weekly, Monthly, Yearly, Custom];
    public static readonly string[] RepeatUnits = ["day", "week", "month", "year"];
    public const decimal MaxAmount = 999_999_999.99m;

    public static string? Validate(SaveCategoryTargetRequest request)
    {
        if (request.CommandId == Guid.Empty) return "Command ID is required.";
        if (request.Version < 0) return "Target version is invalid.";
        if (!TryParseMonth(request.Month, out _)) return "Month must use YYYY-MM format.";
        if (request.Kind is null || !Kinds.Contains(request.Kind)) return "Target type must be weekly, monthly, yearly or custom.";
        if (request.Amount <= 0 || request.Amount > MaxAmount || decimal.Round(request.Amount, 2) != request.Amount)
            return "Target amount must be greater than zero with at most two decimal places.";
        if (request.Behavior is not (SetAside or Refill)) return "Choose whether to set aside another amount or refill up to it.";
        if (request.Kind != Weekly && request.Weekday is not null) return "Only weekly targets take a weekday.";
        if (request.Kind != Monthly && request.DayOfMonth is not null) return "Only monthly targets take a day of the month.";
        if (request.Kind is Weekly or Monthly && request.DueDate is not null) return "Only yearly and custom targets take a date.";
        if (request.Kind != Custom && (request.RepeatEvery is not null || request.RepeatUnit is not null)) return "Only custom targets can repeat.";
        switch (request.Kind)
        {
            case Weekly:
                if (request.Weekday is not int weekday || weekday is < 0 or > 6) return "Choose a weekday from Sunday (0) to Saturday (6).";
                if (request.Behavior != SetAside) return "Weekly targets set aside the amount for every occurrence.";
                break;
            case Monthly:
                if (request.DayOfMonth is int day && day is < 0 or > 31) return "Day of month must be 1 to 31, or empty for the last day.";
                break;
            default:
                if (request.DueDate is not DateOnly due) return "Choose a date for this target.";
                if (due.Year is < 1900 or > 9998) return "Target date must be between 1900 and 9998.";
                if ((request.RepeatEvery is null) != (request.RepeatUnit is null)) return "A repeating target needs both an interval and a unit.";
                if (request.RepeatEvery is int every && every is < 1 or > 99) return "Repeat interval must be between 1 and 99.";
                if (request.RepeatUnit is not null && !RepeatUnits.Contains(request.RepeatUnit)) return "Repeat unit must be day, week, month or year.";
                break;
        }
        return null;
    }

    // Non-repeating custom targets must be due today or later (yearly targets roll forward on their own).
    public static bool IsPastDueOneOff(SaveCategoryTargetRequest request, DateOnly today) =>
        request.Kind == Custom && request.RepeatEvery is null && request.DueDate is DateOnly due && due < today;

    public static bool IsPaymentCategory(string categoryId, string groupId) =>
        groupId == CreditCardRules.PaymentGroupId || categoryId.StartsWith("ccp-", StringComparison.Ordinal);

    // True when the "set aside" calculation needs Assigned values from earlier months.
    public static bool NeedsHistory(CategoryTargetDefinition target) => target.Behavior == SetAside && target.Kind is Yearly or Custom;

    public static CategoryTargetProgressDto Calculate(CategoryTargetDefinition target, string month, decimal assigned, decimal available,
        IReadOnlyDictionary<string, decimal>? priorAssigned = null)
    {
        var context = new Context(target, ParseMonth(month), Math.Max(assigned, 0), available - assigned, priorAssigned);
        return target.Kind switch
        {
            Weekly => WeeklyTarget(context),
            Monthly => MonthlyTarget(context),
            Yearly => RepeatingByMonths(context, Due(target), 12),
            _ => CustomTarget(context),
        };
    }

    private sealed record Context(CategoryTargetDefinition Target, DateOnly Month, decimal Funded, decimal Before,
        IReadOnlyDictionary<string, decimal>? PriorAssigned)
    {
        public int MonthIndex => Index(Month);
        public int StartIndex => TryParseMonth(Target.StartMonth, out var start) ? Index(start) : MonthIndex;
        public DateOnly MonthEnd => Month.AddMonths(1).AddDays(-1);
    }

    private static CategoryTargetProgressDto WeeklyTarget(Context c)
    {
        var occurrences = Enumerable.Range(1, DateTime.DaysInMonth(c.Month.Year, c.Month.Month))
            .Count(day => (int)new DateOnly(c.Month.Year, c.Month.Month, day).DayOfWeek == c.Target.Weekday);
        var weekday = CultureInfo.InvariantCulture.DateTimeFormat.GetDayName((DayOfWeek)c.Target.Weekday!.Value);
        return Result(c, c.Target.Amount * occurrences, occurrences, null, null, false, $"Every {weekday}");
    }

    private static CategoryTargetProgressDto MonthlyTarget(Context c)
    {
        var last = DateTime.DaysInMonth(c.Month.Year, c.Month.Month);
        var day = c.Target.DayOfMonth is int chosen && chosen > 0 ? chosen : 0;
        var due = new DateOnly(c.Month.Year, c.Month.Month, day == 0 ? last : Math.Min(day, last));
        var needed = c.Target.Behavior == Refill ? Math.Max(c.Target.Amount - c.Before, 0) : c.Target.Amount;
        var cadence = day == 0 ? "Every month by the last day" : $"Every month by the {Ordinal(day)}";
        return Result(c, needed, 1, due, 1, false, cadence);
    }

    private static CategoryTargetProgressDto CustomTarget(Context c)
    {
        var anchor = Due(c.Target);
        if (c.Target.RepeatEvery is not int every || c.Target.RepeatUnit is null)
            return Dated(c, anchor, c.StartIndex, oneOff: true, $"By {Display(anchor)}");
        if (c.Target.RepeatUnit is "month" or "year")
            return RepeatingByMonths(c, anchor, c.Target.RepeatUnit == "year" ? 12 * every : every);

        var periodDays = c.Target.RepeatUnit == "week" ? 7 * every : every;
        var cadence = $"Every {Plural(every, c.Target.RepeatUnit)}";
        if (c.MonthEnd < anchor) return Dated(c, anchor, c.StartIndex, oneOff: false, $"{cadence} from {Display(anchor)}");
        // Occurrences are anchor + k * periodDays; find the first one on or after the start of the viewed month.
        var first = c.Month <= anchor ? anchor : anchor.AddDays((c.Month.DayNumber - anchor.DayNumber + periodDays - 1) / periodDays * periodDays);
        if (first > c.MonthEnd)
        {
            var previous = first.AddDays(-periodDays);
            return Dated(c, first, Math.Max(Index(previous) + 1, c.StartIndex), oneOff: false, $"{cadence}, next {Display(first)}");
        }
        var count = (c.MonthEnd.DayNumber - first.DayNumber) / periodDays + 1;
        var needed = c.Target.Amount * count;
        if (c.Target.Behavior == Refill) needed = Math.Max(needed - c.Before, 0);
        return Result(c, needed, count, first, 1, false, $"{cadence}, next {Display(first)}");
    }

    // A dated target whose due date advances by periodMonths after it passes (yearly targets use 12).
    private static CategoryTargetProgressDto RepeatingByMonths(Context c, DateOnly anchor, int periodMonths)
    {
        var due = anchor;
        var cycleStart = c.StartIndex;
        if (c.MonthIndex > Index(anchor))
        {
            var cycles = (c.MonthIndex - Index(anchor) + periodMonths - 1) / periodMonths;
            due = anchor.AddMonths(cycles * periodMonths);
            cycleStart = Math.Max(Index(due) - periodMonths + 1, c.StartIndex);
        }
        var cadence = periodMonths == 12 ? $"Every year by {Display(due)}" : $"Every {Plural(periodMonths, "month")} by {Display(due)}";
        if (c.Target.Kind == Custom && c.Target.RepeatUnit == "year" && c.Target.RepeatEvery > 1) cadence = $"Every {Plural(c.Target.RepeatEvery.Value, "year")} by {Display(due)}";
        return Dated(c, due, cycleStart, oneOff: false, cadence);
    }

    private static CategoryTargetProgressDto Dated(Context c, DateOnly due, int cycleStart, bool oneOff, string cadence)
    {
        var fundedBefore = c.Target.Behavior == Refill ? c.Before : AssignedBetween(c.PriorAssigned, cycleStart, c.MonthIndex);
        var remaining = c.Target.Amount - fundedBefore;
        if (oneOff && c.MonthIndex > Index(due)) return Result(c, Math.Max(remaining, 0), 1, due, null, true, cadence);
        var months = Math.Max(Index(due) - c.MonthIndex + 1, 1);
        return Result(c, Math.Max(CeilingCents(remaining / months), 0), 1, due, months, false, cadence);
    }

    private static CategoryTargetProgressDto Result(Context c, decimal needed, int segments, DateOnly? nextDue, int? monthsRemaining, bool pastDue, string cadence)
    {
        needed = decimal.Round(needed, 2);
        var more = Math.Max(needed - c.Funded, 0);
        var fraction = needed <= 0 ? 1m : Math.Min(decimal.Round(c.Funded / needed, 4, MidpointRounding.ToZero), 1m);
        return new CategoryTargetProgressDto(needed, Math.Min(c.Funded, needed), more, Math.Max(segments, 1), fraction,
            more > 0 ? "underfunded" : "funded", nextDue, monthsRemaining, pastDue, cadence);
    }

    private static decimal AssignedBetween(IReadOnlyDictionary<string, decimal>? priorAssigned, int fromIndex, int toIndexExclusive) =>
        priorAssigned is null ? 0 : priorAssigned
            .Where(item => TryParseMonth(item.Key, out var m) && Index(m) >= fromIndex && Index(m) < toIndexExclusive)
            .Sum(item => item.Value);

    private static DateOnly Due(CategoryTargetDefinition target) =>
        target.DueDate ?? throw new InvalidOperationException("A dated target has no due date.");

    public static bool TryParseMonth(string? value, out DateOnly month)
    {
        month = default;
        return value is { Length: 7 } && DateOnly.TryParseExact($"{value}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out month);
    }

    private static DateOnly ParseMonth(string value) =>
        TryParseMonth(value, out var month) ? month : throw new ArgumentException("Month must use YYYY-MM format.", nameof(value));

    private static int Index(DateOnly date) => date.Year * 12 + date.Month - 1;
    private static decimal CeilingCents(decimal value) => Math.Ceiling(value * 100) / 100;
    private static string Display(DateOnly date) => date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
    private static string Plural(int count, string unit) => count == 1 ? unit : $"{count} {unit}s";

    private static string Ordinal(int day) => day + (day % 100 is 11 or 12 or 13 ? "th" : (day % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });
}

// Calculation input independent of the persistence entity.
public sealed record CategoryTargetDefinition(string Kind, decimal Amount, string Behavior, string StartMonth,
    int? Weekday = null, int? DayOfMonth = null, DateOnly? DueDate = null, int? RepeatEvery = null, string? RepeatUnit = null);
