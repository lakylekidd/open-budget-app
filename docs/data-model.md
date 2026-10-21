# Data model

## Current PostgreSQL schema

EF Core migrations live in `src/OpenBudget.Api/Migrations`.

### `BudgetPlans`

The current application seeds one plan with ID `default`, name `My Budget`, and currency `EUR`. `Version` is an optimistic concurrency token. Separate user-owned budgets and memberships are planned in Milestone 8.

### `Budgets`

One row per `YYYY-MM` snapshot, linked to a budget plan. `OpeningReadyToAssign` and `FundsAdded` are inputs to the monthly Ready to Assign calculation; `Version` guards concurrent edits; `Note` stores the month note.

### `CategoryGroups` and `Categories`

Groups are ordered and can be archived. Categories are ordered within a group and store assignment, target, icon, snooze, archive state, and `OpeningAvailable`. `Activity` remains as a legacy month baseline until Milestone 2 supplies ledger-derived activity. Category `Available` is calculated as `OpeningAvailable + Assigned + Activity`; it is not accepted from browser writes.

## Calculations and month lifecycle

- `Ready to Assign = OpeningReadyToAssign + FundsAdded - sum(Assigned)` for the month.
- Category available values and totals are calculated with .NET decimal arithmetic and rounded to two places.
- The first requested month gets a deterministic blank starter structure. A later unseen month carries each prior category's available amount into `OpeningAvailable`, resets that month's assignment/activity, and carries prior Ready to Assign into `OpeningReadyToAssign`.
- Month initialization is persisted once. Repeated and concurrent GET requests return the same month record.
- The BudgetCore migration converts prior persisted `Available` into `OpeningAvailable = Available - Assigned - Activity`, preserving existing visible category balances. It seeds `FundsAdded` with prior total assignments to preserve the former zero Ready to Assign display.

## Limitations and direction

The schema has no accounts, transactions, payees, transfers, users, household membership, audit log, or target rules. The current activity baseline is not transaction-backed. Add transaction and account entities in Milestone 2; retain explicit money-move history and audit/concurrency records as the product grows. Persist money as fixed-precision decimals, never binary floating point.
