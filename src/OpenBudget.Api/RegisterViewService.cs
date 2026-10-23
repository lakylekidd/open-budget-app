using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed class RegisterViewService(BudgetDbContext db)
{
    public static void Validate(RegisterViewDto value)
    {
        if (value.DatePreset is not ("allDates" or "thisMonth" or "lastMonth" or "latest3Months" or "thisYear" or "lastYear" or "custom"))
            throw new LedgerException("Unknown date preset.");
        static bool Month(string? value) => value is { Length: 7 } && DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date.Year is >= 1900 and <= 9998;
        if ((value.FromMonth is not null && !Month(value.FromMonth)) || (value.ToMonth is not null && !Month(value.ToMonth)))
            throw new LedgerException("Months must use YYYY-MM between 1900 and 9998.");
        if (value.DatePreset == "custom" && (value.FromMonth is null || value.ToMonth is null || string.CompareOrdinal(value.FromMonth, value.ToMonth) > 0))
            throw new LedgerException("Choose a From month on or before the To month.");
        if (value.Version < 0) throw new LedgerException("Invalid view version.");
    }

    private async Task CheckScope(string scope, CancellationToken ct)
    {
        if (scope == "all") return;
        if (!Guid.TryParseExact(scope, "D", out var id)) throw new LedgerException("Invalid account identifier.");
        if (!await db.Accounts.AnyAsync(a => a.Id == id, ct)) throw new LedgerException("Account not found.", 404);
    }

    public async Task<RegisterViewDto> GetAsync(string scope, CancellationToken ct)
    {
        await CheckScope(scope, ct);
        var row = await db.RegisterViews.FindAsync([db.ActiveBudgetId, scope.ToLowerInvariant()], ct);
        return row is null ? new(0, "allDates", null, null, true, true, true, true) : ToDto(row);
    }

    public async Task<RegisterViewDto> SaveAsync(string scope, RegisterViewDto request, CancellationToken ct)
    {
        Validate(request);
        await CheckScope(scope, ct);
        scope = scope.ToLowerInvariant();
        var row = await db.RegisterViews.FindAsync([db.ActiveBudgetId, scope], ct);
        var current = row is null ? 0 : row.Version;
        // A replay after a lost response succeeds only if the saved selection is identical.
        if (row is not null && request.Version == current - 1 && request with { Version = current } == ToDto(row)) return ToDto(row);
        if (request.Version != current) throw new LedgerException("View options changed in another session. Reload saved options and review before applying again.", 409);
        if (row is null) { row = new() { Scope = scope }; db.RegisterViews.Add(row); }
        row.Version = current + 1;
        row.DatePreset = request.DatePreset; row.FromMonth = request.FromMonth; row.ToMonth = request.ToMonth;
        row.ShowReconciled = request.ShowReconciled; row.ShowRunningBalance = request.ShowRunningBalance;
        row.ShowFlags = request.ShowFlags; row.ShowImages = request.ShowImages;
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    private static RegisterViewDto ToDto(RegisterViewEntity row) => new(row.Version, row.DatePreset, row.FromMonth, row.ToMonth,
        row.ShowReconciled, row.ShowRunningBalance, row.ShowFlags, row.ShowImages);
}
