using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed partial class LedgerService(BudgetDbContext db, BudgetService budgets, LedgerGate gate, TimeProvider clock)
{
    private IQueryable<TransactionEntity> Entries => db.Transactions.Include(t => t.Payee).Include(t => t.Splits);

    public async Task<AccountsDto> GetAccountsAsync(CancellationToken ct)
    {
        var version = await db.LedgerStates.Select(x => x.Version).SingleAsync(ct);
        var accounts = await db.Accounts.OrderBy(x => x.IsClosed).ThenBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
        var balances = await db.Transactions.Where(t => !t.IsDeleted).GroupBy(t => t.AccountId)
            .Select(g => new { Id = g.Key, Balance = g.Sum(t => t.Amount), Cleared = g.Sum(t => t.Status != "uncleared" ? t.Amount : 0),
                Unapproved = g.Count(t => !t.IsApproved), Attention = g.Count(t => !t.IsApproved || (t.TransferId == null && !t.IsOpening && t.Amount < 0 && t.CategoryId == null && !t.Splits.Any())),
                NeedsCategory = g.Count(t => t.TransferId == null && !t.IsOpening && t.Amount < 0 && t.CategoryId == null && !t.Splits.Any()) }).ToDictionaryAsync(x => x.Id, ct);
        var reconciledAt = await db.Reconciliations.Where(r => !r.IsInvalidated).GroupBy(r => r.AccountId)
            .Select(g => new { Id = g.Key, At = g.Max(r => r.CreatedAt) }).ToDictionaryAsync(r => r.Id, r => r.At, ct);
        return new(version, accounts.Select(a =>
        {
            balances.TryGetValue(a.Id, out var b);
            return new AccountDto(a.Id, a.Name, a.IsClosed, b?.Balance ?? 0, b?.Cleared ?? 0, (b?.Balance ?? 0) - (b?.Cleared ?? 0),
                a.AccountType, a.IsFavorite, "unlinked", reconciledAt.TryGetValue(a.Id, out var at) ? at : null, b?.NeedsCategory ?? 0, a.Notes, b?.Unapproved ?? 0, b?.Attention ?? 0);
        }).ToList());
    }

    public async Task<RegisterDto> GetRegisterAsync(Guid? accountId, Guid? transferId, string? search, DateOnly? from, DateOnly? to,
        string? categoryId, string? status, int page, int pageSize, CancellationToken ct, bool includeReconciled = true, bool needsAttention = false, Guid? payeeId = null)
    {
        ValidatePage(page, pageSize);
        if (from > to) throw new LedgerException("Start date must be before end date.");
        if (status is not (null or "" or "uncleared" or "cleared" or "reconciled")) throw new LedgerException("Unknown cleared status filter.");
        if (search?.Length > 160) throw new LedgerException("Search must be 160 characters or fewer.");
        var query = Entries.Where(t => !t.IsDeleted);
        if (payeeId is not null) query = query.Where(t => t.PayeeId == payeeId);
        if (needsAttention) query = query.Where(t => !t.IsApproved || (t.TransferId == null && !t.IsOpening && t.Amount < 0 && t.CategoryId == null && !t.Splits.Any()));
        if (!includeReconciled) query = query.Where(t => t.Status != "reconciled");
        if (accountId is not null) query = query.Where(t => t.AccountId == accountId);
        if (transferId is not null) query = query.Where(t => t.TransferId == transferId);
        if (from is not null) query = query.Where(t => t.Date >= from);
        if (to is not null) query = query.Where(t => t.Date <= to);
        if (!string.IsNullOrWhiteSpace(categoryId)) query = categoryId == "uncategorized"
            ? query.Where(t => t.TransferId == null && !t.IsOpening && t.Amount < 0 && t.CategoryId == null && !t.Splits.Any())
            : query.Where(t => t.CategoryId == categoryId || t.Splits.Any(s => s.CategoryId == categoryId));
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(t => t.Memo.ToLower().Contains(term) || (t.Payee != null && t.Payee.Name.ToLower().Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(t => t.Date).ThenBy(t => t.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var accounts = await GetAccountsAsync(ct);
        var ids = items.Select(t => t.Id).ToArray();
        // Compute the full account history BEFORE filtering/page selection. Reverse display
        // ordering within a date so the first displayed row has the latest running balance.
        var running = await db.Database.SqlQueryRaw<LedgerBalanceRow>("""
            SELECT "Id", SUM("Amount") OVER (
                PARTITION BY "AccountId" ORDER BY "Date" ASC, "Id" DESC
                ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS "Balance"
            FROM "Transactions" WHERE NOT "IsDeleted"
            """).Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Balance, ct);
        var transferIds = items.Where(t => t.TransferId != null).Select(t => t.TransferId!.Value).Distinct().ToArray();
        var legs = await db.Transactions.Where(t => !t.IsDeleted && t.TransferId != null && transferIds.Contains(t.TransferId.Value))
            .Select(t => new { t.Id, t.TransferId, t.Status, t.Account.Name, t.Account.IsClosed }).ToListAsync(ct);
        var months = items.Select(t => Month(t.Date)).Distinct().ToArray();
        var labels = await db.Categories.Where(c => months.Contains(c.BudgetMonth))
            .Join(db.CategoryGroups, c => new { c.BudgetMonth, Id = c.GroupId }, g => new { g.BudgetMonth, g.Id },
                (c, g) => new { c.BudgetMonth, c.Id, Name = g.Name + ": " + c.Name })
            .ToDictionaryAsync(c => (c.BudgetMonth, c.Id), c => c.Name, ct);
        return new(accounts.Version, page, pageSize, total, items.Select(entry =>
        {
            var other = entry.TransferId is null ? null : legs.SingleOrDefault(t => t.TransferId == entry.TransferId && t.Id != entry.Id);
            return ToDto(entry) with { RunningBalance = running.GetValueOrDefault(entry.Id), TransferAccountName = other?.Name,
                CategoryName = entry.CategoryId is null ? null : labels.GetValueOrDefault((Month(entry.Date), entry.CategoryId)),
                CanEdit = !accounts.Accounts.Single(a => a.Id == entry.AccountId).IsClosed && other?.IsClosed != true,
                IsLocked = entry.Status == "reconciled" || accounts.Accounts.Single(a => a.Id == entry.AccountId).IsClosed || other?.Status == "reconciled" || other?.IsClosed == true };
        }).ToList(), accounts);
    }

    public async Task<AuditPageDto> GetAuditAsync(Guid? resourceId, int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        var query = db.LedgerCommands.AsNoTracking();
        if (resourceId is not null) query = query.Where(x => x.ResourceId == resourceId);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Version).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AuditDto(x.Id, x.ResourceId, x.Kind, x.Version, x.CreatedAt, x.BeforeJson, x.AfterJson)).ToListAsync(ct);
        return new(page, pageSize, total, rows);
    }

    public Task<MutationDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, "account.create", request, async () =>
        {
            ValidateName(request.Name);
            ValidateAccountType(request.AccountType);
            if (request.Date.Year is < 1900 or > 9998 || !LedgerRules.IsMoney(request.OpeningBalance)) throw new LedgerException("Opening date or balance is invalid.");
            await budgets.GetMonthAsync(Month(request.Date), ct);
            var account = new AccountEntity { Id = Guid.NewGuid(), Name = request.Name.Trim(), AccountType = request.AccountType };
            db.Accounts.Add(account);
            TransactionEntity? opening = null;
            if (request.OpeningBalance != 0)
            {
                opening = new TransactionEntity
                {
                    Id = Guid.NewGuid(), Account = account, AccountId = account.Id, Date = request.Date,
                    Amount = request.OpeningBalance, Status = "cleared", IsOpening = true, Memo = "Opening balance"
                };
                db.Transactions.Add(opening);
            }
            return new Change(account.Id, null, new { account, opening = opening is null ? null : ToDto(opening) });
        }, ct);

    public Task<MutationDto> UpdateAccountAsync(Guid id, UpdateAccountRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, $"account.update/{id}", request, async () =>
        {
            ValidateName(request.Name);
            if (request.AccountType is not null) ValidateAccountType(request.AccountType);
            var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new LedgerException("Account not found.", 404);
            if(request.Notes?.Length > 1000 || request.WorkingBalance is not null && !LedgerRules.IsMoney(request.WorkingBalance.Value)) throw new LedgerException("Notes must be at most 1000 characters and the working balance must be valid money.");
            var balance=await db.Transactions.Where(t=>t.AccountId==id&&!t.IsDeleted).SumAsync(t=>t.Amount,ct);
            var before = new { account.Id, account.Name, account.IsClosed, account.AccountType, account.IsFavorite, account.Notes, Balance=balance };
            var difference=request.WorkingBalance is null?0:request.WorkingBalance.Value-balance;
            if(request.IsClosed&&difference!=0)throw new LedgerException("Reopen the account before adjusting its working balance.");
            if(!LedgerRules.IsMoney(difference))throw new LedgerException("The adjustment exceeds the supported transaction amount.");
            TransactionEntity? adjustment=null;
            if (request.IsClosed && !account.IsClosed)
            {
                var entries = await Entries.Where(t => t.AccountId == id && !t.IsDeleted).ToListAsync(ct);
                if (LedgerRules.Balance(entries) != 0 || entries.Any(t => t.Status == "uncleared" || LedgerRules.NeedsAttention(t)))
                    throw new LedgerException("To close this account, bring its balance to zero, clear outstanding entries, and categorize spending. Transfer remaining funds to another cash account.");
            }
            account.Name = request.Name.Trim();
            account.IsClosed = request.IsClosed;
            account.AccountType = request.AccountType ?? account.AccountType;
            account.IsFavorite = request.IsFavorite ?? account.IsFavorite;
            account.Notes=request.Notes??account.Notes;
            if(difference!=0) {
                var date=DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
                await budgets.GetMonthAsync(Month(date),ct);
                adjustment=new TransactionEntity{Id=Guid.NewGuid(),AccountId=id,Date=date,Amount=difference,Payee=await GetPayeeAsync("Balance Adjustment",ct),Memo="Account working balance adjustment",Status="cleared"};
                db.Transactions.Add(adjustment);
            }
            return new Change(id, before, adjustment is null?account:new{account,adjustment=ToDto(adjustment)});
        }, ct);

    public Task<MutationDto> SaveTransactionAsync(Guid? id, TransactionRequest request, CancellationToken ct, bool imported = false) =>
        ExecuteAsync(request.CommandId, request.Version, imported ? "transaction.import" : id is null ? "transaction.create" : $"transaction.update/{id}", request, async () =>
        {
            if (imported && (id is not null || request.Repeat != "never")) throw new LedgerException("Imported rows cannot edit entries or create schedules.");
            var openingZero = id is not null && request.Amount == 0 && await db.Transactions.AnyAsync(t => t.Id == id && t.IsOpening && !t.IsDeleted, ct);
            var error = LedgerRules.Validate(openingZero ? request with { Amount = 0.01m } : request);
            if (error is not null) throw new LedgerException(error);
            await OpenAccountAsync(request.AccountId, ct);
            var month = await budgets.GetMonthAsync(Month(request.Date), ct);
            var resolvedPayee = imported ? await ResolveImportedPayeeAsync(request.Payee, ct) : await GetPayeeAsync(request.Payee, ct);
            if (imported && request.CategoryId is null && request.Splits?.Count is not > 0)
                request = request with { CategoryId = await SuggestedCategoryAsync(resolvedPayee, request.Date, ct) };
            var categoryIds = (request.Splits ?? []).Select(s => s.CategoryId).Concat(request.CategoryId is null ? [] : [request.CategoryId]);
            foreach (var categoryId in categoryIds)
                if (!month.Groups.Any(g => !g.IsArchived && g.Categories.Any(c => c.Id == categoryId && !c.IsArchived)))
                    throw new LedgerException("Choose an active category belonging to the transaction month.");

            var entry = id is null ? new TransactionEntity { Id = Guid.NewGuid() } : await FindEntryAsync(id.Value, ct);
            if (id is not null)
            {
                await OpenAccountAsync(entry.AccountId, ct);
                if (entry.Status == "reconciled" && !request.ConfirmReconciledEdit) throw new LedgerException("Confirm the warning before editing a reconciled transaction.");
                if (entry.TransferId is not null) throw new LedgerException("Edit a transfer through the transfer action so both entries change together.");
            }
            var before = id is null ? null : ToDto(entry);
            var invalidated = id is null ? [] : await ConfirmEditableAsync(entry, request.ConfirmReconciledEdit, ct);
            if (entry.IsOpening && (request.AccountId != entry.AccountId || request.CategoryId is not null || request.Splits?.Count > 0))
                throw new LedgerException("An opening entry must remain in its original account and cannot have categories.");
            entry.AccountId = request.AccountId;
            entry.Date = request.Date;
            entry.Amount = request.Amount;
            entry.CategoryId = request.CategoryId;
            entry.Memo = request.Memo?.Trim() ?? "";
            entry.Status = before?.Status == "reconciled" ? "cleared" : request.Status;
            entry.Payee = resolvedPayee;
            if (imported) entry.IsApproved = false;
            entry.PayeeId = entry.Payee?.Id;
            // Update existing positions in place so EF never tracks duplicate composite split keys.
            var splits = request.Splits ?? [];
            foreach (var old in entry.Splits.Where(s => s.Position >= splits.Count).ToList()) { entry.Splits.Remove(old); db.Remove(old); }
            for (var position = 0; position < splits.Count; position++)
            {
                var split = entry.Splits.SingleOrDefault(s => s.Position == position);
                if (split is null) { split = new SplitEntity { TransactionId = entry.Id, Position = position }; entry.Splits.Add(split); }
                split.CategoryId = splits[position].CategoryId;
                split.Amount = splits[position].Amount;
            }
            if (id is null) db.Transactions.Add(entry);
            if (request.Repeat != "never" && !entry.IsOpening)
                await CreateNextScheduleAsync(entry, request.Repeat, ct);
            if (imported) return new Change(entry.Id, null, new { sourcePayee = request.Payee, transaction = ToDto(entry) });
            return new Change(entry.Id, before, request.Repeat != "never" ? new { transaction = ToDto(entry), schedule = db.Schedules.Local.Single(r => r.Id == entry.ScheduleId), invalidatedReconciliations = invalidated } : invalidated.Count == 0 ? ToDto(entry) : new { transaction = ToDto(entry), invalidatedReconciliations = invalidated });
        }, ct);

    public Task<MutationDto> SaveTransferAsync(Guid? id, TransferRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, id is null ? "transfer.create" : $"transfer.update/{id}", request, async () =>
        {
            if (request.FromAccountId == request.ToAccountId || request.Amount <= 0 || !LedgerRules.IsMoney(request.Amount)
                || request.Date.Year is < 1900 or > 9998 || request.Memo?.Length > 1000)
                throw new LedgerException("A transfer needs two different open accounts, a valid date, and a positive amount with two decimal places.");
            await OpenAccountAsync(request.FromAccountId, ct);
            await OpenAccountAsync(request.ToAccountId, ct);
            await budgets.GetMonthAsync(Month(request.Date), ct);
            List<Guid> invalidated = [];
            List<TransactionEntity> pair;
            List<TransactionDto>? before = null;
            if (id is null)
            {
                var transferId = Guid.NewGuid();
                pair = [new() { Id = transferId, TransferId = transferId }, new() { Id = Guid.NewGuid(), TransferId = transferId }];
                db.Transactions.AddRange(pair);
            }
            else
            {
                var entry = await FindEntryAsync(id.Value, ct);
                if (entry.TransferId is null) throw new LedgerException("This entry is not a transfer.");
                pair = await Entries.Where(t => t.TransferId == entry.TransferId && !t.IsDeleted).OrderBy(t => t.Amount).ToListAsync(ct);
                if (pair.Count != 2) throw new LedgerException("Transfer pair is incomplete.");
                before = pair.Select(ToDto).ToList();
                foreach (var item in pair) { await OpenAccountAsync(item.AccountId, ct); invalidated.AddRange(await ConfirmEditableAsync(item, request.ConfirmReconciledEdit, ct)); }
            }
            pair[0].AccountId = request.FromAccountId;
            pair[0].Amount = -request.Amount;
            pair[1].AccountId = request.ToAccountId;
            pair[1].Amount = request.Amount;
            foreach (var item in pair) { item.Date = request.Date; item.Memo = request.Memo?.Trim() ?? ""; }
            if (request.Repeat != "never") await CreateNextScheduleAsync(pair[0], request.Repeat, ct, pair[1].AccountId);
            return new Change(pair[0].TransferId!.Value, before, request.Repeat != "never" ? new { entries = pair.Select(ToDto).ToList(), schedule = db.Schedules.Local.Single(r => r.Id == pair[0].ScheduleId), invalidatedReconciliations = invalidated } : invalidated.Count == 0 ? pair.Select(ToDto).ToList() : new { entries = pair.Select(ToDto).ToList(), invalidatedReconciliations = invalidated });
        }, ct);

    public Task<MutationDto> DeleteTransactionAsync(Guid id, DeleteTransactionRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, $"transaction.delete/{id}", request, async () =>
        {
            var entry = await FindEntryAsync(id, ct);
            var rows = entry.TransferId is null ? [entry] : await Entries.Where(t => t.TransferId == entry.TransferId && !t.IsDeleted).ToListAsync(ct);
            var before = rows.Select(ToDto).ToList();
            foreach (var item in rows) { EnsureEditable(item); await OpenAccountAsync(item.AccountId, ct); item.IsDeleted = true; }
            return new Change(entry.TransferId ?? entry.Id, before, new { deleted = true, entries = rows.Select(ToDto).ToList() });
        }, ct);

    public Task<MutationDto> ReconcileAsync(Guid id, ReconcileRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, $"account.reconcile/{id}", request, async () =>
        {
            await OpenAccountAsync(id, ct);
            if (!LedgerRules.IsMoney(request.StatementBalance)) throw new LedgerException("Statement balance is invalid.");
            var entries = await Entries.Where(t => t.AccountId == id && !t.IsDeleted).ToListAsync(ct);
            var cleared = LedgerRules.Balance(entries, clearedOnly: true);
            if (cleared != request.StatementBalance)
                throw new LedgerException($"Reconciliation difference is {request.StatementBalance - cleared:0.00}. Correct or clear transactions before confirming a zero difference.");
            var before = entries.Where(t => t.Status == "cleared").Select(ToDto).ToList();
            foreach (var entry in entries.Where(t => t.Status == "cleared")) entry.Status = "reconciled";
            var snapshot = new ReconciliationEntity { Id = Guid.NewGuid(), AccountId = id, StatementBalance = cleared, CreatedAt = DateTimeOffset.UtcNow };
            db.Reconciliations.Add(snapshot);
            return new Change(id, before, new { snapshot, entries = entries.Where(t => t.Status == "reconciled").Select(ToDto).ToList() });
        }, ct);

    public Task<MutationDto> SetStatusAsync(Guid id, StatusRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, $"transaction.status/{id}", request, async () =>
        {
            if (request.Status is not ("uncleared" or "cleared")) throw new LedgerException("Use cleared or uncleared status.");
            var entry = await FindEntryAsync(id, ct);
            await OpenAccountAsync(entry.AccountId, ct);
            EnsureEditable(entry);
            var before = ToDto(entry);
            entry.Status = request.Status;
            if(request.Status == "cleared")entry.IsApproved = true;
            return new Change(entry.TransferId ?? id, before, ToDto(entry));
        }, ct);

    public Task<List<string>> GetPayeesAsync(CancellationToken ct) =>
        db.Payees.AsNoTracking().Where(p => !p.IsHidden).OrderBy(p => p.Name).Select(p => p.Name).ToListAsync(ct);

    private async Task<MutationDto> ExecuteAsync<T>(Guid commandId, long version, string kind, T payload, Func<Task<Change>> action, CancellationToken ct) =>
        await gate.RunAsync(async () =>
        {
            if (commandId == Guid.Empty || version < 1) throw new LedgerException("Command ID and current ledger version are required.");
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kind + JsonSerializer.Serialize(payload))));
            var previous = await db.LedgerCommands.SingleOrDefaultAsync(c => c.Id == commandId, ct);
            if (previous is not null)
            {
                if (previous.Fingerprint != fingerprint) throw new LedgerException("This command ID was already used for a different request.", 409);
                return new MutationDto(previous.ResourceId, previous.Version);
            }
            var state = await db.LedgerStates.SingleAsync(ct);
            if (state.Version != version) throw new LedgerException("The ledger changed in another session. Reload, review the latest entries, and retry.", 409);
            var change = await action();
            await db.SaveChangesAsync(ct);
            await RecalculateMonthsAsync(ct);
            state.Version++;
            db.LedgerCommands.Add(new LedgerCommandEntity
            {
                Id = commandId, ResourceId = change.Id, Kind = kind, Fingerprint = fingerprint, Version = state.Version,
                BeforeJson = JsonSerializer.Serialize(change.Before), AfterJson = JsonSerializer.Serialize(change.After), CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
            return new MutationDto(change.Id, state.Version);
        }, ct);

    private async Task RecalculateMonthsAsync(CancellationToken ct)
    {
        var months = await db.Budgets.Include(b => b.Groups).ThenInclude(g => g.Categories).OrderBy(b => b.Month).ToListAsync(ct);
        var entries = await Entries.Where(t => !t.IsDeleted).ToListAsync(ct);
        var byMonth = entries.GroupBy(t => Month(t.Date)).ToDictionary(g => g.Key, g => g.ToList());
        decimal rtaDelta = 0;
        var categoryDeltas = new Dictionary<string, decimal>();
        BudgetEntity? previous = null;
        foreach (var month in months)
        {
            var oldRta = BudgetService.CalculateReadyToAssign(month);
            var oldAvailable = month.Groups.SelectMany(g => g.Categories).ToDictionary(c => c.Id, c => c.Available);
            var monthlyEntries = byMonth.GetValueOrDefault(month.Month) ?? [];
            var activity = LedgerRules.Activity(monthlyEntries);
            var income = monthlyEntries.Sum(LedgerRules.Income);
            var changed = rtaDelta != 0 || income != month.LedgerIncome;
            if (previous is not null) changed |= BudgetCarryForward.Apply(month, rtaDelta, categoryDeltas, previous);
            month.LedgerIncome = income;
            var categories = month.Groups.SelectMany(g => g.Categories).ToList();
            foreach (var category in categories)
            {
                var nextActivity = category.LegacyActivity + activity.GetValueOrDefault(category.Id);
                changed |= category.Activity != nextActivity;
                category.Activity = nextActivity;
            }
            rtaDelta = BudgetService.CalculateReadyToAssign(month) - oldRta;
            categoryDeltas = categories.ToDictionary(c => c.Id, c => c.Available - oldAvailable.GetValueOrDefault(c.Id));
            if (changed) month.Version++;
            previous = month;
        }
    }

    private async Task<AccountEntity> OpenAccountAsync(Guid id, CancellationToken ct)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new LedgerException("Account not found.", 404);
        if (account.IsClosed) throw new LedgerException("Reopen this account before changing its ledger.");
        return account;
    }

    private async Task<TransactionEntity> FindEntryAsync(Guid id, CancellationToken ct) =>
        await Entries.SingleOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct) ?? throw new LedgerException("Transaction not found.", 404);

    private async Task<PayeeEntity?> GetPayeeAsync(string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalized = name.Trim().ToUpperInvariant();
        var payee = await db.Payees.SingleOrDefaultAsync(p => p.NormalizedName == normalized, ct);
        if (payee is not null) return payee;
        payee = new() { Id = Guid.NewGuid(), Name = name.Trim(), NormalizedName = normalized };
        db.Payees.Add(payee);
        return payee;
    }

    private static void EnsureEditable(TransactionEntity entry)
    {
        if (entry.Status == "reconciled") throw new LedgerException("Reconciled entries are locked. Record a new correcting transaction to preserve the reconciliation audit.");
    }
    private static void ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120) throw new LedgerException("Account name is required (maximum 120 characters).");
    }
    private static void ValidateAccountType(string? type)
    {
        if (type is not ("checking" or "savings" or "cash")) throw new LedgerException("Account type must be checking, savings or cash.");
    }
    private sealed class LedgerBalanceRow
    {
        public Guid Id { get; set; }
        public decimal Balance { get; set; }
    }
    private static void ValidatePage(int page, int size)
    {
        if (page is < 1 or > 1_000_000 || size is < 1 or > 100) throw new LedgerException("Page must be 1–1000000 and pageSize 1–100.");
    }
    private static string Month(DateOnly date) => date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
    private static TransactionDto ToDto(TransactionEntity t) => new(t.Id, t.AccountId, t.Date, t.Amount, t.Payee?.Name ?? "", t.Memo,
        t.CategoryId, t.Status, t.TransferId, t.IsOpening, t.Splits.OrderBy(s => s.Position).Select(s => new SplitDto(s.CategoryId, s.Amount)).ToList(), !t.IsApproved || LedgerRules.NeedsAttention(t), FlagColor:t.FlagColor, ScheduleId:t.ScheduleId, IsApproved:t.IsApproved, NeedsCategory:LedgerRules.NeedsAttention(t));
    private sealed record Change(Guid Id, object? Before, object? After);
}

