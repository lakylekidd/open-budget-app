# Graph Report - ynab-clone  (2026-10-29)

## Corpus Check
- 198 files · ~132,114 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 13 file(s) not represented in the graph (top: .ttf 4, .css 4, (none) 3)

## Summary
- 1887 nodes · 5266 edges · 100 communities (78 shown, 22 thin omitted)
- Extraction: 91% EXTRACTED · 9% INFERRED · 0% AMBIGUOUS · INFERRED: 455 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `30ae8137`
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
- MembershipEntitlements.cs
- CancellationToken
- FailureInjector
- CancellationToken
- .MapAuthenticationEndpoints
- AccountsPage
- TransactionEntity
- AccountEmail.cs
- Payees.test.tsx
- Testing and acceptance coverage
- API reference
- AccountRegisterPresentation
- BudgetDbContext
- Milestone 2a — Multi-User Authentication & Administration
- RegisterViewEntity
- InlineTransactionEditor.tsx
- AccountsPage
- AdminPanel.tsx
- Architecture and current status
- 20261021181641_TransactionFlags.Designer.cs
- Agent Instructions
- parseMoney
- docs/README.md
- AccountRegister.tsx
- PayeeEntity
- Milestone 1 — Reliable budget core and month lifecycle
- Milestone 2 — Accounts and transaction register
- .OnModelCreating
- UserMembershipEntity
- Milestone 7 — File import, matching, and portable data
- DateTimeOffset
- Developer setup
- Milestone 4 — Scheduled transactions and cash-flow awareness
- RenameFlagsRequest
- LongTermPlanning.test.tsx
- CategoryMoves
- DateOnly
- Guid
- AccountEmailContent
- LedgerRulesTests
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
- RegisterViewDto
- ModelBuilder
- AuthGate
- planHelpers.ts
- .Describe
- .ProcessAllBudgetsAsync
- API
- BudgetInvitationEntity
- .UpdateMonthAsync
- Migration
- OpenBudget product roadmap
- AccountLifecycle
- PayeeManagement
- AdminLifecycle
- BudgetScopedMonths
- BudgetFinancialIsolation
- RegisterColumnWidths
- BudgetTogether.tsx
- vitest
- BudgetSharingAndPlanHistory

## God Nodes (most connected - your core abstractions)
1. `BudgetDbContext` - 82 edges
2. `OpenBudget.Api` - 72 edges
3. `BudgetApplication()` - 72 edges
4. `LedgerApiTests` - 67 edges
5. `OpenBudget.Api.Migrations` - 49 edges
6. `LedgerException` - 37 edges
7. `AccountsPage()` - 37 edges
8. `TransactionEntity` - 34 edges
9. `Milestone 2 acceptance map` - 32 edges
10. `LedgerService` - 32 edges

## Surprising Connections (you probably didn't know these)
- ``GET /api/budget/{month}`` --references--> `month()`  [INFERRED]
  docs/api.md → src/OpenBudget.UI/src/ledger/ViewOptions.tsx
- `Importing an accepted normalized row` --references--> `TransactionRequest`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/LedgerContracts.cs
- `Ledger tables` --references--> `SplitEntity`  [INFERRED]
  docs/data-model.md → src/OpenBudget.Api/LedgerModel.cs
- `Scheduling and flag storage` --references--> `ScheduledTransactions`  [INFERRED]
  docs/data-model.md → src/OpenBudget.Api/Migrations/20261021181045_ScheduledTransactions.Designer.cs
- `Payee management` --references--> `PayeeManagement`  [INFERRED]
  docs/data-model.md → src/OpenBudget.Api/Migrations/20261021190028_PayeeManagement.Designer.cs

## Import Cycles
- None detected.

## Communities (100 total, 22 thin omitted)

### Community 0 - "BudgetApplication"
Cohesion: 0.06
Nodes (30): BudgetApplication(), addFunds(), applyAssignmentChanges(), applyAutoAssignOption(), applyMove(), cancelClosePlans(), closeCategoryDialog(), createBudget() (+22 more)

### Community 1 - "App.tsx"
Cohesion: 0.09
Nodes (24): App(), BudgetPlan, BudgetState, Category, CategoryGroup, CategoryTransactionDialog, currency, Filter (+16 more)

### Community 2 - "CategoryEntity"
Cohesion: 0.12
Nodes (16): CategoryEntity, Activity, Assigned, Available, BudgetId, BudgetMonth, GroupId, Icon (+8 more)

### Community 3 - "App.test.tsx"
Cohesion: 0.25
Nodes (3): Month, next(), saved()

