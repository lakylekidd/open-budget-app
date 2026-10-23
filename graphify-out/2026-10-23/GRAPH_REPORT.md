# Graph Report - ynab-clone  (2026-10-23)

## Corpus Check
- 187 files · ~115,463 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 106 file(s) not represented in the graph (top: .dll 91, .ttf 4, .css 4)

## Summary
- 1648 nodes · 4754 edges · 80 communities (65 shown, 15 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 458 edges (avg confidence: 0.86)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `bc8f4c36`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- BudgetApplication
- .SaveChangesAsync
- BudgetEntity
- OpenBudget.Api
- vitest
- http
- compilerOptions
- http
- List
- OpenBudget.IntegrationTests.csproj
- Task
- DateTimeOffset
- MembershipEntitlements.cs
- .Calculate
- FailureInjector
- CancellationToken
- .ScheduleActionAsync
- DateOnly
- TransactionEntity
- AccountsPage.test.tsx
- Payees.test.tsx
- Testing and acceptance coverage
- API reference
- CategoryGroupEntity
- BudgetDbContext
- Milestone 2a — Multi-User Authentication & Administration
- package.json
- MigrationBuilder
- AccountsPage.tsx
- Authentication.cs
- Architecture and current status
- 20261021181641_TransactionFlags.Designer.cs
- Agent Instructions
- CategoryPicker
- docs/README.md
- AccountRegister.tsx
- BudgetCarryForwardApiTests
- Milestone 1 — Reliable budget core and month lifecycle
- Milestone 2 — Accounts and transaction register
- ReconciliationEntity
- UserMembershipEntity
- Milestone 7 — File import, matching, and portable data
- BudgetScopedMonths
- Developer setup
- Milestone 4 — Scheduled transactions and cash-flow awareness
- RenameFlagsRequest
- LongTermPlanning.tsx
- AdminPanel.tsx
- ModelBuilder
- Guid
- MembershipEntitlements
- InlineData
- BudgetPlanEntity
- Milestone 3 — Goals, templates, and guided assignment
- LedgerCommandEntity
- Milestone 5 — Credit cards and debt payoff planning
- Milestone 6 — Focused plan views and reports
- Milestone 8 — Identity, multiple budgets, and household sharing
- List
- Milestone 9 — Cross-device experience and offline resilience
- Milestone 10 — Optional linked-account import
- ModelBuilder
- Milestone: Annual Long-Term Budget Planning
- YNAB Web app: reverse-engineering notes
- SchedulePostingService
- Milestone 0 — Local application foundation
- .OnModelCreating
- AccountRegisterPresentation
- RegisterViewEntity
- AccountEntity
- LongTermPlanning
- PayeeEntity
- AccountLifecycle
- CategoryMoves
- OpenBudget product roadmap
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
- `API` --references--> `BudgetCarryForward`  [INFERRED]
  docs/backend.md → src/OpenBudget.Api/BudgetCarryForward.cs
- `Backdated assignment corrections` --references--> `BudgetCarryForward`  [INFERRED]
  docs/data-model.md → src/OpenBudget.Api/BudgetCarryForward.cs

## Import Cycles
- None detected.

## Communities (80 total, 15 thin omitted)

### Community 0 - "BudgetApplication"
Cohesion: 0.05
Nodes (45): react-dom, App(), BudgetApplication(), addFunds(), applyMove(), archiveBudget(), closeCategoryDialog(), createBudget() (+37 more)

### Community 1 - ".SaveChangesAsync"
Cohesion: 0.23
Nodes (3): API, LedgerGate, EntitlementService

### Community 2 - "BudgetEntity"
Cohesion: 0.05
Nodes (35): Backdated assignment corrections, Payee management, BudgetCarryForward, BudgetUpdateResult, CategoryDto, CategoryGroupDto, BudgetEntity, BudgetId (+27 more)

### Community 3 - "OpenBudget.Api"
Cohesion: 0.07
Nodes (5): OpenBudget.Api, OpenBudget.Api.Migrations, OpenBudget.UnitTests, OpenBudget.IntegrationTests, RecurrenceTests

### Community 4 - "vitest"
Cohesion: 0.08
Nodes (15): @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, vitest, Month, next(), saved(), Session (+7 more)

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
Cohesion: 0.06
Nodes (36): SplitDto, TransactionDto, LongTermPlanningActivityDto, ScheduledItemDto, SchedulesDto, ScheduleTemplateDto, ForecastOccurrence, ScheduledPlanningTotals (+28 more)

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.08
Nodes (24): Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Testcontainers.PostgreSql (4.16.0) (+16 more)

### Community 12 - "Task"
Cohesion: 0.09
Nodes (32): Milestone 2 acceptance map, ChangePasswordRequest, LoginRequest, UpdateProfileRequest, BudgetDto, CreateOwnedBudgetRequest, AccountsDto, AuditPageDto (+24 more)

### Community 13 - "DateTimeOffset"
Cohesion: 0.15
Nodes (7): AccountDto, IdentityFoundation, BudgetOwnershipAndMembership, TestClock, LocalTimeZone, UtcNow, Zone

### Community 14 - "MembershipEntitlements.cs"
Cohesion: 0.06
Nodes (21): `BudgetPlans`, `Budgets`, `CategoryGroups` and `Categories`, Current PostgreSQL schema, Identity and account lifecycle, Long-Term Planning, Register view preferences, BudgetLimitDto (+13 more)

### Community 15 - ".Calculate"
Cohesion: 0.18
Nodes (9): LongTermPlanningCategoryEntity, BudgetId, CategoryId, Month, PlannedBudgetOverride, PlannedExpenses, CalculatedLongTermPlanningMonth, LongTermPlanningCalculations (+1 more)

### Community 16 - "FailureInjector"
Cohesion: 0.22
Nodes (3): FailureInjector, FailAuditSave, FailBudgetAfterSave

### Community 17 - "CancellationToken"
Cohesion: 0.17
Nodes (5): LedgerService, LedgerException, StatusCode, Change, Entries

### Community 18 - ".ScheduleActionAsync"
Cohesion: 0.21
Nodes (6): SplitEntity, Amount, CategoryId, Position, TransactionId, RecurrenceRules

### Community 20 - "TransactionEntity"
Cohesion: 0.07
Nodes (20): TransactionEntity, Account, AccountId, Amount, BudgetId, CategoryId, Date, FlagColor (+12 more)

### Community 21 - "AccountsPage.test.tsx"
Cohesion: 0.09
Nodes (27): AccountsPage(), applyView(), clearFilters(), mutate(), renameFlags(), saveInline(), scheduleAction(), checking (+19 more)

### Community 22 - "Payees.test.tsx"
Cohesion: 0.11
Nodes (15): ManagePayees(), close(), done(), edit(), rule(), update(), PayeeList, PayeePicker() (+7 more)

### Community 23 - "Testing and acceptance coverage"
Cohesion: 0.07
Nodes (32): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 acceptance map (+24 more)

### Community 24 - "API reference"
Cohesion: 0.08
Nodes (25): API reference, Approval and attention, Assignment rollover and earlier-month edits, Authentication, Boundaries, Cash accounts and reconciliation, `GET /api/budget`, `GET /api/budget/{month}` (+17 more)

### Community 25 - "CategoryGroupEntity"
Cohesion: 0.25
Nodes (8): CategoryGroupEntity, BudgetId, BudgetMonth, Categories, Id, IsArchived, Name, SortOrder

### Community 26 - "BudgetDbContext"
Cohesion: 0.06
Nodes (25): BudgetDbContext, Accounts, ActiveBudgetId, AdminAuditLogs, ApplicationSettings, BudgetPlans, Budgets, Categories (+17 more)

### Community 27 - "Milestone 2a — Multi-User Authentication & Administration"
Cohesion: 0.10
Nodes (20): 10. Definition of Done, 1. Authentication & Login, 2. Permanent Administrator Accounts, 3. Registration & User Lifecycle, 4.1 Dashboard, 4.2 User Management, 4.3 Application Settings, 4. Separate Administration Panel (+12 more)

### Community 28 - "package.json"
Cohesion: 0.06
Nodes (30): jsdom, @types/react, @types/react-dom, typescript, vite, @vitejs/plugin-react, dependencies, react (+22 more)

### Community 29 - "MigrationBuilder"
Cohesion: 0.15
Nodes (5): InitialCreate, BudgetCore, NamedBudget, AccountNotes, BudgetFinancialIsolation

### Community 30 - "AccountsPage.tsx"
Cohesion: 0.10
Nodes (37): Cash ledger slice, react, AccountForm(), editor(), Dialog, ReconcileForm(), Account, Accounts (+29 more)

### Community 31 - "Authentication.cs"
Cohesion: 0.07
Nodes (28): AdminAuditDto, AdminDashboardDto, AdminInviteUserRequest, AdminMembershipCountDto, AdminMembershipRequest, AdminRegistrationDto, AdminUserDto, AuthenticationSetup (+20 more)

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
Cohesion: 0.27
Nodes (8): categoryGroups(), CategoryPicker(), choose(), close(), option(), outside(), categories, Picker()

### Community 36 - "docs/README.md"
Cohesion: 0.17
Nodes (8): OpenBudget API and local services, Coding conventions, Calculations and month lifecycle, Data model, Documentation, Highlights, Project status, Quick start

### Community 37 - "AccountRegister.tsx"
Cohesion: 0.11
Nodes (27): AccountHeader(), balanceSummary(), ClearedIndicator(), reconciliationText(), RegisterIcon(), account, entry, TransactionRow() (+19 more)

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.18
Nodes (11): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Plan interaction extension (2026-10-22) (+3 more)

### Community 41 - "ReconciliationEntity"
Cohesion: 0.29
Nodes (7): ReconciliationEntity, AccountId, BudgetId, CreatedAt, Id, IsInvalidated, StatementBalance

### Community 42 - "UserMembershipEntity"
Cohesion: 0.22
Nodes (9): UserMembershipEntity, BillingProvider, BudgetLimitOverride, ExpiresOn, ExternalSubscriptionId, MembershipPlanId, StartsOn, UserId (+1 more)

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.22
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

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
Cohesion: 0.07
Nodes (39): formatMoney(), ActivityDialog, ActivityKind, ActivitySelection, actualLabel(), actualMetrics, ConflictState, EditingValue (+31 more)

### Community 49 - "AdminPanel.tsx"
Cohesion: 0.15
Nodes (7): AdminAudit, AdminPanel(), AdminSummary, AdminUser, MembershipDraft, MembershipPlan, modes

### Community 51 - "Guid"
Cohesion: 0.18
Nodes (6): AuditDto, LedgerBalanceRow, Balance, Id, ScheduledTransactions, AdminLifecycle

### Community 53 - "InlineData"
Cohesion: 0.19
Nodes (5): RegisterViewDto, RegisterViewService, EntitlementKeyTests, PayeeRulesTests, RegisterViewTests

### Community 54 - "BudgetPlanEntity"
Cohesion: 0.22
Nodes (9): BudgetPlanEntity, CreatedAt, Currency, Id, IsArchived, Months, Name, OwnerUserId (+1 more)

### Community 55 - "Milestone 3 — Goals, templates, and guided assignment"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 3 — Goals, templates, and guided assignment, Product references, User value, Vertical slice

### Community 56 - "LedgerCommandEntity"
Cohesion: 0.20
Nodes (10): LedgerCommandEntity, AfterJson, BeforeJson, BudgetId, CreatedAt, Fingerprint, Id, Kind (+2 more)

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
Nodes (10): FlagDto, FlagPaletteDto, RenameFlagsRequest, SetFlagRequest, PayeeDto, PayeeRuleDto, PayeeRules, PayeesDto (+2 more)

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

### Community 75 - ".OnModelCreating"
Cohesion: 0.07
Nodes (29): AdminAuditLogEntity, Action, ActorUserId, CreatedAt, Id, TargetUserId, ApplicationSettingEntity, Key (+21 more)

### Community 79 - "RegisterViewEntity"
Cohesion: 0.18
Nodes (11): RegisterViewEntity, BudgetId, DatePreset, FromMonth, Scope, ShowFlags, ShowImages, ShowReconciled (+3 more)

### Community 80 - "AccountEntity"
Cohesion: 0.25
Nodes (8): AccountEntity, AccountType, BudgetId, Id, IsClosed, IsFavorite, Name, Notes

### Community 82 - "PayeeEntity"
Cohesion: 0.11
Nodes (18): FlagDefinitionEntity, BudgetId, Color, Name, LedgerModel, LedgerStateEntity, BudgetId, Id (+10 more)

### Community 90 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

### Community 94 - "Documentation"
Cohesion: 0.67
Nodes (3): Current release shape, Documentation, Documents

## Knowledge Gaps
- **243 isolated node(s):** `IsSuspended`, `IsBootstrapAdministrator`, `RequiresApproval`, `CreatedAt`, `LastLoginAt` (+238 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 643 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **15 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Cash ledger slice` connect `AccountsPage.tsx` to `Architecture and current status`, `.SaveChangesAsync`, `BudgetEntity`, `CategoryPicker`, `CancellationToken`, `Payees.test.tsx`?**
  _High betweenness centrality (0.299) - this node is a cross-community bridge._
- **What connects `IsSuspended`, `IsBootstrapAdministrator`, `RequiresApproval` to the rest of the system?**
  _243 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `BudgetApplication` be split into smaller, more focused modules?**
  _Cohesion score 0.04803312629399586 - nodes in this community are weakly interconnected._
- **Why does `LedgerService` connect `CancellationToken` to `.SaveChangesAsync`, `BudgetEntity`, `OpenBudget.Api`, `SchedulePostingService`, `List`, `.ScheduleActionAsync`, `Guid`, `TransactionEntity`, `BudgetDbContext`, `List`, `AccountsPage.tsx`?**
  _High betweenness centrality (0.182) - this node is a cross-community bridge._
- **Should `BudgetEntity` be split into smaller, more focused modules?**
  _Cohesion score 0.05499735589635114 - nodes in this community are weakly interconnected._
- **Why does `BudgetDbContext` connect `BudgetDbContext` to `.SaveChangesAsync`, `BudgetEntity`, `List`, `Task`, `MembershipEntitlements.cs`, `.Calculate`, `CancellationToken`, `TransactionEntity`, `Testing and acceptance coverage`, `CategoryGroupEntity`, `Authentication.cs`, `ReconciliationEntity`, `UserMembershipEntity`, `InlineData`, `BudgetPlanEntity`, `LedgerCommandEntity`, `SchedulePostingService`, `.OnModelCreating`, `RegisterViewEntity`, `AccountEntity`, `PayeeEntity`?**
  _High betweenness centrality (0.147) - this node is a cross-community bridge._
- **Should `OpenBudget.Api` be split into smaller, more focused modules?**
  _Cohesion score 0.06926406926406926 - nodes in this community are weakly interconnected._