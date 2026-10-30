# Graph Report - ynab-clone  (2026-10-30)

## Corpus Check
- 230 files · ~165,327 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 13 file(s) not represented in the graph (top: .ttf 4, .css 4, (none) 3)

## Summary
- 2216 nodes · 6499 edges · 105 communities (88 shown, 17 thin omitted)
- Extraction: 91% EXTRACTED · 9% INFERRED · 0% AMBIGUOUS · INFERRED: 565 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `894ba619`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- BudgetApplication
- Guid
- CategoryEntity
- CategoryTarget.tsx
- vitest
- http
- compilerOptions
- http
- ScheduleEntity
- OpenBudget.IntegrationTests.csproj
- Task
- TransactionDatePicker.tsx
- OpenBudgetUser
- CancellationToken
- FailureInjector
- CancellationToken
- CategoryTargetApiTests
- RegisterViewEntity
- TransactionEntity
- Authentication.cs
- requestJson
- List
- API reference
- .Calculate
- BudgetDbContext
- Milestone 2a — Multi-User Authentication & Administration
- CreditPayoffTarget.tsx
- MoneyInput
- BudgetDbContext.cs
- vitest
- Architecture and current status
- CategoryTargetService
- Agent Instructions
- CategoryTargetRules
- docs/README.md
- CategoryTargetEntity
- CancellationToken
- Milestone 1 — Reliable budget core and month lifecycle
- Milestone 2 — Accounts and transaction register
- LedgerCommandEntity
- UserMembershipEntity
- Milestone 7 — File import, matching, and portable data
- MigrationBuilder
- Developer setup
- Milestone 4 — Scheduled transactions and cash-flow awareness
- RenameFlagsRequest
- AuthenticationApiTests
- RegisterViewDto
- PayeeEntity
- App.test.tsx
- Testing and acceptance coverage
- LongTermPlanningDto
- DateOnly
- Milestone 3 — Goals, templates, and guided assignment
- LongTermPlanning.tsx
- Milestone 5 — Credit cards and debt payoff planning
- Milestone 6 — Focused plan views and reports
- Milestone 8 — Identity, multiple budgets, and household sharing
- AccountEmailSettingsService
- Milestone 9 — Cross-device experience and offline resilience
- Milestone 10 — Optional linked-account import
- BudgetCarryForward
- Milestone: Annual Long-Term Budget Planning
- YNAB Web app: reverse-engineering notes
- Milestone 0 — Local application foundation
- TransactionSelectionBar.test.tsx
- LedgerService
- LongTermPlanningIncomeEntity
- .UpdateMonthAsync
- BudgetCarryForwardTests
- OpenBudget.Api
- PlanActionEntity
- AdminAuditLogEntity
- UserEntitlementOverrideEntity
- AccountEmail.cs
- InlineData
- .From
- AccountEntity
- BudgetTogether.tsx
- .Describe
- CreditCardRulesTests
- Milestone 12 — Icons and suggested icons
- BudgetInvitationEntity
- BudgetService
- OpenBudget product roadmap
- SmtpSettingsEntity
- BudgetPlanEntity
- API
- CreditPayoffTargetEntity
- .ScheduleActionAsync
- AccountsPage.tsx
- LongTermPlanningStateEntity
- MembershipPlanEntity
- TransactionFlags
- AccountRegisterPresentation
- PayeeManagement
- RegisterColumnWidths
- TransactionSplitDetails
- CreditAccounts

## God Nodes (most connected - your core abstractions)
1. `BudgetDbContext` - 90 edges
2. `OpenBudget.Api` - 88 edges
3. `BudgetApplication()` - 82 edges
4. `MutationDto` - 76 edges
5. `LedgerApiTests` - 69 edges
6. `OpenBudget.Api.Migrations` - 59 edges
7. `LedgerService` - 51 edges
8. `BudgetDto` - 44 edges
9. `TransactionEntity` - 44 edges
10. `LedgerException` - 42 edges

## Surprising Connections (you probably didn't know these)
- ``GET /api/budget/{month}`` --references--> `month()`  [INFERRED]
  docs/api.md → src/OpenBudget.UI/src/ledger/ViewOptions.tsx
