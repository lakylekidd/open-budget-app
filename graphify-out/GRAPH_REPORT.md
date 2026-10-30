# Graph Report - ynab-clone  (2026-10-30)

## Corpus Check
- 230 files · ~165,327 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 13 file(s) not represented in the graph (top: .ttf 4, .css 4, (none) 3)

## Summary
- 2185 nodes · 6523 edges · 107 communities (85 shown, 22 thin omitted)
- Extraction: 91% EXTRACTED · 9% INFERRED · 0% AMBIGUOUS · INFERRED: 588 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `e014ca7b`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- BudgetApplication
- DateTimeOffset
- CategoryEntity
- CategoryTarget.tsx
- vitest
- http
- compilerOptions
- http
- ScheduleEntity
- OpenBudget.IntegrationTests.csproj
- Task
- MembershipEntitlements.cs
- PostgresFixture.cs
- CancellationToken
- FailureInjector
- CancellationToken
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
- .OnModelCreating
- AdminPanel.tsx
- Architecture and current status
- CategoryTargetService
- Agent Instructions
- CategoryTargetRules
- docs/README.md
- CategoryTargetEntity
- SchedulePosting.cs
- Milestone 1 — Reliable budget core and month lifecycle
- Milestone 2 — Accounts and transaction register
- LedgerCommandEntity
- UserMembershipEntity
- Milestone 7 — File import, matching, and portable data
- MigrationBuilder
- Developer setup
- Milestone 4 — Scheduled transactions and cash-flow awareness
- RenameFlagsRequest
- devDependencies
- ReconciliationEntity
- PayeeEntity
- BudgetMemberEntity
- scripts
- LongTermPlanningDto
- DateOnly
- Milestone 3 — Goals, templates, and guided assignment
- LongTermPlanning.tsx
- Milestone 5 — Credit cards and debt payoff planning
- Milestone 6 — Focused plan views and reports
- Milestone 8 — Identity, multiple budgets, and household sharing
- Category targets
- Milestone 9 — Cross-device experience and offline resilience
- Milestone 10 — Optional linked-account import
- BudgetCarryForward
- Milestone: Annual Long-Term Budget Planning
- YNAB Web app: reverse-engineering notes
- Context
- Milestone 0 — Local application foundation
- TransactionSelectionBar.test.tsx
- LedgerContracts.cs
- FlagDefinitionEntity
- AccountNotes
- BudgetCarryForwardTests
- OpenBudget.Api
- PlanActionEntity
- AdminAuditLogEntity
- CategoryMoves
- LongTermPlanning
- Theory
- .From
- AccountEntity
- BudgetTogether.tsx
- .Describe
- CreditCardRulesTests
- Milestone 12 — Icons and suggested icons
- BudgetInvitationEntity
- BudgetService
- AccountLifecycle
- OpenBudget product roadmap
- SmtpSettingsEntity
- BudgetPlanEntity
- OpenBudget.Api
- CreditPayoffTargetEntity
- .ScheduleActionAsync
- AccountsPage.tsx
- AdminLifecycle
- MembershipEntitlements
- TransactionFlags
- AccountRegisterPresentation
- ScheduledTransactionsViewState
- Migration
- ModelBuilder
- CreditAccounts
- CreditPayoffTargets
- LedgerBalanceRow

## God Nodes (most connected - your core abstractions)
1. `BudgetDbContext` - 90 edges
2. `OpenBudget.Api` - 88 edges
3. `BudgetApplication()` - 82 edges
4. `MutationDto` - 77 edges
5. `LedgerService` - 69 edges
6. `LedgerApiTests` - 69 edges
7. `OpenBudget.Api.Migrations` - 59 edges
8. `TransactionEntity` - 46 edges
9. `BudgetDto` - 44 edges
10. `LedgerException` - 42 edges

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

## Communities (107 total, 22 thin omitted)

### Community 0 - "BudgetApplication"
Cohesion: 0.03
Nodes (68): react-dom, accountSections, App(), BudgetApplication(), addFunds(), applyAssignmentChanges(), applyAutoAssignOption(), applyMove() (+60 more)

### Community 1 - "DateTimeOffset"
Cohesion: 0.13
Nodes (7): BudgetOwnershipAndMembership, AdminEmailSettings, BudgetSharingAndPlanHistory, TestClock, LocalTimeZone, UtcNow, Zone

### Community 2 - "CategoryEntity"
Cohesion: 0.08
Nodes (25): CategoryEntity, Activity, Assigned, Available, BudgetId, BudgetMonth, CreditOverspending, GroupId (+17 more)

