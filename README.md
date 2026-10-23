<p align="center">
  <img src="src/OpenBudget.UI/public/openbudget-logo.svg" alt="OpenBudget logo" width="112">
</p>

<h1 align="center">OpenBudget</h1>

<p align="center">
  Plan your money, track your accounts, and see what is ahead.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/React-TypeScript-61DAFB?logo=react&logoColor=111827" alt="React and TypeScript">
  <img src="https://img.shields.io/badge/PostgreSQL-17-4169E1?logo=postgresql&logoColor=white" alt="PostgreSQL 17">
  <img src="https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white" alt="Docker Compose">
</p>

OpenBudget is a budgeting app with a React UI, .NET API, and PostgreSQL database. .NET Aspire runs the development stack locally; Docker Compose packages the services for deployment.

## Highlights

- Monthly Plan with category assignments, available balances, and transaction activity
- Cash accounts with a searchable register, transfers, splits, and reconciliation
- Scheduled transactions, payee management, and audit history
- Long-Term Planning with annual forecasts and monthly actuals
- Identity sign-in, verified registration and recovery email, plus audited administrator account controls

## Quick start

Requirements: .NET 10 SDK, Node.js/npm, and Docker Desktop with Linux containers.

On a new machine, [set the local PostgreSQL password](docs/developer-guide.md#first-run), then run:

```powershell
dotnet run --project src/OpenBudget.AppHost
```

Open the `ui` endpoint in the Aspire dashboard, or visit `http://127.0.0.1:5173/`.

## Documentation

- [Developer setup, builds, tests, and releases](docs/developer-guide.md)
- [Architecture and current capabilities](docs/architecture.md)
- [API reference](docs/api.md) and [data model](docs/data-model.md)
- [Testing and acceptance coverage](docs/testing.md)
- [Product roadmap](docs/roadmap.md)

## Project status

OpenBudget is under active development. Authentication, administrator account controls and owner-scoped budget identity/limits are in place, while Plan, ledger and planning data still use the legacy single-budget model. Keep deployments private until financial-data isolation is implemented. See the [architecture](docs/architecture.md) and [roadmap](docs/roadmap.md) for current scope and planned work.
