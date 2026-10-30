# Graph Report - ynab-clone  (2026-10-27)

## Corpus Check
- 198 files · ~130,939 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 13 file(s) not represented in the graph (top: .ttf 4, .css 4, (none) 3)

## Summary
- 1875 nodes · 5240 edges · 99 communities (81 shown, 18 thin omitted)
- Extraction: 91% EXTRACTED · 9% INFERRED · 0% AMBIGUOUS · INFERRED: 452 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `696092f0`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- BudgetApplication
- App.tsx
- CategoryEntity
- App.test.tsx
- vitest
- http
- compilerOptions
- http
- List
- OpenBudget.IntegrationTests.csproj
- Task
- MigrationBuilder
- EntitlementService
- CancellationToken
- FailureInjector
- CancellationToken
- Authentication.cs
- .ScheduleActionAsync
- TransactionEntity
- AccountEmail.cs
- Payees.test.tsx
- Testing and acceptance coverage
- API reference
- Current PostgreSQL schema
- BudgetDbContext
- Milestone 2a — Multi-User Authentication & Administration
- .RunAsync
- InlineTransactionEditor.tsx
- AccountsPage
- AdminPanel.tsx
- Architecture and current status
- 20261021181641_TransactionFlags.Designer.cs
- Agent Instructions
- AccountsPage.tsx
- docs/README.md
- AccountRegister.tsx
- LedgerService
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
- Guid
- AccountEmailContent
- RegisterViewEntity
- BudgetPlanEntity
- Milestone 3 — Goals, templates, and guided assignment
- LongTermPlanning
- Milestone 5 — Credit cards and debt payoff planning
- Milestone 6 — Focused plan views and reports
- Milestone 8 — Identity, multiple budgets, and household sharing
- List
- Milestone 9 — Cross-device experience and offline resilience
- Milestone 10 — Optional linked-account import
- .CalculateChanges
- Milestone: Annual Long-Term Budget Planning
- YNAB Web app: reverse-engineering notes
- TransactionSelectionApiTests
- Milestone 0 — Local application foundation
- TransactionSelectionBar.test.tsx
- NewCategoryDialog.tsx
- devDependencies
- BudgetEntity
- BudgetCarryForwardTests
- OpenBudget.Api
- PlanActionEntity
- BudgetDbContext.cs
- AdminAuditLogEntity
- CategoryGroupEntity
- RegisterViewEntity
- ModelBuilder
- LongTermPlanning.test.tsx
- planHelpers.ts
- .Describe
- BudgetSharing.cs
- RenameFlagsRequest
- scripts
- .UpdateMonthAsync
- AccountViewOptions
- OpenBudget product roadmap
- AccountNotes
- PayeeManagement
- BudgetOwnershipAndMembership
- AdminEmailSettings
- BudgetFinancialIsolation
- RegisterColumnWidths
- BudgetTogether.tsx
- ProfileSettings

## God Nodes (most connected - your core abstractions)
1. `BudgetDbContext` - 83 edges
2. `OpenBudget.Api` - 72 edges
3. `LedgerApiTests` - 67 edges
4. `BudgetApplication()` - 65 edges
5. `OpenBudget.Api.Migrations` - 49 edges
6. `LedgerException` - 37 edges
7. `AccountsPage()` - 37 edges
8. `TransactionEntity` - 34 edges
9. `BudgetEntity` - 32 edges
10. `LedgerService` - 32 edges

## Surprising Connections (you probably didn't know these)
- ``GET /api/budget/{month}`` --references--> `month()`  [INFERRED]
  docs/api.md → src/OpenBudget.UI/src/ledger/ViewOptions.tsx
- `Importing an accepted normalized row` --references--> `TransactionRequest`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/LedgerContracts.cs
- `Backdated assignment corrections` --references--> `BudgetCarryForward`  [INFERRED]
  docs/data-model.md → src/OpenBudget.Api/BudgetCarryForward.cs
