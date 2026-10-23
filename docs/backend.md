# OpenBudget API and local services

The API targets .NET 10 and uses Entity Framework Core with PostgreSQL. Aspire is the supported local orchestration path; it starts the PostgreSQL resource, API, and Vite UI together.

From the repository root run:

```powershell
dotnet run --project src/OpenBudget.AppHost
```

For API-only development, start a local PostgreSQL instance yourself, set `ConnectionStrings__BudgetDatabase`, then run:

```powershell
dotnet run --project src/OpenBudget.Api --urls http://127.0.0.1:5080
```

The API applies EF Core migrations at startup. Each owned budget has independent monthly snapshots, a cash ledger, schedules, flags and long-term planning. Existing rows stay in the administrator-only `default` budget. First-month initialization uses blank starter categories; later months carry forward available/category and Ready to Assign balances once. Activity combines a preserved legacy baseline with that budget's actual transactions.

## API

- `GET /health` checks database connectivity.
- `GET /api/budget/{YYYY-MM}` loads or initializes that month's plan.
- `PUT /api/budget/{YYYY-MM}` saves that month's editable snapshot using optimistic concurrency and server-calculated totals.
- `GET /api/budget` reads the default budget identity.
- `PUT /api/budget` renames the default budget using optimistic concurrency.

`BudgetService` owns budget behavior. `LedgerService` owns accounts, transactions, payees, splits, paired transfers, cleared state, reconciliation, receipts, audit and projection. `BudgetRequestContext` is initialized from the UI's active-budget cookie only after the API verifies ownership and active status. EF query filters partition financial entities; `LedgerGate` uses a budget-specific advisory lock and ledger-state row lock to order commands, monthly initialization and Plan saves. Responses follow commit; financial rows, projection changes and audit receipts roll back together on failure. Ledger commands use per-budget optimistic revisions and replay-safe command IDs.

See [API contracts](api.md) for cash ledger endpoints and [testing](testing.md) for verification. Financial endpoints require an authenticated Identity session outside Testing. Verified registration, password recovery, audited administrator lifecycle controls, owner-scoped budget identity, monthly Plan snapshots and budget-isolated financial data are implemented. Financial APIs verify the active owned budget; legacy default budget identity/month routes remain administrator-only.


The ledger application service also owns schedule rules/occurrences, named flags, review approval and account balance adjustments. A hosted worker enumerates active budgets, and production read catch-up processes due schedules for the selected budget, both through the same locked, idempotent command path. TimeProvider supplies server-local calendar dates; integration tests inject a clock and invoke processing through the service/API. Forecasts never change actual totals until posted; automatic posts are uncleared/unapproved, with independent review and clearing-as-approval.


Payee management lives in the `PayeeService` partial of `LedgerService`, with separate contracts and pure validation/matching/history rules in `PayeeContracts`. It extends the shared versioned command transaction rather than writing from handlers. Accepted normalized imports use the same transaction service with explicit imported provenance, rule resolution, category suggestions and unapproved state. Full statement parsing/matching remains milestone 7.


Assignment corrections now share `BudgetCarryForward` with ledger projections. All affected month versions and opening amounts save in one gated transaction. The helper creates missing destination category/group records when a backdated category has money to carry, preserving later explicit values. Budget snapshot validation/conflict results cause gate rollback, including newly initialized months. The PostgreSQL test interceptor can fail after SaveChanges to verify rollback after SQL has executed, not only before a save.
