# Graph Report - ynab-clone  (2026-10-24)

## Corpus Check
- 192 files · ~125,410 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 13 file(s) not represented in the graph (top: .ttf 4, .css 4, (none) 3)

## Summary
- 1768 nodes · 5014 edges · 84 communities (71 shown, 13 thin omitted)
- Extraction: 91% EXTRACTED · 9% INFERRED · 0% AMBIGUOUS · INFERRED: 471 edges (avg confidence: 0.86)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `eba68053`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- BudgetApplication
- MembershipEntitlements.cs
- CategoryEntity
- vitest
- package.json
- http
- compilerOptions
- http
- List
- OpenBudget.IntegrationTests.csproj
- Task
- MigrationBuilder
- EntitlementService
- .SaveChangesAsync
- FailureInjector
- CancellationToken
- Authentication.cs
- .ScheduleActionAsync
- TransactionEntity
- SmtpSettingsEntity
- Payees.test.tsx
- Testing and acceptance coverage
- API reference
- Current PostgreSQL schema
- BudgetDbContext
- Milestone 2a — Multi-User Authentication & Administration
- .RunAsync
- BudgetOwnership.cs
- AccountsPage
- AdminPanel.tsx
- Architecture and current status
- 20261021181641_TransactionFlags.Designer.cs
- Agent Instructions
- AccountsPage.tsx
- docs/README.md
- AccountsPage.tsx
- MembershipPlanEntity
- Milestone 1 — Reliable budget core and month lifecycle
- Milestone 2 — Accounts and transaction register
- .OnModelCreating
- UserMembershipEntity
- Milestone 7 — File import, matching, and portable data
- DateTimeOffset
- Developer setup
- Milestone 4 — Scheduled transactions and cash-flow awareness
- RenameFlagsRequest
- LongTermPlanning.tsx
- CategoryMoves
- DateOnly
- LedgerBalanceRow
- MembershipEntitlements
- RegisterViewEntity
- BudgetPlanEntity
- Milestone 3 — Goals, templates, and guided assignment
- Milestone 5 — Credit cards and debt payoff planning
- Milestone 6 — Focused plan views and reports
- Milestone 8 — Identity, multiple budgets, and household sharing
- List
- Milestone 9 — Cross-device experience and offline resilience
- Milestone 10 — Optional linked-account import
- API
- Milestone: Annual Long-Term Budget Planning
- YNAB Web app: reverse-engineering notes
- Milestone 0 — Local application foundation
- BudgetCarryForwardApiTests
- RegisterViewDto
- BudgetEntity
- OpenBudget.Api
- BudgetDbContext.cs
- PayeeEntity
- RegisterViewEntity
- ModelBuilder
- LedgerCommandEntity
- SchedulePostingService
- AccountRegisterPresentation
- LongTermPlanningStateEntity
- .UpdateMonthAsync
- OpenBudget product roadmap
- BudgetFinancialIsolation
- RegisterColumnWidths

## God Nodes (most connected - your core abstractions)
1. `BudgetDbContext` - 76 edges
2. `MutationDto` - 71 edges
3. `OpenBudget.Api` - 69 edges
4. `LedgerApiTests` - 67 edges
5. `BudgetApplication()` - 63 edges
6. `LedgerService` - 62 edges
7. `OpenBudget.Api.Migrations` - 47 edges
8. `TransactionEntity` - 41 edges
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

## Communities (84 total, 13 thin omitted)

### Community 0 - "BudgetApplication"
Cohesion: 0.04
Nodes (55): react-dom, App(), BudgetApplication(), addFunds(), applyAssignmentChanges(), applyAutoAssignOption(), applyMove(), cancelClosePlans() (+47 more)

### Community 1 - "MembershipEntitlements.cs"
Cohesion: 0.16
Nodes (4): MembershipEntitlementDto, SaveMembershipEntitlementsRequest, UserEntitlementOverrideRequest, Program

### Community 2 - "CategoryEntity"
Cohesion: 0.12
Nodes (16): CategoryEntity, Activity, Assigned, Available, BudgetId, BudgetMonth, GroupId, Icon (+8 more)

### Community 3 - "vitest"
Cohesion: 0.06
Nodes (19): @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, vitest, Month, next(), saved(), AuthBrand() (+11 more)

### Community 4 - "package.json"
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

