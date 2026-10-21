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

The API creates its tables through EF Core migrations at startup. Each month is stored independently. The initial sample plan is currently used to initialize unseen months; month carry-forward and ledger semantics are planned work.

## API

- `GET /health` checks database connectivity.
- `GET /api/budget/{YYYY-MM}` loads or initializes that month's plan.
- `PUT /api/budget/{YYYY-MM}` atomically replaces that month's group and category plan.

The current API stores a monthly plan snapshot. It is not yet an account transaction ledger, multi-user service, or concurrency-safe collaborative API.