### Community 4 - "vitest"
Cohesion: 0.10
Nodes (14): jsdom, @types/react, @types/react-dom, typescript, vite, @vitejs/plugin-react, dependencies, react (+6 more)

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
Nodes (32): RecurrenceRules, ScheduleTemplateDto, ForecastOccurrence, ScheduledPlanningTotals, ScheduleEntity, AccountId, AnchorDate, BudgetId (+24 more)

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.08
Nodes (24): Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Testcontainers.PostgreSql (4.16.0) (+16 more)

### Community 12 - "Task"
Cohesion: 0.05
Nodes (35): Milestone 2 acceptance map, Milestone 2a final verification (2026-10-23), AdminInviteUserRequest, AdminMembershipRequest, ChangePasswordRequest, ForgotPasswordRequest, LoginRequest, OpenBudgetUser (+27 more)

### Community 13 - "MigrationBuilder"
Cohesion: 0.16
Nodes (4): BudgetCore, NamedBudget, AccountNotes, MembershipEntitlements

### Community 14 - "MembershipEntitlements.cs"
Cohesion: 0.07
Nodes (17): `BudgetPlans`, MembershipPlanEntitlementEntity, EntitlementKey, IsEnabled, MembershipPlanId, UserEntitlementOverrideEntity, EntitlementKey, IsEnabled (+9 more)

### Community 15 - "CancellationToken"
Cohesion: 0.12
Nodes (12): BudgetLimitDto, BudgetOwnershipService, DeleteOwnedBudgetRequest, OwnedBudgetDto, RenameOwnedBudgetRequest, BudgetInvitationDto, BudgetPersonDto, BudgetSharingDto (+4 more)

### Community 16 - "FailureInjector"
Cohesion: 0.20
Nodes (3): FailureInjector, FailAuditSave, FailBudgetAfterSave

### Community 17 - "CancellationToken"
Cohesion: 0.13
Nodes (6): LedgerService, LedgerException, StatusCode, Change, LedgerService, Entries

### Community 18 - ".MapAuthenticationEndpoints"
Cohesion: 0.25
Nodes (5): AdminAuditDto, AdminDashboardDto, AdminMembershipCountDto, AdminRegistrationDto, AuthenticationSetup

### Community 19 - "AccountsPage"
Cohesion: 0.19
Nodes (11): AccountsPage(), applyView(), beginColumnResize(), clearFilters(), finishColumnResize(), mutate(), persistColumnWidths(), renameFlags() (+3 more)

### Community 20 - "TransactionEntity"
Cohesion: 0.08
Nodes (25): SplitEntity, Amount, CategoryId, Position, TransactionId, TransactionEntity, Account, AccountId (+17 more)

### Community 21 - "AccountEmail.cs"
Cohesion: 0.13
Nodes (14): AccountEmailSettingsService, SendTestEmailRequest, SmtpConnectionSettings, SmtpSettingsDto, UpdateSmtpSettingsRequest, SmtpSettingsEntity, EnableSsl, EncryptedPassword (+6 more)

### Community 22 - "Payees.test.tsx"
Cohesion: 0.10
Nodes (19): requestJson(), ManagePayees(), close(), done(), edit(), rule(), update(), PayeeList (+11 more)

### Community 23 - "Testing and acceptance coverage"
Cohesion: 0.06
Nodes (37): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 acceptance map (+29 more)

### Community 24 - "API reference"
Cohesion: 0.08
Nodes (26): API reference, Approval and attention, Assignment rollover and earlier-month edits, Authentication, Boundaries, Budget collaboration and Plan history, Cash accounts and reconciliation, `GET /api/budget` (+18 more)

### Community 26 - "BudgetDbContext"
Cohesion: 0.05
Nodes (36): ApplicationSettingEntity, Key, Value, BudgetDbContext, Accounts, ActiveBudgetId, AdminAuditLogs, ApplicationSettings (+28 more)

### Community 27 - "Milestone 2a — Multi-User Authentication & Administration"
Cohesion: 0.10
Nodes (20): 10. Definition of Done, 1. Authentication & Login, 2. Permanent Administrator Accounts, 3. Registration & User Lifecycle, 4.1 Dashboard, 4.2 User Management, 4.3 Application Settings, 4. Separate Administration Panel (+12 more)

### Community 28 - "RegisterViewEntity"
Cohesion: 0.17
Nodes (12): RegisterViewEntity, BudgetId, ColumnWidthsJson, DatePreset, FromMonth, Scope, ShowFlags, ShowImages (+4 more)

