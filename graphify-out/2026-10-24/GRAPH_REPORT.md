# Graph Report - ynab-clone  (2026-10-24)

## Corpus Check
- 183 files · ~114,626 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 13 file(s) not represented in the graph (top: .ttf 4, .css 4, (none) 3)

## Summary
- 1654 nodes · 4764 edges · 89 communities (75 shown, 14 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 460 edges (avg confidence: 0.86)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `fd7d97a7`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- BudgetApplication
- Testing and acceptance coverage
- CategoryEntity
- OpenBudget.Api
- vitest
- http
- compilerOptions
- http
- List
- OpenBudget.IntegrationTests.csproj
- Task
- DateTimeOffset
- .SaveChangesAsync
- AccountRegister.tsx
- LedgerCommandEntity
- CancellationToken
- .ForYear
- LongTermPlanning
- TransactionEntity
- AccountsPage.test.tsx
- Payees.test.tsx
- Testing and acceptance coverage
- API reference
- LedgerContracts.cs
- BudgetDbContext
- Milestone 2a — Multi-User Authentication & Administration
- devDependencies
- MigrationBuilder
- AccountsPage.tsx
- Authentication.cs
- Architecture and current status
- 20261021181641_TransactionFlags.Designer.cs
- Agent Instructions
- CategoryPicker
- docs/README.md
- AccountRegister.tsx
- SplitEntity
- Milestone 1 — Reliable budget core and month lifecycle
- Milestone 2 — Accounts and transaction register
- .OnModelCreating
- BudgetDbContext.cs
- Milestone 7 — File import, matching, and portable data
- AdminPanel.tsx
- Developer setup
- Milestone 4 — Scheduled transactions and cash-flow awareness
- RenameFlagsRequest
- LongTermPlanning.tsx
- App.tsx
- DateOnly
- LedgerBalanceRow
- planHelpers.ts
- InlineData
- BudgetPlanEntity
- Milestone 3 — Goals, templates, and guided assignment
- BudgetScopedMonths
- Milestone 5 — Credit cards and debt payoff planning
- Milestone 6 — Focused plan views and reports
- Milestone 8 — Identity, multiple budgets, and household sharing
- List
- Milestone 9 — Cross-device experience and offline resilience
- Milestone 10 — Optional linked-account import
- ModelBuilder
- Milestone: Annual Long-Term Budget Planning
- YNAB Web app: reverse-engineering notes
- MembershipEntitlements
- Milestone 0 — Local application foundation
- ModelBuilder
- .SaveAsync
- .RunAsync
- .UpdateMonthAsync
- .Apply
- App.test.tsx
- AdminAuditLogEntity
- LongTermPlanning.test.tsx
- RegisterViewEntity
- AccountEntity
- parseAssignment
- PayeeEntity
- AccountRegisterPresentation
- scripts
- AccountNotes
- OpenBudget product roadmap
- LongTermPlanning
- AccountLifecycle
- Documentation

## God Nodes (most connected - your core abstractions)
1. `BudgetDbContext` - 74 edges
2. `MutationDto` - 71 edges
3. `LedgerApiTests` - 67 edges
4. `OpenBudget.Api` - 66 edges
5. `LedgerService` - 62 edges
6. `BudgetApplication()` - 49 edges
7. `TransactionEntity` - 44 edges
8. `OpenBudget.Api.Migrations` - 43 edges
9. `LedgerException` - 37 edges
10. `BudgetDto` - 36 edges

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

## Communities (89 total, 14 thin omitted)

### Community 0 - "BudgetApplication"
Cohesion: 0.09
Nodes (19): BudgetApplication(), addFunds(), archiveBudget(), closeCategoryDialog(), createBudget(), deleteBudget(), dropCategory(), dropGroup() (+11 more)

### Community 1 - "Testing and acceptance coverage"
Cohesion: 0.13
Nodes (15): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 verification (2026-10-22) (+7 more)

### Community 2 - "CategoryEntity"
Cohesion: 0.12
Nodes (16): CategoryEntity, Activity, Assigned, Available, BudgetId, BudgetMonth, GroupId, Icon (+8 more)

### Community 3 - "OpenBudget.Api"
Cohesion: 0.06
Nodes (8): OpenBudget.Api, OpenBudget.Api.Migrations, OpenBudget.UnitTests, OpenBudget.IntegrationTests, SchedulePostingService, SchedulePostingWorker, RecurrenceTests, RegisterViewTests

### Community 4 - "vitest"
Cohesion: 0.07
Nodes (23): jsdom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript, vite (+15 more)

### Community 5 - "http"
Cohesion: 0.13
Nodes (18): ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL, ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL, ASPNETCORE_ENVIRONMENT, DOTNET_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables (+10 more)

### Community 6 - "compilerOptions"
Cohesion: 0.11
Nodes (17): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+9 more)

