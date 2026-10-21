# Data model

## Current PostgreSQL schema

The initial EF Core migration creates three tables:

### `Budgets`

| Column | Meaning |
|---|---|
| `Month` (primary key, `varchar(7)`) | Budget month in `YYYY-MM` form |
| `Note` (`varchar(1000)`) | User-authored month note |

### `CategoryGroups`

Composite primary key: (`BudgetMonth`, `Id`). A cascading foreign key connects `BudgetMonth` to `Budgets.Month`.

| Column | Meaning |
|---|---|
| `BudgetMonth` | Parent budget month |
| `Id` | Stable group identifier within that month |
| `Name` | Display name |
| `SortOrder` | Display order |

### `Categories`

Composite primary key: (`BudgetMonth`, `GroupId`, `Id`). A cascading composite foreign key connects (`BudgetMonth`, `GroupId`) to the parent group.

| Column | Meaning |
|---|---|
| `BudgetMonth`, `GroupId`, `Id` | Parent month/group and stable category identifier |
| `Name`, `Icon`, `Snoozed` | Display and current UI state |
| `Assigned`, `Activity`, `Available`, `Target` (`numeric(14,2)`) | Current monthly category snapshot |
| `SortOrder` | Display order |

The EF model is in `src/OpenBudget.Api/BudgetDbContext.cs`; migrations are in `src/OpenBudget.Api/Migrations`.

## Limitations of the current snapshot model

There is no account, payee, transaction, target-rule, transfer, import, user, household, or audit table. `Activity` and `Available` are persisted fields supplied by the browser, rather than values projected from a ledger. This is adequate for the current UI prototype but not sufficient for a trustworthy budgeting engine.

## Target domain direction

The roadmap should evolve toward these concepts:

- **Identity and plan ownership:** users, plans/budgets, memberships, roles, and ownership boundaries.
- **Accounts:** account type, closed/linked/tracking state, currency, current cleared/working balance, and reconciliation metadata.
- **Categories:** plan-scoped groups and categories with archive/hide state, order, and optional account pairing.
- **Assignments:** month-scoped assignment entries and explicit money-move history, rather than only a mutable total.
- **Transactions:** account, date, payee, amount, inflow/outflow, category, memo, cleared/reconciled state, transfer or split details, and import identity.
- **Targets:** category target type, cadence, amount, due date, rollover rules, and month-level snooze state.
- **Scheduled transactions:** recurrence rule, next occurrence, and generated/entered state.
- **Imports:** provider connection, sync cursor, raw/imported transaction identity, review/approval state, and deduplication key.
- **Audit/concurrency:** actor, timestamp, command, revision, and optimistic concurrency token for collaborative changes.

Use integer minor units or a documented decimal strategy for money; never use binary floating point in persisted domain calculations. Preserve the plan's currency and make currency conversion an explicit later feature (YNAB documents one selected currency per plan rather than combining currencies in one plan).