- `Importing an accepted normalized row` --references--> `TransactionRequest`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/LedgerContracts.cs
- `Category targets` --references--> `month()`  [INFERRED]
  docs/api.md → src/OpenBudget.UI/src/ledger/ViewOptions.tsx
- `Cash ledger slice` --references--> `BudgetCarryForward`  [INFERRED]
  docs/architecture.md → src/OpenBudget.Api/BudgetCarryForward.cs
- `Cash ledger slice` --references--> `BudgetService`  [INFERRED]
  docs/architecture.md → src/OpenBudget.Api/BudgetService.cs

## Import Cycles
- None detected.

## Communities (105 total, 17 thin omitted)

### Community 0 - "BudgetApplication"
Cohesion: 0.04
Nodes (63): react-dom, accountSections, App(), BudgetApplication(), addFunds(), applyAssignmentChanges(), applyAutoAssignOption(), applyMove() (+55 more)

### Community 1 - "Guid"
Cohesion: 0.07
Nodes (17): PlanActionDto, AccountDto, AuditDto, LedgerBalanceRow, Balance, Id, IdentityFoundation, AdminLifecycle (+9 more)

### Community 2 - "CategoryEntity"
Cohesion: 0.08
Nodes (25): CategoryEntity, Activity, Assigned, Available, BudgetId, BudgetMonth, CreditOverspending, GroupId (+17 more)

### Community 3 - "CategoryTarget.tsx"
Cohesion: 0.09
Nodes (30): BehaviorPicker(), choose(), keys(), CategoryTargetCard(), CategoryTargetEditor(), submit(), describe(), Money (+22 more)

### Community 4 - "vitest"
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

### Community 8 - "ScheduleEntity"
Cohesion: 0.06
Nodes (34): SplitDto, ScheduleActionRequest, ScheduleRequest, ScheduleTemplateDto, ForecastOccurrence, ScheduledPlanningTotals, ScheduleEntity, AccountId (+26 more)

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.08
Nodes (24): Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Testcontainers.PostgreSql (4.16.0) (+16 more)

### Community 12 - "Task"
Cohesion: 0.08
Nodes (34): Milestone 2 acceptance map, BudgetDto, CreateOwnedBudgetRequest, AccountsDto, AuditPageDto, CreateAccountRequest, CreditPayoffDto, CreditPayoffProgressDto (+26 more)

### Community 13 - "TransactionDatePicker.tsx"
Cohesion: 0.27
Nodes (9): displayDate(), parseRegisterDate(), repeatOptions, Test(), TransactionDatePicker(), iso(), TargetDateField(), choose() (+1 more)

### Community 14 - "OpenBudgetUser"
Cohesion: 0.12
Nodes (11): SendTestEmailRequest, UpdateSmtpSettingsRequest, ChangePasswordRequest, LoginRequest, OpenBudgetUser, CreatedAt, IsBootstrapAdministrator, IsSuspended (+3 more)

### Community 15 - "CancellationToken"
Cohesion: 0.15
Nodes (6): BudgetLimitDto, BudgetOwnershipService, DeleteOwnedBudgetRequest, OwnedBudgetDto, RenameOwnedBudgetRequest, BudgetSharingService

### Community 16 - "FailureInjector"
Cohesion: 0.22
Nodes (3): FailureInjector, FailAuditSave, FailBudgetAfterSave

### Community 17 - "CancellationToken"
Cohesion: 0.20
Nodes (4): LedgerService, LedgerException, StatusCode, Change

### Community 19 - "RegisterViewEntity"
Cohesion: 0.15
Nodes (13): RegisterViewEntity, BudgetId, ColumnWidthsJson, DatePreset, FromMonth, Scope, ShowFlags, ShowImages (+5 more)

### Community 20 - "TransactionEntity"
Cohesion: 0.11
Nodes (19): TransactionEntity, Account, AccountId, Amount, BudgetId, CategoryId, Date, FlagColor (+11 more)

### Community 21 - "Authentication.cs"
Cohesion: 0.22
Nodes (8): AdminAuditDto, AdminDashboardDto, AdminMembershipCountDto, AdminMembershipRequest, AdminRegistrationDto, AdminUserDto, AuthenticationSetup, UserSessionDto

