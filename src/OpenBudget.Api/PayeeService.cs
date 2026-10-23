using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed partial class LedgerService
{
    public async Task<PayeesDto> GetManagedPayeesAsync(CancellationToken ct)
    {
        var rows = await db.Payees.AsNoTracking().OrderBy(p => p.Name).ThenBy(p => p.Id).ToListAsync(ct);
        var history = await db.Transactions.AsNoTracking().Where(t => !t.IsDeleted && t.PayeeId != null && !t.IsOpening && t.TransferId == null)
            .OrderByDescending(t => t.Date).ThenBy(t => t.Id).Select(t => new { t.PayeeId, t.CategoryId }).ToListAsync(ct);
        return new((await db.LedgerStates.SingleAsync(ct)).Version, rows.Select(p => new PayeeDto(p.Id, p.Name,
            p.IsHidden, p.CategoryMode, p.DefaultCategoryId, p.CategoryMode switch {
                "always" => p.DefaultCategoryId, "never" => null,
                _ => PayeeRules.HistoryCategory(history.Where(t => t.PayeeId == p.Id).Select(t => t.CategoryId)) },
            history.Count(t => t.PayeeId == p.Id), JsonSerializer.Deserialize<List<PayeeRuleDto>>(p.RulesJson) ?? [])).ToList());
    }

    public Task<MutationDto> SavePayeesAsync(SavePayeesRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, "payees.update", request, async () =>
        {
            if (request.Payees is null || request.Payees.Count is < 1 or > 500 || request.Payees.Any(p => p is null) || request.Payees.Select(p => p.Id).Distinct().Count() != request.Payees.Count)
                throw new LedgerException("Choose 1–500 distinct saved payees.");
            var rows = await db.Payees.ToListAsync(ct);
            var originalNames = rows.ToDictionary(p => p.Id, p => p.NormalizedName);
            var before = await GetManagedPayeesAsync(ct);
            foreach (var p in request.Payees)
            {
                var row = rows.SingleOrDefault(r => r.Id == p.Id) ?? throw new LedgerException("Payee not found.", 404);
                if (PayeeRules.Validate(p) is { } error) throw new LedgerException(error);
                if (!p.Delete && originalNames.Any(other => other.Key != p.Id && other.Value == p.Name.Trim().ToUpperInvariant()))
                    throw new LedgerException("Another payee already has this name. Choose a unique name.");
                if (p.Delete)
                {
                    // Keep every historical label, including soft-deleted and scheduled entries.
                    var scheduled = await db.Schedules.ToListAsync(ct);
                    if (await db.Transactions.AnyAsync(t => t.PayeeId == p.Id, ct) || scheduled.Any(s => string.Equals(JsonSerializer.Deserialize<ScheduleTemplateDto>(s.TemplateJson)?.Payee, row.Name, StringComparison.OrdinalIgnoreCase)))
                        throw new LedgerException("This payee has transaction history or a schedule. Hide it to keep that history.");
                    db.Payees.Remove(row); continue;
                }
                if (p.CategoryMode == "always" && !await db.Categories.AnyAsync(c => c.BudgetId == db.ActiveBudgetId && c.Id == p.DefaultCategoryId && !c.IsArchived, ct))
                    throw new LedgerException("Choose an existing active default category.");
                var oldName = row.Name;
                row.Name = p.Name.Trim(); row.NormalizedName = row.Name.ToUpperInvariant(); row.IsHidden = p.IsHidden;
                row.CategoryMode = p.CategoryMode; row.DefaultCategoryId = p.CategoryMode == "always" ? p.DefaultCategoryId : null;
                row.RulesJson = JsonSerializer.Serialize(p.Rules.Select(r => r with { Value = r.Value.Trim() }));
                if (oldName != row.Name)
                {
                    foreach (var schedule in await db.Schedules.Where(s => !s.IsEnded).ToListAsync(ct))
                    {
                        var template = JsonSerializer.Deserialize<ScheduleTemplateDto>(schedule.TemplateJson)!;
                        if (string.Equals(template.Payee, oldName, StringComparison.OrdinalIgnoreCase)) schedule.TemplateJson = JsonSerializer.Serialize(template with { Payee = row.Name });
                    }
                    foreach (var occurrence in await db.ScheduleOccurrences.Where(o => o.Disposition == "pending" && o.OverrideJson != null).ToListAsync(ct))
                    {
                        var template = JsonSerializer.Deserialize<ScheduleTemplateDto>(occurrence.OverrideJson!)!;
                        if (string.Equals(template.Payee, oldName, StringComparison.OrdinalIgnoreCase)) occurrence.OverrideJson = JsonSerializer.Serialize(template with { Payee = row.Name });
                    }
                }
            }
            var retained = rows.Where(r => db.Entry(r).State != EntityState.Deleted).ToList();
            if (retained.GroupBy(r => r.NormalizedName).Any(g => g.Count() > 1)) throw new LedgerException("Another payee already has this name.");
            return new Change(Guid.Empty, before, request.Payees);
        }, ct);

    private async Task<PayeeEntity?> ResolveImportedPayeeAsync(string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var matches = (await db.Payees.ToListAsync(ct)).SelectMany(p =>
            (JsonSerializer.Deserialize<List<PayeeRuleDto>>(p.RulesJson) ?? []).Where(r => PayeeRules.Matches(r, name))
            .Select(r => new { Payee = p, Exact = r.Operator == "is", Length = r.Value.Trim().Length }))
            .OrderByDescending(m => m.Exact).ThenByDescending(m => m.Length).ToList();
        if (matches.Count == 0) return await GetPayeeAsync(name, ct);
        var best = matches[0];
        if (matches.Any(m => m.Exact == best.Exact && m.Length == best.Length && m.Payee.Id != best.Payee.Id))
            throw new LedgerException("Multiple payees match this imported name. Update the renaming rules and retry.");
        return best.Payee;
    }

    private async Task<string?> SuggestedCategoryAsync(PayeeEntity? payee, DateOnly date, CancellationToken ct)
    {
        if (payee is null || payee.CategoryMode == "never") return null;
        var category = payee.CategoryMode == "always" ? payee.DefaultCategoryId : PayeeRules.HistoryCategory(
            await db.Transactions.Where(t => t.PayeeId == payee.Id && !t.IsDeleted && !t.IsOpening && t.TransferId == null)
                .OrderByDescending(t => t.Date).ThenBy(t => t.Id).Take(3).Select(t => t.CategoryId).ToListAsync(ct));
        return category is not null && await db.Categories.AnyAsync(c => c.BudgetId == db.ActiveBudgetId && c.BudgetMonth == Month(date) && c.Id == category && !c.IsArchived && db.CategoryGroups.Any(g => g.BudgetId == db.ActiveBudgetId && g.BudgetMonth == c.BudgetMonth && g.Id == c.GroupId && !g.IsArchived), ct) ? category : null;
    }
}

