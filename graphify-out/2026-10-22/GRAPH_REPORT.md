# Graph Report - ynab-clone  (2026-10-22)

## Corpus Check
- 150 files · ~80,311 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 13 file(s) not represented in the graph (top: .ttf 4, .css 4, (none) 3)

## Summary
- 1327 nodes · 3830 edges · 80 communities (69 shown, 11 thin omitted)
- Extraction: 91% EXTRACTED · 9% INFERRED · 0% AMBIGUOUS · INFERRED: 341 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `9a9e9b4d`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- App
- .Apply
- CategoryEntity
- OpenBudget.Api.Migrations
- vitest
- http
- compilerOptions
- http
- HttpClient
- OpenBudget.IntegrationTests.csproj
- Task
- Guid
- LedgerRulesTests
- AccountsPage
- AccountsPage.tsx
- ScheduleEntity
- NewCategoryDialog.tsx
- TransactionEntity
- AccountRegister.tsx
- Payees.test.tsx
- LongTermPlanningDto
- API reference
- BudgetEntity
- BudgetDbContext
- LedgerBalanceRow
- ReconciliationEntity
- OpenBudget.Api
- TransactionSelectionBar.test.tsx
- LongTermPlanning.tsx
- Architecture and current status
- LedgerCommandEntity
- Agent Instructions
- CategoryPicker
- docs/README.md
- react
- AccountEntity
- Milestone 1 — Reliable budget core and month lifecycle
- Milestone 2 — Accounts and transaction register
- api.ts
- .ScheduleActionAsync
- Milestone 7 — File import, matching, and portable data
- LedgerCommandEntity
- Developer setup
- Milestone 4 — Scheduled transactions and cash-flow awareness
- BudgetContracts.cs
- CategoryGroupEntity
- LedgerRulesTests
- LongTermPlanning
- List
- MigrationBuilder
- Guid
- vitest
- BudgetService
- BudgetPlanEntity
- 20261021190028_PayeeManagement.Designer.cs
- RegisterViewEntity
- CategoryGroupEntity
- Current PostgreSQL schema
- OpenBudget product roadmap
- LongTermPlanning.test.tsx
- Milestone 3 — Goals, templates, and guided assignment
- Milestone 5 — Credit cards and debt payoff planning
- Milestone 6 — Focused plan views and reports
- Milestone 8 — Identity, multiple budgets, and household sharing
- Milestone 9 — Cross-device experience and offline resilience
- Milestone 10 — Optional linked-account import
- App.test.tsx
- Milestone: Annual Long-Term Budget Planning
- YNAB Web app: reverse-engineering notes
- Documentation
- Milestone 0 — Local application foundation
- planHelpers.ts
- PayeeEntity
- SchedulePostingWorker
- TestClock

## God Nodes (most connected - your core abstractions)
1. `MutationDto` - 69 edges
2. `LedgerApiTests` - 67 edges
3. `LedgerService` - 62 edges
4. `OpenBudget.Api` - 50 edges
5. `BudgetDbContext` - 46 edges
6. `App()` - 43 edges
7. `TransactionEntity` - 41 edges
8. `LedgerException` - 37 edges
9. `BudgetDto` - 34 edges
10. `AccountsPage()` - 32 edges

## Surprising Connections (you probably didn't know these)
- `Importing an accepted normalized row` --references--> `TransactionRequest`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/LedgerContracts.cs
- `Register view preferences` --references--> `AccountViewOptions`  [INFERRED]
  docs/data-model.md → src/OpenBudget.Api/Migrations/20261021175223_AccountViewOptions.Designer.cs
- ``GET /api/budget/{month}`` --references--> `month()`  [INFERRED]
  docs/api.md → src/OpenBudget.UI/src/ledger/ViewOptions.tsx
- `Milestone 2 acceptance map` --references--> `LedgerRulesTests`  [INFERRED]
  docs/testing.md → tests/OpenBudget.UnitTests/LedgerRulesTests.cs
