# Milestone 2 — Accounts and transaction register

**Status:** Implemented

Selection actions extension: the floating bottom bar and upward More menu support named flags, clearing, duplication, repeating schedules, memo edits, moving ordinary transactions, CSV export and confirmed deletion. Multi-row changes use one atomic/replay-safe audited command; paired transfer changes include both legs once. Reconciled memo/move edits require acknowledgement. Imported matching is still deferred, so Match/Unmatch are disabled.

## Delivered behavior and verification

- Cash account creation, rename, close/reopen, real sidebar balances and historical closed-account registers.
- Opening entries, signed transactions, payee suggestions, categorized spending/refunds, uncategorized spending attention, exact splits, editing and soft deletion.
- Atomic paired transfers with linked editing/deletion, excluded from budget income/spending.
- Combined server-side search/account/date/category/status filters and stable pagination. View Options persists presets, month ranges and visibility switches per account across clients of the same server; Apply/Cancel and stale-edit recovery are covered by tests.
- Cleared controls, zero-difference reconciliation snapshots, confirmed reconciled edits with retained/invalidated snapshots and a retained before/after audit.
- Global ledger revision and command receipts: exact replay is safe, stale edits require reload/review, and lost-response retries reuse their original command.
- Ledger-backed balances and monthly activity/income. Historical edits propagate deltas into already initialized later months, preserving assignments and explicit legacy baselines. The migration invents no sample accounts/history.
- Automated domain, real HTTP/PostgreSQL and UI tests cover the slice; see the [acceptance coverage map](../testing.md).

Current limits: reconciliation uses the current cleared balance (no statement date cutoff or automatic adjustment); reconciled edits require explicit acknowledgement and fresh reconciliation; old snapshots and edits remain audited. Working-balance edits create an adjustment transaction. Month projection recomputes initialized months from the local ledger within each write. The application remains single-budget and unauthenticated/local-only. Credit accounts and imports remain later milestones. Scheduled register behavior is described in Milestone 4.

## Goal

Introduce an auditable cash transaction ledger so account balances, category activity, and available amounts are derived from actual transactions.

## Dependencies

Milestone 1 provides authoritative budgets, categories, month lifecycle, and concurrency. This slice covers on-budget cash accounts. Credit-card accounting is reserved for Milestone 5; statement-file import is reserved for Milestone 7.

## User value

A person can record what happened in checking or savings, see the account balance change, and see its budget category reflect the same activity.

## Vertical slice

- **UI:** Account list and details; create/edit/close account flow; transaction register; add/edit/delete transaction form; payee/category selectors; transfer flow; search/date/account/category/cleared filters; reconciliation workflow and visible balances.
- **API/domain:** Cash account lifecycle, opening balance, inflow, categorized outflow, transfer, cleared/reconciled status, payee handling, transaction edits and deletion. Recompute account balance, category activity, and category available values from ledger entries. Transfers affect account balances but do not count as category spending or income.
- **Persistence:** Accounts, transactions, transfer pairing, payees, reconciliation snapshot/audit, and stable identifiers; use a transaction boundary for writes that affect both account and budget state. Preserve an audit trail for edits and deletions.
- **Operational behavior:** Paginate/filter large registers on the server; validate dates and decimal values; make repeated writes safe through command IDs or equivalent idempotency handling.

## Acceptance criteria

1. A user can create, rename, and close a cash account; closing is blocked while unresolved account state exists, with a clear explanation and supported resolution path.
2. Opening balance is persisted as a ledger entry and is included exactly once in the account balance.
3. Creating an inflow or categorized outflow updates the account balance and the corresponding budget/category calculations together.
4. An uncategorized transaction is visibly marked as needing attention and is handled by the documented ready-to-assign rules; it is not silently assigned a fabricated category.
5. A transfer between two accounts creates a linked pair of entries, changes both balances by equal and opposite amounts, and does not create budget income or spending.
6. Editing or deleting a transaction recalculates all affected account and budget values; the UI identifies the change and the audit history records it.
7. Split transactions allocate their full amount across selected categories; the server rejects splits whose parts do not equal the transaction total.
8. Search and filters combine correctly and return stable paginated results; an empty result is distinguishable from a loading or failure state.
9. Reconciliation compares the statement balance with cleared ledger entries, shows the difference, and records a reconciliation only when the user confirms a zero difference or an explicit documented adjustment.
10. Duplicate command submission caused by retry or network replay does not create a second transaction or transfer pair.
11. Concurrent changes to the same account/register are detected and can be recovered without losing another committed edit.
12. Account balances, category activity, and available values match a recomputation from the ledger after restart and after transaction edits.
13. The API and UI handle deleted/closed accounts in historical reports without erasing their transaction history.
14. API documentation describes amount sign conventions, transfer semantics, cleared/reconciled state, pagination, and error behavior.

## Product references

[Adding an account](https://support.ynab.com/en_us/how-to-add-an-account-ByoxswMJs), [adding transactions](https://support.ynab.com/en_us/how-to-add-transactions-in-ynab-HyDwA_byi).


### Payee management extension (2026-10-21)

Implemented grouped Selected / Payments and Transfers / Saved Payees choices, stable account-ID transfer selection, server-persisted merchant names/hidden state/category policies/renaming rules, atomic Manage Payees Done, draft Undo/Redo, safe unused deletion and exact-payee register filtering. Accepted normalized imports apply Is/Contains rules through the ledger service and enter approval. Transfer labels follow account edits. Full file import remains milestone 7. Unit, PostgreSQL HTTP integration and component tests cover the supported slice.

Payee typing filters merchant and transfer choices even for exact matches/full transfer names. Unmatched names create a saved payee only on successful transaction save, available immediately in the next draft. Regression coverage checks case-insensitive reuse, replay, validation/rollback, clearing searches and keyboard selection after a query changes.


### Grouped category dropdown extension (2026-10-21)

Implemented the reference-style category picker: Selected with parent label, Inflow/Ready to Assign, left-aligned groups and indented category names, right-aligned month-specific available balances and sign colors. Supports category/group search, keyboard selection, fresh reads on open/date changes, retry/error states, Split and account transfer choices, and category creation into an existing group through the existing versioned month snapshot API. Category creation preserves the inline draft, and lost responses recover by the stable category ID. Unit/component and real PostgreSQL HTTP tests cover grouping, authoritative money, date changes, creation, persistence and carry-forward.
