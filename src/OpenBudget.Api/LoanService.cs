using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

// Mortgage, loan and contract accounts. Term changes, pairing and balance confirmations are ledger commands
// (command ID, ledger version, replay receipt, audit). Summaries, projections and simulations are read-only.
public sealed partial class LedgerService
{
    private const int MaxActivityMonths = 600;

    public async Task<LoanDto> GetLoanAsync(Guid id, CancellationToken ct)
    {
        var (account, terms) = await LoanAccountAsync(id, ct);
        var version = await db.LedgerStates.Select(x => x.Version).SingleAsync(ct);
        var today = Today();
        var currentMonth = LoanRules.Month(today);
        var entries = await db.Transactions.AsNoTracking().Include(t => t.Payee).Where(t => t.AccountId == id && !t.IsDeleted)
            .OrderBy(t => t.Date).ThenBy(t => t.Id).ToListAsync(ct);
        var balance = entries.Sum(t => t.Amount);
        var debt = Math.Max(-balance, 0);
        var contract = terms.Contract;
        var projection = contract is null
            ? ToProjection(debt, terms.InterestRate, terms.MinimumPayment, terms.EscrowAmount, 0, currentMonth)
            : ContractProjection(debt, contract, today, currentMonth);
        var history = MonthEndBalances(entries, currentMonth).Select(x => new LoanBalancePointDto(x.Month, Math.Max(-x.Balance, 0))).ToList();
        return new(id, account.AccountType, version, currentMonth, balance, terms.InterestRate,
            contract is null ? terms.MinimumPayment : projection.MonthlyPayment, terms.EscrowAmount, terms.OriginalBalance,
            terms.OriginalBalance > 0 ? decimal.Round(debt / terms.OriginalBalance * 100, 1, MidpointRounding.ToEven) : null,
            terms.BalanceConfirmedAt, LoanRules.MonthlyInterest(debt, terms.InterestRate), await PairedCategoryAsync(terms.PairedCategoryId, account.Id, currentMonth, ct),
            projection, history, await ActivityAsync(entries, terms.InterestRate, currentMonth, ct), contract is null ? null : ToContractDto(contract, today),
            terms.SeparateDeviceCategory, terms.SeparateDeviceCategory ? await PairedCategoryAsync(terms.DevicePairedCategoryId, account.Id, currentMonth, ct) : null);
    }

