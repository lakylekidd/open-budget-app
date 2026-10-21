# Milestone 6 — Focused plan views and reports

**Status:** Planned

## Goal

Help a person focus on the categories and transactions needing attention and inspect consistent, ledger-backed budget reports.

## Dependencies

Milestones 1 and 2 provide category/month calculations and the transaction ledger. Milestones 4 and 5 add scheduled and debt data that can be included in relevant views or reports when available.

## User value

A person can filter the plan to actionable work and answer questions about spending, income, and net worth over a selected period.

## Vertical slice

- **UI:** Focused category views (for example, underfunded or upcoming obligations), category/register filtering, Reflect/report pages, date/account/category selectors, visualizations, empty/loading/error states, and CSV download.
- **API/domain:** Server-side report queries over persisted budget and ledger data; explicit date-range and inclusion rules; category spending, income-versus-expense, and net-worth series. Saved views store only filter configuration, not copied financial results.
- **Persistence:** Saved view definitions and ownership; no report aggregates become a second source of financial truth. Add appropriate query indexes and bounded result limits.
- **Accessibility/privacy:** Charts have text/table equivalents; exported data is scoped to the selected budget and filters.

## Acceptance criteria

1. A user can open focused views for documented actionable states and see only categories meeting the exact server-defined filter rule.
2. Category and transaction filters combine predictably, can be cleared, and remain reflected in the URL or saved view where applicable.
3. A user can create, rename, edit, and delete a saved view; saved filters persist and are isolated to the correct budget/user.
4. Category-spending reports use transaction dates and the documented treatment of transfers, refunds, split transactions, and uncategorized activity.
5. Income-versus-expense totals reconcile to the underlying register for the selected period, with transfers excluded from income/expense unless explicitly included by the report definition.
6. Net-worth reporting combines supported account balances at consistent period boundaries and explains which account types are included.
7. Changing account, category, or date filters updates the report query and displayed totals; the same values are returned after refresh.
8. Empty ranges and insufficient data display an informative empty state; API failures remain distinguishable from valid zero totals.
9. CSV export matches the displayed period and filters, includes stable headers and currency/date formats, and does not export data outside the selected budget.
10. Reports support a defined maximum range/page or aggregation policy and remain responsive on a representative large ledger.
11. Every chart includes an accessible text summary or data table and does not rely on color alone to convey positive/negative or category distinctions.
12. API documentation defines each report’s date semantics, aggregation, sign conventions, and treatment of transfers/refunds.

## Product references

[Focused views](https://support.ynab.com/en_us/focused-views-a-guide-BksnNYqLh), [YNAB reports and data](https://www.ynab.com/blog/ynab-reports-and-data), [income versus expense](https://support.ynab.com/en_us/income-v-expense-Byu1BYWRq).