### Community 7 - "http"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 8 - "List"
Cohesion: 0.07
Nodes (27): ScheduleEntity, AccountId, AnchorDate, BudgetId, Count, EndDate, FlagColor, Id (+19 more)

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.08
Nodes (24): Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Testcontainers.PostgreSql (4.16.0) (+16 more)

### Community 12 - "Task"
Cohesion: 0.10
Nodes (23): Milestone 2 acceptance map, BudgetDto, CreateOwnedBudgetRequest, CreateAccountRequest, MutationDto, ReconcileRequest, StatusRequest, TransferRequest (+15 more)

### Community 13 - "DateTimeOffset"
Cohesion: 0.13
Nodes (7): IdentityFoundation, AdminLifecycle, BudgetOwnershipAndMembership, TestClock, LocalTimeZone, UtcNow, Zone

### Community 14 - ".SaveChangesAsync"
Cohesion: 0.06
Nodes (20): `BudgetPlans`, `Budgets`, `CategoryGroups` and `Categories`, Current PostgreSQL schema, Identity and account lifecycle, Long-Term Planning, Register view preferences, BudgetLimitDto (+12 more)

### Community 15 - "AccountRegister.tsx"
Cohesion: 0.21
Nodes (13): AccountHeader(), balanceSummary(), ClearedIndicator(), reconciliationText(), RegisterIcon(), account, entry, TransactionRow() (+5 more)

### Community 16 - "LedgerCommandEntity"
Cohesion: 0.11
Nodes (13): LedgerCommandEntity, AfterJson, BeforeJson, BudgetId, CreatedAt, Fingerprint, Id, Kind (+5 more)

### Community 17 - "CancellationToken"
Cohesion: 0.23
Nodes (6): LedgerService, LedgerException, StatusCode, Change, Entries, TransactionSelectionRequest

### Community 18 - ".ForYear"
Cohesion: 0.14
Nodes (10): SplitDto, TransactionDto, LongTermPlanningActivityDto, RecurrenceRules, ScheduledItemDto, SchedulesDto, ScheduleTemplateDto, ForecastOccurrence (+2 more)

### Community 19 - "LongTermPlanning"
Cohesion: 0.23
Nodes (14): actualLabel(), inputAmount(), LongTermPlanning(), beginEdit(), commitDraft(), groupValues(), metricValue(), openActivity() (+6 more)

### Community 20 - "TransactionEntity"
Cohesion: 0.10
Nodes (20): TransactionEntity, Account, AccountId, Amount, BudgetId, CategoryId, Date, FlagColor (+12 more)

### Community 21 - "AccountsPage.test.tsx"
Cohesion: 0.19
Nodes (12): checking, savings, show(), defaultView, month(), presetMonths(), presets, viewDates() (+4 more)

### Community 22 - "Payees.test.tsx"
Cohesion: 0.07
Nodes (28): Cash ledger slice, react-dom, ApiError, loadCategoryBudget(), requestJson(), InlineTransactionEditor(), Category, Month (+20 more)

### Community 23 - "Testing and acceptance coverage"
Cohesion: 0.08
Nodes (26): Milestone 11 acceptance map, BudgetPlanUpdateDto, BudgetUpdateDto, CategoryDto, CategoryGroupDto, CategoryGroupUpdateDto, CategoryUpdateDto, LongTermPlanningCategoryEntity (+18 more)