    // Every paired slot in the active budget, so pickers can show categories already paired with a loan as unavailable.
    public async Task<LoanPairingsDto> GetLoanPairingsAsync(CancellationToken ct)
    {
        var rows = await db.LoanTerms.AsNoTracking().Join(db.Accounts.AsNoTracking(), t => t.AccountId, a => a.Id,
            (t, a) => new { t.AccountId, a.Name, t.PairedCategoryId, t.SeparateDeviceCategory, t.DevicePairedCategoryId }).ToListAsync(ct);
        var items = new List<LoanPairingDto>();
        foreach (var row in rows.OrderBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.AccountId))
        {
            if (row.PairedCategoryId is { } main) items.Add(new(row.AccountId, row.Name, main, "loan"));
            if (row.SeparateDeviceCategory && row.DevicePairedCategoryId is { } device) items.Add(new(row.AccountId, row.Name, device, "device"));
        }
        return new(items);
    }

    // Read-only payoff simulation. Either a monthly payment (including escrow/fees) or a payoff month is given;
    // a payoff month solves for the smallest whole-cent payment that finishes by then. Extra is a one-time payment this month.
    public async Task<LoanSimulationDto> SimulateLoanAsync(Guid id, decimal? payment, string? payoffMonth, decimal? extra, CancellationToken ct)
    {
        var (account, terms) = await LoanAccountAsync(id, ct);
        if (LoanRules.IsContract(account.AccountType)) throw new LedgerException("Contracts follow their fixed schedule; the payoff simulator is for loans.");
        var oneTime = extra ?? 0;
        if (!LedgerRules.IsMoney(oneTime) || oneTime < 0) throw new LedgerException("The extra payment must be a non-negative amount with at most two decimal places.");
        var currentMonth = LoanRules.Month(Today());
        var balance = await db.Transactions.Where(t => t.AccountId == id && !t.IsDeleted).SumAsync(t => t.Amount, ct);
        var debt = Math.Max(-balance, 0);
        decimal monthly;
        if (payoffMonth is not null)
        {
            if (payment is not null) throw new LedgerException("Choose either a monthly payment or a payoff month.");
            if (!CreditPayoffRules.TryParseMonth(payoffMonth, out _)) throw new LedgerException("Payoff month must use YYYY-MM format.");
            var months = LoanRules.MonthsBetween(currentMonth, payoffMonth);
            if (months < 1 || months > LoanRules.MaxMonths) throw new LedgerException("Choose a payoff month after this month and within 100 years.");
            monthly = LoanRules.PrincipalPaymentFor(Math.Max(debt - oneTime, 0), terms.InterestRate, months) + terms.EscrowAmount;
        }
        else
        {
            monthly = payment ?? terms.MinimumPayment;
            if (!LedgerRules.IsMoney(monthly) || monthly < 0) throw new LedgerException("The monthly payment must be a non-negative amount with at most two decimal places.");
        }
        if (!LedgerRules.IsMoney(monthly)) throw new LedgerException("The payoff month needs a payment larger than the supported amount.");
        return new(id, currentMonth, balance, terms.MinimumPayment, oneTime, ToProjection(debt, terms.InterestRate, monthly, terms.EscrowAmount, oneTime, currentMonth));
    }

    public Task<MutationDto> SaveLoanTermsAsync(Guid id, SaveLoanTermsRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, $"loan.terms/{id}", request, async () =>
        {
            var account = await OpenAccountAsync(id, ct);
            if (!LoanRules.IsLoan(account.AccountType)) throw new LedgerException("Only loan accounts have loan details.");
            var terms = await db.LoanTerms.SingleOrDefaultAsync(t => t.AccountId == id, ct) ?? throw new LedgerException("Loan details not found.", 404);
            var before = ToTermsAudit(terms);
            var created = await ApplyLoanTermsAsync(account, account.AccountType, terms, request.Terms, ct);
            return new Change(id, before, created.Count == 0 ? ToTermsAudit(terms) : new { loan = ToTermsAudit(terms), createdCategories = created });
        }, ct);

    public Task<MutationDto> ConfirmLoanBalanceAsync(Guid id, LoanBalanceRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, $"loan.balance/{id}", request, async () =>
        {
            var account = await OpenAccountAsync(id, ct);
            if (!LoanRules.IsLoan(account.AccountType)) throw new LedgerException("Only loan accounts can confirm a lender balance.");
            var terms = await db.LoanTerms.SingleOrDefaultAsync(t => t.AccountId == id, ct) ?? throw new LedgerException("Loan details not found.", 404);
            if (request.Balance is { } target && !LedgerRules.IsMoney(target)) throw new LedgerException("Enter the lender balance with at most two decimal places.");
            var balance = await db.Transactions.Where(t => t.AccountId == id && !t.IsDeleted).SumAsync(t => t.Amount, ct);
            var difference = request.Balance is null ? 0 : request.Balance.Value - balance;
            if (!LedgerRules.IsMoney(difference)) throw new LedgerException("The adjustment exceeds the supported transaction amount.");
            var before = new { balance, terms.BalanceConfirmedAt };
            TransactionEntity? adjustment = null;
            if (difference != 0) adjustment = await AddBalanceAdjustmentAsync(id, difference, "Lender balance update", ct);
            terms.BalanceConfirmedAt = clock.GetUtcNow();
            terms.UpdatedAt = terms.BalanceConfirmedAt.Value;
            return new Change(id, before, new { balance = balance + difference, terms.BalanceConfirmedAt, adjustment = adjustment is null ? null : ToDto(adjustment) });
        }, ct);

    public const string InterestPayee = "Interest Charge";

    public Task<MutationDto> RecordLoanInterestAsync(Guid id, LoanInterestRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, $"loan.interest/{id}/{request.Month}", request, async () =>
        {
            var account = await OpenAccountAsync(id, ct);
            if (!LoanRules.IsLoan(account.AccountType) || LoanRules.IsContract(account.AccountType)) throw new LedgerException("Only loan and mortgage accounts record interest charges.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Month ?? "", @"^\d{4}-(0[1-9]|1[0-2])$")) throw new LedgerException("Month must be YYYY-MM.");
            if (!LedgerRules.IsMoney(request.Amount) || request.Amount < 0) throw new LedgerException("Interest must be zero or a positive amount with at most two decimal places.");
            var existing = await db.Transactions.Include(t => t.Payee).Where(t => t.AccountId == id && !t.IsDeleted && !t.IsOpening && t.TransferId == null && t.Payee!.Name == InterestPayee).ToListAsync(ct);
            existing = existing.Where(t => LoanRules.Month(t.Date) == request.Month).ToList();
            foreach (var row in existing) EnsureEditable(row);
            var before = existing.Select(ToDto).ToList();
            foreach (var row in existing) row.IsDeleted = true;
            TransactionEntity? added = null;
            if (request.Amount > 0)
            {
                var first = DateOnly.ParseExact(request.Month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                var date = Today() is var today && LoanRules.Month(today) == request.Month ? today : first.AddMonths(1).AddDays(-1);
                await budgets.GetMonthAsync(db.ActiveBudgetId, request.Month, ct);
                added = new TransactionEntity { Id = Guid.NewGuid(), AccountId = id, Date = date, Amount = -request.Amount, Payee = await GetPayeeAsync(InterestPayee, ct), Memo = "", Status = "cleared" };
                db.Transactions.Add(added);
            }
            return new Change(id, before, new { month = request.Month, interest = added is null ? null : ToDto(added) });
        }, ct);

    private async Task<TransactionEntity> AddBalanceAdjustmentAsync(Guid accountId, decimal difference, string memo, CancellationToken ct)
    {
        var date = Today();
        await budgets.GetMonthAsync(db.ActiveBudgetId, Month(date), ct);
        var adjustment = new TransactionEntity { Id = Guid.NewGuid(), AccountId = accountId, Date = date, Amount = difference,
            Payee = await GetPayeeAsync("Balance Adjustment", ct), Memo = memo, Status = "cleared" };
        db.Transactions.Add(adjustment);
        return adjustment;
    }

    // Validates and applies terms and pairing for a (new or existing) loan account. The caller has verified the type family.
    // Categories (and groups) requested by NewPairedCategory/NewDevicePairedCategory are created in every initialized month
    // inside the same ledger command, so a failure anywhere rolls back the account, terms and categories together.
    private async Task<List<CreatedPairCategory>> ApplyLoanTermsAsync(AccountEntity account, string accountType, LoanTermsEntity terms, LoanTermsRequest? request, CancellationToken ct)
    {
        if (LoanRules.ValidateTerms(accountType, request) is { } error) throw new LedgerException(error);
        var currentMonth = LoanRules.Month(Today());
        // The current month is only needed (and initialized) when a slot is paired.
        var pairs = request!.PairedCategoryId is not null || request.NewPairedCategory is not null
            || request.SeparateDeviceCategory && (request.DevicePairedCategoryId is not null || request.NewDevicePairedCategory is not null);
        var month = pairs ? await budgets.GetMonthAsync(db.ActiveBudgetId, currentMonth, ct) : null!;
        var created = new List<CreatedPairCategory>();
        var paired = await ResolvePairSlotAsync(account, month, request.PairedCategoryId, request.NewPairedCategory, terms.PairedCategoryId, terms.DevicePairedCategoryId, true, created, ct);
        string? device = null;
        if (request.SeparateDeviceCategory)
            device = await ResolvePairSlotAsync(account, month, request.DevicePairedCategoryId, request.NewDevicePairedCategory, terms.DevicePairedCategoryId, terms.PairedCategoryId, false, created, ct);
        if (paired is not null && paired == device) throw new LedgerException("Choose different categories for the contract and the device.");
        foreach (var categoryId in new[] { paired, device }.OfType<string>())
            if (await db.LoanTerms.AnyAsync(t => t.AccountId != account.Id && (t.PairedCategoryId == categoryId || t.DevicePairedCategoryId == categoryId), ct))
                throw new LedgerException("That category is already paired with another loan. A category can be paired with only one loan.");
        terms.InterestRate = request.InterestRate;
        terms.MinimumPayment = request.MinimumPayment;
        terms.EscrowAmount = request.EscrowAmount;
        terms.PairedCategoryId = paired;
        terms.SeparateDeviceCategory = request.SeparateDeviceCategory;
        terms.DevicePairedCategoryId = device;
        var contract = request.Contract;
        terms.ContractMonths = contract?.Months;
        terms.ContractStartDate = contract?.StartDate;
        terms.IncludesDevice = contract?.IncludesDevice ?? false;
        terms.UpfrontCost = contract?.UpfrontCost ?? 0;
        terms.MonthlyServiceCost = contract?.MonthlyServiceCost ?? 0;
        terms.DeviceTotal = contract?.DeviceTotal ?? 0;
        terms.DiscountMonths = contract?.DiscountMonths ?? 0;
        terms.DiscountedServiceCost = contract?.DiscountedServiceCost;
        terms.UpdatedAt = clock.GetUtcNow();
        return created;
    }

    private sealed record CreatedPairCategory(string Slot, string CategoryId, string Name, string GroupId, string GroupName, bool NewGroup);

    // Resolves one pairing slot to a category ID (null = unpaired). An existing category must be active in the current month,
    // outside the server-owned Credit Card Payments and Wishlist groups, and not another loan's legacy category. Keeping a
    // slot's current pairing (or moving it from the other slot of the same loan) is always allowed so unrelated edits never
    // fail because the category was archived later. The account's own legacy "loan-{id}" category is restored when re-paired.
    private async Task<string?> ResolvePairSlotAsync(AccountEntity account, BudgetEntity month, string? categoryId, NewPairedCategoryRequest? create,
        string? current, string? otherSlot, bool main, List<CreatedPairCategory> created, CancellationToken ct)
    {
        if (create is not null) return await CreatePairCategoryAsync(month, create, main ? "loan" : "device", created, ct);
        if (categoryId is null) return null;
        if (LoanRules.IsProtectedCategory(categoryId)) throw new LedgerException("Credit card payment and Wishlist categories cannot be paired with a loan.");
        if (categoryId.StartsWith(LoanRules.CategoryPrefix, StringComparison.Ordinal) && categoryId != LoanRules.AutoCategoryId(account.Id))
            throw new LedgerException("That category belongs to another loan.");
        var row = month.Groups.SelectMany(g => g.Categories.Select(c => (Group: g, Category: c))).FirstOrDefault(x => x.Category.Id == categoryId);
        if (main && row.Category is not null && categoryId == LoanRules.AutoCategoryId(account.Id))
        {
            await RestoreAutoCategoryAsync(categoryId, month.Month, ct);
            return categoryId;
        }
        if (row.Category is not null && (categoryId == current || categoryId == otherSlot)) return categoryId;
        if (row.Category is null || row.Category.IsArchived || row.Group.IsArchived)
            throw new LedgerException("Choose an active category from this month's plan.");
        if (LoanRules.IsProtectedGroup(row.Group.Id)) throw new LedgerException("Credit card payment and Wishlist categories cannot be paired with a loan.");
        return categoryId;
    }

    // Creates a user category (and, when requested, its group) in every initialized month of the active budget, appended
    // after existing groups/categories like other server-created categories. Names follow Plan rules: a category name is
    // unique (case-insensitive) within its group and a new group's name is unique among active groups.
    private async Task<string> CreatePairCategoryAsync(BudgetEntity current, NewPairedCategoryRequest request, string slot, List<CreatedPairCategory> created, CancellationToken ct)
    {
        const int maxGroups = 100, maxCategoriesPerGroup = 500;
        var name = request.Name.Trim();
        string groupId, groupName;
        var newGroup = request.NewGroupName is not null;
        var reused = created.FirstOrDefault(c => c.NewGroup && request.NewGroupName is not null && LoanRules.SameName(c.GroupName, request.NewGroupName));
        if (reused is not null) { groupId = reused.GroupId; groupName = reused.GroupName; newGroup = false; }
        else if (request.NewGroupName is { } newName)
        {
            groupName = newName.Trim();
            if (current.Groups.Any(g => !g.IsArchived && LoanRules.SameName(g.Name, groupName))) throw new LedgerException("A category group with that name already exists. Choose it from the list instead.");
            if (current.Groups.Count >= maxGroups) throw new LedgerException("The plan already has the maximum number of category groups.");
            groupId = $"group-{Guid.NewGuid():D}";
        }
        else
        {
            var group = current.Groups.SingleOrDefault(g => g.Id == request.GroupId);
            if (group is null || group.IsArchived) throw new LedgerException("Choose an active category group from this month's plan.");
            if (LoanRules.IsProtectedGroup(group.Id)) throw new LedgerException("Categories cannot be added to the Credit Card Payments or Wishlist groups.");
            groupId = group.Id;
            groupName = group.Name;
        }
        var existing = current.Groups.SingleOrDefault(g => g.Id == groupId)?.Categories ?? [];
        if (existing.Any(c => LoanRules.SameName(c.Name, name)) || created.Any(c => c.GroupId == groupId && LoanRules.SameName(c.Name, name)))
            throw new LedgerException("This category group already has a category with that name.");
        if (existing.Count >= maxCategoriesPerGroup) throw new LedgerException("That category group already has the maximum number of categories.");
        var categoryId = $"category-{Guid.NewGuid():D}";
        var months = await db.Budgets.Where(b => b.BudgetId == db.ActiveBudgetId).Include(b => b.Groups).ThenInclude(g => g.Categories).ToListAsync(ct);
        foreach (var month in months)
        {
            var group = month.Groups.SingleOrDefault(g => g.Id == groupId);
            if (group is null)
            {
                group = new CategoryGroupEntity { BudgetId = month.BudgetId, BudgetMonth = month.Month, Id = groupId, Name = groupName,
                    SortOrder = month.Groups.Count == 0 ? 0 : month.Groups.Max(g => g.SortOrder) + 1 };
                month.Groups.Add(group);
            }
            group.Categories.Add(new CategoryEntity { BudgetId = month.BudgetId, BudgetMonth = month.Month, GroupId = groupId, Id = categoryId, Name = name,
                SortOrder = group.Categories.Count == 0 ? 0 : group.Categories.Max(c => c.SortOrder) + 1 });
            month.Version++;
        }
        created.Add(new(slot, categoryId, name, groupId, groupName, newGroup));
        return categoryId;
    }

    // Re-pairing the account's own category restores it (and the Loans group) from the current month on.
    private async Task RestoreAutoCategoryAsync(string categoryId, string currentMonth, CancellationToken ct)
    {
        var months = await db.Budgets.Where(b => b.BudgetId == db.ActiveBudgetId && string.Compare(b.Month, currentMonth) >= 0)
            .Include(b => b.Groups).ThenInclude(g => g.Categories).ToListAsync(ct);
        foreach (var month in months)
        {
            var changed = false;
            foreach (var group in month.Groups)
                foreach (var category in group.Categories.Where(c => c.Id == categoryId && (c.IsArchived || group.IsArchived)))
                { category.IsArchived = false; group.IsArchived = false; changed = true; }
            if (changed) month.Version++;
        }
    }

    private async Task<(AccountEntity Account, LoanTermsEntity Terms)> LoanAccountAsync(Guid id, CancellationToken ct)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new LedgerException("Account not found.", 404);
        if (!LoanRules.IsLoan(account.AccountType)) throw new LedgerException("Only loan accounts have loan details.");
        var terms = await db.LoanTerms.AsNoTracking().SingleOrDefaultAsync(t => t.AccountId == id, ct) ?? throw new LedgerException("Loan details not found.", 404);
        return (account, terms);
    }

    private async Task<LoanPairedCategoryDto?> PairedCategoryAsync(string? categoryId, Guid accountId, string currentMonth, CancellationToken ct)
    {
        if (categoryId is null) return null;
        var month = await budgets.GetMonthAsync(db.ActiveBudgetId, currentMonth, ct);
        var row = month.Groups.SelectMany(g => g.Categories.Select(c => (Group: g, Category: c))).FirstOrDefault(x => x.Category.Id == categoryId);
        if (row.Category is null) return null;
        var target = await db.CategoryTargets.AsNoTracking().SingleOrDefaultAsync(t => t.CategoryId == categoryId, ct);
        decimal? moreNeeded = null;
        if (target is not null)
        {
            var history = await budgets.LoadTargetHistoryAsync(db.ActiveBudgetId, currentMonth, [target], ct);
            moreNeeded = BudgetService.CalculateTarget(target, currentMonth, row.Category, history).MoreNeeded;
        }
        return new(categoryId, row.Category.Name, row.Group.Name, row.Category.Available, row.Category.IsArchived || row.Group.IsArchived,
            categoryId == LoanRules.AutoCategoryId(accountId), target?.Kind, moreNeeded);
    }

    private static List<(string Month, decimal Balance)> MonthEndBalances(IReadOnlyList<TransactionEntity> entries, string currentMonth)
    {
        var byMonth = entries.GroupBy(t => LoanRules.Month(t.Date)).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
        var first = byMonth.Count == 0 ? currentMonth : byMonth.Keys.Min(StringComparer.Ordinal)!;
        if (string.CompareOrdinal(first, currentMonth) > 0) first = currentMonth;
        if (LoanRules.MonthsBetween(first, currentMonth) > MaxActivityMonths) first = LoanRules.AddMonths(currentMonth, -MaxActivityMonths);
        var running = entries.Where(t => string.CompareOrdinal(LoanRules.Month(t.Date), first) < 0).Sum(t => t.Amount);
        var result = new List<(string, decimal)>();
        for (var month = first; string.CompareOrdinal(month, currentMonth) <= 0; month = LoanRules.AddMonths(month, 1))
        {
            running += byMonth.GetValueOrDefault(month);
            result.Add((month, running));
        }
        return result;
    }

    private async Task<List<LoanActivityMonthDto>> ActivityAsync(IReadOnlyList<TransactionEntity> entries, decimal rate, string currentMonth, CancellationToken ct)
    {
        var transferIds = entries.Where(t => t.TransferId is not null).Select(t => t.TransferId!.Value).ToList();
        var counterparts = await db.Transactions.AsNoTracking().Where(t => t.TransferId != null && transferIds.Contains(t.TransferId.Value) && !t.IsDeleted)
            .Select(t => new { t.Id, t.TransferId, t.AccountId, t.Account.Name }).ToListAsync(ct);
        var ends = MonthEndBalances(entries, currentMonth);
        var result = new List<LoanActivityMonthDto>();
        for (var index = ends.Count - 1; index >= 0; index--)
        {
            var (month, end) = ends[index];
            // Interest accrues on the debt carried into the month; in the account's first month that is the opening debt.
            var previous = index == 0 ? entries.Where(t => t.IsOpening && LoanRules.Month(t.Date) == month).Sum(t => t.Amount)
                + entries.Where(t => string.CompareOrdinal(LoanRules.Month(t.Date), month) < 0).Sum(t => t.Amount) : ends[index - 1].Balance;
            var payments = new List<LoanActivityItemDto>();
            var other = new List<LoanActivityItemDto>();
            LoanActivityItemDto? interest = null;
            foreach (var entry in entries.Where(t => LoanRules.Month(t.Date) == month).OrderByDescending(t => t.Date).ThenBy(t => t.Id))
            {
                var other_ = entry.TransferId is null ? null : counterparts.FirstOrDefault(c => c.TransferId == entry.TransferId && c.Id != entry.Id);
                var counterpart = other_?.Name;
                if (entry.TransferId is null && !entry.IsOpening && entry.Payee?.Name == InterestPayee) { interest = new(entry.Id, entry.Date, entry.Amount, "interest", entry.Memo, null); continue; }
                if (entry.TransferId is not null && entry.Amount > 0) payments.Add(new(entry.Id, entry.Date, entry.Amount, "payment", entry.Memo, counterpart, other_?.AccountId));
                else other.Add(new(entry.Id, entry.Date, entry.Amount,
                    entry.IsOpening ? "opening" : entry.TransferId is not null ? "draw" : "transaction",
                    entry.IsOpening ? "Initial Balance" : entry.Payee?.Name is { Length: > 0 } payee ? payee : entry.Memo, counterpart, other_?.AccountId));
            }
            result.Add(new(month, end, LoanRules.MonthlyInterest(Math.Max(-previous, 0), rate), payments, other, interest));
        }
        return result;
    }

    private static LoanProjectionDto ToProjection(decimal debt, decimal rate, decimal payment, decimal escrow, decimal extra, string currentMonth)
    {
        var principal = payment - escrow;
        var projection = principal <= 0 && debt - extra > 0 ? new LoanRules.Projection(null, 0, 0, [Math.Max(debt - extra, 0)]) : LoanRules.Project(debt, rate, principal, extra);
        var track = projection.Balances.Select((value, index) => new LoanBalancePointDto(LoanRules.AddMonths(currentMonth, index), value)).ToList();
        if (extra > 0) track[0] = track[0] with { Balance = debt };
        return new(payment, projection.Months, projection.Months is { } months ? LoanRules.AddMonths(currentMonth, months) : null,
            projection.TotalInterest, debt, projection.NeverPaidOff ? 0 : debt + projection.TotalInterest, projection.NeverPaidOff, track);
    }

    // A contract is paid down by its remaining schedule (items due from today on), starting in each item's due month.
    private static LoanProjectionDto ContractProjection(decimal debt, ContractTermsRequest contract, DateOnly today, string currentMonth)
    {
        var open = LoanRules.ContractSchedule(contract).Where(item => item.DueDate >= today).ToList();
        var track = new List<LoanBalancePointDto> { new(currentMonth, debt) };
        var remaining = debt;
        int? months = debt == 0 ? 0 : null;
        foreach (var item in open)
        {
            if (remaining <= 0) break;
            remaining = Math.Max(remaining - item.Total, 0);
            var month = LoanRules.Month(item.DueDate);
            if (track[^1].Month == month) track[^1] = track[^1] with { Balance = remaining };
            else track.Add(new(month, remaining));
            if (remaining == 0) months = Math.Max(LoanRules.MonthsBetween(currentMonth, month), 0);
        }
        if (remaining > 0)
        {
            // Debt beyond the schedule (for example unrecorded payments) is shown as due in the final month.
            var end = LoanRules.Month(open.Count > 0 ? open[^1].DueDate : contract.StartDate.AddMonths(contract.Months - 1));
            if (string.CompareOrdinal(end, currentMonth) < 0) end = currentMonth;
            if (track[^1].Month == end) track[^1] = track[^1] with { Balance = 0 }; else track.Add(new(end, 0));
            months = LoanRules.MonthsBetween(currentMonth, end);
        }
        var next = open.FirstOrDefault()?.Total ?? 0;
        return new(next, open.Count, LoanRules.AddMonths(currentMonth, months ?? 0), 0, debt, debt, false, track);
    }

    private static ContractDto ToContractDto(ContractTermsRequest contract, DateOnly today)
    {
        var position = LoanRules.ContractPositionAt(contract, today);
        return new(contract.Months, contract.StartDate, position.EndDate, contract.IncludesDevice, contract.UpfrontCost, contract.MonthlyServiceCost,
            contract.DeviceTotal, contract.DiscountMonths, contract.DiscountedServiceCost, position.TotalCost, position.RemainingDevice, position.RemainingService,
            position.Remaining, position.RemainingMonths,
            LoanRules.ContractSchedule(contract).Select(item => new ContractScheduleItemDto(item.Number, item.DueDate, item.Service, item.Device, item.Total, item.IsDiscounted, item.DueDate < today)).ToList());
    }

    private static object ToTermsAudit(LoanTermsEntity t) => new
    {
        t.InterestRate, t.MinimumPayment, t.EscrowAmount, t.PairedCategoryId, t.SeparateDeviceCategory, t.DevicePairedCategoryId, t.OriginalBalance, t.BalanceConfirmedAt, Contract = t.Contract
    };

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}