- `Cash ledger slice` --references--> `BudgetCarryForward`  [INFERRED]
  docs/architecture.md → src/OpenBudget.Api/BudgetCarryForward.cs
- `Cash ledger slice` --references--> `BudgetService`  [INFERRED]
  docs/architecture.md → src/OpenBudget.Api/BudgetService.cs

## Import Cycles
- None detected.

## Communities (99 total, 18 thin omitted)

### Community 0 - "BudgetApplication"
Cohesion: 0.06
Nodes (28): BudgetApplication(), addFunds(), applyAssignmentChanges(), applyAutoAssignOption(), applyMove(), cancelClosePlans(), closeCategoryDialog(), createBudget() (+20 more)

### Community 1 - "App.tsx"
Cohesion: 0.08
Nodes (26): BudgetPlan, BudgetState, Category, CategoryGroup, CategoryTransactionDialog, currency, Filter, filters (+18 more)

### Community 2 - "CategoryEntity"
Cohesion: 0.12
Nodes (16): CategoryEntity, Activity, Assigned, Available, BudgetId, BudgetMonth, GroupId, Icon (+8 more)

### Community 3 - "App.test.tsx"
Cohesion: 0.11
Nodes (7): App(), Month, next(), saved(), AuthBrand(), AuthGate(), Session

### Community 4 - "vitest"
Cohesion: 0.08
Nodes (20): jsdom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript, vite (+12 more)

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
Nodes (14): Milestone 2 acceptance map, CreateOwnedBudgetRequest, ReviewTransactionRequest, SavePayeesRequest, ScheduleActionRequest, ScheduleRequest, BudgetCarryForwardApiTests, BudgetOwnershipApiTests (+6 more)

### Community 13 - "MigrationBuilder"
Cohesion: 0.11
Nodes (7): InitialCreate, BudgetCore, NamedBudget, AccountRegisterPresentation, LongTermPlanning, BudgetScopedMonths, MembershipEntitlements

### Community 14 - "EntitlementService"
Cohesion: 0.14
Nodes (11): `BudgetPlans`, MembershipPlanEntitlementEntity, EntitlementKey, IsEnabled, MembershipPlanId, UserEntitlementOverrideEntity, EntitlementKey, IsEnabled (+3 more)

### Community 15 - "CancellationToken"
Cohesion: 0.17
Nodes (6): BudgetLimitDto, BudgetOwnershipService, DeleteOwnedBudgetRequest, OwnedBudgetDto, RenameOwnedBudgetRequest, BudgetSharingService

### Community 16 - "FailureInjector"
Cohesion: 0.20
Nodes (3): FailureInjector, FailAuditSave, FailBudgetAfterSave

### Community 17 - "CancellationToken"
Cohesion: 0.20
Nodes (4): LedgerService, LedgerException, StatusCode, Change

### Community 18 - "Authentication.cs"
Cohesion: 0.05
Nodes (32): Milestone 2a final verification (2026-10-23), AdminAuditDto, AdminDashboardDto, AdminInviteUserRequest, AdminMembershipCountDto, AdminMembershipRequest, AdminRegistrationDto, AdminUserDto (+24 more)

### Community 19 - ".ScheduleActionAsync"
Cohesion: 0.21
Nodes (6): SplitEntity, Amount, CategoryId, Position, TransactionId, RecurrenceRules

### Community 20 - "TransactionEntity"
Cohesion: 0.07
Nodes (21): `CategoryGroups` and `Categories`, TransactionEntity, Account, AccountId, Amount, BudgetId, CategoryId, Date (+13 more)

### Community 21 - "AccountEmail.cs"
Cohesion: 0.13
Nodes (14): AccountEmailSettingsService, SendTestEmailRequest, SmtpConnectionSettings, SmtpSettingsDto, UpdateSmtpSettingsRequest, SmtpSettingsEntity, EnableSsl, EncryptedPassword (+6 more)

### Community 22 - "Payees.test.tsx"
Cohesion: 0.11
Nodes (15): ManagePayees(), close(), done(), edit(), rule(), update(), PayeeList, PayeePicker() (+7 more)