- `Cash ledger slice` --references--> `LedgerService`  [INFERRED]
  docs/architecture.md → src/OpenBudget.Api/FlagService.cs

## Import Cycles
- None detected.

## Communities (80 total, 11 thin omitted)

### Community 0 - "App"
Cohesion: 0.10
Nodes (15): App(), addFunds(), closeCategoryDialog(), dropCategory(), dropGroup(), moveCategory(), moveGroup(), navigate() (+7 more)

### Community 2 - "CategoryEntity"
Cohesion: 0.13
Nodes (15): CategoryEntity, Activity, Assigned, Available, BudgetMonth, GroupId, Icon, Id (+7 more)

### Community 3 - "OpenBudget.Api.Migrations"
Cohesion: 0.12
Nodes (8): OpenBudget.Api.Migrations, BudgetCore, NamedBudget, CashLedger, AccountViewOptions, AccountNotes, LongTermPlanning, BudgetDbContextModelSnapshot

### Community 4 - "vitest"
Cohesion: 0.06
Nodes (30): jsdom, @types/react, @types/react-dom, typescript, vite, @vitejs/plugin-react, dependencies, react (+22 more)

### Community 5 - "http"
Cohesion: 0.13
Nodes (18): ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL, ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL, ASPNETCORE_ENVIRONMENT, DOTNET_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables (+10 more)

### Community 6 - "compilerOptions"
Cohesion: 0.11
Nodes (17): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+9 more)

### Community 7 - "http"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.08
Nodes (23): Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Testcontainers.PostgreSql (4.16.0), Aspire.AppHost.Sdk/13.6.2 (+15 more)

### Community 12 - "Task"
Cohesion: 0.14
Nodes (16): Milestone 2 acceptance map, MutationDto, ReconcileRequest, StatusRequest, TransferRequest, UpdateAccountRequest, SavePayeesRequest, ScheduleActionRequest (+8 more)

### Community 13 - "Guid"
Cohesion: 0.18
Nodes (7): API, LedgerService, LedgerException, StatusCode, LedgerGate, Change, Entries

### Community 14 - "LedgerRulesTests"
Cohesion: 0.22
Nodes (4): RegisterViewDto, RegisterViewService, PayeeRulesTests, RegisterViewTests

### Community 15 - "AccountsPage"
Cohesion: 0.12
Nodes (21): AccountForm(), AccountsPage(), applyView(), clearFilters(), mutate(), renameFlags(), saveInline(), scheduleAction() (+13 more)

### Community 17 - "AccountsPage.tsx"
Cohesion: 0.14
Nodes (26): Cash ledger slice, editor(), Dialog, Account, Accounts, Audit, CategoryOption, Entry (+18 more)

### Community 18 - "ScheduleEntity"
Cohesion: 0.07
Nodes (34): SplitDto, TransactionDto, LongTermPlanningActivityDto, ScheduledItemDto, SchedulesDto, ScheduleTemplateDto, ForecastOccurrence, ScheduledPlanningTotals (+26 more)

### Community 19 - "NewCategoryDialog.tsx"
Cohesion: 0.25
Nodes (5): NewCategoryDialog(), save(), close, created, show()

### Community 20 - "TransactionEntity"
Cohesion: 0.08
Nodes (19): TransactionEntity, Account, AccountId, Amount, CategoryId, Date, FlagColor, Id (+11 more)

### Community 21 - "AccountRegister.tsx"
Cohesion: 0.22
Nodes (13): @testing-library/user-event, AccountHeader(), balanceSummary(), ClearedIndicator(), reconciliationText(), RegisterIcon(), account, entry (+5 more)

### Community 22 - "Payees.test.tsx"
Cohesion: 0.11
Nodes (16): requestJson(), ManagePayees(), close(), done(), edit(), rule(), update(), PayeeList (+8 more)

### Community 23 - "LongTermPlanningDto"
Cohesion: 0.06
Nodes (33): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 acceptance map (+25 more)

