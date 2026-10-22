# Data model

## Current PostgreSQL schema

EF Core migrations live in `src/OpenBudget.Api/Migrations`.

### `BudgetPlans`

The current application seeds one plan with ID `default`, name `My Budget`, and currency `EUR`. `Version` is an optimistic concurrency token. Separate user-owned budgets and memberships are planned in Milestone 8.

### `Budgets`

One row per `YYYY-MM` snapshot, linked to a budget plan. `OpeningReadyToAssign`, preserved legacy `FundsAdded`, and materialized `LedgerIncome` are inputs to Ready to Assign; `Version` guards concurrent edits; `Note` stores the month note.

### `CategoryGroups` and `Categories`

Groups are ordered and can be archived. Categories store assignment, target, icon, snooze, archive state, `OpeningAvailable`, and `LegacyActivity`. Materialized `Activity` is legacy activity plus nondeleted, non-transfer single-category/split ledger amounts in that month. `Available = OpeningAvailable + Assigned + Activity`; calculated fields are not accepted from browser writes.

### Register view preferences

`RegisterViews` (migration `AccountViewOptions`) stores one row per canonical account UUID scope, plus an independent `all` scope. It contains its own optimistic version, date preset, nullable From/To month selectors and four visibility switches. The application validates account existence and every selection before writing under the shared PostgreSQL gate. Reads of unsaved scopes return defaults without inserting. No existing ledger/account/month data is changed by this migration or by a view save. Account deletion is not supported; closed-account preferences remain available. Preferences are server-owned and shared across PCs using the same server/database.

### Ledger tables

- `Accounts`: UUID, name, closed flag, account type (`checking`/`savings`/`cash`) favorite flag and notes (AccountNotes migration, empty default). Balance is derived from nondeleted transactions; cleared balance includes cleared/reconciled entries, uncleared balance is the remainder. `AccountRegisterPresentation` adds type/favorite with checking/false defaults, preserving existing balances.
- `Transactions`: UUID, account/payee FKs, date, signed decimal amount, memo, optional logical category ID, cleared status, optional shared transfer ID, opening and soft-delete flags. Indexes support date/account/UUID pagination and transfer lookup. Account/payee deletion is restricted.
- `SplitEntity`: transaction FK plus position key, category ID and amount. Parts must sum exactly to the parent amount. The service validates categories against the transaction month's retained category records.
- `Payees`: UUID, display/normalized name; normalized names have a unique index and are reused case-insensitively.
- `LedgerStates`: one row (ID 1) with the global revision. PostgreSQL `SELECT ... FOR UPDATE` orders commands, Plan writes and month initialization across API processes.
- `LedgerCommands`: command ID primary key, resource ID, kind/path, SHA-256 input fingerprint, before/after JSON, revision and UTC timestamp. Append-only receipts provide replay safety and audit atomically with ledger/projection changes.
- `Reconciliations`: UUID, account FK, confirmed statement balance and UTC time. Confirmation marks cleared entries reconciled. `IsInvalidated` retains the snapshot after an explicitly confirmed reconciled edit; the entry becomes cleared and audit records the change. Deletion remains blocked while reconciled.

## Calculations and month lifecycle

- `Ready to Assign = OpeningReadyToAssign + FundsAdded + LedgerIncome - sum(Assigned)` for the month. Ledger income sums nondeleted/non-transfer entries without categories/splits, including opening balances and negative uncategorized spending. Categorized positive amounts are refunds; transfers are budget-neutral.
- Category available values and totals are calculated with .NET decimal arithmetic and rounded to two places.
- The first requested month gets a deterministic blank starter structure. A later unseen month carries each prior category's available amount into `OpeningAvailable`, resets that month's assignment/activity, and carries prior Ready to Assign into `OpeningReadyToAssign`.
- Month initialization is persisted once. Repeated and concurrent GET requests return the same month record.
- The BudgetCore migration converts prior persisted `Available` into `OpeningAvailable = Available - Assigned - Activity`, preserving existing visible category balances. It seeds `FundsAdded` with prior total assignments to preserve the former zero Ready to Assign display.
- The CashLedger migration copies existing `Activity` to `LegacyActivity`, adds zero `LedgerIncome` and empty ledger tables, and preserves all existing assignments/opening balances/manual funds. It fabricates no account/transaction history. Manual funds become read-only; record new income through accounts.
- On each ledger write, initialized months are recalculated chronologically. Changes in Ready to Assign and category available propagate as deltas into later opening values, preserving later explicit assignments and legacy baselines. Affected month versions increase. Date/category/account edits reverse their original effects and apply new ones atomically. Repeated recomputation does not duplicate carry-forward.
- Positive and negative available balances carry forward, including cash overspending; no debt category is invented. Assignment saves and ledger changes both propagate exact deltas into every initialized later month. Later explicit assignments, activity, metadata and preserved legacy opening baselines remain intact. Affected month versions advance; unchanged money/metadata-only saves do not invalidate later months.
- Existing prototype funds/activity and new account opening entries are additive. Do not count previously entered funds a second time when selecting an opening balance; there is no automatic conversion of untraceable demo activity into real transactions.
- Register running balances are PostgreSQL window sums partitioned by account over all nondeleted history, ordered date ascending/UUID descending, then filtered to page IDs. They are not recomputed from the visible rows. Display order reverses that window. The latest account reconciliation date and attention count are derived, not browser-entered. Connection status is unlinked until a real sync provider exists.

