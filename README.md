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

- Monthly Plan with category assignments, selected-category Auto-Assign, available balances, and transaction activity
- Cash accounts with a searchable register, transfers, splits, and reconciliation
- Credit Card and Line of Credit accounts with automatic Credit Card Payments categories and debt payoff targets (pay off by date or a fixed monthly amount)
- Category targets (weekly, monthly, yearly or custom with repeat) with server-calculated "more needed this month" progress in Plan
- Scheduled transactions, payee management, and audit history
- Receipt images on transactions: drag and drop, paste, upload or import from a link, with a full-screen viewer; files are type-checked by content and stripped of location metadata
- Long-Term Planning with annual forecasts and monthly actuals
- Wishlist (Plus/Pro): ranked wish-list buckets funded from Plan, with "ready to buy" progress for the most-wanted item
- Identity sign-in, verified registration and branded recovery email, plus audited administrator account controls and SMTP settings

## Quick start

Requirements: .NET 10 SDK, Node.js/npm, and Docker Desktop with Linux containers.

On a new machine, [set the local PostgreSQL password](docs/developer-guide.md#first-run), then run:

```powershell
dotnet run --project src/OpenBudget.AppHost
```

Open the `ui` endpoint in the Aspire dashboard, or visit `http://127.0.0.1:5173/`.
The local Development administrator is `admin` / `admin`. Production deployments require `BootstrapAdmin__Username`, `BootstrapAdmin__Email`, and `BootstrapAdmin__Password`; see the [release deployment guide](docs/release-deployment.md#2-configure-coolify).

## Documentation

- [Developer setup, builds, tests, and releases](docs/developer-guide.md)
- [Architecture and current capabilities](docs/architecture.md)
- [API reference](docs/api.md) and [data model](docs/data-model.md)
- [Testing and acceptance coverage](docs/testing.md)
- [Product roadmap](docs/roadmap.md)

## Project status

OpenBudget is under active development. Milestone 2a is implemented: authentication, administrator controls, owner-scoped budgets, monthly Plan snapshots, and budget-isolated ledger and long-term planning data are in place. See the [architecture](docs/architecture.md) and [roadmap](docs/roadmap.md) for current capabilities and planned work.