### Community 24 - "API reference"
Cohesion: 0.09
Nodes (23): API reference, Approval and attention, Assignment rollover and earlier-month edits, Boundaries, Cash accounts and reconciliation, `GET /api/budget`, `GET /api/budget/{month}`, `GET /health` (+15 more)

### Community 25 - "BudgetEntity"
Cohesion: 0.20
Nodes (10): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+2 more)

### Community 26 - "BudgetDbContext"
Cohesion: 0.10
Nodes (18): BudgetDbContext, Accounts, BudgetPlans, Budgets, Categories, CategoryGroups, FlagDefinitions, LedgerCommands (+10 more)

### Community 27 - "LedgerBalanceRow"
Cohesion: 0.67
Nodes (3): LedgerBalanceRow, Balance, Id

### Community 28 - "ReconciliationEntity"
Cohesion: 0.18
Nodes (10): LedgerModel, LedgerStateEntity, Id, Version, ReconciliationEntity, AccountId, CreatedAt, Id (+2 more)

### Community 29 - "OpenBudget.Api"
Cohesion: 0.10
Nodes (6): OpenBudget.Api, OpenBudget.UnitTests, OpenBudget.IntegrationTests, CalculatedLongTermPlanningMonth, Program, RecurrenceTests

### Community 30 - "TransactionSelectionBar.test.tsx"
Cohesion: 0.15
Nodes (13): csvText(), selectedTransactionsCsv(), accounts, apply, clear, entry, show(), TransactionSelectionBar() (+5 more)

### Community 31 - "LongTermPlanning.tsx"
Cohesion: 0.11
Nodes (22): ActivityDialog, ActivityKind, ActivitySelection, actualMetrics, ConflictState, EditingValue, LongTermPlanningProps, Metric (+14 more)

### Community 32 - "Architecture and current status"
Cohesion: 0.33
Nodes (6): Architecture and current status, Current feature inventory, Design limits to address, Implemented user path, Runtime topology, Technologies

### Community 33 - "LedgerCommandEntity"
Cohesion: 0.22
Nodes (9): LedgerCommandEntity, AfterJson, BeforeJson, CreatedAt, Fingerprint, Id, Kind, ResourceId (+1 more)

### Community 34 - "Agent Instructions"
Cohesion: 0.15
Nodes (12): Agent Instructions, Before making changes, Coding Guidelines, Development Principles, Documentation, Git, Project - OpenBudget, Roadmap (+4 more)

### Community 35 - "CategoryPicker"
Cohesion: 0.27
Nodes (8): categoryGroups(), CategoryPicker(), choose(), close(), option(), outside(), categories, Picker()

### Community 36 - "docs/README.md"
Cohesion: 0.20
Nodes (6): OpenBudget API and local services, Coding conventions, Documentation, Highlights, Project status, Quick start

### Community 37 - "react"
Cohesion: 0.14
Nodes (15): react, react-dom, ApiError, defaultFlags, EditFlags(), Flag, FlagButton(), Test() (+7 more)

### Community 38 - "AccountEntity"
Cohesion: 0.29
Nodes (7): AccountEntity, AccountType, Id, IsClosed, IsFavorite, Name, Notes

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.18
Nodes (11): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Plan interaction extension (2026-10-22) (+3 more)

### Community 41 - "api.ts"
Cohesion: 0.12
Nodes (17): applyMove(), BudgetPlan, BudgetState, Category, CategoryGroup, CategoryTransactionDialog, currency, Filter (+9 more)

### Community 42 - ".ScheduleActionAsync"
Cohesion: 0.21
Nodes (6): SplitEntity, Amount, CategoryId, Position, TransactionId, RecurrenceRules

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.22
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

### Community 44 - "LedgerCommandEntity"
Cohesion: 0.22
Nodes (3): FailureInjector, FailAuditSave, FailBudgetAfterSave

