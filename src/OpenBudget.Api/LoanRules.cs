using System.Globalization;

namespace OpenBudget.Api;

// Mortgages and loans (plus device/service contracts) are budget accounts that hold debt you are paying off.
//   - Opening balances and loan-only activity (interest, fees, balance adjustments) change the debt but never
//     Ready to Assign or category activity; loan transactions cannot carry a category.
//   - A transfer between a loan and a non-loan account is a payment (or a draw when money leaves the loan).
//     With a paired Plan category the payment is spending from that category; without one it reduces
//     Ready to Assign, so budget accounts always equal Ready to Assign plus category Available.
//   - Pairing is optional and one-to-one: a category is paired with at most one loan slot. A contract with a device
//     may pair a separate device category; each payment is then split by SplitContractPayment.
//   - Interest is estimated, never posted: estimate = debt x annual rate / 12 / 100, rounded half-even to cents.
//   - Projections are read-only decimal simulations that start with next month's payment.
public static class LoanRules
{
    public const string Contract = "contract";
    public static readonly string[] LoanTypes = ["mortgage", "autoLoan", "studentLoan", "personalLoan", "medicalDebt", "otherDebt", Contract];
    // Legacy: loans created before optional pairing were auto-paired with "loan-{accountId}" in this group.
    // Those pairings are kept (and their categories kept present) but new loans never create them.
    public const string PaymentGroupId = "loan-payments";
    public const string PaymentGroupName = "Loans";
    public const int MaxCategoryNameLength = 160;
    public const int MaxGroupNameLength = 120;
    public const string CategoryPrefix = "loan-";
    // 100 years: a payment that cannot clear the debt within this horizon is reported as never paid off.
    public const int MaxMonths = 1200;
    public const int MaxContractMonths = 60;

    public static bool IsLoan(string? accountType) => accountType is not null && LoanTypes.Contains(accountType);
    public static bool IsContract(string? accountType) => accountType == Contract;
    public static string Family(string accountType) => IsLoan(accountType) ? "loan" : CreditCardRules.IsCredit(accountType) ? "credit" : LedgerRules.IsTracking(accountType) ? "tracking" : "cash";
    public static string AutoCategoryId(Guid accountId) => CategoryPrefix + accountId.ToString("D");
    public static bool IsRate(decimal rate) => rate is >= 0 and <= 100 && decimal.Round(rate, 3) == rate;

    public static decimal MonthlyInterest(decimal debt, decimal annualRatePercent) =>
        debt <= 0 || annualRatePercent <= 0 ? 0 : decimal.Round(debt * annualRatePercent / 1200m, 2, MidpointRounding.ToEven);

    public static string? ValidateTerms(string accountType, LoanTermsRequest? terms)
    {
        if (terms is null) return "Loan accounts require loan details.";
        if (!IsRate(terms.InterestRate)) return "Interest rate must be between 0 and 100 with at most three decimal places.";
        if (!LedgerRules.IsMoney(terms.MinimumPayment) || terms.MinimumPayment < 0 || !LedgerRules.IsMoney(terms.EscrowAmount) || terms.EscrowAmount < 0)
            return "Monthly payment and escrow must be non-negative amounts with at most two decimal places.";
        if (ValidatePairing(accountType, terms) is { } pairingError) return pairingError;
        if (IsContract(accountType))
        {
            if (terms.Contract is null) return "Contract accounts require contract details.";
            if (terms.InterestRate != 0 || terms.MinimumPayment != 0 || terms.EscrowAmount != 0)
                return "Contracts are interest-free and derive their monthly payment from the contract schedule.";
            return ValidateContract(terms.Contract);
        }
        if (terms.Contract is not null) return "Only contract accounts take contract details.";
        if (terms.MinimumPayment <= 0) return "Enter the monthly payment required by your lender.";
        if (terms.EscrowAmount >= terms.MinimumPayment) return "The escrow or fee amount must be less than the monthly payment.";
        return null;
    }