### Community 3 - "CategoryTarget.tsx"
Cohesion: 0.08
Nodes (33): BehaviorPicker(), choose(), keys(), CategoryTargetCard(), describe(), Money, TargetCategory, progress (+25 more)

### Community 4 - "vitest"
Cohesion: 0.05
Nodes (28): jsdom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript, vite (+20 more)

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
Nodes (36): SplitDto, TransactionDto, LongTermPlanningActivityDto, ScheduledItemDto, SchedulesDto, ScheduleTemplateDto, ForecastOccurrence, ScheduledPlanningTotals (+28 more)

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.08
Nodes (24): Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Testcontainers.PostgreSql (4.16.0) (+16 more)

### Community 12 - "Task"
Cohesion: 0.06
Nodes (40): Milestone 2 acceptance map, Milestone 2a final verification (2026-10-23), ChangePasswordRequest, LoginRequest, OpenBudgetUser, CreatedAt, IsBootstrapAdministrator, IsSuspended (+32 more)

### Community 13 - "MembershipEntitlements.cs"
Cohesion: 0.08
Nodes (11): `BudgetPlans`, BudgetLimitDto, DeleteOwnedBudgetRequest, RenameOwnedBudgetRequest, EntitlementAuthorizationHandler, EntitlementEndpointExtensions, EntitlementRequirement, MembershipEntitlementDto (+3 more)

### Community 15 - "CancellationToken"
Cohesion: 0.11
Nodes (13): API, BudgetOwnershipService, OwnedBudgetDto, BudgetRequestContext, BudgetId, BudgetInvitationDto, BudgetPersonDto, BudgetSharingDto (+5 more)

### Community 16 - "FailureInjector"
Cohesion: 0.22
Nodes (3): FailureInjector, FailAuditSave, FailBudgetAfterSave

### Community 17 - "CancellationToken"
Cohesion: 0.17
Nodes (12): LedgerService, SetFlagRequest, CreditSummaryDto, DeleteCreditPayoffTargetRequest, DeleteTransactionRequest, LedgerException, StatusCode, ReviewTransactionRequest (+4 more)

### Community 19 - "RegisterViewEntity"
Cohesion: 0.15
Nodes (13): RegisterViewEntity, BudgetId, ColumnWidthsJson, DatePreset, FromMonth, Scope, ShowFlags, ShowImages (+5 more)

### Community 20 - "TransactionEntity"
Cohesion: 0.08
Nodes (21): TransactionRequest, TransactionEntity, Account, AccountId, Amount, BudgetId, CategoryId, Date (+13 more)

### Community 21 - "Authentication.cs"
Cohesion: 0.07
Nodes (22): AccountEmailContent, AccountEmailSettingsService, AccountEmailTemplates, IAccountEmailSender, SendTestEmailRequest, SmtpAccountEmailSender, SmtpConnectionSettings, SmtpSettingsDto (+14 more)

### Community 22 - "requestJson"
Cohesion: 0.05
Nodes (52): Cash ledger slice, editor(), ApiError, CategoryOption, isCreditAccount(), loadCategories(), loadCategoryBudget(), requestJson() (+44 more)

### Community 23 - "List"
Cohesion: 0.22
Nodes (8): FlagDto, FlagPaletteDto, RenameFlagsRequest, PayeeDto, PayeeRuleDto, PayeeRules, PayeesDto, PayeeUpdateDto

### Community 24 - "API reference"
Cohesion: 0.07
Nodes (29): API reference, Approval and attention, Assignment rollover and earlier-month edits, Authentication, Boundaries, Budget collaboration and Plan history, Cash accounts and reconciliation, Category targets (+21 more)

### Community 26 - "BudgetDbContext"
Cohesion: 0.05
Nodes (31): BudgetDbContext, Accounts, ActiveBudgetId, AdminAuditLogs, ApplicationSettings, BudgetInvitations, BudgetMembers, BudgetPlans (+23 more)

### Community 27 - "Milestone 2a — Multi-User Authentication & Administration"
Cohesion: 0.10
Nodes (20): 10. Definition of Done, 1. Authentication & Login, 2. Permanent Administrator Accounts, 3. Registration & User Lifecycle, 4.1 Dashboard, 4.2 User Management, 4.3 Application Settings, 4. Separate Administration Panel (+12 more)

### Community 28 - "CreditPayoffTarget.tsx"
Cohesion: 0.11
Nodes (20): CreditPaymentDetails(), mutate(), card, progress, renderPanel(), CreditPayoff, currentMonth(), kindLabels (+12 more)