### Community 22 - "requestJson"
Cohesion: 0.07
Nodes (36): ApiError, CategoryOption, requestJson(), categoryGroups(), CategoryPicker(), choose(), close(), option() (+28 more)

### Community 23 - "List"
Cohesion: 0.19
Nodes (10): FlagDto, FlagPaletteDto, RenameFlagsRequest, SetFlagRequest, PayeeDto, PayeeRuleDto, PayeeRules, PayeesDto (+2 more)

### Community 24 - "API reference"
Cohesion: 0.07
Nodes (27): API reference, Approval and attention, Assignment rollover and earlier-month edits, Authentication, Boundaries, Budget collaboration and Plan history, Cash accounts and reconciliation, Credit accounts and card payments (+19 more)

### Community 26 - "BudgetDbContext"
Cohesion: 0.06
Nodes (31): BudgetDbContext, Accounts, ActiveBudgetId, AdminAuditLogs, ApplicationSettings, BudgetInvitations, BudgetMembers, BudgetPlans (+23 more)

### Community 27 - "Milestone 2a — Multi-User Authentication & Administration"
Cohesion: 0.10
Nodes (20): 10. Definition of Done, 1. Authentication & Login, 2. Permanent Administrator Accounts, 3. Registration & User Lifecycle, 4.1 Dashboard, 4.2 User Management, 4.3 Application Settings, 4. Separate Administration Panel (+12 more)

### Community 28 - "CreditPayoffTarget.tsx"
Cohesion: 0.11
Nodes (20): CreditPaymentDetails(), mutate(), card, progress, renderPanel(), CreditPayoff, currentMonth(), kindLabels (+12 more)

### Community 29 - "MoneyInput"
Cohesion: 0.14
Nodes (18): parseAssignment(), AccountSidebarRow(), ReconcileForm(), Account, formatMoney(), EditAccountForm(), save(), account (+10 more)

### Community 30 - "BudgetDbContext.cs"
Cohesion: 0.13
Nodes (13): ApplicationSettingEntity, Key, Value, BudgetMemberEntity, BudgetId, InvitedByUserId, JoinedAt, Role (+5 more)

### Community 31 - "vitest"
Cohesion: 0.05
Nodes (22): @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, vitest, AdminAudit, AdminPanel(), AdminSummary, AdminUser (+14 more)

### Community 32 - "Architecture and current status"
Cohesion: 0.29
Nodes (7): Architecture and current status, Category targets, Current feature inventory, Design limits to address, Implemented user path, Runtime topology, Technologies

### Community 33 - "CategoryTargetService"
Cohesion: 0.32
Nodes (3): CategoryTargetCommandResult, CategoryTargetResultDto, CategoryTargetService

### Community 34 - "Agent Instructions"
Cohesion: 0.15
Nodes (12): Agent Instructions, Before making changes, Coding Guidelines, Development Principles, Documentation, Git, Project - OpenBudget, Roadmap (+4 more)

### Community 36 - "docs/README.md"
Cohesion: 0.15
Nodes (9): OpenBudget API and local services, Coding conventions, Current release shape, Documentation, Documents, Documentation, Highlights, Project status (+1 more)

### Community 37 - "CategoryTargetEntity"
Cohesion: 0.07
Nodes (21): CategoryTargetEntity, Amount, Behavior, BudgetId, CategoryId, CreatedAt, DayOfMonth, DueDate (+13 more)

### Community 38 - "CancellationToken"
Cohesion: 0.17
Nodes (6): IAccountEmailSender, SmtpAccountEmailSender, TestAccountEmailSender, FailSending, IsConfigured, Messages

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.18
Nodes (11): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Plan interaction extension (2026-10-22) (+3 more)

### Community 41 - "LedgerCommandEntity"
Cohesion: 0.20
Nodes (10): LedgerCommandEntity, AfterJson, BeforeJson, BudgetId, CreatedAt, Fingerprint, Id, Kind (+2 more)

### Community 42 - "UserMembershipEntity"
Cohesion: 0.22
Nodes (9): UserMembershipEntity, BillingProvider, BudgetLimitOverride, ExpiresOn, ExternalSubscriptionId, MembershipPlanId, StartsOn, UserId (+1 more)

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.22
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