### Community 29 - "InlineTransactionEditor.tsx"
Cohesion: 0.14
Nodes (20): Cash ledger slice, react-dom, editor(), CategoryOption, loadCategories(), loadCategoryBudget(), Split, categoryGroups() (+12 more)

### Community 30 - "AccountsPage"
Cohesion: 0.12
Nodes (24): AccountForm(), Dialog, checking, savings, show(), TransferForm(), Accounts, Audit (+16 more)

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

### Community 35 - "parseMoney"
Cohesion: 0.27
Nodes (9): ReconcileForm(), parseMoney(), EditAccountForm(), save(), account, save(), parseRegisterMoney(), TransactionForm() (+1 more)

### Community 36 - "docs/README.md"
Cohesion: 0.18
Nodes (8): Coding conventions, Current release shape, Documentation, Documents, Documentation, Highlights, Project status, Quick start

### Community 37 - "AccountRegister.tsx"
Cohesion: 0.30
Nodes (12): AccountHeader(), balanceSummary(), ClearedIndicator(), reconciliationText(), RegisterIcon(), account, entry, TransactionRow() (+4 more)

### Community 38 - "PayeeEntity"
Cohesion: 0.22
Nodes (9): PayeeEntity, BudgetId, CategoryMode, DefaultCategoryId, Id, IsHidden, Name, NormalizedName (+1 more)

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.18
Nodes (11): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Plan interaction extension (2026-10-22) (+3 more)

### Community 41 - ".OnModelCreating"
Cohesion: 0.06
Nodes (30): AccountEntity, AccountType, BudgetId, Id, IsClosed, IsFavorite, Name, Notes (+22 more)

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

### Community 48 - "LongTermPlanning.test.tsx"
Cohesion: 0.13
Nodes (12): input(), monthName(), amountFromCents(), cents(), LongTermPlanning, PlanningCategory, PlanningCategoryMonth, PlanningChange (+4 more)

### Community 50 - "DateOnly"
Cohesion: 0.18
Nodes (4): AdminUserDto, CashLedger, ScheduledTransactions, MembershipSubscriptionMetadata

### Community 51 - "Guid"
Cohesion: 0.12
Nodes (21): AccountDto, AccountsDto, AuditDto, AuditPageDto, CreateAccountRequest, DeleteTransactionRequest, ReconcileRequest, RegisterDto (+13 more)

### Community 52 - "AccountEmailContent"
Cohesion: 0.12
Nodes (8): AccountEmailContent, AccountEmailTemplates, IAccountEmailSender, SmtpAccountEmailSender, TestAccountEmailSender, FailSending, IsConfigured, Messages

### Community 55 - "Milestone 3 — Goals, templates, and guided assignment"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 3 — Goals, templates, and guided assignment, Product references, User value, Vertical slice

### Community 56 - "LongTermPlanning"
Cohesion: 0.11
Nodes (29): ActivityDialog, ActivityKind, ActivitySelection, actualLabel(), actualMetrics, availableTone(), ConflictState, EditingValue (+21 more)

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

### Community 63 - ".CalculateChanges"
Cohesion: 0.15
Nodes (13): Backdated assignment corrections, `Budgets`, Calculations and month lifecycle, `CategoryGroups` and `Categories`, Current PostgreSQL schema, Data model, Identity and account lifecycle, Long-Term Planning (+5 more)

### Community 64 - "Milestone: Annual Long-Term Budget Planning"
Cohesion: 0.29
Nodes (6): Acceptance Criteria, Budget propagation rule, Core behavior, Goal, Implementation notes, Milestone: Annual Long-Term Budget Planning

### Community 65 - "YNAB Web app: reverse-engineering notes"
Cohesion: 0.29
Nodes (6): Account-linking code observed in the bundle, Evidence and scope, Frontend structure, Lazy modules and browser storage, Limits and follow-up, YNAB Web app: reverse-engineering notes

### Community 66 - "TransactionSelectionApiTests"
Cohesion: 0.18
Nodes (9): BudgetDto, BudgetPlanDto, BudgetPlanUpdateDto, BudgetPlanUpdateResult, BudgetUpdateResult, CategoryDto, CategoryGroupDto, MutationDto (+1 more)

### Community 67 - "Milestone 0 — Local application foundation"
Cohesion: 0.33
Nodes (5): Acceptance criteria, Goal, Milestone 0 — Local application foundation, User and developer outcome, Vertical slice