### Community 29 - "MoneyInput"
Cohesion: 0.14
Nodes (26): CategoryTargetEditor(), submit(), MoneyField(), NativeSelect(), Toggle(), AccountForm(), TransferForm(), parseMoney() (+18 more)

### Community 30 - ".OnModelCreating"
Cohesion: 0.09
Nodes (23): ApplicationSettingEntity, Key, Value, LongTermPlanningIncomeEntity, BudgetId, ExpectedIncome, Month, LongTermPlanningStateEntity (+15 more)

### Community 31 - "AdminPanel.tsx"
Cohesion: 0.12
Nodes (8): AdminAudit, AdminPanel(), AdminSummary, AdminUser, EmailSettings, MembershipDraft, MembershipPlan, modes

### Community 32 - "Architecture and current status"
Cohesion: 0.33
Nodes (6): Architecture and current status, Current feature inventory, Design limits to address, Implemented user path, Runtime topology, Technologies

### Community 33 - "CategoryTargetService"
Cohesion: 0.28
Nodes (4): CategoryTargetCommandResult, CategoryTargetResultDto, DeleteCategoryTargetRequest, CategoryTargetService

### Community 34 - "Agent Instructions"
Cohesion: 0.15
Nodes (12): Agent Instructions, Before making changes, Coding Guidelines, Development Principles, Documentation, Git, Project - OpenBudget, Roadmap (+4 more)

### Community 36 - "docs/README.md"
Cohesion: 0.16
Nodes (9): OpenBudget API and local services, Coding conventions, Current release shape, Documentation, Documents, Documentation, Highlights, Project status (+1 more)

### Community 37 - "CategoryTargetEntity"
Cohesion: 0.09
Nodes (18): Category targets, CategoryTargetEntity, Amount, Behavior, BudgetId, CategoryId, CreatedAt, DayOfMonth (+10 more)

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.17
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
Cohesion: 0.13
Nodes (5): InitialCreate, NamedBudget, PayeeManagement, IdentityFoundation, BudgetScopedMonths

### Community 45 - "Developer setup"
Cohesion: 0.22
Nodes (9): Build commands, Current development boundaries, Database lifecycle, Developer setup, First run, GitHub pull requests and releases, Prerequisites, Request flow (+1 more)

### Community 46 - "Milestone 4 — Scheduled transactions and cash-flow awareness"
Cohesion: 0.29
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 4 — Scheduled transactions and cash-flow awareness, Product references, User value, Vertical slice

### Community 47 - "RenameFlagsRequest"
Cohesion: 0.25
Nodes (7): 1. Add the GitHub workflows, 2. Configure Coolify, 3. Create the Coolify webhook, 4. Add GitHub secrets, 5. Publish a release, GitHub Release to Coolify, Useful checks

### Community 48 - "devDependencies"
Cohesion: 0.18
Nodes (11): devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript (+3 more)

### Community 49 - "ReconciliationEntity"
Cohesion: 0.29
Nodes (7): ReconciliationEntity, AccountId, BudgetId, CreatedAt, Id, IsInvalidated, StatementBalance

### Community 50 - "PayeeEntity"
Cohesion: 0.13
Nodes (14): LedgerModel, LedgerStateEntity, BudgetId, Id, Version, PayeeEntity, BudgetId, CategoryMode (+6 more)

### Community 51 - "BudgetMemberEntity"
Cohesion: 0.33
Nodes (6): BudgetMemberEntity, BudgetId, InvitedByUserId, JoinedAt, Role, UserId

### Community 52 - "scripts"
Cohesion: 0.33
Nodes (6): scripts, build, dev, preview, test, test:watch

### Community 53 - "LongTermPlanningDto"
Cohesion: 0.06
Nodes (38): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 acceptance map (+30 more)

### Community 54 - "DateOnly"
Cohesion: 0.18
Nodes (4): AdminUserDto, CashLedger, ScheduledTransactions, MembershipSubscriptionMetadata

### Community 55 - "Milestone 3 — Goals, templates, and guided assignment"
Cohesion: 0.25
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented: category targets (2026-10-30), Milestone 3 — Goals, templates, and guided assignment, Product references, User value, Vertical slice

### Community 56 - "LongTermPlanning.tsx"
Cohesion: 0.06
Nodes (41): ActivityDialog, ActivityKind, ActivitySelection, actualLabel(), actualMetrics, availableTone(), ConflictState, EditingValue (+33 more)

### Community 57 - "Milestone 5 — Credit cards and debt payoff planning"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 5 — Credit cards and debt payoff planning, Product references, User value, Vertical slice

### Community 58 - "Milestone 6 — Focused plan views and reports"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 6 — Focused plan views and reports, Product references, User value, Vertical slice