### Community 24 - "API reference"
Cohesion: 0.08
Nodes (25): API reference, Approval and attention, Assignment rollover and earlier-month edits, Authentication, Boundaries, Cash accounts and reconciliation, `GET /api/budget`, `GET /api/budget/{month}` (+17 more)

### Community 25 - "LedgerContracts.cs"
Cohesion: 0.24
Nodes (7): AccountDto, AccountsDto, AuditDto, AuditPageDto, DeleteTransactionRequest, RegisterDto, ReviewTransactionRequest

### Community 26 - "BudgetDbContext"
Cohesion: 0.06
Nodes (25): BudgetDbContext, Accounts, ActiveBudgetId, AdminAuditLogs, ApplicationSettings, BudgetPlans, Budgets, Categories (+17 more)

### Community 27 - "Milestone 2a — Multi-User Authentication & Administration"
Cohesion: 0.10
Nodes (20): 10. Definition of Done, 1. Authentication & Login, 2. Permanent Administrator Accounts, 3. Registration & User Lifecycle, 4.1 Dashboard, 4.2 User Management, 4.3 Application Settings, 4. Separate Administration Panel (+12 more)

### Community 28 - "devDependencies"
Cohesion: 0.18
Nodes (11): devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript (+3 more)

### Community 29 - "MigrationBuilder"
Cohesion: 0.15
Nodes (5): InitialCreate, BudgetCore, NamedBudget, AccountViewOptions, BudgetFinancialIsolation

### Community 30 - "AccountsPage.tsx"
Cohesion: 0.11
Nodes (29): AccountForm(), AccountsPage(), applyView(), clearFilters(), editor(), mutate(), renameFlags(), saveInline() (+21 more)

### Community 31 - "Authentication.cs"
Cohesion: 0.06
Nodes (32): Milestone 2a final verification (2026-10-23), AdminAuditDto, AdminDashboardDto, AdminInviteUserRequest, AdminMembershipCountDto, AdminMembershipRequest, AdminRegistrationDto, AuthenticationSetup (+24 more)

### Community 32 - "Architecture and current status"
Cohesion: 0.33
Nodes (6): Architecture and current status, Current feature inventory, Design limits to address, Implemented user path, Runtime topology, Technologies

### Community 33 - "20261021181641_TransactionFlags.Designer.cs"
Cohesion: 0.20
Nodes (4): Limitations and direction, Scheduling and flag storage, TransactionFlags, ScheduledApproval

### Community 34 - "Agent Instructions"
Cohesion: 0.15
Nodes (12): Agent Instructions, Before making changes, Coding Guidelines, Development Principles, Documentation, Git, Project - OpenBudget, Roadmap (+4 more)

### Community 35 - "CategoryPicker"
Cohesion: 0.16
Nodes (17): react, CategoryOption, Split, categoryGroups(), CategoryPicker(), choose(), close(), option() (+9 more)

### Community 36 - "docs/README.md"
Cohesion: 0.17
Nodes (8): OpenBudget API and local services, Coding conventions, Calculations and month lifecycle, Data model, Documentation, Highlights, Project status, Quick start

### Community 37 - "AccountRegister.tsx"
Cohesion: 0.12
Nodes (20): defaultFlags, EditFlags(), Flag, FlagButton(), Test(), LedgerDialog(), csvText(), selectedTransactionsCsv() (+12 more)

### Community 38 - "SplitEntity"
Cohesion: 0.40
Nodes (5): SplitEntity, Amount, CategoryId, Position, TransactionId

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.18
Nodes (11): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Plan interaction extension (2026-10-22) (+3 more)

### Community 41 - ".OnModelCreating"
Cohesion: 0.08
Nodes (24): MembershipPlanEntitlementEntity, EntitlementKey, IsEnabled, MembershipPlanId, MembershipPlanEntity, Id, MaxActiveBudgets, Name (+16 more)

### Community 42 - "BudgetDbContext.cs"
Cohesion: 0.08
Nodes (24): ApplicationSettingEntity, Key, Value, LongTermPlanningIncomeEntity, BudgetId, ExpectedIncome, Month, LongTermPlanningStateEntity (+16 more)

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.22
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

