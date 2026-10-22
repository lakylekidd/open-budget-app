using System.Text.Json;

namespace OpenBudget.Api;

public static class ScheduledPlanningTotals
{
    public sealed record ForecastOccurrence(Guid Id, Guid AccountId, Guid ScheduleId, DateOnly Date, ScheduleTemplateDto Template);

    public static Dictionary<string, (decimal Income, decimal Expenses)> ForYear(
        int year, IReadOnlyCollection<ScheduleEntity> schedules, IReadOnlyCollection<ScheduleOccurrenceEntity> occurrences,
        out Dictionary<(string Month, string CategoryId), decimal> categoryExpenses)
        => ForYear(year, schedules, occurrences, out categoryExpenses, out _);

    public static Dictionary<string, (decimal Income, decimal Expenses)> ForYear(
        int year, IReadOnlyCollection<ScheduleEntity> schedules, IReadOnlyCollection<ScheduleOccurrenceEntity> occurrences,
        out Dictionary<(string Month, string CategoryId), decimal> categoryExpenses,
        out List<ForecastOccurrence> forecastOccurrences)
    {
        var result = new Dictionary<string, (decimal Income, decimal Expenses)>(StringComparer.Ordinal);
        var expensesByCategory = new Dictionary<(string Month, string CategoryId), decimal>();
        forecastOccurrences = [];
        var last = new DateOnly(year, 12, 31);
        var pending = occurrences.ToDictionary(x => (x.ScheduleId, x.Sequence));

        foreach (var schedule in schedules)
        {
            if (schedule.IsPaused || schedule.IsEnded || schedule.NextDate is null || schedule.ToAccountId is not null)
                continue;

            var defaultTemplate = JsonSerializer.Deserialize<ScheduleTemplateDto>(schedule.TemplateJson)!;
            for (var index = schedule.RecurrenceIndex; schedule.Count is null || index < schedule.Count; index++)
            {
                var due = RecurrenceRules.Occurrence(schedule.AnchorDate, schedule.Repeat, index);
                if (due is null || due > last || schedule.EndDate is not null && due > schedule.EndDate)
                    break;
                if (due < schedule.NextDate) continue;

                var isCurrent = index == schedule.RecurrenceIndex;
                var occurrence = isCurrent && pending.TryGetValue((schedule.Id, schedule.Sequence), out var current)
                    ? current : null;
                var date = occurrence?.DeferredDate ?? due.Value;
                if (date > last) continue;
                var template = occurrence?.OverrideJson is null ? defaultTemplate
                    : JsonSerializer.Deserialize<ScheduleTemplateDto>(occurrence.OverrideJson)!;
                forecastOccurrences.Add(new ForecastOccurrence(
                    RecurrenceRules.OccurrenceId(schedule.Id, schedule.Sequence + index - schedule.RecurrenceIndex),
                    schedule.AccountId, schedule.Id, date, template));
                var amount = template.Amount;
                var month = date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
                var value = result.GetValueOrDefault(month);
                result[month] = amount > 0 ? (value.Income + amount, value.Expenses)
                    : (value.Income, value.Expenses - amount);
                if (amount < 0)
                {
                    if (template.Splits is { Count: > 0 })
                    {
                        foreach (var split in template.Splits.Where(x => x.Amount < 0))
                            AddCategoryExpense(month, split.CategoryId, -split.Amount);
                    }
                    else if (template.CategoryId is not null)
                        AddCategoryExpense(month, template.CategoryId, -amount);
                }
            }
        }

        categoryExpenses = expensesByCategory;
        return result;

        void AddCategoryExpense(string month, string categoryId, decimal amount)
        {
            var key = (month, categoryId);
            expensesByCategory[key] = expensesByCategory.GetValueOrDefault(key) + amount;
        }
    }
}