### Community 59 - "Milestone 8 — Identity, multiple budgets, and household sharing"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 8 — Identity, multiple budgets, and household sharing, Product reference, User value, Vertical slice

### Community 61 - "Milestone 9 — Cross-device experience and offline resilience"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 9 — Cross-device experience and offline resilience, Product reference, User value, Vertical slice

### Community 62 - "Milestone 10 — Optional linked-account import"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies and release gate, Goal, Milestone 10 — Optional linked-account import, Product references, User value, Vertical slice

### Community 63 - "BudgetCarryForward"
Cohesion: 0.08
Nodes (15): Backdated assignment corrections, `Budgets`, Calculations and month lifecycle, `CategoryGroups` and `Categories`, Credit accounts, Current PostgreSQL schema, Data model, Identity and account lifecycle (+7 more)

### Community 64 - "Milestone: Annual Long-Term Budget Planning"
Cohesion: 0.29
Nodes (6): Acceptance Criteria, Budget propagation rule, Core behavior, Goal, Implementation notes, Milestone: Annual Long-Term Budget Planning

### Community 65 - "YNAB Web app: reverse-engineering notes"
Cohesion: 0.29
Nodes (6): Account-linking code observed in the bundle, Evidence and scope, Frontend structure, Lazy modules and browser storage, Limits and follow-up, YNAB Web app: reverse-engineering notes

### Community 66 - "Context"
Cohesion: 0.50
Nodes (4): Context, MonthEnd, MonthIndex, StartIndex

### Community 67 - "Milestone 0 — Local application foundation"
Cohesion: 0.33
Nodes (5): Acceptance criteria, Goal, Milestone 0 — Local application foundation, User and developer outcome, Vertical slice

### Community 68 - "TransactionSelectionBar.test.tsx"
Cohesion: 0.08
Nodes (45): react, AccountHeader(), balanceSummary(), ClearedIndicator(), reconciliationText(), RegisterIcon(), account, entry (+37 more)

### Community 71 - "LedgerContracts.cs"
Cohesion: 0.24
Nodes (8): AccountDto, AccountsDto, AuditDto, AuditPageDto, CreditPayoffDto, CreditPayoffProgressDto, CreditPayoffTargetDto, RegisterDto

### Community 72 - "FlagDefinitionEntity"
Cohesion: 0.50
Nodes (4): FlagDefinitionEntity, BudgetId, Color, Name

### Community 74 - "BudgetCarryForwardTests"
Cohesion: 0.18
Nodes (11): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+3 more)

### Community 76 - "PlanActionEntity"
Cohesion: 0.20
Nodes (10): PlanActionEntity, ActorUserId, Amount, BudgetId, CreatedAt, FromCategoryName, Id, Kind (+2 more)

### Community 77 - "AdminAuditLogEntity"
Cohesion: 0.33
Nodes (6): AdminAuditLogEntity, Action, ActorUserId, CreatedAt, Id, TargetUserId

### Community 80 - "Theory"
Cohesion: 0.09
Nodes (7): CreditPayoffRules, RegisterViewDto, RegisterViewService, CreditPayoffRulesTests, EntitlementKeyTests, PayeeRulesTests, RegisterViewTests

### Community 81 - ".From"
Cohesion: 0.60
Nodes (3): CategoryDto, CategoryGroupDto, CategoryTargetDto

### Community 82 - "AccountEntity"
Cohesion: 0.11
Nodes (10): CreditCardRules, AccountEntity, AccountType, BudgetId, Id, IsClosed, IsFavorite, Name (+2 more)

### Community 83 - "BudgetTogether.tsx"
Cohesion: 0.29
Nodes (4): BudgetTogether(), Invitation, Person, Sharing

### Community 84 - ".Describe"
Cohesion: 0.30
Nodes (4): PlanActionDraft, PlanActionRules, Flows, PlanActionRulesTests

### Community 86 - "Milestone 12 — Icons and suggested icons"
Cohesion: 0.22
Nodes (9): Acceptance criteria, Dependencies, Goal, Milestone 12 — Icons and suggested icons, Open questions, Product references, User value, Vertical slice (+1 more)

### Community 87 - "BudgetInvitationEntity"
Cohesion: 0.29
Nodes (7): BudgetInvitationEntity, BudgetId, CreatedAt, Email, Id, InvitedByUserId, NormalizedEmail

### Community 88 - "BudgetService"
Cohesion: 0.17
Nodes (5): BudgetPlanDto, BudgetPlanUpdateDto, BudgetPlanUpdateResult, BudgetUpdateResult, BudgetService