### Community 8 - "List"
Cohesion: 0.07
Nodes (32): SplitDto, ScheduleTemplateDto, ForecastOccurrence, ScheduledPlanningTotals, ScheduleEntity, AccountId, AnchorDate, BudgetId (+24 more)

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.08
Nodes (24): Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Testcontainers.PostgreSql (4.16.0) (+16 more)

### Community 12 - "Task"
Cohesion: 0.07
Nodes (40): Milestone 2 acceptance map, Milestone 2a final verification (2026-10-23), ChangePasswordRequest, LoginRequest, OpenBudgetUser, CreatedAt, IsBootstrapAdministrator, IsSuspended (+32 more)

### Community 13 - "MigrationBuilder"
Cohesion: 0.09
Nodes (8): InitialCreate, BudgetCore, NamedBudget, AccountViewOptions, AccountNotes, LongTermPlanning, AccountLifecycle, BudgetScopedMonths

### Community 14 - "EntitlementService"
Cohesion: 0.17
Nodes (5): `BudgetPlans`, EntitlementAuthorizationHandler, EntitlementEndpointExtensions, EntitlementRequirement, EntitlementService

### Community 16 - "FailureInjector"
Cohesion: 0.20
Nodes (3): FailureInjector, FailAuditSave, FailBudgetAfterSave

### Community 17 - "CancellationToken"
Cohesion: 0.18
Nodes (5): LedgerService, LedgerException, StatusCode, Change, Entries

### Community 18 - "Authentication.cs"
Cohesion: 0.06
Nodes (25): AccountEmailContent, AccountEmailSettingsService, AccountEmailTemplates, IAccountEmailSender, SendTestEmailRequest, SmtpAccountEmailSender, SmtpConnectionSettings, SmtpSettingsDto (+17 more)

### Community 19 - ".ScheduleActionAsync"
Cohesion: 0.19
Nodes (7): SplitEntity, Amount, CategoryId, Position, TransactionId, RecurrenceRules, RecurrenceTests

### Community 20 - "TransactionEntity"
Cohesion: 0.07
Nodes (21): TransactionEntity, Account, AccountId, Amount, BudgetId, CategoryId, Date, FlagColor (+13 more)

### Community 21 - "SmtpSettingsEntity"
Cohesion: 0.22
Nodes (9): SmtpSettingsEntity, EnableSsl, EncryptedPassword, From, Host, Id, Port, UpdatedAt (+1 more)

### Community 22 - "Payees.test.tsx"
Cohesion: 0.07
Nodes (27): ApiError, requestJson(), Category, Month, NewCategoryDialog(), save(), close, created (+19 more)

### Community 23 - "Testing and acceptance coverage"
Cohesion: 0.06
Nodes (36): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 acceptance map (+28 more)

### Community 24 - "API reference"
Cohesion: 0.08
Nodes (24): API reference, Approval and attention, Assignment rollover and earlier-month edits, Authentication, Boundaries, Cash accounts and reconciliation, `GET /api/budget`, `GET /health` (+16 more)

### Community 25 - "Current PostgreSQL schema"
Cohesion: 0.33
Nodes (6): `Budgets`, `CategoryGroups` and `Categories`, Current PostgreSQL schema, Identity and account lifecycle, Long-Term Planning, Register view preferences

### Community 26 - "BudgetDbContext"
Cohesion: 0.06
Nodes (26): BudgetDbContext, Accounts, ActiveBudgetId, AdminAuditLogs, ApplicationSettings, BudgetPlans, Budgets, Categories (+18 more)

### Community 27 - "Milestone 2a — Multi-User Authentication & Administration"
Cohesion: 0.10
Nodes (20): 10. Definition of Done, 1. Authentication & Login, 2. Permanent Administrator Accounts, 3. Registration & User Lifecycle, 4.1 Dashboard, 4.2 User Management, 4.3 Application Settings, 4. Separate Administration Panel (+12 more)

### Community 28 - ".RunAsync"
Cohesion: 0.33
Nodes (4): FlagDefinitionEntity, BudgetId, Color, Name

### Community 29 - "BudgetOwnership.cs"
Cohesion: 0.33
Nodes (3): BudgetLimitDto, DeleteOwnedBudgetRequest, RenameOwnedBudgetRequest

### Community 30 - "AccountsPage"
Cohesion: 0.09
Nodes (26): `GET /api/budget/{month}`, AccountsPage(), applyView(), beginColumnResize(), clearFilters(), finishColumnResize(), mutate(), persistColumnWidths() (+18 more)