### Community 45 - "Developer setup"
Cohesion: 0.22
Nodes (9): Build commands, Current development boundaries, Database lifecycle, Developer setup, First run, GitHub pull requests and releases, Prerequisites, Request flow (+1 more)

### Community 46 - "Milestone 4 — Scheduled transactions and cash-flow awareness"
Cohesion: 0.29
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 4 — Scheduled transactions and cash-flow awareness, Product references, User value, Vertical slice

### Community 47 - "BudgetContracts.cs"
Cohesion: 0.39
Nodes (6): BudgetPlanUpdateDto, BudgetUpdateDto, CategoryDto, CategoryGroupDto, CategoryGroupUpdateDto, CategoryUpdateDto

### Community 48 - "CategoryGroupEntity"
Cohesion: 0.29
Nodes (7): CategoryGroupEntity, BudgetMonth, Categories, Id, IsArchived, Name, SortOrder

### Community 50 - "LongTermPlanning"
Cohesion: 0.23
Nodes (14): actualLabel(), inputAmount(), LongTermPlanning(), beginEdit(), commitDraft(), groupValues(), metricValue(), openActivity() (+6 more)

### Community 51 - "List"
Cohesion: 0.19
Nodes (10): FlagDto, FlagPaletteDto, RenameFlagsRequest, SetFlagRequest, PayeeDto, PayeeRuleDto, PayeeRules, PayeesDto (+2 more)

### Community 52 - "MigrationBuilder"
Cohesion: 0.09
Nodes (7): Limitations and direction, Scheduling and flag storage, InitialCreate, ScheduledTransactions, TransactionFlags, ScheduledApproval, CategoryMoves

### Community 53 - "Guid"
Cohesion: 0.17
Nodes (9): AccountDto, AccountsDto, AuditDto, AuditPageDto, CreateAccountRequest, DeleteTransactionRequest, RegisterDto, ReviewTransactionRequest (+1 more)

### Community 54 - "vitest"
Cohesion: 0.23
Nodes (9): @testing-library/jest-dom, @testing-library/react, vitest, account, displayDate(), parseRegisterDate(), repeatOptions, Test() (+1 more)

### Community 56 - "BudgetPlanEntity"
Cohesion: 0.23
Nodes (8): BudgetPlanDto, BudgetPlanUpdateResult, BudgetPlanEntity, Currency, Id, Months, Name, Version

### Community 57 - "20261021190028_PayeeManagement.Designer.cs"
Cohesion: 0.17
Nodes (6): Backdated assignment corrections, Calculations and month lifecycle, Data model, Payee management, BudgetCarryForward, PayeeManagement

### Community 58 - "RegisterViewEntity"
Cohesion: 0.20
Nodes (10): RegisterViewEntity, DatePreset, FromMonth, Scope, ShowFlags, ShowImages, ShowReconciled, ShowRunningBalance (+2 more)

### Community 59 - "CategoryGroupEntity"
Cohesion: 0.20
Nodes (9): LongTermPlanningIncomeEntity, ExpectedIncome, Month, LongTermPlanningStateEntity, Id, Version, FlagDefinitionEntity, Color (+1 more)

### Community 60 - "Current PostgreSQL schema"
Cohesion: 0.17
Nodes (8): `BudgetPlans`, `Budgets`, `CategoryGroups` and `Categories`, Current PostgreSQL schema, Ledger tables, Long-Term Planning, Register view preferences, AccountRegisterPresentation

### Community 61 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

### Community 63 - "Milestone 3 — Goals, templates, and guided assignment"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 3 — Goals, templates, and guided assignment, Product references, User value, Vertical slice

### Community 64 - "Milestone 5 — Credit cards and debt payoff planning"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 5 — Credit cards and debt payoff planning, Product references, User value, Vertical slice

### Community 65 - "Milestone 6 — Focused plan views and reports"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 6 — Focused plan views and reports, Product references, User value, Vertical slice

### Community 66 - "Milestone 8 — Identity, multiple budgets, and household sharing"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 8 — Identity, multiple budgets, and household sharing, Product reference, User value, Vertical slice

