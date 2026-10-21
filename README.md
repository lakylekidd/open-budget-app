# OpenBudget

OpenBudget is a local-first budgeting application inspired by publicly documented budgeting workflows. The current implementation provides a React and TypeScript Plan screen backed by a .NET 10 API and PostgreSQL, orchestrated locally with .NET Aspire.

## Run the application

From the repository root:

```powershell
dotnet run --project src/OpenBudget.AppHost
```

Aspire starts PostgreSQL, the API, and the Vite UI. Open the `ui` endpoint shown in the Aspire dashboard; the stable local URL is `http://127.0.0.1:5173/`.

Requirements: .NET 10 SDK, Node.js/npm, and Docker Desktop. PostgreSQL uses a persistent volume. On a fresh machine, set the local AppHost password as described in [Developer setup](docs/developer-guide.md).

## Project layout

- [`docs/`](docs/README.md): developer documentation and product roadmap.
- [`src/OpenBudget.UI/`](src/OpenBudget.UI/): React and TypeScript frontend.
- [`src/OpenBudget.Api/`](src/OpenBudget.Api/): .NET 10 Minimal API, EF Core model, and migrations.
- [`src/OpenBudget.AppHost/`](src/OpenBudget.AppHost/AppHost.cs): Aspire orchestration for PostgreSQL, API, and UI.
- [`OpenBudget.slnx`](OpenBudget.slnx): .NET solution.

## Build

```powershell
dotnet build OpenBudget.slnx
npm --prefix src/OpenBudget.UI run build
```

See the [documentation index](docs/README.md) for architecture, API, data model, and roadmap details.