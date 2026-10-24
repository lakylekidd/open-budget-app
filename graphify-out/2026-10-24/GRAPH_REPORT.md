# Graph Report - ynab-clone  (2026-10-24)

## Corpus Check
- 189 files · ~120,838 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 13 file(s) not represented in the graph (top: .ttf 4, .css 4, (none) 3)

## Summary
- 1709 nodes · 4868 edges · 97 communities (78 shown, 19 thin omitted)
- Extraction: 91% EXTRACTED · 9% INFERRED · 0% AMBIGUOUS · INFERRED: 459 edges (avg confidence: 0.86)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `77b7fce8`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- BudgetApplication
- App.tsx
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
- MembershipEntitlements.cs
- .SaveChangesAsync
- FailureInjector
- LedgerService
- Authentication.cs
- Guid
- TransactionEntity
- AccountsPage
- Payees.test.tsx
- Testing and acceptance coverage
- API reference
- .GetSchedulesAsync
- BudgetDbContext
- Milestone 2a — Multi-User Authentication & Administration
- .AddOpenBudgetIdentity
- InlineTransactionEditor.tsx
- AccountsPage.tsx
- AdminPanel.tsx
- Architecture and current status
- 20261021181641_TransactionFlags.Designer.cs
- Agent Instructions
- api.ts
- docs/README.md
- AccountsPage.tsx
- NewCategoryDialog.tsx
- Milestone 1 — Reliable budget core and month lifecycle
- Milestone 2 — Accounts and transaction register
- .OnModelCreating
- UserMembershipEntity
- Milestone 7 — File import, matching, and portable data
- DateTimeOffset
- Developer setup
- Milestone 4 — Scheduled transactions and cash-flow awareness
- RenameFlagsRequest
- LongTermPlanning
- longTermPlanningMath.ts
- DateOnly
- LedgerBalanceRow
- .MapAuthenticationEndpoints
- RegisterViewEntity
- BudgetPlanEntity
- Milestone 3 — Goals, templates, and guided assignment
- LongTermPlanning.tsx
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
- BudgetCarryForwardApiTests
- RegisterViewDto
- TransactionRequest
- BudgetService
- BudgetEntity
- OpenBudget.Api
- parseAssignment
- AdminAuditLogEntity
- PayeeEntity
- App.test.tsx
- RegisterViewEntity
- ModelBuilder
- LedgerCommandEntity
- LongTermPlanning.test.tsx
- SchedulePostingService
- .RenameFlagsAsync
- AccountRegisterPresentation
- LongTermPlanningStateEntity
- .UpdateMonthAsync
- AccountNotes
- OpenBudget product roadmap
- PayeeManagement
- LongTermPlanning
- AccountLifecycle
- BudgetScopedMonths
- BudgetFinancialIsolation
- RegisterColumnWidths

## God Nodes (most connected - your core abstractions)
1. `BudgetDbContext` - 73 edges
2. `MutationDto` - 71 edges
3. `LedgerApiTests` - 67 edges
4. `OpenBudget.Api` - 67 edges
5. `BudgetApplication()` - 63 edges
6. `LedgerService` - 62 edges
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

## Communities (97 total, 19 thin omitted)

### Community 0 - "BudgetApplication"
Cohesion: 0.07
Nodes (23): BudgetApplication(), addFunds(), cancelClosePlans(), closeCategoryDialog(), createBudget(), deleteUnsavedNameEditorTarget(), dropCategory(), dropGroup() (+15 more)

### Community 1 - "App.tsx"
Cohesion: 0.07
Nodes (27): react-dom, App(), BudgetPlan, BudgetState, Category, CategoryGroup, CategoryTransactionDialog, currency (+19 more)

### Community 2 - "CategoryEntity"
Cohesion: 0.12
Nodes (16): CategoryEntity, Activity, Assigned, Available, BudgetId, BudgetMonth, GroupId, Icon (+8 more)

### Community 3 - "vitest"
Cohesion: 0.12
Nodes (18): @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, vitest, AccountHeader(), balanceSummary(), ClearedIndicator(), reconciliationText() (+10 more)

### Community 4 - "package.json"
Cohesion: 0.06
Nodes (31): jsdom, @types/react, @types/react-dom, typescript, vite, @vitejs/plugin-react, dependencies, react (+23 more)

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
Nodes (38): Milestone 2 acceptance map, Milestone 2a final verification (2026-10-23), ChangePasswordRequest, LoginRequest, OpenBudgetUser, CreatedAt, IsBootstrapAdministrator, IsSuspended (+30 more)

### Community 13 - "MigrationBuilder"
Cohesion: 0.15
Nodes (5): InitialCreate, BudgetCore, NamedBudget, AccountViewOptions, CategoryMoves