### Community 67 - "Milestone 9 — Cross-device experience and offline resilience"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 9 — Cross-device experience and offline resilience, Product reference, User value, Vertical slice

### Community 68 - "Milestone 10 — Optional linked-account import"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies and release gate, Goal, Milestone 10 — Optional linked-account import, Product references, User value, Vertical slice

### Community 69 - "App.test.tsx"
Cohesion: 0.29
Nodes (3): Month, next(), saved()

### Community 70 - "Milestone: Annual Long-Term Budget Planning"
Cohesion: 0.29
Nodes (6): Acceptance Criteria, Budget propagation rule, Core behavior, Goal, Implementation notes, Milestone: Annual Long-Term Budget Planning

### Community 71 - "YNAB Web app: reverse-engineering notes"
Cohesion: 0.29
Nodes (6): Account-linking code observed in the bundle, Evidence and scope, Frontend structure, Lazy modules and browser storage, Limits and follow-up, YNAB Web app: reverse-engineering notes

### Community 72 - "Documentation"
Cohesion: 0.67
Nodes (3): Current release shape, Documentation, Documents

### Community 73 - "Milestone 0 — Local application foundation"
Cohesion: 0.33
Nodes (5): Acceptance criteria, Goal, Milestone 0 — Local application foundation, User and developer outcome, Vertical slice

### Community 74 - "planHelpers.ts"
Cohesion: 0.60
Nodes (3): openUpcoming(), categoryProgress(), categoryTransactionAmount()

### Community 77 - "PayeeEntity"
Cohesion: 0.25
Nodes (8): PayeeEntity, CategoryMode, DefaultCategoryId, Id, IsHidden, Name, NormalizedName, RulesJson

### Community 80 - "TestClock"
Cohesion: 0.20
Nodes (4): TestClock, LocalTimeZone, UtcNow, Zone

## Knowledge Gaps
- **181 isolated node(s):** `Clock`, `ConnectionString`, `Failure`, `StatusCode`, `LocalTimeZone` (+176 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 523 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **11 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Cash ledger slice` connect `AccountsPage.tsx` to `Architecture and current status`, `CategoryPicker`, `Guid`, `NewCategoryDialog.tsx`, `Payees.test.tsx`, `BudgetService`, `20261021190028_PayeeManagement.Designer.cs`?**
  _High betweenness centrality (0.347) - this node is a cross-community bridge._
- **What connects `Clock`, `ConnectionString`, `Failure` to the rest of the system?**
  _181 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `App` be split into smaller, more focused modules?**
  _Cohesion score 0.09788359788359788 - nodes in this community are weakly interconnected._
- **Why does `LedgerService` connect `Guid` to `.ScheduleActionAsync`, `TestClock`, `AccountsPage.tsx`, `ScheduleEntity`, `List`, `TransactionEntity`, `Guid`, `BudgetService`, `BudgetDbContext`, `LedgerBalanceRow`, `OpenBudget.Api`?**
  _High betweenness centrality (0.241) - this node is a cross-community bridge._
- **Should `CategoryEntity` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._
- **Why does `BudgetDbContext` connect `BudgetDbContext` to `LedgerCommandEntity`, `CategoryEntity`, `AccountEntity`, `PayeeEntity`, `Guid`, `LedgerRulesTests`, `TestClock`, `CategoryGroupEntity`, `ScheduleEntity`, `TransactionEntity`, `BudgetService`, `LongTermPlanningDto`, `BudgetPlanEntity`, `BudgetEntity`, `RegisterViewEntity`, `CategoryGroupEntity`, `ReconciliationEntity`, `OpenBudget.Api`?**
  _High betweenness centrality (0.097) - this node is a cross-community bridge._
- **Should `OpenBudget.Api.Migrations` be split into smaller, more focused modules?**
  _Cohesion score 0.12280701754385964 - nodes in this community are weakly interconnected._