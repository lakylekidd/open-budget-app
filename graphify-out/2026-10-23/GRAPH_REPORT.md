# Graph Report - ynab-clone  (2026-10-22)

## Corpus Check
- 138 files · ~71,298 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 9 file(s) not represented in the graph (top: .ttf 4, .css 3, (none) 2)

## Summary
- 1162 nodes · 3303 edges · 64 communities (54 shown, 10 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 260 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `8525f4c4`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- App
- BudgetEntity
- CategoryEntity
- OpenBudget.Api.Migrations
- vitest
- http
- compilerOptions
- http
- HttpClient
- OpenBudget.IntegrationTests.csproj
- Task
- LedgerService
- LedgerRulesTests
- AccountsPage
- PostgresFixture.cs
- AccountsPage.tsx
- ScheduleEntity
- NewCategoryDialog.tsx
- TransactionEntity
- AccountRegister.tsx
- Payees.test.tsx
- TransactionSelectionApiTests
- API reference
- Guid
- BudgetDbContext
- LedgerBalanceRow
- PayeeEntity
- OpenBudget.Api
- TransactionSelectionBar.test.tsx
- .Activity
- Architecture and current status
- Testing and acceptance coverage
- Agent Instructions
- InlineTransactionEditor.tsx
- docs/README.md
- .Apply
- AccountEntity
- Milestone 1 — Reliable budget core and month lifecycle
- Milestone 2 — Accounts and transaction register
- api.ts
- LedgerService
- Milestone 7 — File import, matching, and portable data
- LedgerCommandEntity
- Developer setup
- Milestone 4 — Scheduled transactions and cash-flow awareness
- RenameFlagsRequest
- OpenBudget product roadmap
- OpenBudget
- SplitEntity
- Documentation
- MigrationBuilder
- TestApp
- npgsql_entityframeworkcore_postgresql_metadata
- BudgetService
- BudgetPlanEntity
- API
- 20261021181641_TransactionFlags.Designer.cs
- CategoryGroupEntity
- 20261021175223_AccountViewOptions.Designer.cs
- ReconciliationEntity
- 20261021184241_ScheduledApproval.Designer.cs
- SchedulePostingWorker

## God Nodes (most connected - your core abstractions)
1. `LedgerApiTests` - 67 edges
2. `OpenBudget.Api` - 43 edges
3. `App()` - 42 edges
4. `BudgetDbContext` - 39 edges
5. `LedgerException` - 37 edges
6. `TransactionEntity` - 33 edges
7. `LedgerService` - 32 edges
8. `AccountsPage()` - 32 edges
9. `LedgerService` - 29 edges
10. `BudgetEntity` - 27 edges

## Surprising Connections (you probably didn't know these)
- ``GET /api/budget/{month}`` --references--> `month()`  [INFERRED]
  docs/api.md → src/OpenBudget.UI/src/ledger/ViewOptions.tsx
- `Importing an accepted normalized row` --references--> `TransactionRequest`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/LedgerContracts.cs
- `Cash ledger slice` --references--> `BudgetCarryForward`  [INFERRED]
  docs/architecture.md → src/OpenBudget.Api/BudgetCarryForward.cs
- `Cash ledger slice` --references--> `BudgetService`  [INFERRED]
  docs/architecture.md → src/OpenBudget.Api/BudgetService.cs
- `Cash ledger slice` --references--> `LedgerGate`  [INFERRED]
  docs/architecture.md → src/OpenBudget.Api/LedgerGate.cs

## Import Cycles
- None detected.

## Communities (64 total, 10 thin omitted)

### Community 0 - "App"
Cohesion: 0.06
Nodes (24): App(), addFunds(), applyMove(), closeCategoryDialog(), dropCategory(), dropGroup(), moveCategory(), moveGroup() (+16 more)

### Community 1 - "BudgetEntity"
Cohesion: 0.18
Nodes (10): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+2 more)

