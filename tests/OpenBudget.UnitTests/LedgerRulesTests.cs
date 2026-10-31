using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public sealed class LedgerRulesTests
{
    [Theory]
    [InlineData("0", true)]
    [InlineData("0.01", true)]
    [InlineData("-999999999.99", true)]
    [InlineData("999999999.99", true)]
    [InlineData("1000000000", false)]
    [InlineData("-1000000000", false)]
    [InlineData("1.001", false)]
    public void Money_rejects_precision_loss_and_overflow(string value, bool expected) =>
        Assert.Equal(expected, LedgerRules.IsMoney(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Activity_and_income_exclude_transfers_and_deleted_entries_and_include_refunds()
    {
        TransactionEntity[] entries = [
            new() { Amount = 100 },
            new() { Amount = -7 },
            new() { Amount = -30, CategoryId = "food" },
            new() { Amount = 5, CategoryId = "food" },
            new() { Amount = -20, Splits = [new() { CategoryId = "food", Amount = -15 }, new() { CategoryId = "home", Amount = -5 }] },
            new() { Amount = -900, CategoryId = "food", IsDeleted = true },
            new() { Amount = 200, TransferId = Guid.NewGuid() }
        ];
        Assert.Equal(93, entries.Sum(LedgerRules.Income));
        var activity = LedgerRules.Activity(entries);
        Assert.Equal(-40, activity["food"]);
        Assert.Equal(-5, activity["home"]);
        Assert.Equal(2, activity.Count);
    }

    [Theory]
    [InlineData(-1, false, false, true)]
    [InlineData(1, false, false, false)]
    [InlineData(-1, true, false, false)]
    [InlineData(-1, false, true, false)]
    public void Only_uncategorized_spending_needs_attention(decimal amount, bool opening, bool transfer, bool expected) =>
        Assert.Equal(expected, LedgerRules.NeedsAttention(new() { Amount = amount, IsOpening = opening, TransferId = transfer ? Guid.NewGuid() : null }));

    [Fact]
    public void Balances_distinguish_cleared_and_reconciled_from_uncleared()
    {
        TransactionEntity[] entries = [new() { Amount = 100, Status = "reconciled" }, new() { Amount = -20, Status = "cleared" }, new() { Amount = -10 }, new() { Amount = 999, IsDeleted = true }];
        Assert.Equal(70, LedgerRules.Balance(entries));
        Assert.Equal(80, LedgerRules.Balance(entries, true));
    }

    [Fact]
    public void Split_validation_requires_exact_total_and_no_single_category()
    {
        var valid = Request() with { Amount = -12.34m, Splits = [new("a", -10m), new("b", -2.34m)] };
        Assert.Null(LedgerRules.Validate(valid));
        Assert.NotNull(LedgerRules.Validate(valid with { Amount = -12.35m }));
        Assert.NotNull(LedgerRules.Validate(valid with { CategoryId = "a" }));
        Assert.NotNull(LedgerRules.Validate(valid with { Splits = [new("", -12.34m)] }));
        Assert.NotNull(LedgerRules.Validate(valid with { Splits = [new("a", 0), new("b", -12.34m)] }));
    }

    [Fact]
    public void Transaction_validation_rejects_invalid_date_precision_status_and_text()
    {
        var request = Request();
        Assert.Null(LedgerRules.Validate(request));
        Assert.NotNull(LedgerRules.Validate(request with { Date = new(1899, 12, 31) }));
        Assert.NotNull(LedgerRules.Validate(request with { Amount = 1.001m }));
        Assert.NotNull(LedgerRules.Validate(request with { Amount = 0 }));
        Assert.NotNull(LedgerRules.Validate(request with { Status = "reconciled" }));
        Assert.NotNull(LedgerRules.Validate(request with { Payee = new string('x', 161) }));
        Assert.NotNull(LedgerRules.Validate(request with { Memo = new string('x', 1001) }));
    }

    [Fact]
    public void Ready_to_assign_includes_ledger_income_without_rounding_drift()
    {
        var budget = new BudgetEntity { OpeningReadyToAssign = 0.1m, FundsAdded = 0.2m, LedgerIncome = 10m,
            Groups = [new() { Categories = [new() { Assigned = 0.3m }] }] };
        for (var i = 0; i < 100; i++) Assert.Equal(10m, BudgetService.CalculateReadyToAssign(budget));
        var category = new CategoryEntity { OpeningAvailable = 0.1m, Assigned = 0.2m, Activity = -0.3m };
        Assert.Equal(0m, category.Available);
    }

    [Theory]
    [InlineData(0, 0, false, null)]
    [InlineData(1, 0, false, "transactions")]
    [InlineData(1, 1, true, "transactions")]
    [InlineData(0, 1, false, "scheduled transactions")]
    [InlineData(0, 1, true, "scheduled transactions")]
    [InlineData(0, 0, true, "payment category")]
    public void Account_deletion_requires_no_transactions_active_schedules_or_payment_money(int transactions, int activeSchedules, bool paymentMoney, string? expected)
    {
        var error = LedgerRules.AccountDeletionError(transactions, activeSchedules, paymentMoney);
        if (expected is null) Assert.Null(error);
        else Assert.Contains(expected, error);
    }

    private static TransactionRequest Request() => new(Guid.NewGuid(), 1, Guid.NewGuid(), new(2026, 10, 21), -1, "Shop", "", null, "uncleared", []);
}
