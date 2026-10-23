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

The API applies EF Core migrations at startup. A single default budget owns monthly snapshots and a cash ledger. First-month initialization uses blank starter categories; later months carry forward available/category and Ready to Assign balances once. Activity combines a preserved legacy baseline with actual transactions.

## API

- `GET /health` checks database connectivity.
- `GET /api/budget/{YYYY-MM}` loads or initializes that month's plan.
- `PUT /api/budget/{YYYY-MM}` saves that month's editable snapshot using optimistic concurrency and server-calculated totals.
- `GET /api/budget` reads the default budget identity.
- `PUT /api/budget` renames the default budget using optimistic concurrency.

`BudgetService` owns budget behavior. `LedgerService` owns accounts, transactions, payees, splits, paired transfers, cleared state, reconciliation, receipts, audit and projection. `LedgerGate` uses a database transaction plus the shared ledger-state row lock to order ledger commands, monthly initialization and Plan saves. Responses follow commit; financial rows, projection changes and audit receipts roll back together on failure. Ledger commands also use an optimistic revision and replay-safe command IDs.

See [API contracts](api.md) for cash ledger endpoints and [testing](testing.md) for verification. Financial endpoints require an authenticated Identity session outside Testing. Verified registration, password recovery and audited administrator lifecycle controls are implemented as early Milestone 2a slices. Financial storage still uses the legacy default budget; per-user budget ownership and isolation remain future work in Milestone 2a.


The ledger application service also owns schedule rules/occurrences, named flags, review approval and account balance adjustments. A hosted worker and production read catch-up process due schedules via the same locked, idempotent command path. TimeProvider supplies server-local calendar dates; integration tests inject a clock and invoke processing over HTTP. Forecasts never change actual totals until posted; automatic posts are uncleared/unapproved, with independent review and clearing-as-approval.


Payee management lives in the `PayeeService` partial of `LedgerService`, with separate contracts and pure validation/matching/history rules in `PayeeContracts`. It extends the shared versioned command transaction rather than writing from handlers. Accepted normalized imports use the same transaction service with explicit imported provenance, rule resolution, category suggestions and unapproved state. Full statement parsing/matching remains milestone 7.


Assignment corrections now share `BudgetCarryForward` with ledger projections. All affected month versions and opening amounts save in one gated transaction. The helper creates missing destination category/group records when a backdated category has money to carry, preserving later explicit values. Budget snapshot validation/conflict results cause gate rollback, including newly initialized months. The PostgreSQL test interceptor can fail after SaveChanges to verify rollback after SQL has executed, not only before a save.