### Community 2 - "CategoryEntity"
Cohesion: 0.13
Nodes (15): CategoryEntity, Activity, Assigned, Available, BudgetMonth, GroupId, Icon, Id (+7 more)

### Community 3 - "OpenBudget.Api.Migrations"
Cohesion: 0.17
Nodes (5): OpenBudget.Api.Migrations, BudgetCore, NamedBudget, AccountNotes, PayeeManagement

### Community 4 - "vitest"
Cohesion: 0.06
Nodes (31): jsdom, @testing-library/jest-dom, @types/react, @types/react-dom, typescript, vite, @vitejs/plugin-react, dependencies (+23 more)

### Community 5 - "http"
Cohesion: 0.13
Nodes (18): ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL, ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL, ASPNETCORE_ENVIRONMENT, DOTNET_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables (+10 more)

### Community 6 - "compilerOptions"
Cohesion: 0.11
Nodes (17): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+9 more)

### Community 7 - "http"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 8 - "HttpClient"
Cohesion: 0.16
Nodes (10): BudgetDto, BudgetPlanDto, BudgetPlanUpdateDto, BudgetPlanUpdateResult, BudgetUpdateDto, CategoryDto, CategoryGroupDto, CategoryGroupUpdateDto (+2 more)

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.09
Nodes (23): Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Testcontainers.PostgreSql (4.16.0), Aspire.AppHost.Sdk/13.6.2 (+15 more)

### Community 12 - "Task"
Cohesion: 0.18
Nodes (5): Milestone 2 acceptance map, SavePayeesRequest, ScheduleActionRequest, ScheduleRequest, LedgerApiTests

### Community 13 - "LedgerService"
Cohesion: 0.11
Nodes (5): LedgerException, StatusCode, LedgerRules, LedgerService, Entries

### Community 14 - "LedgerRulesTests"
Cohesion: 0.06
Nodes (22): OpenBudget.UnitTests, RegisterViewDto, RegisterViewEntity, DatePreset, FromMonth, Scope, ShowFlags, ShowImages (+14 more)

### Community 15 - "AccountsPage"
Cohesion: 0.19
Nodes (12): checking, savings, show(), defaultView, month(), presetMonths(), presets, viewDates() (+4 more)

### Community 17 - "AccountsPage.tsx"
Cohesion: 0.12
Nodes (27): Cash ledger slice, AccountForm(), AccountsPage(), applyView(), clearFilters(), editor(), mutate(), renameFlags() (+19 more)

### Community 18 - "ScheduleEntity"
Cohesion: 0.08
Nodes (25): ScheduleEntity, AccountId, AnchorDate, Count, EndDate, FlagColor, Id, IsEnded (+17 more)

### Community 19 - "NewCategoryDialog.tsx"
Cohesion: 0.18
Nodes (8): ApiError, Category, Month, NewCategoryDialog(), save(), close, created, show()

### Community 20 - "TransactionEntity"
Cohesion: 0.11
Nodes (18): TransactionEntity, Account, AccountId, Amount, CategoryId, Date, FlagColor, Id (+10 more)

### Community 21 - "AccountRegister.tsx"
Cohesion: 0.18
Nodes (16): @testing-library/react, @testing-library/user-event, vitest, AccountHeader(), balanceSummary(), ClearedIndicator(), reconciliationText(), account (+8 more)

### Community 22 - "Payees.test.tsx"
Cohesion: 0.10
Nodes (18): ManagePayees(), close(), done(), edit(), rule(), update(), PayeeList, PayeePicker() (+10 more)

### Community 24 - "API reference"
Cohesion: 0.09
Nodes (22): API reference, Approval and attention, Assignment rollover and earlier-month edits, Boundaries, Cash accounts and reconciliation, `GET /api/budget`, `GET /api/budget/{month}`, `GET /health` (+14 more)

