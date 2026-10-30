# Credit card payment Activity

The Plan grid's Activity amount for a credit payment category (`ccp-<accountId>`) opens the shared transaction popup in its `credit` variant. Its summary box comes from `GET /api/accounts/{id}/credit-activity/{YYYY-MM}`, which runs `CreditCardRules.Project` with the same inputs as the ledger projection, so the numbers always reconcile with the payment category's Activity.

All values are for one card and one calendar month. Only transactions categorized to budget categories (including splits) count as spending or returns; uncategorized rows and transfers do not.

| Field | Definition |
| --- | --- |
| Spending | Sum of the card's categorized outflows (zero or negative). |
| Returns | Sum of the card's categorized inflows (zero or positive). |
| Total Spending | Spending + Returns (net spending). |
| Funded Spending | Spending moved into the payment category: the part covered by the category's funds, plus the part of spending a same-category return offsets. Unfunded spending (credit overspending) is excluded. |
| Payments & Returns | Transfers with the card (a payment into the card is negative, a transfer out is positive) minus Returns. |
| Total Activity | Funded Spending + Payments & Returns, equal to the payment category's Activity in Plan. |

Across several cards sharing one category, funds are allocated in account-id order exactly as in `CreditCardRules.Project`.

The popup table lists the card's transactions for the month through `GET /api/transactions?accountId=...&from=...&to=...` with columns Date, Payee, Category (`Group: Category`), Memo and Amount; transfers show `Transfer : <account>`.