## Limitations and direction

One local default budget has no authentication/membership boundary, credit/debt accounting, bank connections, imports or target rules. Account balances are ledger queries; monthly projection currently reads initialized months and the local ledger on writes, favoring explicit correctness over speculative incremental infrastructure. Authorization and budget isolation belong to Milestone 8. Persist money as fixed-precision decimals, never binary floating point.

### Scheduling and flag storage

`ScheduledTransactions` migration adds `Schedules`, `ScheduleOccurrences`, a nullable transaction `ScheduleId` and reconciliation invalidation state without changing existing amounts or statuses. Rules store source/destination account FKs, original calendar anchor, next date, recurrence index, monotonic sequence, validated template JSON, pause/end state and optional end bounds. Occurrences have deterministic UUIDs derived from rule UUID/sequence, a unique rule/sequence index, original due date, optional snooze/override, disposition and generated transaction link. Rule edits increment sequence without reusing occurrence identities. Historical posted/skipped/superseded records are retained. All commands share the ledger transaction/receipt/audit path.

`TransactionFlags` adds nullable flag colors to transactions/rules and seeds six `FlagDefinitions` keyed by fixed color. Names are mutable per budget; colors remain fixed. Flags change no financial values or reconciliation state. The account balance-adjustment workflow appends ordinary cleared transactions; it adds no mutable balance column. API/domain validation owns template and flag integrity; JSON persistence is internal, never accepted as unchecked JSON from a client.

`ScheduledApproval` adds transaction `IsApproved` with a true backfill/default, preserving existing manual/history rows as approved, plus nullable schedule `PostingError`. Due processing generates deterministic uncleared/unapproved transactions and advances occurrences under the same lock/receipt/projection transaction. Approval is separate from cleared state: clearing implies approval, while pending actual amounts already participate in financial totals. Rejection soft-deletes and reverses the posted entry/pair; occurrence history prevents regenerated duplicates. Account attention counts are derived from nondeleted entries. Scheduler polling/catch-up uses the server clock/local date through TimeProvider, not browser clocks.


## Payee management

The `PayeeManagement` migration adds `IsHidden` (false), `CategoryMode` (history), nullable `DefaultCategoryId`, and `RulesJson` (empty array) to existing stable `Payees` rows. The normalized unique name index remains authoritative. Rules use public `PayeeRuleDto` contracts serialized by the application service, separate from persistence entities. Existing names/history/money are preserved. Transfer payees are derived from Accounts rather than duplicate merchant records.

Settings mutations and accepted imported rows use the shared ledger gate/receipt and global version. Payee rename changes labels through the existing foreign key and updates active schedule/pending override names atomically. Imported source names are retained in the command audit; normal money projections include unapproved imported rows. Default category IDs are logical budget category IDs across months; application checks category/group activity in the transaction month. There is no new financial total stored on a payee. See [API](api.md#saved-payees-and-renaming-rules) for matching priority, history tie rules, deletion protection and the accepted-row import boundary.


### Backdated assignment corrections

`BudgetCarryForward` applies the same decimal opening-balance changes for monthly assignment saves and ledger recomputation. Available remains opening available + this month's assigned + this month's activity. Assignment edits change only the selected month's assigned value; the resulting available and Ready to Assign deltas adjust later months' opening values in the same database transaction. No later assignment is copied or replaced. Missing calendar months do not consume/reset carry; when opened they initialize from the nearest earlier month as usual. Positive balances and cash overspending both carry.

If a historical category did not exist in an already initialized later month, a zero-assigned/zero-activity category record (and group if needed) is created there to retain the incoming amount. Existing later category/group names, archive state and ordering are preserved; newly needed rows append with distinct ordering. Ledger recomputation uses the same helper so refunds, recategorization, amount/date edits and soft deletion cannot drop that carry across missing category rows. All calculations use decimal; reads do not reapply saved changes.

This preserves explicit opening/legacy baselines rather than replacing historical snapshots wholesale. The gate rolls back invalid or stale monthly updates, including any month initialized during that rejected request. A save failure after the database writes rolls back source and future rows together. There is no schema migration or automatic replacement of old baseline values in this fix.