### Community 25 - "Guid"
Cohesion: 0.15
Nodes (17): AccountDto, AccountsDto, AuditDto, AuditPageDto, CreateAccountRequest, DeleteTransactionRequest, ReconcileRequest, RegisterDto (+9 more)

### Community 26 - "BudgetDbContext"
Cohesion: 0.09
Nodes (15): BudgetDbContext, Accounts, BudgetPlans, Budgets, Categories, CategoryGroups, FlagDefinitions, LedgerCommands (+7 more)

### Community 27 - "LedgerBalanceRow"
Cohesion: 0.67
Nodes (3): LedgerBalanceRow, Balance, Id

### Community 28 - "PayeeEntity"
Cohesion: 0.25
Nodes (8): PayeeEntity, CategoryMode, DefaultCategoryId, Id, IsHidden, Name, NormalizedName, RulesJson

### Community 29 - "OpenBudget.Api"
Cohesion: 0.12
Nodes (6): OpenBudget.Api, OpenBudget.IntegrationTests, FlagDefinitionEntity, Color, Name, Program

### Community 30 - "TransactionSelectionBar.test.tsx"
Cohesion: 0.13
Nodes (21): RegisterIcon(), defaultFlags, EditFlags(), Flag, FlagButton(), Test(), LedgerDialog(), csvText() (+13 more)

### Community 31 - ".Activity"
Cohesion: 0.20
Nodes (7): `BudgetPlans`, `Budgets`, Calculations and month lifecycle, `CategoryGroups` and `Categories`, Current PostgreSQL schema, Data model, Ledger tables

### Community 32 - "Architecture and current status"
Cohesion: 0.33
Nodes (6): Architecture and current status, Current feature inventory, Design limits to address, Implemented user path, Runtime topology, Technologies

### Community 33 - "Testing and acceptance coverage"
Cohesion: 0.14
Nodes (14): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 2 verification (2026-10-21) (+6 more)

### Community 34 - "Agent Instructions"
Cohesion: 0.15
Nodes (12): Agent Instructions, Before making changes, Coding Guidelines, Development Principles, Documentation, Git, Project - OpenBudget, Roadmap (+4 more)

### Community 35 - "InlineTransactionEditor.tsx"
Cohesion: 0.15
Nodes (18): react, react-dom, CategoryOption, Split, categoryGroups(), CategoryPicker(), choose(), close() (+10 more)

### Community 38 - "AccountEntity"
Cohesion: 0.17
Nodes (11): AccountEntity, AccountType, Id, IsClosed, IsFavorite, Name, Notes, LedgerModel (+3 more)

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.22
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.18
Nodes (11): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Plan interaction extension (2026-10-22) (+3 more)

### Community 41 - "api.ts"
Cohesion: 0.14
Nodes (19): BudgetPlan, BudgetState, Category, CategoryGroup, CategoryTransactionDialog, currency, Filter, filters (+11 more)

### Community 42 - "LedgerService"
Cohesion: 0.23
Nodes (6): LedgerService, MutationDto, SplitDto, Change, RecurrenceRules, ScheduleTemplateDto

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.25
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

### Community 44 - "LedgerCommandEntity"
Cohesion: 0.11
Nodes (12): LedgerCommandEntity, AfterJson, BeforeJson, CreatedAt, Fingerprint, Id, Kind, ResourceId (+4 more)

### Community 45 - "Developer setup"
Cohesion: 0.25
Nodes (8): Build commands, Current development boundaries, Database lifecycle, Developer setup, First run, Prerequisites, Request flow, Tests

### Community 46 - "Milestone 4 — Scheduled transactions and cash-flow awareness"
Cohesion: 0.29
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 4 — Scheduled transactions and cash-flow awareness, Product references, User value, Vertical slice

### Community 47 - "RenameFlagsRequest"
Cohesion: 0.48
Nodes (4): FlagDto, FlagPaletteDto, RenameFlagsRequest, SetFlagRequest

