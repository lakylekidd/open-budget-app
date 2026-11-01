using OpenBudget.Api;

namespace OpenBudget.UnitTests;

public sealed class PaymentPresentationTests
{
    [Theory]
    [InlineData("checking", -50, "creditCard", true)]
    [InlineData("cash", -50, "mortgage", true)]
    [InlineData("checking", 50, "creditCard", false)]       // money coming back from the card is not a payment
    [InlineData("checking", -50, "savings", false)]
    [InlineData("creditCard", -50, "mortgage", false)]      // debt-to-debt is not a cash payment
    public void Only_cash_outflows_into_credit_or_loan_accounts_are_payments(string own, decimal amount, string other, bool expected) =>
        Assert.Equal(expected, PaymentPresentation.IsPayment(own, amount, other));

    [Fact]
    public void Payee_text_follows_the_requested_format() => Assert.Equal("Payment: Car Loan", PaymentPresentation.Payee("Car Loan"));

    [Theory]
    [InlineData("checking", -10, "savings", "Savings", "To Savings")]
    [InlineData("savings", 10, "checking", "Checking", "From Checking")]
    [InlineData("checking", -10, "creditCard", "Card", null)]
    [InlineData("creditCard", 10, "checking", "Checking", null)]
    public void Cash_transfers_read_To_or_From(string own, decimal amount, string other, string name, string? expected) =>
        Assert.Equal(expected, PaymentPresentation.CashTransferPayee(own, amount, other, name));
}
