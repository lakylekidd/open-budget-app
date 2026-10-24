# Graph Report - ynab-clone  (2026-10-24)

## Corpus Check
- 185 files · ~117,990 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 13 file(s) not represented in the graph (top: .ttf 4, .css 4, (none) 3)

## Summary
- 1684 nodes · 4814 edges · 88 communities (76 shown, 12 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 459 edges (avg confidence: 0.86)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `28062562`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- BudgetApplication
- PostgresFixture.cs
- CategoryEntity
- OpenBudget.Api
- vitest
- http
- compilerOptions
- http
- List
- OpenBudget.IntegrationTests.csproj
- Task
- MigrationBuilder
- MembershipEntitlements.cs
- .SaveChangesAsync
- FailureInjector
- CancellationToken
- Authentication.cs
- OpenBudgetUser
- TransactionEntity
- AccountsPage.test.tsx
- Payees.test.tsx
- HttpClient
- API reference
- List
- BudgetDbContext
- Milestone 2a — Multi-User Authentication & Administration
- .AddOpenBudgetIdentity
- 20261021064416_InitialCreate.Designer.cs
- AccountsPage.tsx
- LongTermPlanningCategoryEntity
- Architecture and current status
- 20261021181641_TransactionFlags.Designer.cs
- Agent Instructions
- CategoryPicker
- docs/README.md
- AccountsPage.tsx
- Testing and acceptance coverage
- Milestone 1 — Reliable budget core and month lifecycle
- Milestone 2 — Accounts and transaction register
- PayeeEntity
- UserMembershipEntity
- Milestone 7 — File import, matching, and portable data
- TestClock
- Developer setup
- Milestone 4 — Scheduled transactions and cash-flow awareness
- RenameFlagsRequest
- LongTermPlanning.tsx
- MembershipEntitlements.cs
- Guid
- LedgerBalanceRow
- .MapAuthenticationEndpoints
- RegisterViewEntity
- BudgetPlanEntity
- Milestone 3 — Goals, templates, and guided assignment
- devDependencies
- Milestone 5 — Credit cards and debt payoff planning
- Milestone 6 — Focused plan views and reports
- Milestone 8 — Identity, multiple budgets, and household sharing
- List
- Milestone 9 — Cross-device experience and offline resilience
- Milestone 10 — Optional linked-account import
- API
- Milestone: Annual Long-Term Budget Planning
- YNAB Web app: reverse-engineering notes
- .Apply
- Milestone 0 — Local application foundation
- .UpdateMonthAsync
- AccountEntity
- ReconciliationEntity
- CategoryGroupEntity
- BudgetEntity
- microsoft_entityframeworkcore
- LedgerRulesTests
- AdminAuditLogEntity
- scripts
- MembershipPlanEntitlementEntity
- RegisterViewEntity
- BudgetDbContextModelSnapshot
- LedgerCommandEntity
- SchedulePostingService
- Current PostgreSQL schema
- .OnModelCreating
- OpenBudget product roadmap
- Documentation

## God Nodes (most connected - your core abstractions)
1. `BudgetDbContext` - 73 edges
2. `MutationDto` - 71 edges
3. `LedgerApiTests` - 67 edges
4. `OpenBudget.Api` - 67 edges
5. `LedgerService` - 62 edges
6. `BudgetApplication()` - 51 edges
7. `OpenBudget.Api.Migrations` - 45 edges
8. `TransactionEntity` - 44 edges
9. `LedgerException` - 37 edges
10. `AccountsPage()` - 37 edges

## Surprising Connections (you probably didn't know these)
- `Importing an accepted normalized row` --references--> `TransactionRequest`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/LedgerContracts.cs
- ``GET /api/budget/{month}`` --references--> `month()`  [INFERRED]
  docs/api.md → src/OpenBudget.UI/src/ledger/ViewOptions.tsx
- `Cash ledger slice` --references--> `BudgetCarryForward`  [INFERRED]
  docs/architecture.md → src/OpenBudget.Api/BudgetCarryForward.cs
- `Cash ledger slice` --references--> `BudgetService`  [INFERRED]
  docs/architecture.md → src/OpenBudget.Api/BudgetService.cs
- `Cash ledger slice` --references--> `LedgerService`  [INFERRED]
  docs/architecture.md → src/OpenBudget.Api/FlagService.cs

## Import Cycles
- None detected.

## Communities (88 total, 12 thin omitted)

### Community 0 - "BudgetApplication"
Cohesion: 0.05
Nodes (43): App(), BudgetApplication(), addFunds(), applyMove(), cancelClosePlans(), closeCategoryDialog(), createBudget(), dropCategory() (+35 more)

### Community 2 - "CategoryEntity"
Cohesion: 0.12
Nodes (16): CategoryEntity, Activity, Assigned, Available, BudgetId, BudgetMonth, GroupId, Icon (+8 more)

### Community 3 - "OpenBudget.Api"
Cohesion: 0.11
Nodes (3): OpenBudget.Api, OpenBudget.UnitTests, RecurrenceTests

### Community 4 - "vitest"
Cohesion: 0.04
Nodes (30): jsdom, @testing-library/jest-dom, @testing-library/react, @types/react, @types/react-dom, typescript, vite, @vitejs/plugin-react (+22 more)

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
Nodes (33): SplitDto, RecurrenceRules, ScheduleTemplateDto, ForecastOccurrence, ScheduledPlanningTotals, ScheduleEntity, AccountId, AnchorDate (+25 more)

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.08
Nodes (24): Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Testcontainers.PostgreSql (4.16.0) (+16 more)

### Community 12 - "Task"
Cohesion: 0.11
Nodes (19): Milestone 2 acceptance map, CreateOwnedBudgetRequest, CreateAccountRequest, MutationDto, ReconcileRequest, StatusRequest, TransferRequest, UpdateAccountRequest (+11 more)

### Community 13 - "MigrationBuilder"
Cohesion: 0.04
Nodes (19): Ledger tables, BudgetCore, NamedBudget, CashLedger, AccountRegisterPresentation, AccountViewOptions, AccountNotes, PayeeManagement (+11 more)

### Community 14 - "MembershipEntitlements.cs"
Cohesion: 0.13
Nodes (9): `BudgetPlans`, UserEntitlementOverrideEntity, EntitlementKey, IsEnabled, UserId, EntitlementAuthorizationHandler, EntitlementEndpointExtensions, EntitlementRequirement (+1 more)

### Community 15 - ".SaveChangesAsync"
Cohesion: 0.19
Nodes (5): BudgetLimitDto, BudgetOwnershipService, DeleteOwnedBudgetRequest, OwnedBudgetDto, RenameOwnedBudgetRequest

### Community 16 - "FailureInjector"
Cohesion: 0.22
Nodes (3): FailureInjector, FailAuditSave, FailBudgetAfterSave

### Community 17 - "CancellationToken"
Cohesion: 0.19
Nodes (5): LedgerService, LedgerException, StatusCode, Change, Entries

### Community 18 - "Authentication.cs"
Cohesion: 0.16
Nodes (9): Milestone 2a final verification (2026-10-23), AdminInviteUserRequest, AdminMembershipRequest, ForgotPasswordRequest, RegisterRequest, RegistrationModeRequest, ResetPasswordRequest, TokenRequest (+1 more)

### Community 19 - "OpenBudgetUser"
Cohesion: 0.19
Nodes (10): ChangePasswordRequest, LoginRequest, OpenBudgetUser, CreatedAt, IsBootstrapAdministrator, IsSuspended, LastLoginAt, RequiresApproval (+2 more)

### Community 20 - "TransactionEntity"
Cohesion: 0.08
Nodes (25): SplitEntity, Amount, CategoryId, Position, TransactionId, TransactionEntity, Account, AccountId (+17 more)

### Community 21 - "AccountsPage.test.tsx"
Cohesion: 0.09
Nodes (24): AccountsPage(), applyView(), beginColumnResize(), clearFilters(), finishColumnResize(), mutate(), persistColumnWidths(), renameFlags() (+16 more)

### Community 22 - "Payees.test.tsx"
Cohesion: 0.07
Nodes (23): Cash ledger slice, ApiError, requestJson(), NewCategoryDialog(), save(), close, created, show() (+15 more)

### Community 23 - "HttpClient"
Cohesion: 0.10
Nodes (18): Milestone 11 acceptance map, BudgetDto, BudgetUpdateDto, CategoryGroupUpdateDto, CategoryUpdateDto, LongTermPlanningCategoryDto, LongTermPlanningCategoryMonthDto, LongTermPlanningDto (+10 more)

### Community 24 - "API reference"
Cohesion: 0.08
Nodes (25): API reference, Approval and attention, Assignment rollover and earlier-month edits, Authentication, Boundaries, Cash accounts and reconciliation, `GET /api/budget`, `GET /api/budget/{month}` (+17 more)

### Community 25 - "List"
Cohesion: 0.15
Nodes (12): AccountDto, AccountsDto, AuditDto, AuditPageDto, DeleteTransactionRequest, RegisterDto, ReviewTransactionRequest, TransactionDto (+4 more)

### Community 26 - "BudgetDbContext"
Cohesion: 0.07
Nodes (25): BudgetDbContext, Accounts, ActiveBudgetId, AdminAuditLogs, ApplicationSettings, BudgetPlans, Budgets, Categories (+17 more)

### Community 27 - "Milestone 2a — Multi-User Authentication & Administration"
Cohesion: 0.10
Nodes (20): 10. Definition of Done, 1. Authentication & Login, 2. Permanent Administrator Accounts, 3. Registration & User Lifecycle, 4.1 Dashboard, 4.2 User Management, 4.3 Application Settings, 4. Separate Administration Panel (+12 more)

### Community 28 - ".AddOpenBudgetIdentity"
Cohesion: 0.14
Nodes (8): IAccountEmailSender, IsConfigured, SmtpAccountEmailSender, IsConfigured, TestAccountEmailSender, FailSending, IsConfigured, Messages

### Community 30 - "AccountsPage.tsx"
Cohesion: 0.10
Nodes (38): react-dom, AccountForm(), editor(), Dialog, ReconcileForm(), TransferForm(), Account, Accounts (+30 more)

### Community 31 - "LongTermPlanningCategoryEntity"
Cohesion: 0.16
Nodes (9): LongTermPlanningCategoryEntity, BudgetId, CategoryId, Month, PlannedBudgetOverride, PlannedExpenses, CalculatedLongTermPlanningMonth, LongTermPlanningCalculations (+1 more)

### Community 32 - "Architecture and current status"
Cohesion: 0.33
Nodes (6): Architecture and current status, Current feature inventory, Design limits to address, Implemented user path, Runtime topology, Technologies

### Community 33 - "20261021181641_TransactionFlags.Designer.cs"
Cohesion: 0.15
Nodes (5): Limitations and direction, Scheduling and flag storage, ScheduledTransactions, TransactionFlags, ScheduledApproval

### Community 34 - "Agent Instructions"
Cohesion: 0.15
Nodes (12): Agent Instructions, Before making changes, Coding Guidelines, Development Principles, Documentation, Git, Project - OpenBudget, Roadmap (+4 more)

### Community 35 - "CategoryPicker"
Cohesion: 0.27
Nodes (8): categoryGroups(), CategoryPicker(), choose(), close(), option(), outside(), categories, Picker()

### Community 36 - "docs/README.md"
Cohesion: 0.20
Nodes (6): OpenBudget API and local services, Coding conventions, Documentation, Highlights, Project status, Quick start

### Community 37 - "AccountsPage.tsx"
Cohesion: 0.08
Nodes (34): react, @testing-library/user-event, AccountHeader(), balanceSummary(), ClearedIndicator(), reconciliationText(), RegisterIcon(), account (+26 more)

### Community 38 - "Testing and acceptance coverage"
Cohesion: 0.13
Nodes (15): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 verification (2026-10-22) (+7 more)

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.18
Nodes (11): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Plan interaction extension (2026-10-22) (+3 more)

### Community 41 - "PayeeEntity"
Cohesion: 0.11
Nodes (18): FlagDefinitionEntity, BudgetId, Color, Name, LedgerModel, LedgerStateEntity, BudgetId, Id (+10 more)

### Community 42 - "UserMembershipEntity"
Cohesion: 0.22
Nodes (9): UserMembershipEntity, BillingProvider, BudgetLimitOverride, ExpiresOn, ExternalSubscriptionId, MembershipPlanId, StartsOn, UserId (+1 more)

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.22
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

### Community 44 - "TestClock"
Cohesion: 0.20
Nodes (4): TestClock, LocalTimeZone, UtcNow, Zone

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

### Community 49 - "MembershipEntitlements.cs"
Cohesion: 0.17
Nodes (4): MembershipEntitlementDto, SaveMembershipEntitlementsRequest, UserEntitlementOverrideRequest, Program

### Community 51 - "LedgerBalanceRow"
Cohesion: 0.67
Nodes (3): LedgerBalanceRow, Balance, Id

### Community 52 - ".MapAuthenticationEndpoints"
Cohesion: 0.25
Nodes (5): AdminAuditDto, AdminDashboardDto, AdminMembershipCountDto, AdminRegistrationDto, AuthenticationSetup

### Community 53 - "RegisterViewEntity"
Cohesion: 0.14
Nodes (5): RegisterViewDto, RegisterViewService, EntitlementKeyTests, PayeeRulesTests, RegisterViewTests

### Community 54 - "BudgetPlanEntity"
Cohesion: 0.18
Nodes (11): BudgetPlanDto, BudgetPlanUpdateResult, BudgetPlanEntity, CreatedAt, Currency, Id, IsArchived, Months (+3 more)

### Community 55 - "Milestone 3 — Goals, templates, and guided assignment"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 3 — Goals, templates, and guided assignment, Product references, User value, Vertical slice

### Community 56 - "devDependencies"
Cohesion: 0.18
Nodes (11): devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript (+3 more)

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
Cohesion: 0.20
Nodes (10): FlagDto, FlagPaletteDto, RenameFlagsRequest, SetFlagRequest, PayeeDto, PayeeRuleDto, PayeeRules, PayeesDto (+2 more)

### Community 61 - "Milestone 9 — Cross-device experience and offline resilience"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 9 — Cross-device experience and offline resilience, Product reference, User value, Vertical slice

### Community 62 - "Milestone 10 — Optional linked-account import"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies and release gate, Goal, Milestone 10 — Optional linked-account import, Product references, User value, Vertical slice

### Community 63 - "API"
Cohesion: 0.22
Nodes (7): API, Backdated assignment corrections, Payee management, BudgetCarryForward, BudgetRequestContext, BudgetId, LedgerGate

### Community 64 - "Milestone: Annual Long-Term Budget Planning"
Cohesion: 0.29
Nodes (6): Acceptance Criteria, Budget propagation rule, Core behavior, Goal, Implementation notes, Milestone: Annual Long-Term Budget Planning

### Community 65 - "YNAB Web app: reverse-engineering notes"
Cohesion: 0.29
Nodes (6): Account-linking code observed in the bundle, Evidence and scope, Frontend structure, Lazy modules and browser storage, Limits and follow-up, YNAB Web app: reverse-engineering notes

### Community 67 - "Milestone 0 — Local application foundation"
Cohesion: 0.33
Nodes (5): Acceptance criteria, Goal, Milestone 0 — Local application foundation, User and developer outcome, Vertical slice

### Community 68 - ".UpdateMonthAsync"
Cohesion: 0.33
Nodes (4): BudgetPlanUpdateDto, BudgetUpdateResult, CategoryDto, CategoryGroupDto

### Community 71 - "AccountEntity"
Cohesion: 0.25
Nodes (8): AccountEntity, AccountType, BudgetId, Id, IsClosed, IsFavorite, Name, Notes

### Community 72 - "ReconciliationEntity"
Cohesion: 0.29
Nodes (7): ReconciliationEntity, AccountId, BudgetId, CreatedAt, Id, IsInvalidated, StatementBalance

### Community 73 - "CategoryGroupEntity"
Cohesion: 0.25
Nodes (8): CategoryGroupEntity, BudgetId, BudgetMonth, Categories, Id, IsArchived, Name, SortOrder

### Community 74 - "BudgetEntity"
Cohesion: 0.16
Nodes (11): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+3 more)