### Community 48 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

### Community 49 - "OpenBudget"
Cohesion: 0.40
Nodes (5): Build, OpenBudget, Project layout, Run the application, Test

### Community 50 - "SplitEntity"
Cohesion: 0.40
Nodes (5): SplitEntity, Amount, CategoryId, Position, TransactionId

### Community 51 - "Documentation"
Cohesion: 0.67
Nodes (3): Current release shape, Documentation, Documents

### Community 52 - "MigrationBuilder"
Cohesion: 0.13
Nodes (3): InitialCreate, AccountRegisterPresentation, CategoryMoves

### Community 53 - "TestApp"
Cohesion: 0.13
Nodes (8): TestApp, Clock, ConnectionString, Failure, TestClock, LocalTimeZone, UtcNow, Zone

### Community 54 - "npgsql_entityframeworkcore_postgresql_metadata"
Cohesion: 0.20
Nodes (3): CashLedger, ScheduledTransactions, BudgetDbContextModelSnapshot

### Community 56 - "BudgetPlanEntity"
Cohesion: 0.28
Nodes (6): BudgetPlanEntity, Currency, Id, Months, Name, Version

### Community 57 - "API"
Cohesion: 0.29
Nodes (6): API, OpenBudget API and local services, Backdated assignment corrections, Payee management, BudgetCarryForward, LedgerGate

### Community 58 - "20261021181641_TransactionFlags.Designer.cs"
Cohesion: 0.29
Nodes (3): Limitations and direction, Scheduling and flag storage, TransactionFlags

### Community 59 - "CategoryGroupEntity"
Cohesion: 0.29
Nodes (7): CategoryGroupEntity, BudgetMonth, Categories, Id, IsArchived, Name, SortOrder

### Community 61 - "ReconciliationEntity"
Cohesion: 0.33
Nodes (6): ReconciliationEntity, AccountId, CreatedAt, Id, IsInvalidated, StatementBalance

## Knowledge Gaps
- **168 isolated node(s):** `BudgetPlans`, `Budgets`, `CategoryGroups`, `Categories`, `FlagDefinitions` (+163 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 456 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **10 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Cash ledger slice` connect `AccountsPage.tsx` to `Architecture and current status`, `InlineTransactionEditor.tsx`, `NewCategoryDialog.tsx`, `Payees.test.tsx`, `BudgetService`, `API`?**
  _High betweenness centrality (0.312) - this node is a cross-community bridge._
- **What connects `BudgetPlans`, `Budgets`, `CategoryGroups` to the rest of the system?**
  _168 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `App` be split into smaller, more focused modules?**
  _Cohesion score 0.0613107822410148 - nodes in this community are weakly interconnected._
- **Why does `BudgetService` connect `BudgetService` to `BudgetEntity`, `HttpClient`, `LedgerService`, `AccountsPage.tsx`, `BudgetPlanEntity`, `API`, `BudgetDbContext`, `OpenBudget.Api`?**
  _High betweenness centrality (0.198) - this node is a cross-community bridge._
- **Should `CategoryEntity` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._
- **Why does `OpenBudget.Api` connect `OpenBudget.Api` to `OpenBudget.Api.Migrations`, `HttpClient`, `LedgerRulesTests`, `RenameFlagsRequest`, `PostgresFixture.cs`, `ScheduleEntity`, `MigrationBuilder`, `npgsql_entityframeworkcore_postgresql_metadata`, `20261021184241_ScheduledApproval.Designer.cs`, `API`, `20261021181641_TransactionFlags.Designer.cs`, `20261021175223_AccountViewOptions.Designer.cs`, `Guid`?**
  _High betweenness centrality (0.095) - this node is a cross-community bridge._
- **Should `vitest` be split into smaller, more focused modules?**
  _Cohesion score 0.05714285714285714 - nodes in this community are weakly interconnected._