### Community 23 - "Testing and acceptance coverage"
Cohesion: 0.06
Nodes (39): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 acceptance map (+31 more)

### Community 24 - "API reference"
Cohesion: 0.08
Nodes (26): API reference, Approval and attention, Assignment rollover and earlier-month edits, Authentication, Boundaries, Budget collaboration and Plan history, Cash accounts and reconciliation, `GET /api/budget` (+18 more)

### Community 25 - "Current PostgreSQL schema"
Cohesion: 0.20
Nodes (9): Backdated assignment corrections, Calculations and month lifecycle, Current PostgreSQL schema, Data model, Identity and account lifecycle, Ledger tables, Long-Term Planning, Payee management (+1 more)

### Community 26 - "BudgetDbContext"
Cohesion: 0.05
Nodes (36): ApplicationSettingEntity, Key, Value, BudgetDbContext, Accounts, ActiveBudgetId, AdminAuditLogs, ApplicationSettings (+28 more)

### Community 27 - "Milestone 2a — Multi-User Authentication & Administration"
Cohesion: 0.10
Nodes (20): 10. Definition of Done, 1. Authentication & Login, 2. Permanent Administrator Accounts, 3. Registration & User Lifecycle, 4.1 Dashboard, 4.2 User Management, 4.3 Application Settings, 4. Separate Administration Panel (+12 more)

### Community 28 - ".RunAsync"
Cohesion: 0.50
Nodes (4): FlagDefinitionEntity, BudgetId, Color, Name

### Community 29 - "InlineTransactionEditor.tsx"
Cohesion: 0.15
Nodes (20): react, react-dom, CategoryOption, categoryGroups(), CategoryPicker(), choose(), close(), option() (+12 more)

### Community 30 - "AccountsPage"
Cohesion: 0.09
Nodes (28): AccountForm(), AccountsPage(), applyView(), beginColumnResize(), clearFilters(), finishColumnResize(), mutate(), persistColumnWidths() (+20 more)

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
Cohesion: 0.13
Nodes (26): Cash ledger slice, editor(), Dialog, Account, Accounts, ApiError, Audit, Entry (+18 more)

### Community 36 - "docs/README.md"
Cohesion: 0.17
Nodes (8): Coding conventions, Current release shape, Documentation, Documents, Documentation, Highlights, Project status, Quick start

### Community 37 - "AccountRegister.tsx"
Cohesion: 0.18
Nodes (18): AccountHeader(), balanceSummary(), ClearedIndicator(), reconciliationText(), RegisterIcon(), account, entry, TransactionRow() (+10 more)

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.18
Nodes (11): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Plan interaction extension (2026-10-22) (+3 more)

### Community 41 - ".OnModelCreating"
Cohesion: 0.05
Nodes (39): AccountEntity, AccountType, BudgetId, Id, IsClosed, IsFavorite, Name, Notes (+31 more)

### Community 42 - "UserMembershipEntity"
Cohesion: 0.22
Nodes (9): UserMembershipEntity, BillingProvider, BudgetLimitOverride, ExpiresOn, ExternalSubscriptionId, MembershipPlanId, StartsOn, UserId (+1 more)

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.22
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

### Community 44 - "DateTimeOffset"
Cohesion: 0.12
Nodes (8): PlanActionDto, IdentityFoundation, AdminLifecycle, BudgetSharingAndPlanHistory, TestClock, LocalTimeZone, UtcNow, Zone

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
Nodes (23): ActivityDialog, ActivityKind, ActivitySelection, actualMetrics, ConflictState, EditingValue, inputAmount(), LongTermPlanningProps (+15 more)

### Community 50 - "DateOnly"
Cohesion: 0.19
Nodes (3): CashLedger, ScheduledTransactions, MembershipSubscriptionMetadata