### Community 77 - "AdminAuditLogEntity"
Cohesion: 0.33
Nodes (6): AdminAuditLogEntity, Action, ActorUserId, CreatedAt, Id, TargetUserId

### Community 78 - "scripts"
Cohesion: 0.33
Nodes (6): scripts, build, dev, preview, test, test:watch

### Community 79 - "MembershipPlanEntitlementEntity"
Cohesion: 0.50
Nodes (4): MembershipPlanEntitlementEntity, EntitlementKey, IsEnabled, MembershipPlanId

### Community 80 - "RegisterViewEntity"
Cohesion: 0.17
Nodes (12): RegisterViewEntity, BudgetId, ColumnWidthsJson, DatePreset, FromMonth, Scope, ShowFlags, ShowImages (+4 more)

### Community 82 - "LedgerCommandEntity"
Cohesion: 0.20
Nodes (10): LedgerCommandEntity, AfterJson, BeforeJson, BudgetId, CreatedAt, Fingerprint, Id, Kind (+2 more)

### Community 85 - "Current PostgreSQL schema"
Cohesion: 0.25
Nodes (8): `Budgets`, Calculations and month lifecycle, `CategoryGroups` and `Categories`, Current PostgreSQL schema, Data model, Identity and account lifecycle, Long-Term Planning, Register view preferences