### Community 44 - "MigrationBuilder"
Cohesion: 0.07
Nodes (11): InitialCreate, BudgetCore, NamedBudget, AccountViewOptions, AccountNotes, CategoryMoves, LongTermPlanning, AccountLifecycle (+3 more)

### Community 45 - "Developer setup"
Cohesion: 0.22
Nodes (9): Build commands, Current development boundaries, Database lifecycle, Developer setup, First run, GitHub pull requests and releases, Prerequisites, Request flow (+1 more)

### Community 46 - "Milestone 4 — Scheduled transactions and cash-flow awareness"
Cohesion: 0.29
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 4 — Scheduled transactions and cash-flow awareness, Product references, User value, Vertical slice

### Community 47 - "RenameFlagsRequest"
Cohesion: 0.25
Nodes (7): 1. Add the GitHub workflows, 2. Configure Coolify, 3. Create the Coolify webhook, 4. Add GitHub secrets, 5. Publish a release, GitHub Release to Coolify, Useful checks

### Community 48 - "AuthenticationApiTests"
Cohesion: 0.20
Nodes (8): Milestone 2a final verification (2026-10-23), AdminInviteUserRequest, ForgotPasswordRequest, RegisterRequest, RegistrationModeRequest, ResetPasswordRequest, TokenRequest, AuthenticationApiTests

### Community 49 - "RegisterViewDto"
Cohesion: 0.27
Nodes (3): RegisterViewDto, RegisterViewService, RegisterViewTests

### Community 50 - "PayeeEntity"
Cohesion: 0.13
Nodes (9): PayeeEntity, BudgetId, CategoryMode, DefaultCategoryId, Id, IsHidden, Name, NormalizedName (+1 more)

### Community 51 - "App.test.tsx"
Cohesion: 0.17
Nodes (5): Month, next(), saved(), weeklyProgress, weeklyTarget

### Community 52 - "Testing and acceptance coverage"
Cohesion: 0.13
Nodes (15): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 verification (2026-10-22) (+7 more)

### Community 53 - "LongTermPlanningDto"
Cohesion: 0.08
Nodes (24): Milestone 11 acceptance map, BudgetUpdateDto, CategoryGroupUpdateDto, CategoryUpdateDto, LongTermPlanningCategoryEntity, BudgetId, CategoryId, Month (+16 more)

### Community 54 - "DateOnly"
Cohesion: 0.11
Nodes (7): TransactionDto, CashLedger, ScheduledTransactions, MembershipSubscriptionMetadata, BudgetFinancialIsolation, BudgetDbContextModelSnapshot, ScheduledItemDto

### Community 55 - "Milestone 3 — Goals, templates, and guided assignment"
Cohesion: 0.17
Nodes (12): Category targets, Target progress, Category targets, Acceptance criteria, Dependencies, Goal, Implemented: category targets (2026-10-30), Milestone 3 — Goals, templates, and guided assignment (+4 more)

### Community 56 - "LongTermPlanning.tsx"
Cohesion: 0.06
Nodes (43): openUpcoming(), ActivityDialog, ActivityKind, ActivitySelection, actualLabel(), actualMetrics, availableTone(), ConflictState (+35 more)

### Community 57 - "Milestone 5 — Credit cards and debt payoff planning"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 5 — Credit cards and debt payoff planning, Product references, User value, Vertical slice

### Community 58 - "Milestone 6 — Focused plan views and reports"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 6 — Focused plan views and reports, Product references, User value, Vertical slice

### Community 59 - "Milestone 8 — Identity, multiple budgets, and household sharing"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 8 — Identity, multiple budgets, and household sharing, Product reference, User value, Vertical slice

### Community 60 - "AccountEmailSettingsService"
Cohesion: 0.35
Nodes (3): AccountEmailSettingsService, SmtpConnectionSettings, SmtpSettingsDto

### Community 61 - "Milestone 9 — Cross-device experience and offline resilience"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 9 — Cross-device experience and offline resilience, Product reference, User value, Vertical slice

