using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed partial class LedgerService
{
    private async Task<List<Guid>> ConfirmEditableAsync(TransactionEntity entry, bool confirmed, CancellationToken ct)
    {
        if (entry.Status != "reconciled") return [];
        if (!confirmed) throw new LedgerException("Confirm the warning before editing a reconciled transaction.");
        var snapshots = await db.Reconciliations.Where(r => r.AccountId == entry.AccountId && !r.IsInvalidated).ToListAsync(ct);
        foreach (var snapshot in snapshots) snapshot.IsInvalidated = true;
        entry.Status = "cleared";
        return snapshots.Select(s => s.Id).ToList();
    }

    private static ScheduleTemplateDto Template(ScheduleEntity rule, ScheduleOccurrenceEntity? occurrence = null) =>
        JsonSerializer.Deserialize<ScheduleTemplateDto>(occurrence?.OverrideJson ?? rule.TemplateJson)!;
    private static DateOnly EffectiveDate(ScheduleEntity rule, ScheduleOccurrenceEntity? occurrence) => occurrence?.DeferredDate ?? rule.NextDate!.Value;

    public async Task<SchedulesDto> GetSchedulesAsync(Guid? accountId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        if (from > to) throw new LedgerException("Start date must be before end date.");
        var query = from rule in db.Schedules.Where(r => !r.IsEnded && r.NextDate != null)
                    join occurrence in db.ScheduleOccurrences on new { Id = rule.Id, rule.Sequence } equals new { Id = occurrence.ScheduleId, occurrence.Sequence } into occurrences
                    from occurrence in occurrences.DefaultIfEmpty()
                    select new { Rule = rule, Occurrence = occurrence, Date = occurrence.DeferredDate ?? rule.NextDate!.Value };
        if (accountId is not null) query = query.Where(x => x.Rule.AccountId == accountId || x.Rule.ToAccountId == accountId);
        // Forecast totals use every active next occurrence before date filters and pagination.
        var forecast = await query.Where(x => !x.Rule.IsPaused).ToListAsync(ct);
        if (from is not null) query = query.Where(x => x.Date >= from);
        if (to is not null) query = query.Where(x => x.Date <= to);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Date).ThenBy(x => x.Rule.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var accounts = (await GetAccountsAsync(ct)).Accounts.ToDictionary(a => a.Id);
        var result = rows.Select(x => {
            var rule = x.Rule; var data = Template(rule, x.Occurrence);
            var incoming = accountId is not null && rule.ToAccountId == accountId;
            var target = incoming ? rule.ToAccountId!.Value : rule.AccountId;
            decimal amount = incoming ? -data.Amount : data.Amount;
            var projected = accounts[target].Balance + forecast.Where(f => f.Date < x.Date || f.Date == x.Date && f.Rule.Id.CompareTo(rule.Id) >= 0)
                .Sum(f => { var money = Template(f.Rule, f.Occurrence).Amount; return f.Rule.AccountId == target ? money : f.Rule.ToAccountId == target ? -money : 0; });
            var other = incoming ? rule.AccountId : rule.ToAccountId;
            var entry = new TransactionDto(RecurrenceRules.OccurrenceId(rule.Id, rule.Sequence), target, x.Date, amount, data.Payee ?? "", data.Memo ?? "", data.CategoryId,
                "uncleared", rule.ToAccountId is null ? null : rule.Id, false, data.Splits ?? [], false, rule.IsPaused ? null : projected,
                other is null ? null : accounts[other.Value].Name, FlagColor:rule.FlagColor, ScheduleId:rule.Id);
            return new ScheduledItemDto(rule.Id, entry.Id, rule.Repeat, rule.AnchorDate, rule.IsPaused, rule.EndDate, rule.Count, rule.ToAccountId, entry, rule.PostingError);
        }).ToList();
        return new((await db.LedgerStates.SingleAsync(ct)).Version, page, pageSize, total, result);
    }

    private async Task ValidateScheduleTemplateAsync(Guid accountId, Guid? toAccountId, DateOnly date, ScheduleTemplateDto template, CancellationToken ct)
    {
        var error = LedgerRules.Validate(new(Guid.NewGuid(), 1, accountId, date, template.Amount, template.Payee, template.Memo, template.CategoryId, "uncleared", template.Splits));
        if (error is not null) throw new LedgerException(error);
        var account = await OpenAccountAsync(accountId, ct);
        if (toAccountId is null && LoanRules.IsLoan(account.AccountType) && (template.CategoryId is not null || template.Splits?.Count > 0))
            throw new LedgerException("Loan activity cannot use a category. Schedule loan payments as transfers.");
        if (toAccountId is null && CreditCardRules.IsCredit(account.AccountType) && template.CategoryId is null && template.Splits?.Count is not > 0)
            throw new LedgerException("Credit account transactions require a category.");
        if ((template.Splits ?? []).Select(s => s.CategoryId).Append(template.CategoryId).Any(CreditCardRules.IsPaymentCategory))
            throw new LedgerException("Credit card payment categories are funded automatically. Record a card payment as a transfer.");
        if (toAccountId is not null)
        {
            if (toAccountId == accountId || template.Amount >= 0 || template.CategoryId is not null || template.Splits?.Count > 0)
                throw new LedgerException("Scheduled transfers require different accounts, an outflow and no category.");
            await OpenAccountAsync(toAccountId.Value, ct);
        }
        var month = await budgets.GetMonthAsync(db.ActiveBudgetId, Month(date), ct);
        var ids = (template.Splits ?? []).Select(s => s.CategoryId).Concat(template.CategoryId is null ? [] : [template.CategoryId]);
        foreach (var id in ids)
            if (!month.Groups.Any(g => !g.IsArchived && g.Categories.Any(c => c.Id == id && !c.IsArchived)))
                throw new LedgerException("Choose an active category for this occurrence month before entering it.");
    }

    public Task<MutationDto> SaveScheduleAsync(Guid? id, ScheduleRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, id is null ? "schedule.create" : $"schedule.update/{id}", request, async () => {
            ValidateFlagColor(request.FlagColor);
            _ = RecurrenceRules.Occurrence(request.Date, request.Repeat, 0);
            if (request.EndDate < request.Date || request.EndDate?.Year is < 1900 or > 9998 || request.Count is < 1 or > 10000) throw new LedgerException("Invalid schedule end date or occurrence count.");
            var template = new ScheduleTemplateDto(request.Amount, request.Payee?.Trim(), request.Memo?.Trim(), request.CategoryId, request.Splits);
            await ValidateScheduleTemplateAsync(request.AccountId, request.ToAccountId, request.Date, template, ct);
            var rule = id is null ? new ScheduleEntity { Id = Guid.NewGuid() } : await FindScheduleAsync(id.Value, ct);
            JsonElement? before = id is null ? null : JsonSerializer.SerializeToElement(rule);
            if (id is not null) { await SupersedePendingAsync(rule, ct); rule.Sequence++; }
            rule.AccountId = request.AccountId; rule.ToAccountId = request.ToAccountId;
            rule.AnchorDate = request.Date; rule.NextDate = request.Date; rule.RecurrenceIndex = 0;
            rule.Repeat = request.Repeat; rule.TemplateJson = JsonSerializer.Serialize(template); rule.IsPaused = request.IsPaused;
            rule.PostingError = null;
            rule.EndDate = request.EndDate; rule.Count = request.Count; rule.FlagColor = request.FlagColor;
            if (id is null) db.Schedules.Add(rule);
            return new Change(rule.Id, before, rule);
        }, ct);

    private async Task CreateNextScheduleAsync(TransactionEntity entry, string repeat, CancellationToken ct, Guid? toAccountId = null)
    {
        var next = RecurrenceRules.Occurrence(entry.Date, repeat, 1);
        if (next is null) throw new LedgerException("This recurrence has no next date within the supported calendar.");
        var data = new ScheduleTemplateDto(entry.Amount, entry.Payee?.Name, entry.Memo, entry.CategoryId,
            entry.Splits.OrderBy(s => s.Position).Select(s => new SplitDto(s.CategoryId, s.Amount, string.IsNullOrEmpty(s.Payee) ? null : s.Payee, string.IsNullOrEmpty(s.Memo) ? null : s.Memo)).ToList());
        // Editing a posted occurrence leaves its parent rule unchanged unless Repeat is explicitly selected.
        var rule = entry.ScheduleId is null ? new ScheduleEntity { Id = Guid.NewGuid() } : await FindScheduleAsync(entry.ScheduleId.Value, ct);
        if (entry.ScheduleId is not null) { await SupersedePendingAsync(rule, ct); rule.Sequence++; }
        else db.Schedules.Add(rule);
        rule.AccountId = entry.AccountId; rule.ToAccountId = toAccountId; rule.AnchorDate = entry.Date; rule.RecurrenceIndex = 1;
        rule.NextDate = next; rule.Repeat = repeat; rule.TemplateJson = JsonSerializer.Serialize(data);
        rule.FlagColor = entry.FlagColor;
        entry.ScheduleId = rule.Id;
    }

    public Task<MutationDto> ScheduleActionAsync(Guid id, ScheduleActionRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, $"schedule.{request.Action}/{id}", request, async () => {
            var rule = await FindScheduleAsync(id, ct);
            var occurrenceId = RecurrenceRules.OccurrenceId(id, rule.Sequence);
            if (request.OccurrenceId != occurrenceId || rule.NextDate is null) throw new LedgerException("This occurrence changed. Reload scheduled transactions.", 409);
            var before = JsonSerializer.SerializeToElement(rule);
            rule.PostingError = null;
            if (request.Action is "pause" or "resume" or "end")
            {
                if (request.Action == "end") { await SupersedePendingAsync(rule, ct); rule.IsEnded = true; }
                else rule.IsPaused = request.Action == "pause";
                return new Change(id, before, rule);
            }
            if (rule.IsPaused) throw new LedgerException("Resume this schedule before changing its occurrence.");
            var occurrence = await db.ScheduleOccurrences.FindAsync([occurrenceId], ct);
            JsonElement? occurrenceBefore = occurrence is null ? null : JsonSerializer.SerializeToElement(occurrence);
            if (occurrence is null)
            {
                occurrence = new() { Id = occurrenceId, ScheduleId = id, Sequence = rule.Sequence, DueDate = rule.NextDate.Value };
                db.ScheduleOccurrences.Add(occurrence);
            }
            var effectiveDate = EffectiveDate(rule, occurrence);
            switch (request.Action)
            {
                case "snooze":
                    if (request.Date is null || request.Date <= effectiveDate || request.Date.Value.Year > 9998) throw new LedgerException("Snooze to a later date.");
                    occurrence.DeferredDate = request.Date; break;
                case "editOnce":
                    if (request.Template is null) throw new LedgerException("Occurrence details are required.");
                    var editDate = request.Date ?? effectiveDate;
                    await ValidateScheduleTemplateAsync(rule.AccountId, rule.ToAccountId, editDate, request.Template, ct);
                    occurrence.OverrideJson = JsonSerializer.Serialize(request.Template); occurrence.DeferredDate = editDate; break;
                case "skip": occurrence.Disposition = "skipped"; Advance(rule); break;
                case "post":
                    var data = Template(rule, occurrence);
                    effectiveDate = request.Date ?? effectiveDate;
                    await ValidateScheduleTemplateAsync(rule.AccountId, rule.ToAccountId, effectiveDate, data, ct);
                    var entry = new TransactionEntity { Id = occurrenceId, IsApproved = !request.AutomaticallyPosted, FlagColor = rule.FlagColor, ScheduleId = rule.Id, AccountId = rule.AccountId, Date = effectiveDate, Amount = data.Amount,
                        Memo = data.Memo?.Trim() ?? "", CategoryId = data.CategoryId, Payee = await GetPayeeAsync(data.Payee, ct), Status = "uncleared" };
                    entry.Splits = (data.Splits ?? []).Select((s,i) => new SplitEntity { TransactionId = entry.Id, Position = i, CategoryId = s.CategoryId, Amount = s.Amount, Payee = s.Payee?.Trim() ?? "", Memo = s.Memo?.Trim() ?? "" }).ToList();
                    db.Transactions.Add(entry);
                    if (rule.ToAccountId is not null)
                    {
                        entry.TransferId = occurrenceId;
                        db.Transactions.Add(new() { Id = Guid.NewGuid(), IsApproved = !request.AutomaticallyPosted, ScheduleId = rule.Id, AccountId = rule.ToAccountId.Value, Date = effectiveDate, Amount = -data.Amount, Memo = entry.Memo, TransferId = occurrenceId });
                    }
                    occurrence.TransactionId = entry.Id; occurrence.Disposition = "posted"; Advance(rule); break;
                default: throw new LedgerException("Unknown schedule action.");
            }
            return new Change(id, new { rule = before, occurrence = occurrenceBefore, occurrenceId }, new { rule, occurrence });
        }, ct);

    private static void Advance(ScheduleEntity rule)
    {
        rule.Sequence++; rule.RecurrenceIndex++;
        rule.NextDate = RecurrenceRules.Occurrence(rule.AnchorDate, rule.Repeat, rule.RecurrenceIndex);
        if (rule.NextDate is null || rule.EndDate is not null && rule.NextDate > rule.EndDate || rule.Count is not null && rule.RecurrenceIndex >= rule.Count)
            rule.IsEnded = true;
    }
    private async Task<ScheduleEntity> FindScheduleAsync(Guid id, CancellationToken ct) =>
        await db.Schedules.SingleOrDefaultAsync(r => r.Id == id && !r.IsEnded, ct) ?? throw new LedgerException("Schedule not found or already ended.", 404);
    private async Task SupersedePendingAsync(ScheduleEntity rule, CancellationToken ct)
    {
        var pending = await db.ScheduleOccurrences.FindAsync([RecurrenceRules.OccurrenceId(rule.Id, rule.Sequence)], ct);
        if (pending is not null) pending.Disposition = "superseded";
    }
}
