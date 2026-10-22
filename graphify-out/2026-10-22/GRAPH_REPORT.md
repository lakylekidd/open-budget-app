# Graph Report - ynab-clone  (2026-10-22)

## Corpus Check
- 133 files · ~67,409 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 9 file(s) not represented in the graph (top: .ttf 4, .css 3, (none) 2)

## Summary
- 1094 nodes · 3263 edges · 52 communities (47 shown, 5 thin omitted)
- Extraction: 91% EXTRACTED · 9% INFERRED · 0% AMBIGUOUS · INFERRED: 308 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `eb515193`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- App
- BudgetEntity
- CategoryEntity
- OpenBudget.Api
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
- InlineTransactionEditor.tsx
- TransactionEntity
- AccountRegister.tsx
- Payees.test.tsx
- TransactionSelectionApiTests
- API reference
- List
- BudgetDbContext
- Guid
- PayeeEntity
- devDependencies
- TransactionSelectionBar.test.tsx
- .Activity
- Cash ledger slice
- Testing and acceptance coverage
- Agent Instructions
- CategoryPicker
- docs/README.md
- .Apply
- AccountEntity
- Milestone 1 — Reliable budget core and month lifecycle
- Milestone 2 — Accounts and transaction register
- RegisterViewEntity
- ScheduleOccurrenceEntity
- Milestone 7 — File import, matching, and portable data
- LedgerCommandEntity
- Developer setup
- Milestone 4 — Scheduled transactions and cash-flow awareness
- RenameFlagsRequest
- OpenBudget product roadmap
- OpenBudget
- SplitEntity
- Documentation

## God Nodes (most connected - your core abstractions)
1. `MutationDto` - 68 edges
2. `LedgerApiTests` - 66 edges
3. `LedgerService` - 62 edges
4. `OpenBudget.Api` - 42 edges
5. `TransactionEntity` - 41 edges
6. `BudgetDbContext` - 39 edges
7. `LedgerException` - 37 edges
8. `AccountsPage()` - 32 edges
9. `BudgetDto` - 30 edges
10. `App()` - 29 edges

## Surprising Connections (you probably didn't know these)
- ``GET /api/budget/{month}`` --references--> `month()`  [INFERRED]
  docs/api.md → src/OpenBudget.UI/src/ledger/ViewOptions.tsx
- `Importing an accepted normalized row` --references--> `TransactionRequest`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/LedgerContracts.cs
- `Register view preferences` --references--> `AccountViewOptions`  [INFERRED]
  docs/data-model.md → src/OpenBudget.Api/Migrations/20261021175223_AccountViewOptions.Designer.cs
- `Backdated assignment corrections` --references--> `BudgetCarryForward`  [INFERRED]
  docs/data-model.md → src/OpenBudget.Api/BudgetCarryForward.cs
- `Cash ledger slice` --references--> `BudgetService`  [INFERRED]
  docs/architecture.md → src/OpenBudget.Api/BudgetService.cs

## Import Cycles
- None detected.

## Communities (52 total, 5 thin omitted)

### Community 0 - "App"
Cohesion: 0.06
Nodes (22): App(), addFunds(), applyMove(), navigate(), saveEdit(), updateCategory(), BudgetPlan, BudgetState (+14 more)

### Community 1 - "BudgetEntity"
Cohesion: 0.09
Nodes (23): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+15 more)

### Community 2 - "CategoryEntity"
Cohesion: 0.13
Nodes (15): CategoryEntity, Activity, Assigned, Available, BudgetMonth, GroupId, Icon, Id (+7 more)

### Community 3 - "OpenBudget.Api"
Cohesion: 0.05
Nodes (19): OpenBudget.Api, OpenBudget.Api.Migrations, OpenBudget.UnitTests, Limitations and direction, Scheduling and flag storage, InitialCreate, BudgetCore, NamedBudget (+11 more)

### Community 4 - "vitest"
Cohesion: 0.08
Nodes (21): jsdom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript, vite (+13 more)

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
Cohesion: 0.12
Nodes (12): BudgetDto, BudgetPlanDto, BudgetPlanUpdateDto, BudgetPlanUpdateResult, BudgetUpdateDto, BudgetUpdateResult, CategoryDto, CategoryGroupDto (+4 more)

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.09
Nodes (23): Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Testcontainers.PostgreSql (4.16.0), Aspire.AppHost.Sdk/13.6.2 (+15 more)