    // Shape of the pairing request; existence, activity, protection and one-to-one checks need the database (LoanService).
    public static string? ValidatePairing(string accountType, LoanTermsRequest terms)
    {
        if (terms.PairedCategoryId?.Length > 100 || terms.DevicePairedCategoryId?.Length > 100) return "Category identifier is too long.";
        if (terms.PairedCategoryId is not null && terms.NewPairedCategory is not null || terms.DevicePairedCategoryId is not null && terms.NewDevicePairedCategory is not null)
            return "Choose an existing category or create a new one, not both.";
        if (terms.SeparateDeviceCategory && !(IsContract(accountType) && terms.Contract?.IncludesDevice == true))
            return "Separate device categories are only for contracts that include a device.";
        if (!terms.SeparateDeviceCategory && (terms.DevicePairedCategoryId is not null || terms.NewDevicePairedCategory is not null))
            return "Pair a device category only when the device uses a separate category.";
        if (terms.SeparateDeviceCategory && terms.PairedCategoryId is not null && terms.PairedCategoryId == terms.DevicePairedCategoryId)
            return "Choose different categories for the contract and the device.";
        foreach (var created in new[] { terms.NewPairedCategory, terms.NewDevicePairedCategory })
            if (created is not null && ValidateNewCategory(created) is { } error) return error;
        if (terms.NewPairedCategory is { } a && terms.NewDevicePairedCategory is { } b && SameName(a.Name, b.Name)
            && (a.GroupId is not null ? a.GroupId == b.GroupId : b.NewGroupName is not null && SameName(a.NewGroupName!, b.NewGroupName)))
            return "The contract and device categories need different names.";
        return null;
    }