### Community 62 - "Milestone 10 — Optional linked-account import"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies and release gate, Goal, Milestone 10 — Optional linked-account import, Product references, User value, Vertical slice

### Community 63 - "BudgetCarryForward"
Cohesion: 0.10
Nodes (16): Backdated assignment corrections, `Budgets`, Calculations and month lifecycle, `CategoryGroups` and `Categories`, Credit accounts, Current PostgreSQL schema, Data model, Identity and account lifecycle (+8 more)

### Community 64 - "Milestone: Annual Long-Term Budget Planning"
Cohesion: 0.29
Nodes (6): Acceptance Criteria, Budget propagation rule, Core behavior, Goal, Implementation notes, Milestone: Annual Long-Term Budget Planning

### Community 65 - "YNAB Web app: reverse-engineering notes"
Cohesion: 0.29
Nodes (6): Account-linking code observed in the bundle, Evidence and scope, Frontend structure, Lazy modules and browser storage, Limits and follow-up, YNAB Web app: reverse-engineering notes

### Community 67 - "Milestone 0 — Local application foundation"
Cohesion: 0.33
Nodes (5): Acceptance criteria, Goal, Milestone 0 — Local application foundation, User and developer outcome, Vertical slice

### Community 68 - "TransactionSelectionBar.test.tsx"
Cohesion: 0.09
Nodes (32): react, AccountHeader(), balanceSummary(), ClearedIndicator(), reconciliationText(), RegisterIcon(), account, entry (+24 more)

### Community 71 - "LedgerService"
Cohesion: 0.12
Nodes (3): LedgerService, LedgerRules, Entries

### Community 72 - "LongTermPlanningIncomeEntity"
Cohesion: 0.50
Nodes (4): LongTermPlanningIncomeEntity, BudgetId, ExpectedIncome, Month

### Community 74 - "BudgetCarryForwardTests"
Cohesion: 0.18
Nodes (11): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+3 more)

### Community 75 - "OpenBudget.Api"
Cohesion: 0.06
Nodes (10): OpenBudget.Api, OpenBudget.Api.Migrations, OpenBudget.IntegrationTests, FlagDefinitionEntity, BudgetId, Color, Name, Program (+2 more)

### Community 76 - "PlanActionEntity"
Cohesion: 0.20
Nodes (10): PlanActionEntity, ActorUserId, Amount, BudgetId, CreatedAt, FromCategoryName, Id, Kind (+2 more)

### Community 77 - "AdminAuditLogEntity"
Cohesion: 0.33
Nodes (6): AdminAuditLogEntity, Action, ActorUserId, CreatedAt, Id, TargetUserId

### Community 78 - "UserEntitlementOverrideEntity"
Cohesion: 0.50
Nodes (4): UserEntitlementOverrideEntity, EntitlementKey, IsEnabled, UserId

### Community 80 - "InlineData"
Cohesion: 0.05
Nodes (15): OpenBudget.UnitTests, `BudgetPlans`, CreditPayoffRules, EntitlementAuthorizationHandler, EntitlementEndpointExtensions, EntitlementRequirement, EntitlementService, MembershipEntitlementDto (+7 more)

### Community 81 - ".From"
Cohesion: 0.38
Nodes (4): CategoryDto, CategoryGroupDto, CategoryTargetDto, DeleteCategoryTargetRequest

### Community 82 - "AccountEntity"
Cohesion: 0.13
Nodes (9): CreditCardRules, AccountEntity, AccountType, BudgetId, Id, IsClosed, IsFavorite, Name (+1 more)

### Community 83 - "BudgetTogether.tsx"
Cohesion: 0.29
Nodes (4): BudgetTogether(), Invitation, Person, Sharing

### Community 84 - ".Describe"
Cohesion: 0.23
Nodes (7): BudgetInvitationDto, BudgetPersonDto, BudgetSharingDto, InviteBudgetMemberRequest, PlanActionDraft, PlanActionRules, PlanActionRulesTests

### Community 86 - "Milestone 12 — Icons and suggested icons"
Cohesion: 0.22
Nodes (9): Acceptance criteria, Dependencies, Goal, Milestone 12 — Icons and suggested icons, Open questions, Product references, User value, Vertical slice (+1 more)

