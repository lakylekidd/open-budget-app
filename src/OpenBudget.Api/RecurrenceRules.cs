using System.Security.Cryptography;
using System.Text;

namespace OpenBudget.Api;

public static class RecurrenceRules
{
    public static readonly string[] Repeats = ["never", "daily", "weekly", "everyOtherWeek", "twiceAMonth", "every4Weeks", "monthly", "everyOtherMonth", "every3Months", "every4Months", "twiceAYear", "yearly", "everyOtherYear"];
    public static DateOnly? Occurrence(DateOnly anchor, string repeat, int index)
    {
        if (!Repeats.Contains(repeat) || index < 0 || anchor.Year is < 1900 or > 9998) throw new LedgerException("Invalid recurrence or date.");
        if (index == 0) return anchor;
        if (repeat == "never") return null;
        try
        {
            DateOnly date;
            if (repeat == "twiceAMonth")
            {
                var start = new DateOnly(anchor.Year, anchor.Month, 1);
                var period = index / 2; var second = index % 2 == 1;
                var target = start.AddMonths(period + (second && anchor.Day > 15 ? 1 : 0));
                var day = second ? (anchor.Day <= 15 ? anchor.Day + 15 : anchor.Day - 15) : anchor.Day;
                date = new(target.Year, target.Month, Math.Min(day, DateTime.DaysInMonth(target.Year, target.Month)));
            }
            else if (repeat is "daily" or "weekly" or "everyOtherWeek" or "every4Weeks")
                date = anchor.AddDays(checked(index * (repeat switch { "daily" => 1, "weekly" => 7, "everyOtherWeek" => 14, _ => 28 })));
            else date = anchor.AddMonths(checked(index * (repeat switch { "monthly" => 1, "everyOtherMonth" => 2, "every3Months" => 3, "every4Months" => 4, "twiceAYear" => 6, "yearly" => 12, _ => 24 })));
            return date.Year <= 9998 ? date : null;
        }
        catch (ArgumentOutOfRangeException) { return null; }
        catch (OverflowException) { return null; }
    }
    public static Guid OccurrenceId(Guid ruleId, int sequence) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"{ruleId:D}/{sequence}"))[..16]);
}
