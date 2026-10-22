# Graph Report - ynab-clone  (2026-10-22)

## Corpus Check
- 150 files · ~81,243 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 10 file(s) not represented in the graph (top: .ttf 4, .css 4, (none) 2)

## Summary
- 1327 nodes · 3827 edges · 77 communities (68 shown, 9 thin omitted)
- Extraction: 91% EXTRACTED · 9% INFERRED · 0% AMBIGUOUS · INFERRED: 341 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `711d37be`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- App
- BudgetEntity
- CategoryEntity
- microsoft_entityframeworkcore
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
- PostgresFixture.cs
- AccountsPage.tsx
- ScheduleEntity
- NewCategoryDialog.tsx
- TransactionEntity
- AccountRegister.tsx
- Payees.test.tsx
- LongTermPlanningDto
- API reference
- .ForYear
- BudgetDbContext
- LedgerBalanceRow
- PayeeEntity
- OpenBudget.Api
- TransactionSelectionBar.test.tsx
- LongTermPlanning.tsx
- Architecture and current status
- Testing and acceptance coverage
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
- RenameFlagsRequest
- OpenBudget product roadmap
- OpenBudget
- LongTermPlanning
- List
- MigrationBuilder
- DateTimeOffset
- vitest
- BudgetService
- BudgetPlanEntity
- PayeeManagement
- RegisterViewEntity
- CategoryGroupEntity
- Current PostgreSQL schema
- DateOnly
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
- RegisterViewDto
- Milestone 0 — Local application foundation
- planHelpers.ts

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
- ``GET /api/budget/{month}`` --references--> `month()`  [INFERRED]
  docs/api.md → src/OpenBudget.UI/src/ledger/ViewOptions.tsx
- `Cash ledger slice` --references--> `BudgetCarryForward`  [INFERRED]
  docs/architecture.md → src/OpenBudget.Api/BudgetCarryForward.cs
- `Backdated assignment corrections` --references--> `BudgetCarryForward`  [INFERRED]
  docs/data-model.md → src/OpenBudget.Api/BudgetCarryForward.cs
- `Cash ledger slice` --references--> `BudgetService`  [INFERRED]
  docs/architecture.md → src/OpenBudget.Api/BudgetService.cs

## Import Cycles
- None detected.

## Communities (77 total, 9 thin omitted)

### Community 0 - "App"
Cohesion: 0.10
Nodes (15): App(), addFunds(), closeCategoryDialog(), dropCategory(), dropGroup(), moveCategory(), moveGroup(), navigate() (+7 more)

### Community 1 - "BudgetEntity"
Cohesion: 0.19
Nodes (11): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+3 more)

### Community 2 - "CategoryEntity"
Cohesion: 0.13
Nodes (15): CategoryEntity, Activity, Assigned, Available, BudgetMonth, GroupId, Icon, Id (+7 more)

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

### Community 8 - "HttpClient"
Cohesion: 0.23
Nodes (8): BudgetDto, BudgetPlanUpdateDto, BudgetUpdateDto, CategoryDto, CategoryGroupDto, CategoryGroupUpdateDto, CategoryUpdateDto, BudgetCarryForwardApiTests

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.08
Nodes (23): Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Testcontainers.PostgreSql (4.16.0), Aspire.AppHost.Sdk/13.6.2 (+15 more)

### Community 12 - "Task"
Cohesion: 0.13
Nodes (21): Milestone 2 acceptance map, AuditPageDto, CreateAccountRequest, DeleteTransactionRequest, MutationDto, ReconcileRequest, ReviewTransactionRequest, StatusRequest (+13 more)

### Community 13 - "Guid"
Cohesion: 0.22
Nodes (6): LedgerService, LedgerException, StatusCode, Change, Entries, TransactionSelectionRequest

### Community 14 - "LedgerRulesTests"
Cohesion: 0.18
Nodes (3): LedgerRulesTests, PayeeRulesTests, RegisterViewTests

### Community 15 - "AccountsPage"
Cohesion: 0.12
Nodes (21): AccountForm(), AccountsPage(), applyView(), clearFilters(), mutate(), renameFlags(), saveInline(), scheduleAction() (+13 more)

### Community 17 - "AccountsPage.tsx"
Cohesion: 0.14
Nodes (26): Cash ledger slice, editor(), Dialog, Account, Accounts, Audit, CategoryOption, Entry (+18 more)

### Community 18 - "ScheduleEntity"
Cohesion: 0.08
Nodes (25): ScheduleEntity, AccountId, AnchorDate, Count, EndDate, FlagColor, Id, IsEnded (+17 more)