### Community 87 - "BudgetInvitationEntity"
Cohesion: 0.29
Nodes (7): BudgetInvitationEntity, BudgetId, CreatedAt, Email, Id, InvitedByUserId, NormalizedEmail

### Community 88 - "BudgetService"
Cohesion: 0.27
Nodes (4): BudgetPlanDto, BudgetPlanUpdateDto, BudgetPlanUpdateResult, BudgetService

### Community 90 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

### Community 91 - "SmtpSettingsEntity"
Cohesion: 0.22
Nodes (9): SmtpSettingsEntity, EnableSsl, EncryptedPassword, From, Host, Id, Port, UpdatedAt (+1 more)

### Community 92 - "BudgetPlanEntity"
Cohesion: 0.22
Nodes (9): BudgetPlanEntity, CreatedAt, Currency, Id, IsArchived, Months, Name, OwnerUserId (+1 more)

### Community 93 - "API"
Cohesion: 0.50
Nodes (3): API, BudgetRequestContext, BudgetId

### Community 94 - "CreditPayoffTargetEntity"
Cohesion: 0.09
Nodes (21): CreditPayoffTargetEntity, AccountId, BudgetId, CreatedAt, Kind, MonthlyAmount, PayoffAmount, TargetMonth (+13 more)

### Community 95 - ".ScheduleActionAsync"
Cohesion: 0.15
Nodes (8): SplitEntity, Amount, CategoryId, Memo, Payee, Position, TransactionId, RecurrenceRules

### Community 96 - "AccountsPage.tsx"
Cohesion: 0.06
Nodes (56): Cash ledger slice, AccountForm(), AccountsPage(), applyView(), beginColumnResize(), clearFilters(), editor(), finishColumnResize() (+48 more)

### Community 97 - "LongTermPlanningStateEntity"
Cohesion: 0.50
Nodes (4): LongTermPlanningStateEntity, BudgetId, Id, Version

### Community 98 - "MembershipPlanEntity"
Cohesion: 0.50
Nodes (4): MembershipPlanEntity, Id, MaxActiveBudgets, Name

### Community 99 - "TransactionFlags"
Cohesion: 0.20
Nodes (4): Limitations and direction, Scheduling and flag storage, TransactionFlags, ScheduledApproval

## Knowledge Gaps
- **308 isolated node(s):** `ActiveBudgetId`, `BudgetPlans`, `BudgetMembers`, `BudgetInvitations`, `PlanActions` (+303 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 793 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **17 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Cash ledger slice` connect `AccountsPage.tsx` to `Architecture and current status`, `TransactionSplitDetails`, `requestJson`, `Milestone 3 — Goals, templates, and guided assignment`, `BudgetService`, `vitest`, `BudgetCarryForward`?**
  _High betweenness centrality (0.290) - this node is a cross-community bridge._
- **What connects `ActiveBudgetId`, `BudgetPlans`, `BudgetMembers` to the rest of the system?**
  _308 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `BudgetApplication` be split into smaller, more focused modules?**
  _Cohesion score 0.03607262302914477 - nodes in this community are weakly interconnected._
- **Why does `BudgetService` connect `BudgetService` to `AccountsPage.tsx`, `CategoryTargetService`, `CategoryEntity`, `CategoryTargetEntity`, `LedgerService`, `.UpdateMonthAsync`, `BudgetCarryForwardTests`, `OpenBudget.Api`, `CancellationToken`, `.Describe`, `Milestone 3 — Goals, templates, and guided assignment`, `BudgetDbContext`, `API`, `BudgetCarryForward`?**
  _High betweenness centrality (0.183) - this node is a cross-community bridge._
- **Should `Guid` be split into smaller, more focused modules?**
  _Cohesion score 0.07200929152148665 - nodes in this community are weakly interconnected._
- **Why does `CategoryPicker()` connect `requestJson` to `AccountsPage.tsx`, `BudgetApplication`, `MoneyInput`?**
  _High betweenness centrality (0.147) - this node is a cross-community bridge._
- **Should `CategoryEntity` be split into smaller, more focused modules?**
  _Cohesion score 0.08 - nodes in this community are weakly interconnected._