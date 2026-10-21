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

The API applies EF Core migrations at startup. A single default budget identity owns independent monthly snapshots. The first requested month initializes a blank starter category structure; later months carry forward category available balances and prior Ready to Assign exactly once. Activity remains a legacy baseline until the transaction ledger is implemented.

## API

- `GET /health` checks database connectivity.
- `GET /api/budget/{YYYY-MM}` loads or initializes that month's plan.
- `PUT /api/budget/{YYYY-MM}` saves that month's editable snapshot using optimistic concurrency and server-calculated totals.
- `GET /api/budget` reads the default budget identity.
- `PUT /api/budget` renames the default budget using optimistic concurrency.

The API is not yet an account transaction ledger or authenticated multi-user service. Monthly and budget-name edits reject stale versions, but authorization and shared membership are planned for Milestone 8.