### Community 19 - "NewCategoryDialog.tsx"
Cohesion: 0.25
Nodes (5): NewCategoryDialog(), save(), close, created, show()

### Community 20 - "TransactionEntity"
Cohesion: 0.09
Nodes (19): TransactionEntity, Account, AccountId, Amount, CategoryId, Date, FlagColor, Id (+11 more)

### Community 21 - "AccountRegister.tsx"
Cohesion: 0.22
Nodes (13): @testing-library/user-event, AccountHeader(), balanceSummary(), ClearedIndicator(), reconciliationText(), RegisterIcon(), account, entry (+5 more)

### Community 22 - "Payees.test.tsx"
Cohesion: 0.11
Nodes (16): requestJson(), ManagePayees(), close(), done(), edit(), rule(), update(), PayeeList (+8 more)

### Community 23 - "LongTermPlanningDto"
Cohesion: 0.09
Nodes (18): Milestone 11 acceptance map, LongTermPlanningCategoryEntity, CategoryId, Month, PlannedBudgetOverride, PlannedExpenses, LongTermPlanningCategoryDto, LongTermPlanningCategoryMonthDto (+10 more)

### Community 24 - "API reference"
Cohesion: 0.09
Nodes (23): API reference, Approval and attention, Assignment rollover and earlier-month edits, Boundaries, Cash accounts and reconciliation, `GET /api/budget`, `GET /api/budget/{month}`, `GET /health` (+15 more)

### Community 25 - ".ForYear"
Cohesion: 0.17
Nodes (10): `CategoryGroups` and `Categories`, SplitDto, TransactionDto, LongTermPlanningActivityDto, ScheduledItemDto, SchedulesDto, ScheduleTemplateDto, ForecastOccurrence (+2 more)

### Community 26 - "BudgetDbContext"
Cohesion: 0.08
Nodes (18): BudgetDbContext, Accounts, BudgetPlans, Budgets, Categories, CategoryGroups, FlagDefinitions, LedgerCommands (+10 more)

### Community 27 - "LedgerBalanceRow"
Cohesion: 0.67
Nodes (3): LedgerBalanceRow, Balance, Id

### Community 28 - "PayeeEntity"
Cohesion: 0.08
Nodes (27): LedgerCommandEntity, AfterJson, BeforeJson, CreatedAt, Fingerprint, Id, Kind, ResourceId (+19 more)

### Community 29 - "OpenBudget.Api"
Cohesion: 0.10
Nodes (5): OpenBudget.Api, OpenBudget.UnitTests, CalculatedLongTermPlanningMonth, Program, SchedulePostingWorker

### Community 30 - "TransactionSelectionBar.test.tsx"
Cohesion: 0.15
Nodes (13): csvText(), selectedTransactionsCsv(), accounts, apply, clear, entry, show(), TransactionSelectionBar() (+5 more)

### Community 31 - "LongTermPlanning.tsx"
Cohesion: 0.11
Nodes (22): ActivityDialog, ActivityKind, ActivitySelection, actualMetrics, ConflictState, EditingValue, LongTermPlanningProps, Metric (+14 more)

### Community 32 - "Architecture and current status"
Cohesion: 0.33
Nodes (6): Architecture and current status, Current feature inventory, Design limits to address, Implemented user path, Runtime topology, Technologies

### Community 33 - "Testing and acceptance coverage"
Cohesion: 0.13
Nodes (15): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 verification (2026-10-22) (+7 more)

### Community 34 - "Agent Instructions"
Cohesion: 0.15
Nodes (12): Agent Instructions, Before making changes, Coding Guidelines, Development Principles, Documentation, Git, Project - OpenBudget, Roadmap (+4 more)

### Community 35 - "CategoryPicker"
Cohesion: 0.27
Nodes (8): categoryGroups(), CategoryPicker(), choose(), close(), option(), outside(), categories, Picker()

### Community 36 - "docs/README.md"
Cohesion: 0.22
Nodes (4): Coding conventions, Current release shape, Documentation, Documents

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
Cohesion: 0.17
Nodes (7): SplitEntity, Amount, CategoryId, Position, TransactionId, RecurrenceRules, RecurrenceTests

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.22
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

### Community 44 - "LedgerCommandEntity"
Cohesion: 0.22
Nodes (3): FailureInjector, FailAuditSave, FailBudgetAfterSave

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

### Community 50 - "LongTermPlanning"
Cohesion: 0.23
Nodes (14): actualLabel(), inputAmount(), LongTermPlanning(), beginEdit(), commitDraft(), groupValues(), metricValue(), openActivity() (+6 more)