### Community 31 - "AdminPanel.tsx"
Cohesion: 0.12
Nodes (8): AdminAudit, AdminPanel(), AdminSummary, AdminUser, EmailSettings, MembershipDraft, MembershipPlan, modes

### Community 32 - "Architecture and current status"
Cohesion: 0.33
Nodes (6): Architecture and current status, Current feature inventory, Design limits to address, Implemented user path, Runtime topology, Technologies

### Community 33 - "20261021181641_TransactionFlags.Designer.cs"
Cohesion: 0.20
Nodes (4): Limitations and direction, Scheduling and flag storage, TransactionFlags, ScheduledApproval

### Community 34 - "Agent Instructions"
Cohesion: 0.15
Nodes (12): Agent Instructions, Before making changes, Coding Guidelines, Development Principles, Documentation, Git, Project - OpenBudget, Roadmap (+4 more)

### Community 35 - "AccountsPage.tsx"
Cohesion: 0.10
Nodes (36): Cash ledger slice, AccountForm(), editor(), Dialog, TransferForm(), Accounts, Audit, CategoryOption (+28 more)

### Community 36 - "docs/README.md"
Cohesion: 0.15
Nodes (10): Coding conventions, Calculations and month lifecycle, Data model, Current release shape, Documentation, Documents, Documentation, Highlights (+2 more)

### Community 37 - "AccountsPage.tsx"
Cohesion: 0.10
Nodes (30): react, AccountHeader(), balanceSummary(), ClearedIndicator(), reconciliationText(), RegisterIcon(), account, entry (+22 more)

### Community 38 - "MembershipPlanEntity"
Cohesion: 0.50
Nodes (4): MembershipPlanEntity, Id, MaxActiveBudgets, Name

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.18
Nodes (11): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Plan interaction extension (2026-10-22) (+3 more)

### Community 41 - ".OnModelCreating"
Cohesion: 0.10
Nodes (20): AccountEntity, AccountType, BudgetId, Id, IsClosed, IsFavorite, Name, Notes (+12 more)

### Community 42 - "UserMembershipEntity"
Cohesion: 0.22
Nodes (9): UserMembershipEntity, BillingProvider, BudgetLimitOverride, ExpiresOn, ExternalSubscriptionId, MembershipPlanId, StartsOn, UserId (+1 more)

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.22
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

### Community 44 - "DateTimeOffset"
Cohesion: 0.13
Nodes (7): IdentityFoundation, BudgetOwnershipAndMembership, AdminEmailSettings, TestClock, LocalTimeZone, UtcNow, Zone

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
Cohesion: 0.06
Nodes (47): openUpcoming(), ReconcileForm(), Entry, formatMoney(), ActivityDialog, ActivityKind, ActivitySelection, actualLabel() (+39 more)

### Community 50 - "DateOnly"
Cohesion: 0.13
Nodes (9): AdminUserDto, AccountDto, AuditDto, TransactionDto, CashLedger, ScheduledTransactions, AdminLifecycle, MembershipSubscriptionMetadata (+1 more)

### Community 51 - "LedgerBalanceRow"
Cohesion: 0.67
Nodes (3): LedgerBalanceRow, Balance, Id

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
Nodes (10): FlagDto, FlagPaletteDto, RenameFlagsRequest, SetFlagRequest, PayeeDto, PayeeRuleDto, PayeeRules, PayeesDto (+2 more)

### Community 61 - "Milestone 9 — Cross-device experience and offline resilience"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 9 — Cross-device experience and offline resilience, Product reference, User value, Vertical slice

### Community 62 - "Milestone 10 — Optional linked-account import"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies and release gate, Goal, Milestone 10 — Optional linked-account import, Product references, User value, Vertical slice

### Community 63 - "API"
Cohesion: 0.20
Nodes (8): API, OpenBudget API and local services, Backdated assignment corrections, Payee management, BudgetCarryForward, BudgetRequestContext, BudgetId, LedgerGate

### Community 64 - "Milestone: Annual Long-Term Budget Planning"
Cohesion: 0.29
Nodes (6): Acceptance Criteria, Budget propagation rule, Core behavior, Goal, Implementation notes, Milestone: Annual Long-Term Budget Planning