### Community 14 - "MembershipEntitlements.cs"
Cohesion: 0.07
Nodes (17): `BudgetPlans`, MembershipPlanEntitlementEntity, EntitlementKey, IsEnabled, MembershipPlanId, UserEntitlementOverrideEntity, EntitlementKey, IsEnabled (+9 more)

### Community 15 - ".SaveChangesAsync"
Cohesion: 0.23
Nodes (5): BudgetLimitDto, BudgetOwnershipService, DeleteOwnedBudgetRequest, OwnedBudgetDto, RenameOwnedBudgetRequest

### Community 16 - "FailureInjector"
Cohesion: 0.22
Nodes (3): FailureInjector, FailAuditSave, FailBudgetAfterSave

### Community 17 - "LedgerService"
Cohesion: 0.18
Nodes (5): LedgerService, LedgerException, StatusCode, Entries, RecurrenceRules

### Community 18 - "Authentication.cs"
Cohesion: 0.16
Nodes (7): AdminInviteUserRequest, AdminMembershipRequest, ForgotPasswordRequest, RegisterRequest, RegistrationModeRequest, ResetPasswordRequest, TokenRequest

### Community 19 - "Guid"
Cohesion: 0.31
Nodes (4): SetFlagRequest, AuditDto, Change, TransactionSelectionRequest

### Community 20 - "TransactionEntity"
Cohesion: 0.08
Nodes (24): SplitEntity, Amount, CategoryId, Position, TransactionId, TransactionEntity, Account, AccountId (+16 more)

### Community 21 - "AccountsPage"
Cohesion: 0.19
Nodes (11): AccountsPage(), applyView(), beginColumnResize(), clearFilters(), finishColumnResize(), mutate(), persistColumnWidths(), renameFlags() (+3 more)

### Community 22 - "Payees.test.tsx"
Cohesion: 0.10
Nodes (19): requestJson(), ManagePayees(), close(), done(), edit(), rule(), update(), PayeeList (+11 more)

### Community 23 - "Testing and acceptance coverage"
Cohesion: 0.06
Nodes (35): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 acceptance map (+27 more)

### Community 24 - "API reference"
Cohesion: 0.08
Nodes (25): API reference, Approval and attention, Assignment rollover and earlier-month edits, Authentication, Boundaries, Cash accounts and reconciliation, `GET /api/budget`, `GET /api/budget/{month}` (+17 more)

### Community 25 - ".GetSchedulesAsync"
Cohesion: 0.29
Nodes (5): AccountDto, TransactionDto, LongTermPlanningActivityDto, ScheduledItemDto, SchedulesDto

### Community 26 - "BudgetDbContext"
Cohesion: 0.06
Nodes (29): BudgetDbContext, Accounts, ActiveBudgetId, AdminAuditLogs, ApplicationSettings, BudgetPlans, Budgets, Categories (+21 more)

### Community 27 - "Milestone 2a — Multi-User Authentication & Administration"
Cohesion: 0.10
Nodes (20): 10. Definition of Done, 1. Authentication & Login, 2. Permanent Administrator Accounts, 3. Registration & User Lifecycle, 4.1 Dashboard, 4.2 User Management, 4.3 Application Settings, 4. Separate Administration Panel (+12 more)

### Community 28 - ".AddOpenBudgetIdentity"
Cohesion: 0.16
Nodes (8): IAccountEmailSender, IsConfigured, SmtpAccountEmailSender, IsConfigured, TestAccountEmailSender, FailSending, IsConfigured, Messages

### Community 29 - "InlineTransactionEditor.tsx"
Cohesion: 0.24
Nodes (12): Cash ledger slice, editor(), loadCategories(), loadCategoryBudget(), InlineTransactionEditor(), save(), parseRegisterMoney(), displayDate() (+4 more)

### Community 30 - "AccountsPage.tsx"
Cohesion: 0.10
Nodes (30): AccountForm(), Dialog, ReconcileForm(), checking, savings, show(), TransferForm(), Accounts (+22 more)

### Community 31 - "AdminPanel.tsx"
Cohesion: 0.15
Nodes (7): AdminAudit, AdminPanel(), AdminSummary, AdminUser, MembershipDraft, MembershipPlan, modes

### Community 32 - "Architecture and current status"
Cohesion: 0.33
Nodes (6): Architecture and current status, Current feature inventory, Design limits to address, Implemented user path, Runtime topology, Technologies

### Community 33 - "20261021181641_TransactionFlags.Designer.cs"
Cohesion: 0.20
Nodes (4): Limitations and direction, Scheduling and flag storage, TransactionFlags, ScheduledApproval

