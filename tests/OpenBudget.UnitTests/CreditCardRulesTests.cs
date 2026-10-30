using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public sealed class CreditCardRulesTests
{
    private static readonly Guid Card = Guid.Parse("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid Cash = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly string Payment = CreditCardRules.PaymentCategoryId(Card);

    private static TransactionEntity Entry(Guid account, decimal amount, string? category = null, Guid? transfer = null) =>
        new() { Id = Guid.NewGuid(), AccountId = account, Amount = amount, CategoryId = category, TransferId = transfer };

    private static CreditCardRules.Flows Project(decimal groceriesFunds, params TransactionEntity[] entries) =>
        CreditCardRules.Project(entries, new HashSet<Guid> { Card }, id => id == "groceries" ? groceriesFunds : 0);

    [Fact]
    public void Funded_purchase_moves_the_full_amount_into_the_payment_category()
    {
        var flows = Project(100, Entry(Card, -40, "groceries"));
        Assert.Equal(40, flows.PaymentActivity[Payment]);
        Assert.Empty(flows.CreditOverspending);
    }

    [Fact]
    public void Partially_funded_purchase_reserves_only_available_funds_and_records_credit_overspending()
    {
        var flows = Project(25, Entry(Card, -40, "groceries"));
        Assert.Equal(25, flows.PaymentActivity[Payment]);
        Assert.Equal(15, flows.CreditOverspending["groceries"]);
    }

    [Fact]
    public void Refund_returns_money_from_the_payment_category()
    {
        var flows = Project(0, Entry(Card, -40, "groceries"), Entry(Card, 10, "groceries"));
        Assert.Equal(0, flows.PaymentActivity[Payment]);
        Assert.Equal(30, flows.CreditOverspending["groceries"]);
        Assert.Equal(-10, Project(0, Entry(Card, 10, "groceries")).PaymentActivity[Payment]);
    }

    [Fact]
    public void Payment_transfer_reduces_the_payment_category_and_cash_spending_is_ignored()
    {
        var transfer = Guid.NewGuid();
        var flows = Project(100, Entry(Cash, -30, null, transfer), Entry(Card, 30, null, transfer), Entry(Cash, -50, "groceries"));
        Assert.Equal(-30, flows.PaymentActivity[Payment]);
        Assert.Empty(flows.CreditOverspending);
    }

    [Fact]
    public void Card_breakdown_reconciles_with_payment_category_activity()
    {
        var transfer = Guid.NewGuid();
        var flows = Project(100, Entry(Card, -60, "groceries"), Entry(Card, -20, "groceries"), Entry(Card, 15, "groceries"), Entry(Cash, -30, null, transfer), Entry(Card, 30, null, transfer));
        var card = flows.Cards![Card];
        Assert.Equal(-80, card.Spending); Assert.Equal(15, card.Returns);
        Assert.Equal(80, card.FundedSpending); Assert.Equal(-30, card.Payments); Assert.Equal(-45, card.PaymentsAndReturns);
        Assert.Equal(flows.PaymentActivity[Payment], card.TotalActivity);
        Assert.Equal(35, card.TotalActivity);
    }

    [Fact]
    public void Card_breakdown_counts_only_funded_spending_and_nets_same_category_returns()
    {
        var unfunded = Project(25, Entry(Card, -40, "groceries")).Cards![Card];
        Assert.Equal(25, unfunded.FundedSpending); Assert.Equal(25, unfunded.TotalActivity);
        var refundOnly = Project(0, Entry(Card, 10, "groceries"));
        Assert.Equal(-10, refundOnly.Cards![Card].TotalActivity); Assert.Equal(refundOnly.PaymentActivity[Payment], refundOnly.Cards[Card].TotalActivity);
        var partial = Project(0, Entry(Card, -100, "groceries"), Entry(Card, 30, "groceries"));
        Assert.Equal(partial.PaymentActivity[Payment], partial.Cards![Card].TotalActivity);
        Assert.Equal(30, partial.Cards[Card].FundedSpending);
    }

    [Fact]
    public void Credit_overspending_does_not_reduce_next_months_ready_to_assign()
    {
        Assert.Equal(0, BudgetCarryForward.UncoveredOverspending(-15, 15));
        Assert.Equal(-5, BudgetCarryForward.UncoveredOverspending(-20, 15));
    }

    [Fact]
    public void Payment_categories_are_created_first_and_follow_account_names()
    {
        var month = new BudgetEntity { Month = "2026-10", Groups = [new() { Id = "home", Name = "Home", SortOrder = 0 }] };
        var account = new AccountEntity { Id = Card, Name = "Mastercard", AccountType = "creditCard" };
        Assert.True(CreditCardRules.EnsurePaymentCategories(month, [account]));
        var group = month.Groups.Single(g => g.Id == CreditCardRules.PaymentGroupId);
        Assert.Equal(0, group.SortOrder); Assert.Equal(1, month.Groups.Single(g => g.Id == "home").SortOrder);
        Assert.Equal("Mastercard", Assert.Single(group.Categories).Name);
        account.Name = "ICS Mastercard";
        Assert.True(CreditCardRules.EnsurePaymentCategories(month, [account]));
        Assert.Equal("ICS Mastercard", Assert.Single(group.Categories).Name);
        Assert.False(CreditCardRules.EnsurePaymentCategories(month, [account]));
    }

    [Fact]
    public void Payment_categories_get_no_automatic_icon_and_lose_the_legacy_one()
    {
        var month = new BudgetEntity { Month = "2026-10" };
        var account = new AccountEntity { Id = Card, Name = "💳 Mastercard", AccountType = "creditCard" };
        Assert.True(CreditCardRules.EnsurePaymentCategories(month, [account]));
        var category = Assert.Single(month.Groups.Single(g => g.Id == CreditCardRules.PaymentGroupId).Categories);
        Assert.Null(category.Icon);

        category.Icon = CreditCardRules.LegacyPaymentIcon;
        Assert.True(CreditCardRules.EnsurePaymentCategories(month, [account]));
        Assert.Null(category.Icon);
        Assert.Equal("💳 Mastercard", category.Name);

        category.Icon = "★";
        Assert.False(CreditCardRules.EnsurePaymentCategories(month, [account]));
        Assert.Equal("★", category.Icon);
    }
}
