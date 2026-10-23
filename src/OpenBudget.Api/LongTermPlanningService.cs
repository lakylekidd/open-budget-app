using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed class LongTermPlanningService(BudgetDbContext db, TimeProvider timeProvider)
{
    private const decimal MaxMoney = 999_999_999_999.99m;
    private const string ExpectedIncomeField = "expectedIncome";
    private const string PlannedExpensesField = "plannedExpenses";
    private const string PlannedBudgetOverrideField = "plannedBudgetOverride";

    public async Task<LongTermPlanningActivityDto> GetActivityAsync(int year, string month, CancellationToken cancellationToken)
    {
        var schedules = await db.Schedules.AsNoTracking()
            .Where(x => !x.IsPaused && !x.IsEnded && x.NextDate != null && x.ToAccountId == null)
            .ToListAsync(cancellationToken);
        var pending = await db.ScheduleOccurrences.AsNoTracking()
            .Where(x => x.Disposition == "pending")
            .ToListAsync(cancellationToken);
        ScheduledPlanningTotals.ForYear(year, schedules, pending, out _, out var occurrences);
        var entries = occurrences.Where(x => x.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture) == month)
            .OrderByDescending(x => x.Date).ThenBy(x => x.Id)
            .Select(x => new TransactionDto(x.Id, x.AccountId, x.Date, x.Template.Amount,
                x.Template.Payee ?? "", x.Template.Memo ?? "", x.Template.CategoryId,
                "uncleared", null, false, x.Template.Splits ?? [], false, ScheduleId: x.ScheduleId))
            .ToList();
        return new LongTermPlanningActivityDto(entries);
    }

    public async Task<LongTermPlanningDto> GetYearAsync(int year, CancellationToken cancellationToken)
    {
        if (!IsSupportedYear(year)) throw new ArgumentOutOfRangeException(nameof(year));

        var stateVersion = await db.LongTermPlanningStates.AsNoTracking()
            .Where(x => x.Id == "default")
            .Select(x => (long?)x.Version)
            .SingleOrDefaultAsync(cancellationToken) ?? 1;
        var startMonth = FormatMonth(year, 1);
        var endMonth = FormatMonth(year, 12);
        var schedules = await db.Schedules.AsNoTracking()
            .Where(x => !x.IsPaused && !x.IsEnded && x.NextDate != null && x.ToAccountId == null)
            .ToListAsync(cancellationToken);
        var pendingOccurrences = await db.ScheduleOccurrences.AsNoTracking()
            .Where(x => x.Disposition == "pending")
            .ToListAsync(cancellationToken);
        var scheduledTotals = ScheduledPlanningTotals.ForYear(year, schedules, pendingOccurrences, out var categoryExpenses);
        var planningEntries = await db.LongTermPlanningCategories.AsNoTracking()
            .OrderBy(x => x.Month)
            .ThenBy(x => x.CategoryId)
            .ToListAsync(cancellationToken);
        var latestBudget = await db.Budgets.AsNoTracking()
            .Where(x => x.BudgetId == "default")
            .Include(x => x.Groups).ThenInclude(x => x.Categories)
            .OrderByDescending(x => x.Month)
            .FirstOrDefaultAsync(cancellationToken);
        var currentYearBudgets = await db.Budgets.AsNoTracking()
            .Where(x => x.BudgetId == "default" && string.Compare(x.Month, startMonth) >= 0 && string.Compare(x.Month, endMonth) <= 0)
            .Include(x => x.Groups).ThenInclude(x => x.Categories)
            .ToListAsync(cancellationToken);
        var priorBudget = await db.Budgets.AsNoTracking()
            .Where(x => x.BudgetId == "default" && string.Compare(x.Month, startMonth) < 0)
            .Include(x => x.Groups).ThenInclude(x => x.Categories)
            .OrderByDescending(x => x.Month)
            .FirstOrDefaultAsync(cancellationToken);

        var actualBudgets = currentYearBudgets.ToDictionary(x => x.Month, StringComparer.Ordinal);
        var actualMonthExists = actualBudgets.Keys.ToHashSet(StringComparer.Ordinal);
        var resultMonths = Enumerable.Range(1, 12).Select(monthNumber =>
        {
            var month = FormatMonth(year, monthNumber);
            var expectedIncome = scheduledTotals.GetValueOrDefault(month).Income;
            var expectedExpenses = scheduledTotals.GetValueOrDefault(month).Expenses;
            return new LongTermPlanningMonthDto(
                month,
                expectedIncome,
                expectedIncome != 0m,
                Money(expectedExpenses),
                Money(expectedIncome - expectedExpenses),
                0m,
                actualMonthExists.Contains(month));
        }).ToList();

        var groups = new List<LongTermPlanningGroupDto>();
        if (latestBudget is not null)
        {
            foreach (var group in latestBudget.Groups.OrderBy(x => x.SortOrder))
            {
                if (group.IsArchived) continue;
                var categories = new List<LongTermPlanningCategoryDto>();
                foreach (var category in group.Categories.OrderBy(x => x.SortOrder))
                {
                    if (category.IsArchived) continue;
                    var categoryEntries = planningEntries.Where(x => x.CategoryId == category.Id)
                        .Select(x => new LongTermPlanningCategoryEntity
                        {
                            Month = x.Month, CategoryId = x.CategoryId,
                            PlannedBudgetOverride = x.PlannedBudgetOverride
                        }).ToList();
                    foreach (var ((month, categoryId), expense) in categoryExpenses.Where(x => x.Key.CategoryId == category.Id))
                    {
                        var entry = categoryEntries.FirstOrDefault(x => x.Month == month);
                        if (entry is null)
                        {
                            entry = new LongTermPlanningCategoryEntity { Month = month, CategoryId = categoryId };
                            categoryEntries.Add(entry);
                        }
                        entry.PlannedExpenses = expense;
                    }
                    var forecastMonths = LongTermPlanningCalculations.Calculate(year, category.Id, categoryEntries);
                    var previousAvailable = priorBudget?.Groups.SelectMany(x => x.Categories)
                        .FirstOrDefault(x => x.Id == category.Id)?.Available;
                    var values = new List<LongTermPlanningCategoryMonthDto>(12);

                    foreach (var (forecast, monthNumber) in forecastMonths.Select((value, index) => (value, index + 1)))
                    {
                        var month = FormatMonth(year, monthNumber);
                        decimal? actualAssigned = null;
                        decimal? actualExpenses = null;
                        decimal? actualAvailable = previousAvailable;
                        if (actualBudgets.TryGetValue(month, out var actualBudget))
                        {
                            var actualCategory = actualBudget.Groups.SelectMany(x => x.Categories)
                                .FirstOrDefault(x => x.Id == category.Id);
                            if (actualCategory is null)
                            {
                                actualAvailable = null;
                                previousAvailable = null;
                            }
                            else
                            {
                                actualAssigned = actualCategory.Assigned;
                                actualExpenses = Money(-actualCategory.Activity);
                                actualAvailable = actualCategory.Available;
                                previousAvailable = actualAvailable;
                            }
                        }

                        values.Add(new LongTermPlanningCategoryMonthDto(
                            month,
                            forecast.PlannedBudgetOverride,
                            forecast.PlannedBudgetBeforeMonth,
                            forecast.PlannedBudget,
                            forecast.PlannedExpenses,
                            forecast.PlannedAvailable,
                            actualAssigned,
                            actualExpenses,
                            actualAvailable));
                    }

                    categories.Add(new LongTermPlanningCategoryDto(
                        category.Id, category.Name, category.SortOrder, category.IsArchived, values));
                }

                groups.Add(new LongTermPlanningGroupDto(group.Id, group.Name, group.SortOrder, group.IsArchived, categories));
            }
        }

        for (var monthIndex = 0; monthIndex < resultMonths.Count; monthIndex++)
        {
            var assigned = groups.SelectMany(x => x.Categories)
                .Sum(x => x.Months[monthIndex].PlannedBudget);
            resultMonths[monthIndex] = resultMonths[monthIndex] with
            {
                RemainingToBeAssigned = Money(resultMonths[monthIndex].ExpectedIncome - assigned)
            };
        }

        var hasPlanningData = scheduledTotals.Keys.Any(x => string.CompareOrdinal(x, startMonth) >= 0 && string.CompareOrdinal(x, endMonth) <= 0)
            || planningEntries.Any(x => x.PlannedBudgetOverride is not null && string.Compare(x.Month, endMonth) <= 0);
        return new LongTermPlanningDto(year, stateVersion, hasPlanningData, resultMonths, groups);
    }

    public async Task<LongTermPlanningUpdateResult> UpdateAsync(
        int year,
        LongTermPlanningUpdateDto request,
        CancellationToken cancellationToken)
    {
        if (!IsSupportedYear(year)) return LongTermPlanningUpdateResult.Invalid("Year must be between 0001 and 9999.");
        if (request.Version < 1) return LongTermPlanningUpdateResult.Invalid("Planning version is invalid.");
        if (!TryGetMonth(request.Month, year, out _))
            return LongTermPlanningUpdateResult.Invalid("Month must use YYYY-MM format and belong to the selected year.");

        var currentMonth = timeProvider.GetLocalNow().ToString("yyyy-MM", CultureInfo.InvariantCulture);
        if (string.CompareOrdinal(request.Month, currentMonth) < 0)
            return LongTermPlanningUpdateResult.Invalid("Historical months are locked and cannot be edited.");

        var state = await db.LongTermPlanningStates.SingleAsync(x => x.Id == "default", cancellationToken);
        if (state.Version != request.Version)
            return LongTermPlanningUpdateResult.Conflict(await GetYearAsync(year, cancellationToken));

        LongTermPlanningCategoryEntity? categoryValue = null;
        var categoryId = request.CategoryId?.Trim();
        var amount = request.Amount;
        switch (request.Field)
        {
            case ExpectedIncomeField:
                return LongTermPlanningUpdateResult.Invalid("Expected income is calculated from scheduled account income.");

            case PlannedExpensesField:
                return LongTermPlanningUpdateResult.Invalid("Planned expenses are calculated from scheduled account outflows.");

            case PlannedBudgetOverrideField:
                if (string.IsNullOrWhiteSpace(categoryId) || categoryId.Length > 100 ||
                    (amount is not null && !IsValidAmount(amount, allowNull: false)))
                    return LongTermPlanningUpdateResult.Invalid("A planned budget override needs a category and a non-negative amount with at most two decimal places.");
                if (!await IsActiveCategoryAsync(categoryId, cancellationToken))
                    return LongTermPlanningUpdateResult.Invalid("The selected category is not active in the current budget.");
                categoryValue = await db.LongTermPlanningCategories.SingleOrDefaultAsync(
                    x => x.Month == request.Month && x.CategoryId == categoryId, cancellationToken);
                break;

            default:
                return LongTermPlanningUpdateResult.Invalid("Planning field is not supported.");
        }

        if (categoryValue is null && amount is null)
            return LongTermPlanningUpdateResult.Success(await GetYearAsync(year, cancellationToken));

        if (categoryValue is null)
        {
            categoryValue = new LongTermPlanningCategoryEntity { Month = request.Month, CategoryId = categoryId! };
            db.LongTermPlanningCategories.Add(categoryValue);
        }

        var changed = categoryValue.PlannedBudgetOverride != amount || db.Entry(categoryValue).State == EntityState.Added;
        categoryValue.PlannedBudgetOverride = amount;
        if (categoryValue.PlannedExpenses == 0m && categoryValue.PlannedBudgetOverride is null)
            db.LongTermPlanningCategories.Remove(categoryValue);

        if (!changed)
            return LongTermPlanningUpdateResult.Success(await GetYearAsync(year, cancellationToken));

        state.Version++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return LongTermPlanningUpdateResult.Success(await GetYearAsync(year, cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return LongTermPlanningUpdateResult.Conflict(await GetYearAsync(year, cancellationToken));
        }
    }

    private static bool IsValidAmount(decimal? amount, bool allowNull) =>
        amount is null ? allowNull : amount >= 0m && amount <= MaxMoney && decimal.Round(amount.Value, 2) == amount.Value;

    private async Task<bool> IsActiveCategoryAsync(string categoryId, CancellationToken cancellationToken)
    {
        var category = await db.Categories.AsNoTracking()
            .Where(x => x.Id == categoryId)
            .OrderByDescending(x => x.BudgetMonth)
            .Select(x => new { x.BudgetMonth, x.GroupId, x.IsArchived })
            .FirstOrDefaultAsync(cancellationToken);
        return category is not null && !category.IsArchived &&
            await db.CategoryGroups.AsNoTracking().AnyAsync(x =>
                x.BudgetMonth == category.BudgetMonth && x.Id == category.GroupId && !x.IsArchived, cancellationToken);
    }

    private static bool IsSupportedYear(int year) => year is >= 1 and <= 9999;

    private static string FormatMonth(int year, int month) => $"{year:D4}-{month:D2}";

    private static bool TryGetMonth(string month, int year, out DateOnly date)
    {
        date = default;
        return DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            && date.Year == year;
    }

    private static decimal Money(decimal amount) => decimal.Round(amount, 2, MidpointRounding.ToEven);
}