### Community 65 - "YNAB Web app: reverse-engineering notes"
Cohesion: 0.29
Nodes (6): Account-linking code observed in the bundle, Evidence and scope, Frontend structure, Lazy modules and browser storage, Limits and follow-up, YNAB Web app: reverse-engineering notes

### Community 67 - "Milestone 0 — Local application foundation"
Cohesion: 0.33
Nodes (5): Acceptance criteria, Goal, Milestone 0 — Local application foundation, User and developer outcome, Vertical slice

### Community 68 - "BudgetCarryForwardApiTests"
Cohesion: 0.24
Nodes (8): BudgetDto, BudgetPlanUpdateDto, BudgetUpdateDto, CategoryDto, CategoryGroupDto, CategoryGroupUpdateDto, CategoryUpdateDto, BudgetCarryForwardApiTests

### Community 71 - "RegisterViewDto"
Cohesion: 0.29
Nodes (3): RegisterViewDto, RegisterViewService, RegisterViewTests

### Community 74 - "BudgetEntity"
Cohesion: 0.17
Nodes (11): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+3 more)

### Community 75 - "OpenBudget.Api"
Cohesion: 0.07
Nodes (4): OpenBudget.Api, OpenBudget.Api.Migrations, OpenBudget.UnitTests, OpenBudget.IntegrationTests

### Community 77 - "BudgetDbContext.cs"
Cohesion: 0.09
Nodes (21): AdminAuditLogEntity, Action, ActorUserId, CreatedAt, Id, TargetUserId, ApplicationSettingEntity, Key (+13 more)

### Community 78 - "PayeeEntity"
Cohesion: 0.22
Nodes (9): PayeeEntity, BudgetId, CategoryMode, DefaultCategoryId, Id, IsHidden, Name, NormalizedName (+1 more)

### Community 80 - "RegisterViewEntity"
Cohesion: 0.17
Nodes (12): RegisterViewEntity, BudgetId, ColumnWidthsJson, DatePreset, FromMonth, Scope, ShowFlags, ShowImages (+4 more)

### Community 82 - "LedgerCommandEntity"
Cohesion: 0.20
Nodes (10): LedgerCommandEntity, AfterJson, BeforeJson, BudgetId, CreatedAt, Fingerprint, Id, Kind (+2 more)

### Community 87 - "LongTermPlanningStateEntity"
Cohesion: 0.50
Nodes (4): LongTermPlanningStateEntity, BudgetId, Id, Version

### Community 88 - ".UpdateMonthAsync"
Cohesion: 0.15
Nodes (10): BudgetUpdateResult, CategoryGroupEntity, BudgetId, BudgetMonth, Categories, Id, IsArchived, Name (+2 more)

### Community 90 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

## Knowledge Gaps
- **251 isolated node(s):** `IsSuspended`, `IsBootstrapAdministrator`, `RequiresApproval`, `CreatedAt`, `LastLoginAt` (+246 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 684 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **13 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Cash ledger slice` connect `AccountsPage.tsx` to `Architecture and current status`, `CancellationToken`, `Payees.test.tsx`, `.UpdateMonthAsync`, `API`?**
  _High betweenness centrality (0.303) - this node is a cross-community bridge._
- **What connects `IsSuspended`, `IsBootstrapAdministrator`, `RequiresApproval` to the rest of the system?**
  _251 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `BudgetApplication` be split into smaller, more focused modules?**
  _Cohesion score 0.040822784810126585 - nodes in this community are weakly interconnected._
- **Why does `LedgerService` connect `CancellationToken` to `AccountsPage.tsx`, `List`, `OpenBudget.Api`, `LedgerBalanceRow`, `TransactionEntity`, `.ScheduleActionAsync`, `SchedulePostingService`, `.UpdateMonthAsync`, `BudgetDbContext`, `List`, `API`?**
  _High betweenness centrality (0.197) - this node is a cross-community bridge._
- **Should `CategoryEntity` be split into smaller, more focused modules?**
  _Cohesion score 0.125 - nodes in this community are weakly interconnected._
- **Why does `CategoryPicker()` connect `AccountsPage.tsx` to `BudgetApplication`, `LongTermPlanning.tsx`, `Payees.test.tsx`?**
  _High betweenness centrality (0.138) - this node is a cross-community bridge._
- **Should `vitest` be split into smaller, more focused modules?**
  _Cohesion score 0.06376811594202898 - nodes in this community are weakly interconnected._