    public static string? ValidateNewCategory(NewPairedCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > MaxCategoryNameLength)
            return $"Category name is required (maximum {MaxCategoryNameLength} characters).";
        if ((request.GroupId is null) == (request.NewGroupName is null)) return "Choose a category group or name a new one.";
        if (request.GroupId is { } groupId && (string.IsNullOrWhiteSpace(groupId) || groupId.Length > 100)) return "Category group identifier is invalid.";
        if (request.NewGroupName is { } group && (string.IsNullOrWhiteSpace(group) || group.Trim().Length > MaxGroupNameLength))
            return $"Category group name is required (maximum {MaxGroupNameLength} characters).";
        return null;
    }

    // Groups whose membership the server owns; a loan category can neither be chosen from nor created in them.
    public static bool IsProtectedGroup(string groupId) => groupId is CreditCardRules.PaymentGroupId or WishlistRules.GroupId;
    public static bool IsProtectedCategory(string categoryId) => CreditCardRules.IsPaymentCategory(categoryId) || WishlistRules.IsWishlistCategory(categoryId);
    public static bool SameName(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public static string? ValidateContract(ContractTermsRequest contract)
    {
        if (contract.Months is < 1 or > MaxContractMonths) return $"Contract length must be 1-{MaxContractMonths} months.";
        if (contract.StartDate.Year is < 1900 or > 9990) return "Contract start date is invalid.";
        decimal[] money = [contract.UpfrontCost, contract.MonthlyServiceCost, contract.DeviceTotal, contract.DiscountedServiceCost ?? 0];
        if (money.Any(value => !LedgerRules.IsMoney(value) || value < 0)) return "Contract amounts must be non-negative with at most two decimal places.";
        if (!contract.IncludesDevice && contract.DeviceTotal != 0) return "Device cost applies only when the contract includes a device.";
        if (contract.DiscountMonths < 0 || contract.DiscountMonths > contract.Months) return "Discount months must be between 0 and the contract length.";
        if ((contract.DiscountMonths > 0) != (contract.DiscountedServiceCost is not null))
            return "A discount needs both the number of months and the discounted monthly amount.";
        if (contract.DiscountedServiceCost > contract.MonthlyServiceCost) return "The discounted monthly amount cannot exceed the regular contract cost.";
        if (ContractSchedule(contract).Sum(item => item.Total) + contract.UpfrontCost <= 0) return "A contract needs a cost.";
        return null;
    }

    public sealed record Projection(int? Months, decimal TotalInterest, decimal FinalPayment, IReadOnlyList<decimal> Balances)
    {
        public bool NeverPaidOff => Months is null;
    }

    // Month 0 is the current debt (after any one-time extra). Each later month adds the interest estimate
    // on the previous debt and subtracts the principal payment (payment minus escrow/fees); the final payment
    // is capped at what is owed. Stops as never paid off when a month does not reduce the debt.
    public static Projection Project(decimal debt, decimal annualRatePercent, decimal principalPayment, decimal extraNow = 0)
    {
        var balance = Math.Max(debt - Math.Max(extraNow, 0), 0);
        var balances = new List<decimal> { balance };
        decimal interestTotal = 0;
        if (balance == 0) return new(0, 0, 0, balances);
        for (var month = 1; month <= MaxMonths; month++)
        {
            var interest = MonthlyInterest(balance, annualRatePercent);
            var next = balance + interest - principalPayment;
            if (next <= 0)
            {
                interestTotal += interest;
                balances.Add(0);
                return new(month, interestTotal, balance + interest, balances);
            }
            if (next >= balance) return new(null, 0, 0, balances);
            interestTotal += interest;
            balance = next;
            balances.Add(balance);
        }
        return new(null, 0, 0, balances);
    }

    // Smallest whole-cent principal payment that clears the debt within the given number of payments.
    // Starts from the annuity formula P*r/(1-(1+r)^-n) rounded up to the cent and corrects for monthly
    // interest rounding by adding cents until the simulation finishes in time.
    public static decimal PrincipalPaymentFor(decimal debt, decimal annualRatePercent, int months)
    {
        if (months < 1) throw new ArgumentOutOfRangeException(nameof(months));
        if (debt <= 0) return 0;
        decimal payment;
        if (annualRatePercent == 0) payment = CeilingCents(debt / months);
        else
        {
            var rate = annualRatePercent / 1200m;
            decimal growth = 1;
            for (var i = 0; i < months; i++) growth *= 1 + rate;
            payment = CeilingCents(debt * rate * growth / (growth - 1));
        }
        for (var guard = 0; guard < 500; guard++)
        {
            var result = Project(debt, annualRatePercent, payment);
            if (result.Months is { } taken && taken <= months) break;
            payment += 0.01m;
        }
        return payment;
    }

    public sealed record ContractItem(int Number, DateOnly DueDate, decimal Service, decimal Device, bool IsDiscounted)
    {
        public decimal Total => Service + Device;
    }

    // Schedule item N is due on the start date plus N-1 months (day clamped to the month length). The device
    // total is spread in equal whole-cent instalments with the final month absorbing the remainder. A discount
    // replaces the monthly contract (service) cost for the first N months; the device instalment is unchanged.
    public static List<ContractItem> ContractSchedule(ContractTermsRequest contract)
    {
        var months = Math.Clamp(contract.Months, 1, MaxContractMonths);
        var device = contract.IncludesDevice ? contract.DeviceTotal : 0;
        var instalment = Math.Floor(device * 100 / months) / 100;
        var items = new List<ContractItem>(months);
        for (var number = 1; number <= months; number++)
        {
            var discounted = number <= contract.DiscountMonths && contract.DiscountedServiceCost is not null;
            var deviceAmount = number == months ? device - instalment * (months - 1) : instalment;
            items.Add(new(number, contract.StartDate.AddMonths(number - 1), discounted ? contract.DiscountedServiceCost!.Value : contract.MonthlyServiceCost, deviceAmount, discounted));
        }
        return items;
    }

    public sealed record ContractPosition(decimal RemainingDevice, decimal RemainingService, int RemainingMonths, decimal TotalCost, DateOnly EndDate)
    {
        public decimal Remaining => RemainingDevice + RemainingService;
    }

    // Remaining commitment as of a date: schedule items due on or after that date, plus the upfront cost when
    // the contract starts after that date (it is paid at signing). The upfront cost counts as device cost when a device is included.
    public static ContractPosition ContractPositionAt(ContractTermsRequest contract, DateOnly asOf)
    {
        var schedule = ContractSchedule(contract);
        var open = schedule.Where(item => item.DueDate >= asOf).ToList();
        var upfront = contract.StartDate > asOf ? contract.UpfrontCost : 0;
        var device = open.Sum(item => item.Device) + (contract.IncludesDevice ? upfront : 0);
        var service = open.Sum(item => item.Service) + (contract.IncludesDevice ? 0 : upfront);
        return new(device, service, open.Count, contract.UpfrontCost + schedule.Sum(item => item.Total), schedule[^1].DueDate);
    }

    // Classifies one month's loan transfers. Payments into a loan (positive loan leg) spend from the paired
    // category; draws (negative loan leg) return money to it. Unpaired or missing categories use Ready to Assign.
    // Transfers between two loan accounts are budget-neutral.
    public sealed record Flows(IReadOnlyDictionary<string, decimal> CategoryActivity, decimal Income);

    // A loan's pairing as the projection uses it. DeviceCategoryId applies only when SeparateDevice is set on a contract
    // that includes a device; a null category means that share uses Ready to Assign.
    public sealed record Pairing(string? CategoryId, bool SeparateDevice = false, string? DeviceCategoryId = null, ContractTermsRequest? Contract = null);

    // Scheduled (contract/service, device) amounts for a calendar month: schedule items due in that month, plus the
    // upfront cost in the start month (device cost when the contract includes a device, contract cost otherwise).
    public static (decimal Service, decimal Device) ScheduledInMonth(ContractTermsRequest contract, string month)
    {
        var items = ContractSchedule(contract).Where(item => Month(item.DueDate) == month).ToList();
        decimal service = items.Sum(item => item.Service), device = items.Sum(item => item.Device);
        if (Month(contract.StartDate) == month)
        {
            if (contract.IncludesDevice) device += contract.UpfrontCost; else service += contract.UpfrontCost;
        }
        return (service, device);
    }

    // Splits one payment (positive) or draw (negative) of a contract with a separate device category. The ratio is the
    // payment month's scheduled contract vs device amounts; a month with nothing scheduled (before the start or after
    // the end) uses the whole contract's totals instead. The contract part is amount x contract / (contract + device),
    // truncated toward zero to whole cents; the device part is the remainder, so the parts always add up exactly, any
    // rounding cent goes to the device, and a draw of the same amount reverses a payment exactly.
    public static (decimal Contract, decimal Device) SplitContractPayment(decimal amount, ContractTermsRequest contract, DateOnly date)
    {
        var (service, device) = ScheduledInMonth(contract, Month(date));
        if (service + device <= 0)
        {
            var schedule = ContractSchedule(contract);
            service = schedule.Sum(item => item.Service) + (contract.IncludesDevice ? 0 : contract.UpfrontCost);
            device = schedule.Sum(item => item.Device) + (contract.IncludesDevice ? contract.UpfrontCost : 0);
        }
        if (device <= 0) return (amount, 0);
        if (service <= 0) return (0, amount);
        var part = decimal.Truncate(amount * service / (service + device) * 100) / 100;
        return (part, amount - part);
    }

    public static Flows Project(IEnumerable<TransactionEntity> monthEntries, IReadOnlyDictionary<Guid, Pairing> loanPairs, Func<string, bool> categoryExists)
    {
        var activity = new Dictionary<string, decimal>(StringComparer.Ordinal);
        decimal income = 0;
        void Apply(string? categoryId, decimal amount)
        {
            if (amount == 0) return;
            if (categoryId is not null && categoryExists(categoryId)) activity[categoryId] = activity.GetValueOrDefault(categoryId) - amount;
            else income -= amount;
        }
        var transfers = monthEntries.Where(t => !t.IsDeleted && t.TransferId is not null).GroupBy(t => t.TransferId!.Value);
        foreach (var legs in transfers)
        {
            var pair = legs.ToList();
            if (pair.Count != 2) continue;
            foreach (var leg in pair)
            {
                if (!loanPairs.TryGetValue(leg.AccountId, out var pairing)) continue;
                var other = pair.Single(t => t.Id != leg.Id);
                if (loanPairs.ContainsKey(other.AccountId)) continue;
                // A payment the user categorized explicitly is spending from that category (counted by the ledger activity), not the paired one.
                if (other.CategoryId is not null) continue;
                if (pairing.SeparateDevice && pairing.Contract is { IncludesDevice: true } contract)
                {
                    var (service, device) = SplitContractPayment(leg.Amount, contract, leg.Date);
                    Apply(pairing.CategoryId, service);
                    Apply(pairing.DeviceCategoryId, device);
                }
                else Apply(pairing.CategoryId, leg.Amount);
            }
        }
        return new(activity, income);
    }

    // Keeps the legacy Loans group and each legacy automatically paired loan category present in a month.
    public static bool EnsureCategories(BudgetEntity month, IReadOnlyList<(AccountEntity Account, string CategoryId)> autoPaired)
    {
        var changed = false;
        foreach (var (account, categoryId) in autoPaired)
        {
            if (month.Groups.SelectMany(g => g.Categories).Any(c => c.Id == categoryId)) continue;
            var group = month.Groups.SingleOrDefault(g => g.Id == PaymentGroupId);
            if (group is null)
            {
                group = new CategoryGroupEntity { BudgetId = month.BudgetId, BudgetMonth = month.Month, Id = PaymentGroupId, Name = PaymentGroupName,
                    SortOrder = month.Groups.Count == 0 ? 0 : month.Groups.Max(g => g.SortOrder) + 1 };
                month.Groups.Add(group);
            }
            group.Categories.Add(new CategoryEntity { BudgetId = month.BudgetId, BudgetMonth = month.Month, GroupId = group.Id, Id = categoryId,
                Name = account.Name, SortOrder = group.Categories.Count == 0 ? 0 : group.Categories.Max(c => c.SortOrder) + 1 });
            changed = true;
        }
        return changed;
    }

    public static string Month(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    public static DateOnly MonthStart(string month) => DateOnly.ParseExact($"{month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string AddMonths(string month, int months) => Month(MonthStart(month).AddMonths(months));
    public static int MonthsBetween(string from, string to)
    {
        var a = MonthStart(from); var b = MonthStart(to);
        return (b.Year - a.Year) * 12 + b.Month - a.Month;
    }
    private static decimal CeilingCents(decimal value) => Math.Ceiling(value * 100) / 100;
}

public sealed class LoanTermsEntity
{
    public string BudgetId { get; set; } = BudgetRequestContext.DefaultBudgetId;
    public Guid AccountId { get; set; }
    public decimal InterestRate { get; set; }
    public decimal MinimumPayment { get; set; }
    public decimal EscrowAmount { get; set; }
    public string? PairedCategoryId { get; set; }
    // Contracts with a device: when set, the device share of each payment uses DevicePairedCategoryId (null = Ready to Assign).
    public bool SeparateDeviceCategory { get; set; }
    public string? DevicePairedCategoryId { get; set; }
    // Debt when the account was created (contracts: the total contract cost); the base for "% remaining".
    public decimal OriginalBalance { get; set; }
    public DateTimeOffset? BalanceConfirmedAt { get; set; }
    public int? ContractMonths { get; set; }
    public DateOnly? ContractStartDate { get; set; }
    public bool IncludesDevice { get; set; }
    public decimal UpfrontCost { get; set; }
    public decimal MonthlyServiceCost { get; set; }
    public decimal DeviceTotal { get; set; }
    public int DiscountMonths { get; set; }
    public decimal? DiscountedServiceCost { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ContractTermsRequest? Contract => ContractMonths is { } months && ContractStartDate is { } start
        ? new(months, start, IncludesDevice, UpfrontCost, MonthlyServiceCost, DeviceTotal, DiscountMonths, DiscountedServiceCost) : null;
}