### Community 90 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

### Community 91 - "SmtpSettingsEntity"
Cohesion: 0.22
Nodes (9): SmtpSettingsEntity, EnableSsl, EncryptedPassword, From, Host, Id, Port, UpdatedAt (+1 more)

### Community 92 - "BudgetPlanEntity"
Cohesion: 0.22
Nodes (9): BudgetPlanEntity, CreatedAt, Currency, Id, IsArchived, Months, Name, OwnerUserId (+1 more)

### Community 93 - "OpenBudget.Api"
Cohesion: 0.12
Nodes (3): OpenBudget.Api, OpenBudget.UnitTests, RecurrenceTests

### Community 94 - "CreditPayoffTargetEntity"
Cohesion: 0.22
Nodes (9): CreditPayoffTargetEntity, AccountId, BudgetId, CreatedAt, Kind, MonthlyAmount, PayoffAmount, TargetMonth (+1 more)

### Community 95 - ".ScheduleActionAsync"
Cohesion: 0.16
Nodes (8): SplitEntity, Amount, CategoryId, Memo, Payee, Position, TransactionId, RecurrenceRules

### Community 96 - "AccountsPage.tsx"
Cohesion: 0.09
Nodes (27): AccountsPage(), applyView(), beginColumnResize(), clearFilters(), finishColumnResize(), moveColumnResize(), mutate(), persistColumnWidths() (+19 more)

### Community 99 - "TransactionFlags"
Cohesion: 0.20
Nodes (4): Limitations and direction, Scheduling and flag storage, TransactionFlags, ScheduledApproval

### Community 102 - "Migration"
Cohesion: 0.17
Nodes (3): BudgetCore, AccountViewOptions, RegisterColumnWidths

### Community 103 - "ModelBuilder"
Cohesion: 0.14
Nodes (3): BudgetFinancialIsolation, TransactionSplitDetails, BudgetDbContextModelSnapshot

### Community 106 - "LedgerBalanceRow"
Cohesion: 0.67
Nodes (3): LedgerBalanceRow, Balance, Id

## Knowledge Gaps
- **308 isolated node(s):** `IsSuspended`, `IsBootstrapAdministrator`, `RequiresApproval`, `CreatedAt`, `LastLoginAt` (+303 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 780 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **22 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Cash ledger slice` connect `requestJson` to `Architecture and current status`, `ModelBuilder`, `CancellationToken`, `CancellationToken`, `BudgetService`, `BudgetCarryForward`?**
  _High betweenness centrality (0.283) - this node is a cross-community bridge._
- **What connects `IsSuspended`, `IsBootstrapAdministrator`, `RequiresApproval` to the rest of the system?**
  _308 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `BudgetApplication` be split into smaller, more focused modules?**
  _Cohesion score 0.03223443223443224 - nodes in this community are weakly interconnected._
- **Why does `BudgetDbContext` connect `BudgetDbContext` to `CategoryEntity`, `ScheduleEntity`, `Task`, `CancellationToken`, `CancellationToken`, `RegisterViewEntity`, `TransactionEntity`, `Authentication.cs`, `.OnModelCreating`, `CategoryTargetService`, `CategoryTargetEntity`, `LedgerCommandEntity`, `UserMembershipEntity`, `ReconciliationEntity`, `PayeeEntity`, `BudgetMemberEntity`, `LongTermPlanningDto`, `FlagDefinitionEntity`, `BudgetCarryForwardTests`, `PlanActionEntity`, `AdminAuditLogEntity`, `Theory`, `AccountEntity`, `BudgetInvitationEntity`, `BudgetService`, `SmtpSettingsEntity`, `BudgetPlanEntity`, `CreditPayoffTargetEntity`?**
  _High betweenness centrality (0.156) - this node is a cross-community bridge._
- **Should `DateTimeOffset` be split into smaller, more focused modules?**
  _Cohesion score 0.1286549707602339 - nodes in this community are weakly interconnected._
- **Why does `LedgerService` connect `CancellationToken` to `SchedulePosting.cs`, `LedgerContracts.cs`, `ScheduleEntity`, `LedgerBalanceRow`, `CancellationToken`, `AccountEntity`, `TransactionEntity`, `requestJson`, `List`, `BudgetService`, `BudgetDbContext`, `OpenBudget.Api`, `.ScheduleActionAsync`?**
  _High betweenness centrality (0.152) - this node is a cross-community bridge._
- **Should `CategoryEntity` be split into smaller, more focused modules?**
  _Cohesion score 0.08 - nodes in this community are weakly interconnected._