### Community 51 - "Guid"
Cohesion: 0.21
Nodes (14): AccountDto, AccountsDto, AuditDto, AuditPageDto, CreateAccountRequest, DeleteTransactionRequest, ReconcileRequest, RegisterDto (+6 more)

### Community 52 - "AccountEmailContent"
Cohesion: 0.19
Nodes (8): AccountEmailContent, AccountEmailTemplates, IAccountEmailSender, SmtpAccountEmailSender, TestAccountEmailSender, FailSending, IsConfigured, Messages

### Community 53 - "RegisterViewEntity"
Cohesion: 0.16
Nodes (4): EntitlementKeyTests, LedgerRulesTests, PayeeRulesTests, RegisterViewTests

### Community 54 - "BudgetPlanEntity"
Cohesion: 0.13
Nodes (11): BudgetPlanDto, BudgetPlanUpdateResult, BudgetPlanEntity, CreatedAt, Currency, Id, IsArchived, Months (+3 more)

### Community 55 - "Milestone 3 — Goals, templates, and guided assignment"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 3 — Goals, templates, and guided assignment, Product references, User value, Vertical slice

### Community 56 - "LongTermPlanning"
Cohesion: 0.20
Nodes (16): actualLabel(), availableTone(), LongTermPlanning(), beginEdit(), commitDraft(), groupValues(), metricValue(), openActivity() (+8 more)

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
Cohesion: 0.25
Nodes (6): PayeeDto, PayeeRuleDto, PayeeRules, PayeesDto, PayeeUpdateDto, TransactionSelectionRequest

### Community 61 - "Milestone 9 — Cross-device experience and offline resilience"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 9 — Cross-device experience and offline resilience, Product reference, User value, Vertical slice

### Community 62 - "Milestone 10 — Optional linked-account import"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies and release gate, Goal, Milestone 10 — Optional linked-account import, Product references, User value, Vertical slice

### Community 63 - ".CalculateChanges"
Cohesion: 0.25
Nodes (4): `Budgets`, BudgetCarryForward, Changes, HasChanges

### Community 64 - "Milestone: Annual Long-Term Budget Planning"
Cohesion: 0.29
Nodes (6): Acceptance Criteria, Budget propagation rule, Core behavior, Goal, Implementation notes, Milestone: Annual Long-Term Budget Planning

### Community 65 - "YNAB Web app: reverse-engineering notes"
Cohesion: 0.29
Nodes (6): Account-linking code observed in the bundle, Evidence and scope, Frontend structure, Lazy modules and browser storage, Limits and follow-up, YNAB Web app: reverse-engineering notes

### Community 66 - "TransactionSelectionApiTests"
Cohesion: 0.54
Nodes (3): MutationDto, TransactionRequest, TransactionSelectionApiTests

### Community 67 - "Milestone 0 — Local application foundation"
Cohesion: 0.33
Nodes (5): Acceptance criteria, Goal, Milestone 0 — Local application foundation, User and developer outcome, Vertical slice

### Community 68 - "TransactionSelectionBar.test.tsx"
Cohesion: 0.15
Nodes (13): csvText(), selectedTransactionsCsv(), accounts, apply, clear, entry, show(), TransactionSelectionBar() (+5 more)

### Community 71 - "NewCategoryDialog.tsx"
Cohesion: 0.20
Nodes (7): Category, Month, NewCategoryDialog(), save(), close, created, show()

### Community 72 - "devDependencies"
Cohesion: 0.18
Nodes (11): devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript (+3 more)

### Community 73 - "BudgetEntity"
Cohesion: 0.20
Nodes (10): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+2 more)

### Community 75 - "OpenBudget.Api"
Cohesion: 0.06
Nodes (10): OpenBudget.Api, OpenBudget.Api.Migrations, OpenBudget.UnitTests, OpenBudget.IntegrationTests, BudgetRequestContext, BudgetId, Program, SchedulePostingService (+2 more)

### Community 76 - "PlanActionEntity"
Cohesion: 0.20
Nodes (10): PlanActionEntity, ActorUserId, Amount, BudgetId, CreatedAt, FromCategoryName, Id, Kind (+2 more)

