using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public sealed class LoanRulesTests
{
    private static readonly Guid Loan = Guid.Parse("00000000-0000-0000-0000-00000000001a");
    private static readonly Guid OtherLoan = Guid.Parse("00000000-0000-0000-0000-00000000001b");
    private static readonly Guid Cash = Guid.Parse("00000000-0000-0000-0000-00000000000a");

    [Theory]
    [InlineData("390312.52", "3.47", "1128.65")]
    [InlineData("389651.68", "3.47", "1126.74")]
    [InlineData("1000", "0", "0")]
    [InlineData("0", "5", "0")]
    // 3.000 / 1200 x 100.10 = 0.25025 rounds half-even to 0.25.
    [InlineData("100.10", "3", "0.25")]
    public void Monthly_interest_estimate_is_debt_times_rate_over_twelve_rounded_half_even(string debt, string rate, string expected) =>
        Assert.Equal(Parse(expected), LoanRules.MonthlyInterest(Parse(debt), Parse(rate)));

    [Fact]
    public void Reference_mortgage_pays_off_in_344_months_with_the_documented_interest()
    {
        var result = LoanRules.Project(389_651.68m, 3.47m, 1_789.49m);
        Assert.Equal(344, result.Months);
        Assert.Equal(225_929.45m, result.TotalInterest);
        Assert.Equal(1_786.06m, result.FinalPayment);
        Assert.Equal(345, result.Balances.Count);
        Assert.Equal(389_651.68m, result.Balances[0]);
        // 389,651.68 + 1,126.74 interest - 1,789.49 payment.
        Assert.Equal(388_988.93m, result.Balances[1]);
        Assert.Equal(0, result.Balances[^1]);
    }

    [Fact]
    public void Small_loan_schedule_caps_the_final_payment_at_what_is_owed()
    {
        var result = LoanRules.Project(5_000m, 6m, 500m);
        Assert.Equal(11, result.Months);
        Assert.Equal(142.39m, result.TotalInterest);
        Assert.Equal(142.39m, result.FinalPayment);
    }

    [Fact]
    public void Interest_free_debt_is_divided_by_the_payment()
    {
        var result = LoanRules.Project(1_000m, 0, 300m);
        Assert.Equal(4, result.Months);
        Assert.Equal(0, result.TotalInterest);
        Assert.Equal(100m, result.FinalPayment);
        Assert.Equal(0, LoanRules.Project(0, 5m, 100m).Months);
    }

    [Fact]
    public void Payment_that_does_not_exceed_monthly_interest_never_pays_off()
    {
        var result = LoanRules.Project(100_000m, 12m, 1_000m);
        Assert.True(result.NeverPaidOff);
        Assert.Null(result.Months);
        Assert.True(LoanRules.Project(100_000m, 12m, 0).NeverPaidOff);
        // One cent above the interest pays off, but takes 1,166 months.
        Assert.Equal(1_166, LoanRules.Project(100_000m, 12m, 1_000.01m).Months);
        // A payment that would need more than 100 years is reported as never paid off.
        Assert.True(LoanRules.Project(100_000m, 12m, 1_000.001m).NeverPaidOff);
    }

    [Fact]
    public void One_time_extra_this_month_reduces_the_starting_debt()
    {
        var result = LoanRules.Project(389_651.68m, 3.47m, 1_789.49m, 10_000m);
        Assert.Equal(330, result.Months);
        Assert.Equal(209_462.96m, result.TotalInterest);
        Assert.Equal(379_651.68m, result.Balances[0]);
        Assert.Equal(0, LoanRules.Project(500m, 5m, 100m, 600m).Months);
    }

    [Fact]
    public void Payment_for_a_payoff_date_is_the_smallest_cent_that_finishes_in_time()
    {
        Assert.Equal(1_789.49m, LoanRules.PrincipalPaymentFor(389_651.68m, 3.47m, 344));
        var twentyYears = LoanRules.PrincipalPaymentFor(389_651.68m, 3.47m, 240);
        Assert.Equal(2_253.83m, twentyYears);
        Assert.Equal(240, LoanRules.Project(389_651.68m, 3.47m, twentyYears).Months);
        Assert.NotEqual(240, LoanRules.Project(389_651.68m, 3.47m, twentyYears - 0.01m).Months);
        Assert.Equal(333.34m, LoanRules.PrincipalPaymentFor(1_000m, 0, 3));
        Assert.Equal(0, LoanRules.PrincipalPaymentFor(0, 3m, 12));
    }

    [Fact]
    public void Loan_terms_validation_covers_rate_money_escrow_and_contract_rules()
    {
        var valid = new LoanTermsRequest(3.47m, 1_789.49m, 100m);
        Assert.Null(LoanRules.ValidateTerms("mortgage", valid));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", null));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", valid with { InterestRate = 100.001m }));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", valid with { InterestRate = 3.4705m }));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", valid with { InterestRate = -1 }));
        Assert.Null(LoanRules.ValidateTerms("mortgage", valid with { InterestRate = 100 }));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", valid with { MinimumPayment = 0 }));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", valid with { MinimumPayment = 1.001m }));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", valid with { EscrowAmount = 1_789.49m }));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", valid with { PairedCategoryId = "rent", NewPairedCategory = new("Mortgage", "home") }));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", valid with { Contract = Phone() }));
        var contract = new LoanTermsRequest(0, 0, Contract: Phone());
        Assert.Null(LoanRules.ValidateTerms("contract", contract));
        Assert.NotNull(LoanRules.ValidateTerms("contract", contract with { InterestRate = 1 }));
        Assert.NotNull(LoanRules.ValidateTerms("contract", contract with { Contract = null }));
        Assert.NotNull(LoanRules.ValidateTerms("contract", contract with { Contract = Phone() with { Months = 61 } }));
        Assert.NotNull(LoanRules.ValidateTerms("contract", contract with { Contract = Phone() with { Months = 0 } }));
        Assert.NotNull(LoanRules.ValidateTerms("contract", contract with { Contract = Phone() with { DiscountMonths = 25 } }));
        Assert.NotNull(LoanRules.ValidateTerms("contract", contract with { Contract = Phone() with { DiscountedServiceCost = null } }));
        Assert.NotNull(LoanRules.ValidateTerms("contract", contract with { Contract = Phone() with { DiscountedServiceCost = 25m } }));
        Assert.NotNull(LoanRules.ValidateTerms("contract", contract with { Contract = Phone() with { IncludesDevice = false } }));
        Assert.NotNull(LoanRules.ValidateTerms("contract", contract with { Contract = new(12, new(2026, 1, 1), false, 0, 0, 0) }));
    }

    [Fact]
    public void Contract_schedule_spreads_the_device_and_applies_the_discount_first()
    {
        var schedule = LoanRules.ContractSchedule(Phone());
        Assert.Equal(24, schedule.Count);
        Assert.Equal(new DateOnly(2026, 1, 31), schedule[0].DueDate);
        Assert.Equal(new DateOnly(2026, 2, 28), schedule[1].DueDate);
        Assert.All(schedule.Take(6), item => { Assert.True(item.IsDiscounted); Assert.Equal(10m, item.Service); });
        Assert.All(schedule.Skip(6), item => { Assert.False(item.IsDiscounted); Assert.Equal(20m, item.Service); });
        Assert.All(schedule.SkipLast(1), item => Assert.Equal(24.99m, item.Device));
        Assert.Equal(25.22m, schedule[^1].Device);
        Assert.Equal(599.99m, schedule.Sum(item => item.Device));
        Assert.Equal(420m, schedule.Sum(item => item.Service));
        Assert.Equal(34.99m, schedule[0].Total);
    }

    [Fact]
    public void Contract_position_separates_remaining_device_and_contract_cost()
    {
        var phone = Phone();
        var start = LoanRules.ContractPositionAt(phone, phone.StartDate);
        Assert.Equal(1_019.99m, start.Remaining);
        Assert.Equal(1_069.99m, start.TotalCost);
        Assert.Equal(24, start.RemainingMonths);
        Assert.Equal(new DateOnly(2027, 12, 31), start.EndDate);

        // Items 10-24 (due 31 Oct 2026 onwards) are still open on 30 October 2026.
        var october = LoanRules.ContractPositionAt(phone, new DateOnly(2026, 10, 30));
        Assert.Equal(15, october.RemainingMonths);
        Assert.Equal(375.08m, october.RemainingDevice);
        Assert.Equal(300m, october.RemainingService);
        Assert.Equal(675.08m, october.Remaining);

        // Before the contract starts the upfront cost is still owed (device cost when a device is included).
        var before = LoanRules.ContractPositionAt(phone, new DateOnly(2025, 12, 1));
        Assert.Equal(649.99m, before.RemainingDevice);
        Assert.Equal(1_069.99m, before.Remaining);
        Assert.Equal(0, LoanRules.ContractPositionAt(phone, new DateOnly(2028, 1, 1)).Remaining);

        var simOnly = new ContractTermsRequest(12, new DateOnly(2026, 3, 15), false, 0, 15m, 0);
        var sim = LoanRules.ContractPositionAt(simOnly, simOnly.StartDate);
        Assert.Equal(180m, sim.RemainingService);
        Assert.Equal(0, sim.RemainingDevice);
    }

    [Fact]
    public void Loan_payments_spend_from_the_paired_category_and_unpaired_payments_use_ready_to_assign()
    {
        var pairs = new Dictionary<Guid, LoanRules.Pairing> { [Loan] = new("loan-1"), [OtherLoan] = new(null) };
        var payment = Guid.NewGuid();
        var draw = Guid.NewGuid();
        var unpaired = Guid.NewGuid();
        var between = Guid.NewGuid();
        TransactionEntity[] entries =
        [
            Leg(Cash, -500, payment), Leg(Loan, 500, payment),
            Leg(Loan, -50, draw), Leg(Cash, 50, draw),
            Leg(Cash, -200, unpaired), Leg(OtherLoan, 200, unpaired),
            Leg(Loan, -30, between), Leg(OtherLoan, 30, between),
            new() { Id = Guid.NewGuid(), AccountId = Loan, Amount = -1_128.65m, Memo = "Interest" }
        ];
        var flows = LoanRules.Project(entries, pairs, id => id == "loan-1");
        Assert.Equal(-450m, flows.CategoryActivity["loan-1"]);
        Assert.Equal(-200m, flows.Income);

        var missing = LoanRules.Project(entries, pairs, _ => false);
        Assert.Empty(missing.CategoryActivity);
        Assert.Equal(-650m, missing.Income);
    }

    [Theory]
    // Phone: starts 31 Jan 2026 (upfront 50 counts as device), service 10 for Jan-Jun 2026 then 20, device 24.99/month (25.22 in Dec 2027).
    [InlineData("44.99", "2026-10-31", "20.00", "24.99")]  // exactly the scheduled amounts
    [InlineData("50.00", "2026-10-02", "22.22", "27.78")]  // 50 x 20 / 44.99 = 22.227 -> 22.22; the cent goes to the device
    [InlineData("34.99", "2026-03-31", "10.00", "24.99")]  // discount month: 10 vs 24.99
    [InlineData("100.00", "2026-03-01", "28.57", "71.43")] // discount month ratio 10 / 34.99
    [InlineData("84.99", "2026-01-31", "10.00", "74.99")]  // start month adds the upfront cost to the device side
    [InlineData("-50.00", "2026-10-15", "-22.22", "-27.78")] // a draw reverses a payment of the same amount exactly
    [InlineData("100.00", "2028-03-01", "39.25", "60.75")]  // nothing scheduled: whole-contract ratio 420 vs 649.99
    [InlineData("100.00", "2025-11-01", "39.25", "60.75")]  // before the start date: same whole-contract ratio
    [InlineData("0.01", "2026-10-01", "0.00", "0.01")]     // a single cent always goes to the device
    public void Contract_payments_split_by_the_months_scheduled_contract_and_device_amounts(string amount, string date, string contract, string device)
    {
        var (service, deviceShare) = LoanRules.SplitContractPayment(Parse(amount), Phone(), DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(Parse(contract), service);
        Assert.Equal(Parse(device), deviceShare);
        Assert.Equal(Parse(amount), service + deviceShare);
    }

    [Fact]
    public void Contract_split_handles_zero_sided_months_and_scheduled_amounts()
    {
        // A free discount month puts the whole payment on the device; a contract without a device keeps it all on the contract.
        Assert.Equal((0m, 34.99m), LoanRules.SplitContractPayment(34.99m, Phone() with { DiscountedServiceCost = 0 }, new DateOnly(2026, 3, 31)));
        var simOnly = new ContractTermsRequest(12, new DateOnly(2026, 3, 15), false, 5m, 15m, 0);
        Assert.Equal((15m, 0m), LoanRules.SplitContractPayment(15m, simOnly, new DateOnly(2026, 6, 15)));
        Assert.Equal((20m, 0m), LoanRules.ScheduledInMonth(simOnly, "2026-03"));
        Assert.Equal((10m, 74.99m), LoanRules.ScheduledInMonth(Phone(), "2026-01"));
        Assert.Equal((20m, 25.22m), LoanRules.ScheduledInMonth(Phone(), "2027-12"));
        Assert.Equal((0m, 0m), LoanRules.ScheduledInMonth(Phone(), "2028-01"));
    }

    [Fact]
    public void Separate_device_pairing_splits_contract_payments_between_two_categories()
    {
        var payment = Guid.NewGuid();
        var draw = Guid.NewGuid();
        TransactionEntity[] entries =
        [
            Leg(Cash, -50, payment, new(2026, 10, 2)), Leg(Loan, 50, payment, new(2026, 10, 2)),
            Leg(Loan, -10, draw, new(2026, 10, 20)), Leg(Cash, 10, draw, new(2026, 10, 20))
        ];
        var both = new Dictionary<Guid, LoanRules.Pairing> { [Loan] = new("phone", true, "device", Phone()) };
        var flows = LoanRules.Project(entries, both, _ => true);
        // Payment 50 -> 22.22 / 27.78; draw 10 -> 4.44 / 5.56 back.
        Assert.Equal(-17.78m, flows.CategoryActivity["phone"]);
        Assert.Equal(-22.22m, flows.CategoryActivity["device"]);
        Assert.Equal(0, flows.Income);

        // An unpaired device slot sends the device share to Ready to Assign; turning separation off uses one category.
        var deviceUnpaired = LoanRules.Project(entries, new Dictionary<Guid, LoanRules.Pairing> { [Loan] = new("phone", true, null, Phone()) }, _ => true);
        Assert.Equal(-17.78m, deviceUnpaired.CategoryActivity["phone"]);
        Assert.Equal(-22.22m, deviceUnpaired.Income);
        var single = LoanRules.Project(entries, new Dictionary<Guid, LoanRules.Pairing> { [Loan] = new("phone", false, "device", Phone()) }, _ => true);
        Assert.Equal(-40m, single.CategoryActivity["phone"]);
        Assert.False(single.CategoryActivity.ContainsKey("device"));
    }

    [Fact]
    public void Pairing_validation_rejects_ambiguous_protected_and_invalid_requests()
    {
        var contract = new LoanTermsRequest(0, 0, Contract: Phone());
        var mortgage = new LoanTermsRequest(3m, 500m);
        Assert.Null(LoanRules.ValidateTerms("contract", contract with { PairedCategoryId = "phone", SeparateDeviceCategory = true, DevicePairedCategoryId = "device" }));
        Assert.Null(LoanRules.ValidateTerms("contract", contract with { NewPairedCategory = new("Phone", null, "Bills"), SeparateDeviceCategory = true,
            NewDevicePairedCategory = new("Device", null, "Bills") }));
        Assert.Null(LoanRules.ValidateTerms("mortgage", mortgage with { NewPairedCategory = new("Mortgage", "home") }));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", mortgage with { SeparateDeviceCategory = true }));
        Assert.NotNull(LoanRules.ValidateTerms("contract", contract with { SeparateDeviceCategory = true, Contract = Phone() with { IncludesDevice = false, DeviceTotal = 0 } }));
        Assert.NotNull(LoanRules.ValidateTerms("contract", contract with { DevicePairedCategoryId = "device" }));
        Assert.NotNull(LoanRules.ValidateTerms("contract", contract with { SeparateDeviceCategory = true, PairedCategoryId = "same", DevicePairedCategoryId = "same" }));
        Assert.NotNull(LoanRules.ValidateTerms("contract", contract with { SeparateDeviceCategory = true, DevicePairedCategoryId = "d", NewDevicePairedCategory = new("D", "home") }));
        Assert.NotNull(LoanRules.ValidateTerms("contract", contract with { NewPairedCategory = new("Phone", "home"), SeparateDeviceCategory = true,
            NewDevicePairedCategory = new(" phone ", "home") }));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", mortgage with { NewPairedCategory = new(" ", "home") }));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", mortgage with { NewPairedCategory = new(new string('x', 161), "home") }));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", mortgage with { NewPairedCategory = new("Mortgage") }));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", mortgage with { NewPairedCategory = new("Mortgage", "home", "Debts") }));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", mortgage with { NewPairedCategory = new("Mortgage", null, new string('g', 121)) }));
        Assert.NotNull(LoanRules.ValidateTerms("mortgage", mortgage with { PairedCategoryId = new string('c', 101) }));
        Assert.True(LoanRules.IsProtectedGroup(CreditCardRules.PaymentGroupId));
        Assert.True(LoanRules.IsProtectedGroup(WishlistRules.GroupId));
        Assert.False(LoanRules.IsProtectedGroup("home"));
        Assert.True(LoanRules.IsProtectedCategory(CreditCardRules.PaymentCategoryId(Loan)));
        Assert.True(LoanRules.IsProtectedCategory(WishlistRules.CategoryId(Loan)));
        Assert.False(LoanRules.IsProtectedCategory("rent"));
    }

    [Fact]
    public void Account_families_and_types_are_classified()
    {
        Assert.All(LoanRules.LoanTypes, type => Assert.Equal("loan", LoanRules.Family(type)));
        Assert.Equal("credit", LoanRules.Family("creditCard"));
        Assert.Equal("cash", LoanRules.Family("savings"));
        Assert.True(LoanRules.IsContract("contract"));
        Assert.False(LoanRules.IsLoan(null));
    }

    private static decimal Parse(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private static ContractTermsRequest Phone() => new(24, new DateOnly(2026, 1, 31), true, 50m, 20m, 599.99m, 6, 10m);

    private static TransactionEntity Leg(Guid account, decimal amount, Guid transfer, DateOnly date = default) =>
        new() { Id = Guid.NewGuid(), AccountId = account, Amount = amount, TransferId = transfer, Date = date };
}
