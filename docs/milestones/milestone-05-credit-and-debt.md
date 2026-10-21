# Milestone 5 — Credit cards and debt payoff planning

**Status:** Planned

## Goal

Model credit-card spending and loan debt without double-counting cash, budget activity, or payments, and provide an explainable payoff forecast.

## Dependencies

Milestone 2 provides the transaction ledger and accounts. Milestone 1 provides budget/category calculations. Milestone 3 target logic may be reused for payoff goals, but debt balance and interest calculations remain specific to this milestone.

## User value

A person can spend on a credit card from an existing budget category, reserve cash for payment, record the payment, and compare loan payoff options.

## Vertical slice

- **UI:** Credit-card account/register and payment-category view; card purchase/refund/fee entry; payment transfer workflow; loan details and terms; payoff planner with assumptions, schedule, payoff date, and total interest; clear forecast-versus-posted labeling.
- **API/domain:** Credit account classification; category-funded card purchase rules; movement of budgeted cash into a card payment reserve; refunds, fees, overspending, and payment transfer semantics. Loan balance, interest rate, minimum payment, payment frequency, extra payment, and projection calculation.
- **Persistence:** Credit/debt account terms; card payment reserve state derived from transactions and budget assignments; loan terms and payoff target inputs; audit history for term changes and payments.
- **Consistency:** Card payments are transfers from cash to card liability, not new category spending. Forecasts are read-only calculations and never create ledger entries.

## Acceptance criteria

1. A user can create a credit-card account and enter a card purchase from a funded category; the category records the purchase activity and the linked payment reserve increases by the amount covered by available category funds.
2. A card purchase from an unfunded or partially funded category follows the documented overspending rule; it does not reserve more cash than is actually available.
3. A card refund adjusts the associated category activity and payment reserve according to the refund amount; a fee is recorded as categorized spending.
4. Recording a card payment creates a linked transfer that decreases cash and card liability by the same amount without duplicating category expense.
5. A payment larger than the available cash or card balance is rejected or requires an explicit documented adjustment; the UI explains the resulting balances before confirmation.
6. Editing or deleting a card transaction recalculates the card balance, category activity, and payment reserve consistently.
7. The loan editor validates interest, minimum payment, payment frequency, and balance; changes are versioned and persisted.
8. The payoff planner compares at least baseline minimum-payment and user-specified extra-payment scenarios and shows projected payoff date and total interest with assumptions disclosed.
9. Interest and payment projections use deterministic decimal-safe calculations and display any rounding convention.
10. Forecasting a payoff scenario does not mutate the actual loan balance or add transactions.
11. Applying an actual loan payment records principal/interest or the documented ledger representation once and updates the account balance.
12. Card and loan history can be reviewed after an account is closed; historical reports remain stable.
13. All card-reserve and payment rules are documented with examples for funded purchase, unfunded purchase, refund, fee, and payment.

## Product references

[Credit card payments](https://support.ynab.com/en_us/credit-card-payments-a-guide-r1_506Q1j), [loan accounts](https://support.ynab.com/en_us/loan-accounts-a-guide-HkNSkPHJi), [paired targets](https://support.ynab.com/en_us/paired-targets-BJJI8rdC5).
