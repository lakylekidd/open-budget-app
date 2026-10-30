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

In the Development environment only, OpenBudget provisions the local administrator as `admin` / `admin` when no `BootstrapAdmin` settings are supplied. These defaults are not used in Staging or Production. For the first deployment against a database, set `BootstrapAdmin__Username`, `BootstrapAdmin__Email`, and `BootstrapAdmin__Password` in the deployment environment or secret store. Startup applies database migrations and then creates that permanent administrator. Keep the settings available on later starts: provisioning is idempotent, and an existing administrator's changed password is never reset.

To enable registration and account verification/recovery mail, configure SMTP from **Administration → Email delivery**. The settings page stores host, port, sender, optional username/password and TLS choice; the password is encrypted at rest and never displayed after saving. Use **Send test email** before enabling registration. Runtime `Smtp__Host`, `Smtp__Port`, `Smtp__From`, optional `Smtp__Username` / `Smtp__Password`, and optional `Smtp__EnableSsl` remain a deployment fallback until an administrator saves an in-app SMTP configuration. In shared environments also set `AccountEmail__PublicOrigin` to the app's trusted HTTPS origin so emailed links never use an untrusted request host. The Compose deployment persists Data Protection keys in `openbudget-data-protection`; do not remove that volume, or stored SMTP credentials and Identity tokens become unreadable. The production API is private to the Compose network behind the UI nginx proxy; nginx forwards the external host and HTTPS scheme for secure cookies and origin checks. Do not publish the API port directly. Registration remains disabled until an administrator selects a mode. Verification and reset tokens expire after two hours and are never returned by API responses. Registration and recovery email requests are limited to five per minute per client IP.

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

The browser calls same-origin `/api/...` paths. Vite proxies `/api`, `/health` and `/mcp` to the API on port 5080 with `changeOrigin: false`, preserving the UI host for the API's origin checks; the proxy keys are anchored regexes so client-side routes such as `http://127.0.0.1:5173/{budgetKey}/account/{slug}` are never proxied and load `index.html` on refresh (see [URL routing](architecture.md#url-routing)). Do not replace the proxy options with Vite's string shorthand: it rewrites the host, causing browser writes (including login) to return 403. In Aspire, the API and UI have stable local endpoints so the Vite proxy keeps working while Aspire controls startup and health ordering.

The Plan screen starts with an empty loading state and renders budget amounts only after the monthly budget response arrives. This prevents placeholder/demo values from appearing briefly during a reload.

## Current development boundaries

- Sign-in uses ASP.NET Core Identity and an HttpOnly, SameSite=Strict cookie. Authenticated users can manage owned budget identities and use owner-checked monthly Plan, ledger and long-term planning routes. The UI supports budget creation, rename, selection and persisted active-budget choice. Legacy `default` routes remain administrator-only.
- The Plan API stores versioned editable monthly snapshots per budget. The cash ledger uses replay-safe commands and shared revisions; financial rows and revisions are isolated by budget.
- Long-Term Planning stores versioned category budget overrides, projects income and expenses from active scheduled ledger transactions, and reads existing actual snapshots without initializing missing months. Its requests are scoped to the selected active budget.
- Cash account/sidebar balances and monthly activity are ledger-backed; Reflect, credit/debt and linking remain future milestones; scheduling/approval and payee management are implemented register extensions.
- Every feature requires unit/integration tests and UI workflow coverage, as specified in `AGENTS.md` and [testing](testing.md).

## Tests

```powershell
dotnet test OpenBudget.slnx
npm --prefix src/OpenBudget.UI test
```

`OpenBudget.UnitTests` covers domain rules/calculations. `OpenBudget.IntegrationTests` uses the real HTTP API/EF migrations and a disposable PostgreSQL 17 Testcontainers instance with a fresh database per test. Docker must be running; unavailable Docker fails tests rather than silently skipping them. No developer database connection or financial-service credential is required. Vitest/Testing Library test the ledger UI in jsdom.

## GitHub pull requests and releases

`.github/workflows/test.yml` runs the .NET solution build, unit and PostgreSQL integration tests, UI tests, and UI production build for every pull request and for pushes to `main`. Configure a branch protection rule for `main` that requires the **Build and test / verify** check before merging. This repository setting cannot be enabled by a workflow file.

Creating a GitHub Release from a version tag such as `v0.2.0` runs `.github/workflows/release.yml`. It waits for the same verification job, then sends an authenticated POST to the Coolify deployment webhook using the repository secrets `COOLIFY_WEBHOOK_URL` and `COOLIFY_TOKEN`. Create the Coolify API token with the `deploy` permission for the team that owns the application. Missing secrets fail the deployment job clearly. Keep both values secret and use a persistent PostgreSQL volume in the Coolify service configuration. The workflow reports success when Coolify accepts the webhook; check Coolify for the final deployment and health status. The webhook deploys the ref configured on the Coolify resource, currently `main`, so publish release tags from the intended `main` commit.

To retry a release deployment after a workflow fix, use **Actions → Release and deploy → Run workflow** and enter the existing release tag. This reruns verification against that tag before requesting deployment; it does not move the tag or republish the release.

For Coolify, create an application from this repository using the `docker-compose.yml` file in the repository root. It builds the .NET API and React UI and runs PostgreSQL 17 with a persistent volume. Set `POSTGRES_PASSWORD` as an environment variable in Coolify to a strong random value. Route the application's domain to the `ui` service on port 80. The API applies EF migrations at startup. PostgreSQL, the API, and the UI have Docker health checks; the API check verifies database connectivity through `/health`, and the UI check reaches that endpoint through nginx. Coolify reads these checks from the Compose definition, so redeploy the resource after changing them. Configure the Coolify Git source to use the release tag/ref for production deployments; the release webhook triggers the configured resource.

For focused runs, use `dotnet test tests/OpenBudget.UnitTests`, `dotnet test tests/OpenBudget.IntegrationTests`, or `npm --prefix src/OpenBudget.UI run test:watch`.

After backend changes, stop the AppHost, rebuild the solution and restart before live verification. Windows running executables can lock build output; avoid building over an active API process. Preserve the named development volume. Test databases/containers are independent of it. See the [acceptance coverage map](testing.md).