### Community 34 - "Agent Instructions"
Cohesion: 0.15
Nodes (12): Agent Instructions, Before making changes, Coding Guidelines, Development Principles, Documentation, Git, Project - OpenBudget, Roadmap (+4 more)

### Community 35 - "api.ts"
Cohesion: 0.18
Nodes (15): react, CategoryOption, Entry, formatMoney(), Split, categoryGroups(), CategoryPicker(), choose() (+7 more)

### Community 36 - "docs/README.md"
Cohesion: 0.15
Nodes (10): Coding conventions, Calculations and month lifecycle, Data model, Current release shape, Documentation, Documents, Documentation, Highlights (+2 more)

### Community 37 - "AccountsPage.tsx"
Cohesion: 0.12
Nodes (20): defaultFlags, EditFlags(), Flag, FlagButton(), Test(), LedgerDialog(), csvText(), selectedTransactionsCsv() (+12 more)

### Community 38 - "NewCategoryDialog.tsx"
Cohesion: 0.18
Nodes (8): ApiError, Category, Month, NewCategoryDialog(), save(), close, created, show()

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.18
Nodes (11): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Plan interaction extension (2026-10-22) (+3 more)

### Community 41 - ".OnModelCreating"
Cohesion: 0.07
Nodes (31): ApplicationSettingEntity, Key, Value, MembershipPlanEntity, Id, MaxActiveBudgets, Name, AccountEntity (+23 more)

### Community 42 - "UserMembershipEntity"
Cohesion: 0.22
Nodes (9): UserMembershipEntity, BillingProvider, BudgetLimitOverride, ExpiresOn, ExternalSubscriptionId, MembershipPlanId, StartsOn, UserId (+1 more)

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.22
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

### Community 44 - "DateTimeOffset"
Cohesion: 0.13
Nodes (7): IdentityFoundation, AdminLifecycle, BudgetOwnershipAndMembership, TestClock, LocalTimeZone, UtcNow, Zone

### Community 45 - "Developer setup"
Cohesion: 0.22
Nodes (9): Build commands, Current development boundaries, Database lifecycle, Developer setup, First run, GitHub pull requests and releases, Prerequisites, Request flow (+1 more)

### Community 46 - "Milestone 4 — Scheduled transactions and cash-flow awareness"
Cohesion: 0.14
Nodes (13): `Budgets`, `CategoryGroups` and `Categories`, Current PostgreSQL schema, Identity and account lifecycle, Long-Term Planning, Register view preferences, Acceptance criteria, Dependencies (+5 more)

### Community 47 - "RenameFlagsRequest"
Cohesion: 0.25
Nodes (7): 1. Add the GitHub workflows, 2. Configure Coolify, 3. Create the Coolify webhook, 4. Add GitHub secrets, 5. Publish a release, GitHub Release to Coolify, Useful checks

### Community 48 - "LongTermPlanning"
Cohesion: 0.16
Nodes (20): openUpcoming(), actualLabel(), availableTone(), inputAmount(), LongTermPlanning(), beginEdit(), commitDraft(), groupValues() (+12 more)

### Community 49 - "longTermPlanningMath.ts"
Cohesion: 0.22
Nodes (10): amountFromCents(), cents(), LongTermPlanning, PlanningCategory, PlanningCategoryMonth, PlanningChange, PlanningGroup, PlanningMonth (+2 more)

### Community 50 - "DateOnly"
Cohesion: 0.18
Nodes (4): AdminUserDto, CashLedger, ScheduledTransactions, MembershipSubscriptionMetadata

### Community 51 - "LedgerBalanceRow"
Cohesion: 0.67
Nodes (3): LedgerBalanceRow, Balance, Id

### Community 52 - ".MapAuthenticationEndpoints"
Cohesion: 0.23
Nodes (5): AdminAuditDto, AdminDashboardDto, AdminMembershipCountDto, AdminRegistrationDto, AuthenticationSetup

### Community 53 - "RegisterViewEntity"
Cohesion: 0.15
Nodes (4): EntitlementKeyTests, LedgerRulesTests, PayeeRulesTests, RegisterViewTests

### Community 54 - "BudgetPlanEntity"
Cohesion: 0.22
Nodes (9): BudgetPlanEntity, CreatedAt, Currency, Id, IsArchived, Months, Name, OwnerUserId (+1 more)

### Community 55 - "Milestone 3 — Goals, templates, and guided assignment"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 3 — Goals, templates, and guided assignment, Product references, User value, Vertical slice

### Community 56 - "LongTermPlanning.tsx"
Cohesion: 0.18
Nodes (11): ActivityDialog, ActivityKind, ActivitySelection, actualMetrics, ConflictState, EditingValue, LongTermPlanningProps, Metric (+3 more)

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
Cohesion: 0.33
Nodes (5): PayeeDto, PayeeRuleDto, PayeeRules, PayeesDto, PayeeUpdateDto