### Community 87 - ".OnModelCreating"
Cohesion: 0.12
Nodes (15): ApplicationSettingEntity, Key, Value, LongTermPlanningIncomeEntity, BudgetId, ExpectedIncome, Month, LongTermPlanningStateEntity (+7 more)

### Community 90 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

### Community 94 - "Documentation"
Cohesion: 0.67
Nodes (3): Current release shape, Documentation, Documents

## Knowledge Gaps
- **244 isolated node(s):** `Clock`, `ConnectionString`, `Email`, `Failure`, `EntitlementKey` (+239 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 664 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **12 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Cash ledger slice` connect `Payees.test.tsx` to `Architecture and current status`, `CategoryPicker`, `BudgetEntity`, `CancellationToken`, `AccountsPage.tsx`, `API`?**
  _High betweenness centrality (0.319) - this node is a cross-community bridge._
- **What connects `Clock`, `ConnectionString`, `Email` to the rest of the system?**
  _244 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `BudgetApplication` be split into smaller, more focused modules?**
  _Cohesion score 0.0463768115942029 - nodes in this community are weakly interconnected._
- **Why does `LedgerService` connect `CancellationToken` to `OpenBudget.Api`, `List`, `BudgetEntity`, `TestClock`, `LedgerBalanceRow`, `TransactionEntity`, `Payees.test.tsx`, `List`, `BudgetDbContext`, `List`, `API`?**
  _High betweenness centrality (0.207) - this node is a cross-community bridge._
- **Should `CategoryEntity` be split into smaller, more focused modules?**
  _Cohesion score 0.125 - nodes in this community are weakly interconnected._
- **Why does `BudgetDbContext` connect `BudgetDbContext` to `CategoryEntity`, `OpenBudget.Api`, `List`, `Task`, `MembershipEntitlements.cs`, `.SaveChangesAsync`, `CancellationToken`, `OpenBudgetUser`, `TransactionEntity`, `HttpClient`, `.AddOpenBudgetIdentity`, `LongTermPlanningCategoryEntity`, `PayeeEntity`, `UserMembershipEntity`, `TestClock`, `.MapAuthenticationEndpoints`, `BudgetPlanEntity`, `API`, `AccountEntity`, `ReconciliationEntity`, `CategoryGroupEntity`, `BudgetEntity`, `AdminAuditLogEntity`, `MembershipPlanEntitlementEntity`, `RegisterViewEntity`, `LedgerCommandEntity`, `.OnModelCreating`?**
  _High betweenness centrality (0.117) - this node is a cross-community bridge._
- **Should `OpenBudget.Api` be split into smaller, more focused modules?**
  _Cohesion score 0.11375661375661375 - nodes in this community are weakly interconnected._