### Community 68 - "TransactionSelectionBar.test.tsx"
Cohesion: 0.11
Nodes (23): react, defaultFlags, EditFlags(), Flag, FlagButton(), Test(), LedgerDialog(), Category (+15 more)

### Community 71 - "NewCategoryDialog.tsx"
Cohesion: 0.20
Nodes (6): ApiError, NewCategoryDialog(), save(), close, created, show()

### Community 72 - "devDependencies"
Cohesion: 0.11
Nodes (17): devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript (+9 more)

### Community 73 - "BudgetEntity"
Cohesion: 0.20
Nodes (10): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+2 more)

### Community 75 - "OpenBudget.Api"
Cohesion: 0.06
Nodes (12): OpenBudget.Api, OpenBudget.Api.Migrations, OpenBudget.UnitTests, OpenBudget.IntegrationTests, FlagDefinitionEntity, BudgetId, Color, Name (+4 more)

### Community 76 - "PlanActionEntity"
Cohesion: 0.20
Nodes (10): PlanActionEntity, ActorUserId, Amount, BudgetId, CreatedAt, FromCategoryName, Id, Kind (+2 more)

### Community 77 - "BudgetDbContext.cs"
Cohesion: 0.08
Nodes (23): BudgetMemberEntity, BudgetId, InvitedByUserId, JoinedAt, Role, UserId, BudgetPlanEntity, CreatedAt (+15 more)

### Community 78 - "AdminAuditLogEntity"
Cohesion: 0.33
Nodes (6): AdminAuditLogEntity, Action, ActorUserId, CreatedAt, Id, TargetUserId

### Community 79 - "CategoryGroupEntity"
Cohesion: 0.25
Nodes (8): CategoryGroupEntity, BudgetId, BudgetMonth, Categories, Id, IsArchived, Name, SortOrder

### Community 82 - "AuthGate"
Cohesion: 0.29
Nodes (3): AuthBrand(), AuthGate(), Session

### Community 83 - "planHelpers.ts"
Cohesion: 0.36
Nodes (6): moveCategory(), moveGroup(), openUpcoming(), categoryProgress(), categoryTransactionAmount(), moveItem()

### Community 86 - "API"
Cohesion: 0.29
Nodes (5): API, OpenBudget API and local services, BudgetRequestContext, BudgetId, LedgerGate

### Community 87 - "BudgetInvitationEntity"
Cohesion: 0.29
Nodes (7): BudgetInvitationEntity, BudgetId, CreatedAt, Email, Id, InvitedByUserId, NormalizedEmail

### Community 90 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

### Community 97 - "BudgetTogether.tsx"
Cohesion: 0.29
Nodes (4): BudgetTogether(), Invitation, Person, Sharing

### Community 98 - "vitest"
Cohesion: 0.12
Nodes (13): @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, vitest, ApprovalButton(), displayDate(), parseRegisterDate(), repeatOptions (+5 more)

## Knowledge Gaps
- **275 isolated node(s):** `Clock`, `ConnectionString`, `Email`, `Failure`, `EntitlementKey` (+270 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 728 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **22 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Cash ledger slice` connect `InlineTransactionEditor.tsx` to `Architecture and current status`, `NewCategoryDialog.tsx`, `API`, `Payees.test.tsx`, `.UpdateMonthAsync`, `.CalculateChanges`?**
  _High betweenness centrality (0.334) - this node is a cross-community bridge._
- **What connects `Clock`, `ConnectionString`, `Email` to the rest of the system?**
  _275 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `BudgetApplication` be split into smaller, more focused modules?**
  _Cohesion score 0.05656108597285068 - nodes in this community are weakly interconnected._
- **Why does `CategoryPicker()` connect `InlineTransactionEditor.tsx` to `BudgetApplication`, `App.tsx`, `parseMoney`, `AccountRegister.tsx`, `Payees.test.tsx`?**
  _High betweenness centrality (0.156) - this node is a cross-community bridge._
- **Should `App.tsx` be split into smaller, more focused modules?**
  _Cohesion score 0.09032258064516129 - nodes in this community are weakly interconnected._
- **Why does `BudgetService` connect `.UpdateMonthAsync` to `TransactionSelectionApiTests`, `OpenBudget.Api`, `RegisterViewDto`, `CancellationToken`, `.GetPlanAsync`, `API`, `InlineTransactionEditor.tsx`?**
  _High betweenness centrality (0.138) - this node is a cross-community bridge._
- **Should `CategoryEntity` be split into smaller, more focused modules?**
  _Cohesion score 0.125 - nodes in this community are weakly interconnected._