### Community 61 - "Milestone 9 — Cross-device experience and offline resilience"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 9 — Cross-device experience and offline resilience, Product reference, User value, Vertical slice

### Community 62 - "Milestone 10 — Optional linked-account import"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies and release gate, Goal, Milestone 10 — Optional linked-account import, Product references, User value, Vertical slice

### Community 63 - "API"
Cohesion: 0.17
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
Cohesion: 0.23
Nodes (8): BudgetDto, BudgetPlanUpdateDto, BudgetUpdateDto, CategoryDto, CategoryGroupDto, CategoryGroupUpdateDto, CategoryUpdateDto, BudgetCarryForwardApiTests

### Community 73 - "BudgetService"
Cohesion: 0.36
Nodes (3): BudgetPlanDto, BudgetPlanUpdateResult, BudgetService

### Community 74 - "BudgetEntity"
Cohesion: 0.10
Nodes (18): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+10 more)

### Community 75 - "OpenBudget.Api"
Cohesion: 0.07
Nodes (5): OpenBudget.Api, OpenBudget.Api.Migrations, OpenBudget.UnitTests, OpenBudget.IntegrationTests, RecurrenceTests

### Community 76 - "parseAssignment"
Cohesion: 0.27
Nodes (8): applyAssignmentChanges(), applyAutoAssignOption(), applyMove(), saveEdit(), submitManualAssignment(), updateCategory(), money(), parseAssignment()

### Community 77 - "AdminAuditLogEntity"
Cohesion: 0.33
Nodes (6): AdminAuditLogEntity, Action, ActorUserId, CreatedAt, Id, TargetUserId

### Community 78 - "PayeeEntity"
Cohesion: 0.22
Nodes (9): PayeeEntity, BudgetId, CategoryMode, DefaultCategoryId, Id, IsHidden, Name, NormalizedName (+1 more)

### Community 79 - "App.test.tsx"
Cohesion: 0.25
Nodes (3): Month, next(), saved()

### Community 80 - "RegisterViewEntity"
Cohesion: 0.17
Nodes (12): RegisterViewEntity, BudgetId, ColumnWidthsJson, DatePreset, FromMonth, Scope, ShowFlags, ShowImages (+4 more)

### Community 82 - "LedgerCommandEntity"
Cohesion: 0.20
Nodes (10): LedgerCommandEntity, AfterJson, BeforeJson, BudgetId, CreatedAt, Fingerprint, Id, Kind (+2 more)

### Community 85 - ".RenameFlagsAsync"
Cohesion: 0.60
Nodes (3): FlagDto, FlagPaletteDto, RenameFlagsRequest

### Community 87 - "LongTermPlanningStateEntity"
Cohesion: 0.50
Nodes (4): LongTermPlanningStateEntity, BudgetId, Id, Version

### Community 90 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

## Knowledge Gaps
- **244 isolated node(s):** `Clock`, `ConnectionString`, `Email`, `Failure`, `EntitlementKey` (+239 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 669 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **19 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Cash ledger slice` connect `InlineTransactionEditor.tsx` to `Architecture and current status`, `api.ts`, `NewCategoryDialog.tsx`, `BudgetService`, `LedgerService`, `Payees.test.tsx`, `API`?**
  _High betweenness centrality (0.359) - this node is a cross-community bridge._
- **What connects `Clock`, `ConnectionString`, `Email` to the rest of the system?**
  _244 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `BudgetApplication` be split into smaller, more focused modules?**
  _Cohesion score 0.07152496626180836 - nodes in this community are weakly interconnected._
- **Why does `LedgerService` connect `LedgerService` to `TransactionRequest`, `BudgetService`, `BudgetEntity`, `OpenBudget.Api`, `List`, `Guid`, `TransactionEntity`, `.RenameFlagsAsync`, `LedgerBalanceRow`, `SchedulePostingService`, `.GetSchedulesAsync`, `BudgetDbContext`, `List`, `InlineTransactionEditor.tsx`, `API`?**
  _High betweenness centrality (0.230) - this node is a cross-community bridge._
- **Should `App.tsx` be split into smaller, more focused modules?**
  _Cohesion score 0.07051282051282051 - nodes in this community are weakly interconnected._
- **Why does `CategoryPicker()` connect `api.ts` to `BudgetApplication`, `App.tsx`, `Payees.test.tsx`, `InlineTransactionEditor.tsx`, `AccountsPage.tsx`?**
  _High betweenness centrality (0.170) - this node is a cross-community bridge._
- **Should `CategoryEntity` be split into smaller, more focused modules?**
  _Cohesion score 0.125 - nodes in this community are weakly interconnected._