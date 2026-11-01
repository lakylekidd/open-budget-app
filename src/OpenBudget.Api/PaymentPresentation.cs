using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

// A transfer from a cash (non-credit, non-loan) account into a credit or loan account is a payment. Its cash-side leg is
// presented as payee "Payment: <account>" with the target account's own category: the Credit Card Payments category of a
// credit account, or the paired category of a loan or contract. This is presentation only: the stored transfer legs keep
// no category, so the Plan projections that already derive payment flows from the legs are not counted twice.
public static class PaymentPresentation
{
    public const string PayeePrefix = "Payment: ";

    public sealed record Target(string AccountName, string? CategoryId);

    public static bool IsDebt(string accountType) => CreditCardRules.IsCredit(accountType) || LoanRules.IsLoan(accountType);

    public static string Payee(string accountName) => PayeePrefix + accountName;

    // Transfers between two cash accounts read "To <account>" for the outflow and "From <account>" for the inflow.
    public static string? CashTransferPayee(string ownAccountType, decimal amount, string otherAccountType, string otherName) =>
        IsDebt(ownAccountType) || IsDebt(otherAccountType) ? null : (amount < 0 ? "To " : "From ") + otherName;

    // Payment legs are the outflow (cash side) of a transfer into a debt account from an account that is not itself a debt account.
    public static bool IsPayment(string ownAccountType, decimal amount, string otherAccountType) =>
        amount < 0 && !IsDebt(ownAccountType) && IsDebt(otherAccountType);

    // The category payments to this account use, or null when a loan has no paired category.
    public static string? CategoryId(AccountEntity account, LoanTermsEntity? terms) =>
        CreditCardRules.IsCredit(account.AccountType) ? CreditCardRules.PaymentCategoryId(account.Id) : terms?.PairedCategoryId;
}

public sealed partial class LedgerService
{
    // Validates a transfer's optional category and returns the one to store on its budget-side leg.
    // - between two tracking accounts, or two plain budget accounts (cash, credit, loan to loan): no category;
    // - between a tracking account and a budget account: a category is required (the budget side spends from or returns to it);
    // - a cash account paying a loan: optional, replaces the loan's paired category; a credit account's category is always its payment category.
    private async Task<string?> TransferCategoryAsync(AccountEntity from, AccountEntity to, TransferRequest request, CancellationToken ct)
    {
        var fromTracking = LedgerRules.IsTracking(from.AccountType); var toTracking = LedgerRules.IsTracking(to.AccountType);
        var categoryId = string.IsNullOrWhiteSpace(request.CategoryId) ? null : request.CategoryId.Trim();
        if (fromTracking && toTracking) { if (categoryId is not null) throw new LedgerException("Transfers between tracking accounts do not use a category."); return null; }
        var tracked = fromTracking || toTracking;
        var loanPayment = !tracked && !PaymentPresentation.IsDebt(from.AccountType) && LoanRules.IsLoan(to.AccountType);
        if (categoryId is null)
        {
            if (tracked) throw new LedgerException("Choose a category for money moving between a tracking account and a budget account.");
            return null;
        }
        if (!tracked && !loanPayment) throw new LedgerException("This transfer does not use a category.");
        if (CreditCardRules.IsPaymentCategory(categoryId)) throw new LedgerException("Credit card payment categories are funded automatically. Record a card payment as a transfer.");
        var month = await budgets.GetMonthAsync(db.ActiveBudgetId, Month(request.Date), ct);
        if (!month.Groups.Any(g => !g.IsArchived && g.Categories.Any(c => c.Id == categoryId && !c.IsArchived)))
            throw new LedgerException("Choose an active category for the transfer's month.");
        return categoryId;
    }

    // Debt accounts of the active budget by id, with the category payments into them use.
    private async Task<Dictionary<Guid, PaymentPresentation.Target>> PaymentTargetsAsync(CancellationToken ct)
    {
        var accounts = await db.Accounts.Where(a => CreditCardRules.CreditTypes.Contains(a.AccountType) || LoanRules.LoanTypes.Contains(a.AccountType)).ToListAsync(ct);
        var terms = await db.LoanTerms.ToDictionaryAsync(t => t.AccountId, ct);
        return accounts.ToDictionary(a => a.Id, a => new PaymentPresentation.Target(a.Name, PaymentPresentation.CategoryId(a, terms.GetValueOrDefault(a.Id))));
    }
}