### Community 77 - "BudgetDbContext.cs"
Cohesion: 0.09
Nodes (21): BudgetInvitationEntity, BudgetId, CreatedAt, Email, Id, InvitedByUserId, NormalizedEmail, BudgetMemberEntity (+13 more)

### Community 78 - "AdminAuditLogEntity"
Cohesion: 0.25
Nodes (6): AdminAuditLogEntity, Action, ActorUserId, CreatedAt, Id, TargetUserId

### Community 79 - "CategoryGroupEntity"
Cohesion: 0.22
Nodes (8): CategoryGroupEntity, BudgetId, BudgetMonth, Categories, Id, IsArchived, Name, SortOrder

### Community 80 - "RegisterViewEntity"
Cohesion: 0.12
Nodes (14): RegisterViewDto, RegisterViewEntity, BudgetId, ColumnWidthsJson, DatePreset, FromMonth, Scope, ShowFlags (+6 more)

### Community 83 - "planHelpers.ts"
Cohesion: 0.36
Nodes (6): moveCategory(), moveGroup(), openUpcoming(), categoryProgress(), categoryTransactionAmount(), moveItem()

### Community 85 - "BudgetSharing.cs"
Cohesion: 0.47
Nodes (5): BudgetInvitationDto, BudgetPersonDto, BudgetSharingDto, InviteBudgetMemberRequest, PlanActionRules

### Community 86 - "RenameFlagsRequest"
Cohesion: 0.53
Nodes (4): FlagDto, FlagPaletteDto, RenameFlagsRequest, SetFlagRequest

### Community 87 - "scripts"
Cohesion: 0.33
Nodes (6): scripts, build, dev, preview, test, test:watch

### Community 88 - ".UpdateMonthAsync"
Cohesion: 0.17
Nodes (8): API, OpenBudget API and local services, BudgetDto, BudgetUpdateResult, CategoryDto, CategoryGroupDto, BudgetService, LedgerGate

### Community 90 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

### Community 97 - "BudgetTogether.tsx"
Cohesion: 0.50
Nodes (3): Invitation, Person, Sharing

## Knowledge Gaps
- **275 isolated node(s):** `HasChanges`, `ActiveBudgetId`, `BudgetPlans`, `BudgetMembers`, `BudgetInvitations` (+270 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 723 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **18 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Cash ledger slice` connect `AccountsPage.tsx` to `Architecture and current status`, `NewCategoryDialog.tsx`, `Payees.test.tsx`, `.UpdateMonthAsync`, `InlineTransactionEditor.tsx`, `.CalculateChanges`?**
  _High betweenness centrality (0.315) - this node is a cross-community bridge._
- **What connects `HasChanges`, `ActiveBudgetId`, `BudgetPlans` to the rest of the system?**
  _275 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `BudgetApplication` be split into smaller, more focused modules?**
  _Cohesion score 0.0647342995169082 - nodes in this community are weakly interconnected._
- **Why does `BudgetService` connect `.UpdateMonthAsync` to `AccountsPage.tsx`, `LedgerService`, `BudgetCarryForwardTests`, `OpenBudget.Api`, `CategoryGroupEntity`, `BudgetPlanEntity`, `BudgetDbContext`, `.CalculateChanges`?**
  _High betweenness centrality (0.163) - this node is a cross-community bridge._
- **Should `App.tsx` be split into smaller, more focused modules?**
  _Cohesion score 0.08021390374331551 - nodes in this community are weakly interconnected._
- **Why does `CategoryPicker()` connect `InlineTransactionEditor.tsx` to `BudgetApplication`, `App.tsx`, `AccountsPage.tsx`, `AccountRegister.tsx`, `Payees.test.tsx`?**
  _High betweenness centrality (0.141) - this node is a cross-community bridge._
- **Should `CategoryEntity` be split into smaller, more focused modules?**
  _Cohesion score 0.125 - nodes in this community are weakly interconnected._