### Community 51 - "List"
Cohesion: 0.28
Nodes (5): PayeeDto, PayeeRuleDto, PayeeRules, PayeesDto, PayeeUpdateDto

### Community 52 - "MigrationBuilder"
Cohesion: 0.07
Nodes (11): Limitations and direction, Scheduling and flag storage, InitialCreate, BudgetCore, NamedBudget, ScheduledTransactions, TransactionFlags, AccountNotes (+3 more)

### Community 53 - "DateTimeOffset"
Cohesion: 0.15
Nodes (7): AuditDto, CashLedger, BudgetDbContextModelSnapshot, TestClock, LocalTimeZone, UtcNow, Zone

### Community 54 - "vitest"
Cohesion: 0.23
Nodes (9): @testing-library/jest-dom, @testing-library/react, vitest, account, displayDate(), parseRegisterDate(), repeatOptions, Test() (+1 more)

### Community 56 - "BudgetPlanEntity"
Cohesion: 0.23
Nodes (8): BudgetPlanDto, BudgetPlanUpdateResult, BudgetPlanEntity, Currency, Id, Months, Name, Version

### Community 57 - "PayeeManagement"
Cohesion: 0.12
Nodes (9): API, OpenBudget API and local services, Backdated assignment corrections, Calculations and month lifecycle, Data model, Payee management, BudgetCarryForward, LedgerGate (+1 more)

### Community 58 - "RegisterViewEntity"
Cohesion: 0.20
Nodes (10): RegisterViewEntity, DatePreset, FromMonth, Scope, ShowFlags, ShowImages, ShowReconciled, ShowRunningBalance (+2 more)

### Community 59 - "CategoryGroupEntity"
Cohesion: 0.12
Nodes (16): CategoryGroupEntity, BudgetMonth, Categories, Id, IsArchived, Name, SortOrder, LongTermPlanningIncomeEntity (+8 more)

### Community 60 - "Current PostgreSQL schema"
Cohesion: 0.12
Nodes (8): `BudgetPlans`, `Budgets`, Current PostgreSQL schema, Ledger tables, Long-Term Planning, Register view preferences, AccountRegisterPresentation, AccountViewOptions

### Community 61 - "DateOnly"
Cohesion: 0.33
Nodes (3): AccountDto, AccountsDto, RegisterDto

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

### Community 73 - "Milestone 0 — Local application foundation"
Cohesion: 0.33
Nodes (5): Acceptance criteria, Goal, Milestone 0 — Local application foundation, User and developer outcome, Vertical slice

### Community 74 - "planHelpers.ts"
Cohesion: 0.60
Nodes (3): openUpcoming(), categoryProgress(), categoryTransactionAmount()

## Knowledge Gaps
- **181 isolated node(s):** `BudgetPlans`, `LongTermPlanningStates`, `LongTermPlanningIncomes`, `LongTermPlanningCategories`, `Budgets` (+176 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 522 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **9 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Cash ledger slice` connect `AccountsPage.tsx` to `Architecture and current status`, `CategoryPicker`, `Guid`, `NewCategoryDialog.tsx`, `Payees.test.tsx`, `BudgetService`, `PayeeManagement`?**
  _High betweenness centrality (0.370) - this node is a cross-community bridge._
- **What connects `BudgetPlans`, `LongTermPlanningStates`, `LongTermPlanningIncomes` to the rest of the system?**
  _181 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `App` be split into smaller, more focused modules?**
  _Cohesion score 0.09788359788359788 - nodes in this community are weakly interconnected._
- **Why does `LedgerService` connect `Guid` to `.ScheduleActionAsync`, `RenameFlagsRequest`, `AccountsPage.tsx`, `ScheduleEntity`, `DateOnly`, `TransactionEntity`, `List`, `BudgetService`, `PayeeManagement`, `BudgetDbContext`, `LedgerBalanceRow`, `OpenBudget.Api`, `.ForYear`?**
  _High betweenness centrality (0.256) - this node is a cross-community bridge._
- **Should `CategoryEntity` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._
- **Why does `BudgetDbContext` connect `BudgetDbContext` to `BudgetEntity`, `CategoryEntity`, `AccountEntity`, `RegisterViewDto`, `Guid`, `ScheduleEntity`, `TransactionEntity`, `BudgetService`, `LongTermPlanningDto`, `BudgetPlanEntity`, `PayeeManagement`, `RegisterViewEntity`, `CategoryGroupEntity`, `PayeeEntity`?**
  _High betweenness centrality (0.105) - this node is a cross-community bridge._
- **Should `vitest` be split into smaller, more focused modules?**
  _Cohesion score 0.0625 - nodes in this community are weakly interconnected._