# Developer setup

## Prerequisites

- .NET 10 SDK
- Node.js and npm
- Docker Desktop with its Linux container engine available

The current machine uses .NET SDK 10.0.102 and Aspire 13.6.2 packages. The UI dependencies are locked in `src/OpenBudget.UI/package-lock.json`.

## First run

The Aspire AppHost expects a local PostgreSQL password in the .NET user-secrets store. The existing development database was initialized with the current workspace's local secret. On a new machine, set a PostgreSQL password before starting Aspire:

```powershell
dotnet user-secrets set "Parameters:postgres-password" "<local-development-password>" --project src/OpenBudget.AppHost
```

Use a local development-only value. Do not commit it to source control. The database username is set to `budgetboard` by the AppHost.

Start the complete stack from the repository root:

```powershell
dotnet run --project src/OpenBudget.AppHost
```

Aspire provisions PostgreSQL, waits for the database, starts the API, then starts Vite. Use the `ui` HTTP endpoint in the Aspire dashboard; its stable local endpoint is `http://127.0.0.1:5173/`. The API is available at `http://127.0.0.1:5080/` and PostgreSQL at `127.0.0.1:5432` through Aspire's local endpoint.

The AppHost attaches PostgreSQL to the existing named volume `ynab-clone_budget-board-data`; the volume survives AppHost restarts and container recreation. The legacy volume and database role names are retained to preserve local data; application folders, assemblies, and C# namespaces use `OpenBudget`. PostgreSQL is pinned to major version 17 for compatibility with that data directory.

## Build commands

```powershell
dotnet build OpenBudget.slnx
npm --prefix src/OpenBudget.UI ci
npm --prefix src/OpenBudget.UI run build
```

## Database lifecycle

EF Core migrations live in `src/OpenBudget.Api/Migrations`. The API applies pending migrations during startup. To add a migration after changing the EF model:

```powershell
dotnet ef migrations add <MigrationName> --project src/OpenBudget.Api --startup-project src/OpenBudget.Api
```

The local connection string comes from Aspire's `budgetdatabase` reference. For standalone API development, `appsettings.json` includes a development-only local connection fallback; set `ConnectionStrings__BudgetDatabase` (or `ConnectionStrings__budgetdatabase`) to override it.

## Request flow

The browser calls same-origin `/api/...` paths. Vite proxies those paths to the API on port 5080. In Aspire, the API and UI have stable local endpoints so the Vite proxy keeps working while Aspire controls startup and health ordering.

## Current development boundaries

- There is no authentication or user/tenant boundary yet. Keep this local and synthetic until the roadmap's identity and authorization work is complete.
- The Plan API stores versioned editable monthly snapshots. The cash ledger uses replay-safe commands and shared revisions, but there is no authenticated multi-user boundary.
- Long-Term Planning stores versioned category budget overrides, projects income and expenses from active scheduled ledger transactions, and reads existing actual snapshots without initializing missing months. It remains scoped to the local default budget.
- Cash account/sidebar balances and monthly activity are ledger-backed; Reflect, credit/debt and linking remain future milestones; scheduling/approval and payee management are implemented register extensions.
- Every feature requires unit/integration tests and UI workflow coverage, as specified in `AGENTS.md` and [testing](testing.md).

## Tests

```powershell
dotnet test OpenBudget.slnx
npm --prefix src/OpenBudget.UI test
```

`OpenBudget.UnitTests` covers domain rules/calculations. `OpenBudget.IntegrationTests` uses the real HTTP API/EF migrations and a disposable PostgreSQL 17 Testcontainers instance with a fresh database per test. Docker must be running; unavailable Docker fails tests rather than silently skipping them. No developer database connection or financial-service credential is required. Vitest/Testing Library test the ledger UI in jsdom.

For focused runs, use `dotnet test tests/OpenBudget.UnitTests`, `dotnet test tests/OpenBudget.IntegrationTests`, or `npm --prefix src/OpenBudget.UI run test:watch`.

After backend changes, stop the AppHost, rebuild the solution and restart before live verification. Windows running executables can lock build output; avoid building over an active API process. Preserve the named development volume. Test databases/containers are independent of it. See the [acceptance coverage map](testing.md).