### Community 44 - "AdminPanel.tsx"
Cohesion: 0.15
Nodes (7): AdminAudit, AdminPanel(), AdminSummary, AdminUser, MembershipDraft, MembershipPlan, modes

### Community 45 - "Developer setup"
Cohesion: 0.22
Nodes (9): Build commands, Current development boundaries, Database lifecycle, Developer setup, First run, GitHub pull requests and releases, Prerequisites, Request flow (+1 more)

### Community 46 - "Milestone 4 — Scheduled transactions and cash-flow awareness"
Cohesion: 0.29
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 4 — Scheduled transactions and cash-flow awareness, Product references, User value, Vertical slice

### Community 47 - "RenameFlagsRequest"
Cohesion: 0.25
Nodes (7): 1. Add the GitHub workflows, 2. Configure Coolify, 3. Create the Coolify webhook, 4. Add GitHub secrets, 5. Publish a release, GitHub Release to Coolify, Useful checks

### Community 48 - "LongTermPlanning.tsx"
Cohesion: 0.11
Nodes (22): ActivityDialog, ActivityKind, ActivitySelection, actualMetrics, ConflictState, EditingValue, LongTermPlanningProps, Metric (+14 more)

### Community 49 - "App.tsx"
Cohesion: 0.12
Nodes (15): BudgetPlan, BudgetState, Category, CategoryGroup, CategoryTransactionDialog, currency, Filter, filters (+7 more)

### Community 50 - "DateOnly"
Cohesion: 0.18
Nodes (4): AdminUserDto, CashLedger, ScheduledTransactions, MembershipSubscriptionMetadata

### Community 51 - "LedgerBalanceRow"
Cohesion: 0.67
Nodes (3): LedgerBalanceRow, Balance, Id

### Community 52 - "planHelpers.ts"
Cohesion: 0.60
Nodes (3): openUpcoming(), categoryProgress(), categoryTransactionAmount()

### Community 53 - "InlineData"
Cohesion: 0.16
Nodes (4): TransactionRequest, EntitlementKeyTests, LedgerRulesTests, PayeeRulesTests

### Community 54 - "BudgetPlanEntity"
Cohesion: 0.18
Nodes (11): BudgetPlanDto, BudgetPlanUpdateResult, BudgetPlanEntity, CreatedAt, Currency, Id, IsArchived, Months (+3 more)

### Community 55 - "Milestone 3 — Goals, templates, and guided assignment"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 3 — Goals, templates, and guided assignment, Product references, User value, Vertical slice

### Community 57 - "Milestone 5 — Credit cards and debt payoff planning"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 5 — Credit cards and debt payoff planning, Product references, User value, Vertical slice

### Community 58 - "Milestone 6 — Focused plan views and reports"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 6 — Focused plan views and reports, Product references, User value, Vertical slice

### Community 59 - "Milestone 8 — Identity, multiple budgets, and household sharing"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 8 — Identity, multiple budgets, and household sharing, Product reference, User value, Vertical slice

### Community 60 - "List"
Cohesion: 0.19
Nodes (9): FlagDto, FlagPaletteDto, RenameFlagsRequest, SetFlagRequest, PayeeDto, PayeeRuleDto, PayeeRules, PayeesDto (+1 more)

### Community 61 - "Milestone 9 — Cross-device experience and offline resilience"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 9 — Cross-device experience and offline resilience, Product reference, User value, Vertical slice

### Community 62 - "Milestone 10 — Optional linked-account import"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies and release gate, Goal, Milestone 10 — Optional linked-account import, Product references, User value, Vertical slice

### Community 64 - "Milestone: Annual Long-Term Budget Planning"
Cohesion: 0.29
Nodes (6): Acceptance Criteria, Budget propagation rule, Core behavior, Goal, Implementation notes, Milestone: Annual Long-Term Budget Planning

### Community 65 - "YNAB Web app: reverse-engineering notes"
Cohesion: 0.29
Nodes (6): Account-linking code observed in the bundle, Evidence and scope, Frontend structure, Lazy modules and browser storage, Limits and follow-up, YNAB Web app: reverse-engineering notes

### Community 67 - "Milestone 0 — Local application foundation"
Cohesion: 0.33
Nodes (5): Acceptance criteria, Goal, Milestone 0 — Local application foundation, User and developer outcome, Vertical slice