### Community 12 - "Task"
Cohesion: 0.18
Nodes (14): Milestone 2 acceptance map, MutationDto, ReconcileRequest, StatusRequest, TransferRequest, UpdateAccountRequest, SavePayeesRequest, ScheduleActionRequest (+6 more)

### Community 13 - "LedgerService"
Cohesion: 0.20
Nodes (5): LedgerService, LedgerException, StatusCode, Change, Entries

### Community 14 - "LedgerRulesTests"
Cohesion: 0.10
Nodes (11): RegisterViewDto, PayeeDto, PayeeRuleDto, PayeeRules, PayeesDto, PayeeUpdateDto, RegisterViewService, LedgerRulesTests (+3 more)

### Community 15 - "AccountsPage"
Cohesion: 0.11
Nodes (23): AccountForm(), AccountsPage(), applyView(), clearFilters(), editor(), mutate(), renameFlags(), saveInline() (+15 more)

### Community 16 - "PostgresFixture.cs"
Cohesion: 0.07
Nodes (8): FailureInjector, FailAuditSave, FailBudgetAfterSave, PostgresFixture, TestClock, LocalTimeZone, UtcNow, Zone

### Community 17 - "AccountsPage.tsx"
Cohesion: 0.15
Nodes (21): Dialog, Account, Audit, Entry, loadCategories(), loadCategoryBudget(), parseMoney(), Register (+13 more)

### Community 18 - "ScheduleEntity"
Cohesion: 0.10
Nodes (19): SplitDto, RecurrenceRules, ScheduleTemplateDto, ScheduleEntity, AccountId, AnchorDate, Count, EndDate (+11 more)

### Community 19 - "InlineTransactionEditor.tsx"
Cohesion: 0.14
Nodes (17): react, react-dom, ApiError, InlineFields, LedgerDialog(), Category, Month, NewCategoryDialog() (+9 more)

### Community 20 - "TransactionEntity"
Cohesion: 0.10
Nodes (19): TransactionEntity, Account, AccountId, Amount, CategoryId, Date, FlagColor, Id (+11 more)

### Community 21 - "AccountRegister.tsx"
Cohesion: 0.19
Nodes (17): AccountHeader(), balanceSummary(), ClearedIndicator(), reconciliationText(), RegisterIcon(), account, entry, TransactionRow() (+9 more)

### Community 22 - "Payees.test.tsx"
Cohesion: 0.11
Nodes (16): requestJson(), ManagePayees(), close(), done(), edit(), rule(), update(), PayeeList (+8 more)

### Community 24 - "API reference"
Cohesion: 0.09
Nodes (22): API reference, Approval and attention, Assignment rollover and earlier-month edits, Boundaries, Cash accounts and reconciliation, `GET /api/budget`, `GET /api/budget/{month}`, `GET /health` (+14 more)

### Community 25 - "List"
Cohesion: 0.16
Nodes (11): AccountDto, AccountsDto, AuditDto, AuditPageDto, DeleteTransactionRequest, RegisterDto, ReviewTransactionRequest, TransactionDto (+3 more)

### Community 26 - "BudgetDbContext"
Cohesion: 0.10
Nodes (15): BudgetDbContext, Accounts, BudgetPlans, Budgets, Categories, CategoryGroups, FlagDefinitions, LedgerCommands (+7 more)

### Community 27 - "Guid"
Cohesion: 0.25
Nodes (5): CreateAccountRequest, TransactionRequest, LedgerBalanceRow, Balance, Id

### Community 28 - "PayeeEntity"
Cohesion: 0.12
Nodes (18): LedgerModel, LedgerStateEntity, Id, Version, PayeeEntity, CategoryMode, DefaultCategoryId, Id (+10 more)

### Community 29 - "devDependencies"
Cohesion: 0.11
Nodes (17): devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript (+9 more)

### Community 30 - "TransactionSelectionBar.test.tsx"
Cohesion: 0.15
Nodes (13): csvText(), selectedTransactionsCsv(), accounts, apply, clear, entry, show(), TransactionSelectionBar() (+5 more)

### Community 31 - ".Activity"
Cohesion: 0.15
Nodes (10): Backdated assignment corrections, `BudgetPlans`, `Budgets`, Calculations and month lifecycle, `CategoryGroups` and `Categories`, Current PostgreSQL schema, Data model, Ledger tables (+2 more)