public sealed record CalculatedLongTermPlanningMonth(
    string Month,
    decimal? PlannedBudgetOverride,
    decimal PlannedBudgetBeforeMonth,
    decimal PlannedBudget,
    decimal PlannedExpenses,
    decimal PlannedAvailable);

public static class LongTermPlanningCalculations
{
    public static List<CalculatedLongTermPlanningMonth> Calculate(
        int year,
        string categoryId,
        IReadOnlyCollection<LongTermPlanningCategoryEntity> entries)
    {
        var targetStart = year * 12;
        var byMonth = entries.Where(x => x.CategoryId == categoryId)
            .ToDictionary(x => x.Month, StringComparer.Ordinal);
        var earlier = entries.Where(x => x.CategoryId == categoryId && MonthIndex(x.Month) < targetStart)
            .OrderBy(x => x.Month, StringComparer.Ordinal);
        var budget = 0m;
        var available = 0m;
        var cursor = 0;

        foreach (var entry in earlier)
        {
            var index = MonthIndex(entry.Month);
            available = Money(available + budget * (index - cursor));
            if (entry.PlannedBudgetOverride.HasValue) budget = entry.PlannedBudgetOverride.Value;
            available = Money(available + budget - entry.PlannedExpenses);
            cursor = index + 1;
        }

        available = Money(available + budget * (targetStart - cursor));
        var result = new List<CalculatedLongTermPlanningMonth>(12);
        for (var month = 1; month <= 12; month++)
        {
            var monthText = $"{year:D4}-{month:D2}";
            var beforeMonth = budget;
            var expenses = 0m;
            decimal? budgetOverride = null;
            if (byMonth.TryGetValue(monthText, out var entry))
            {
                budgetOverride = entry.PlannedBudgetOverride;
                expenses = entry.PlannedExpenses;
                if (entry.PlannedBudgetOverride.HasValue) budget = entry.PlannedBudgetOverride.Value;
            }

            available = Money(available + budget - expenses);
            result.Add(new CalculatedLongTermPlanningMonth(
                monthText, budgetOverride, beforeMonth, budget, expenses, available));
        }

        return result;
    }

    private static int MonthIndex(string month)
    {
        var year = int.Parse(month.AsSpan(0, 4), CultureInfo.InvariantCulture);
        var monthNumber = int.Parse(month.AsSpan(5, 2), CultureInfo.InvariantCulture);
        return year * 12 + monthNumber - 1;
    }

    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.ToEven);
}