### Community 72 - ".RunAsync"
Cohesion: 0.22
Nodes (7): API, Backdated assignment corrections, Payee management, BudgetCarryForward, BudgetRequestContext, BudgetId, LedgerGate

### Community 73 - ".UpdateMonthAsync"
Cohesion: 0.15
Nodes (10): BudgetUpdateResult, CategoryGroupEntity, BudgetId, BudgetMonth, Categories, Id, IsArchived, Name (+2 more)

### Community 74 - ".Apply"
Cohesion: 0.17
Nodes (11): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+3 more)

### Community 76 - "App.test.tsx"
Cohesion: 0.13
Nodes (5): App(), Month, next(), saved(), AuthGate()

### Community 77 - "AdminAuditLogEntity"
Cohesion: 0.33
Nodes (6): AdminAuditLogEntity, Action, ActorUserId, CreatedAt, Id, TargetUserId

### Community 79 - "RegisterViewEntity"
Cohesion: 0.18
Nodes (11): RegisterViewEntity, BudgetId, DatePreset, FromMonth, Scope, ShowFlags, ShowImages, ShowReconciled (+3 more)

### Community 80 - "AccountEntity"
Cohesion: 0.25
Nodes (8): AccountEntity, AccountType, BudgetId, Id, IsClosed, IsFavorite, Name, Notes

### Community 81 - "parseAssignment"
Cohesion: 0.33
Nodes (5): applyMove(), saveEdit(), updateCategory(), money(), parseAssignment()

### Community 82 - "PayeeEntity"
Cohesion: 0.22
Nodes (9): PayeeEntity, BudgetId, CategoryMode, DefaultCategoryId, Id, IsHidden, Name, NormalizedName (+1 more)

### Community 87 - "scripts"
Cohesion: 0.33
Nodes (6): scripts, build, dev, preview, test, test:watch

### Community 90 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

### Community 94 - "Documentation"
Cohesion: 0.67
Nodes (3): Current release shape, Documentation, Documents

## Knowledge Gaps
- **243 isolated node(s):** `IsSuspended`, `IsBootstrapAdministrator`, `RequiresApproval`, `CreatedAt`, `LastLoginAt` (+238 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 647 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **14 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Cash ledger slice` connect `Payees.test.tsx` to `Architecture and current status`, `CategoryPicker`, `.RunAsync`, `.UpdateMonthAsync`, `CancellationToken`, `AccountsPage.tsx`?**
  _High betweenness centrality (0.322) - this node is a cross-community bridge._
- **What connects `IsSuspended`, `IsBootstrapAdministrator`, `RequiresApproval` to the rest of the system?**
  _243 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `BudgetApplication` be split into smaller, more focused modules?**
  _Cohesion score 0.09475806451612903 - nodes in this community are weakly interconnected._
- **Why does `LedgerService` connect `CancellationToken` to `OpenBudget.Api`, `.RunAsync`, `.UpdateMonthAsync`, `List`, `.ForYear`, `LedgerBalanceRow`, `TransactionEntity`, `Payees.test.tsx`, `LedgerContracts.cs`, `BudgetDbContext`, `List`?**
  _High betweenness centrality (0.194) - this node is a cross-community bridge._
- **Should `Testing and acceptance coverage` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._
- **Why does `BudgetDbContext` connect `BudgetDbContext` to `CategoryEntity`, `List`, `Task`, `.SaveChangesAsync`, `LedgerCommandEntity`, `CancellationToken`, `TransactionEntity`, `Testing and acceptance coverage`, `Authentication.cs`, `.OnModelCreating`, `BudgetDbContext.cs`, `BudgetPlanEntity`, `.SaveAsync`, `.RunAsync`, `.UpdateMonthAsync`, `.Apply`, `AdminAuditLogEntity`, `RegisterViewEntity`, `AccountEntity`, `PayeeEntity`?**
  _High betweenness centrality (0.147) - this node is a cross-community bridge._
- **Should `CategoryEntity` be split into smaller, more focused modules?**
  _Cohesion score 0.125 - nodes in this community are weakly interconnected._