### Community 32 - "Cash ledger slice"
Cohesion: 0.15
Nodes (11): Architecture and current status, Cash ledger slice, Current feature inventory, Design limits to address, Implemented user path, Runtime topology, Technologies, API (+3 more)

### Community 33 - "Testing and acceptance coverage"
Cohesion: 0.14
Nodes (14): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 2 verification (2026-10-21) (+6 more)

### Community 34 - "Agent Instructions"
Cohesion: 0.15
Nodes (12): Agent Instructions, Before making changes, Coding Guidelines, Development Principles, Documentation, Git, Project - OpenBudget, Roadmap (+4 more)

### Community 35 - "CategoryPicker"
Cohesion: 0.26
Nodes (9): CategoryOption, categoryGroups(), CategoryPicker(), choose(), close(), option(), outside(), categories (+1 more)

### Community 38 - "AccountEntity"
Cohesion: 0.18
Nodes (10): AccountEntity, AccountType, Id, IsClosed, IsFavorite, Name, Notes, FlagDefinitionEntity (+2 more)

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.20
Nodes (10): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Product references (+2 more)

### Community 41 - "RegisterViewEntity"
Cohesion: 0.20
Nodes (10): RegisterViewEntity, DatePreset, FromMonth, Scope, ShowFlags, ShowImages, ShowReconciled, ShowRunningBalance (+2 more)

### Community 42 - "ScheduleOccurrenceEntity"
Cohesion: 0.20
Nodes (9): ScheduleOccurrenceEntity, DeferredDate, Disposition, DueDate, Id, OverrideJson, ScheduleId, Sequence (+1 more)

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.22
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

### Community 44 - "LedgerCommandEntity"
Cohesion: 0.22
Nodes (9): LedgerCommandEntity, AfterJson, BeforeJson, CreatedAt, Fingerprint, Id, Kind, ResourceId (+1 more)

### Community 45 - "Developer setup"
Cohesion: 0.25
Nodes (8): Build commands, Current development boundaries, Database lifecycle, Developer setup, First run, Prerequisites, Request flow, Tests

### Community 46 - "Milestone 4 — Scheduled transactions and cash-flow awareness"
Cohesion: 0.29
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 4 — Scheduled transactions and cash-flow awareness, Product references, User value, Vertical slice

### Community 47 - "RenameFlagsRequest"
Cohesion: 0.53
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

## Knowledge Gaps
- **168 isolated node(s):** `BudgetPlans`, `Budgets`, `CategoryGroups`, `Categories`, `FlagDefinitions` (+163 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 431 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Cash ledger slice` connect `Cash ledger slice` to `CategoryPicker`, `HttpClient`, `LedgerService`, `AccountsPage.tsx`, `InlineTransactionEditor.tsx`, `Payees.test.tsx`?**
  _High betweenness centrality (0.357) - this node is a cross-community bridge._
- **What connects `BudgetPlans`, `Budgets`, `CategoryGroups` to the rest of the system?**
  _168 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `App` be split into smaller, more focused modules?**
  _Cohesion score 0.05919661733615222 - nodes in this community are weakly interconnected._
- **Why does `LedgerService` connect `LedgerService` to `Cash ledger slice`, `OpenBudget.Api`, `HttpClient`, `LedgerRulesTests`, `RenameFlagsRequest`, `ScheduleEntity`, `TransactionEntity`, `List`, `BudgetDbContext`, `Guid`?**
  _High betweenness centrality (0.225) - this node is a cross-community bridge._
- **Should `BudgetEntity` be split into smaller, more focused modules?**
  _Cohesion score 0.0873015873015873 - nodes in this community are weakly interconnected._
- **Why does `BudgetDbContext` connect `BudgetDbContext` to `Cash ledger slice`, `BudgetEntity`, `CategoryEntity`, `AccountEntity`, `HttpClient`, `RegisterViewEntity`, `ScheduleOccurrenceEntity`, `LedgerCommandEntity`, `LedgerService`, `LedgerRulesTests`, `Task`, `ScheduleEntity`, `TransactionEntity`, `PayeeEntity`?**
  _High betweenness centrality (0.109) - this node is a cross-community bridge._
- **Should `CategoryEntity` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._