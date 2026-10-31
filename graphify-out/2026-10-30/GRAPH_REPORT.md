# Graph Report - ynab-clone  (2026-10-30)

## Corpus Check
- 316 files · ~281,531 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 18 file(s) not represented in the graph (top: .css 9, .ttf 4, (none) 3)

## Summary
- 3478 nodes · 11278 edges · 144 communities (122 shown, 22 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 1112 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `704ecbad`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- BudgetApplication
- DateTimeOffset
- CategoryTargetService
- CategoryTarget.tsx
- requestJson
- http
- compilerOptions
- http
- List
- OpenBudget.IntegrationTests.csproj
- Fact
- MembershipEntitlements.cs
- PostgresFixture.cs
- IResult
- TransactionEntity
- CancellationToken
- Authentication.cs
- RegisterViewEntity
- WishlistRulesTests
- vitest
- WishlistService
- routes.ts
- API reference
- Wishlist slice
- BudgetDbContext
- TestApp
- CreditPayoffTarget.tsx
- Task
- BudgetChangeInterceptor
- Milestone 2a — Multi-User Authentication & Administration
- AccountsPage
- Testing and acceptance coverage
- Agent Instructions
- JsonElement
- docs/README.md
- TestApp
- AgentTokenEntity
- Milestone 1 — Reliable budget core and month lifecycle
- Milestone 2 — Accounts and transaction register
- WishlistPage.tsx
- LongTermPlanning.tsx
- Milestone 7 — File import, matching, and portable data
- MigrationBuilder
- system_text_json
- Milestone 4 — Scheduled transactions and cash-flow awareness
- RenameFlagsRequest
- package.json
- microsoft_entityframeworkcore
- .GetYearAsync
- WishlistItemEntity
- SafeTransactionImageUrlFetcher
- .Calculate
- LoanAccountPage.tsx
- WishlistApiTests
- AppRouting.test.tsx
- Milestone 5 — Credit cards and debt payoff planning
- Milestone 6 — Focused plan views and reports
- Milestone 8 — Identity, multiple budgets, and household sharing
- parseMoney
- Milestone 9 — Cross-device experience and offline resilience
- Milestone 10 — Optional linked-account import
- IReadOnlyDictionary
- Milestone: Annual Long-Term Budget Planning
- YNAB Web app: reverse-engineering notes
- .Project
- Milestone 0 — Local application foundation
- WishlistApp.test.tsx
- McpToolDefinition
- DateOnly
- .Validate
- BudgetCarryForwardTests
- OpenBudget product roadmap
- ScheduleEntity
- Payees.tsx
- Documentation
- .GetManagedPayeesAsync
- .Calculate
- vitest
- LongTermPlanningDto
- Milestone 13 — Wishlist
- IReadOnlyDictionary
- .HandleAuthenticateAsync
- Milestone 12 — Icons and suggested icons
- CategoryEntity
- IReadOnlyList
- AccountEmailContent
- McpToolCatalog
- AccountEmailSettingsService
- .CallToolAsync
- OpenBudget.Api
- CategoryTargetEntity
- LedgerContracts.cs
- ModelBuilder
- .MapAuthenticationEndpoints
- Task
- ScheduledTransactions
- McpToolContext
- Current PostgreSQL schema
- LoanTermsEntity
- WishlistPurchaseApiTests
- .SaveChangesAsync
- TransactionImageApiTests
- .ToDtoAsync
- RegisterViewDto
- liveUpdates.test.tsx
- BudgetEntity
- AgentTokenAuthenticationHandler
- MCP server for personal AI agents
- TransactionImageService
- BudgetMemberEntity
- .IsBlocked
- TransactionImageEntity
- LedgerCommandEntity
- AuthGate
- .UpdateMonthAsync
- CategoryDto
- BudgetPlanEntity
- BudgetInvitationEntity
- SmtpSettingsEntity
- WishlistSettingsEntity
- App.test.tsx
- AdminPanel
- useAppRouter.ts
- EntitlementSettings.tsx
- AccountEntity
- TransactionImagesDialog
- LedgerRulesTests
- Theory
- AdminPanel.tsx
- Developer setup
- .Validate
- UserMembershipEntity
- WishlistContracts.cs
- .Validate
- AgentTokensAndAudit
- Milestone 3 — Goals, templates, and guided assignment
- WebApplication
- URL routing
- .MapEndpoint
- LedgerBalanceRow

## God Nodes (most connected - your core abstractions)
1. `OpenBudget.Api` - 119 edges
2. `BudgetDbContext` - 115 edges
3. `MutationDto` - 103 edges
4. `BudgetApplication()` - 101 edges
5. `LedgerService` - 96 edges
6. `OpenBudget.Api.Migrations` - 71 edges
7. `LedgerApiTests` - 69 edges
8. `BudgetDto` - 61 edges
9. `LedgerException` - 58 edges
10. `TransactionEntity` - 55 edges

## Surprising Connections (you probably didn't know these)
- `Target progress` --references--> `CategoryDto`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/BudgetContracts.cs
- `Importing an accepted normalized row` --references--> `TransactionRequest`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/LedgerContracts.cs
- `Enable/disable semantics` --references--> `Wishlist`  [INFERRED]
  docs/milestones/milestone-13-wishlist.md → src/OpenBudget.Api/Migrations/20261030124559_Wishlist.Designer.cs
- `Entitlement and enablement` --references--> `Wishlist`  [INFERRED]
  docs/milestones/milestone-13-wishlist.md → src/OpenBudget.Api/Migrations/20261030124559_Wishlist.Designer.cs
- `Proposed entities` --references--> `UserEntitlementOverrides()`  [INFERRED]
  docs/milestones/milestone-02a-multiuser-auth-and-admin.md → src/OpenBudget.UI/src/admin/EntitlementSettings.tsx

## Import Cycles
- None detected.

## Communities (144 total, 22 thin omitted)

### Community 0 - "BudgetApplication"
Cohesion: 0.03
Nodes (70): accountSectionId(), accountSections, BudgetApplication(), addFunds(), applyAssignmentChanges(), applyAutoAssignOption(), applyMove(), applyRoute() (+62 more)

### Community 1 - "DateTimeOffset"
Cohesion: 0.05
Nodes (23): Owned budgets and memberships, AdminAuditDto, AdminDashboardDto, AdminInviteUserRequest, AdminMembershipCountDto, AdminMembershipRequest, AdminRegistrationDto, AdminUserDto (+15 more)

### Community 2 - "CategoryTargetService"
Cohesion: 0.28
Nodes (4): CategoryTargetCommandResult, CategoryTargetResultDto, DeleteCategoryTargetRequest, CategoryTargetService

### Community 3 - "CategoryTarget.tsx"
Cohesion: 0.08
Nodes (35): BehaviorPicker(), choose(), keys(), CategoryTargetCard(), CategoryTargetEditor(), submit(), describe(), Money (+27 more)

### Community 4 - "requestJson"
Cohesion: 0.10
Nodes (19): requestJson(), ManagePayees(), close(), done(), edit(), rule(), update(), PayeeList (+11 more)

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
Cohesion: 0.12
Nodes (18): FlagDto, FlagPaletteDto, RenameFlagsRequest, SetFlagRequest, ContractDto, ContractScheduleItemDto, LoanActivityItemDto, LoanActivityMonthDto (+10 more)

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.07
Nodes (25): Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), ModelContextProtocol.AspNetCore (2.2.0), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3) (+17 more)

### Community 12 - "Fact"
Cohesion: 0.17
Nodes (11): Milestone 2 acceptance map, BudgetDto, MutationDto, ReconcileRequest, ReviewTransactionRequest, StatusRequest, UpdateAccountRequest, SavePayeesRequest (+3 more)

### Community 13 - "MembershipEntitlements.cs"
Cohesion: 0.14
Nodes (7): `BudgetPlans`, EntitlementAuthorizationHandler, EntitlementEndpointExtensions, EntitlementRequirement, EntitlementService, MembershipEntitlementDto, SaveMembershipEntitlementsRequest

### Community 15 - "IResult"
Cohesion: 0.17
Nodes (6): BudgetLimitDto, BudgetOwnershipService, DeleteOwnedBudgetRequest, RenameOwnedBudgetRequest, BudgetRequestContext, BudgetId

### Community 16 - "TransactionEntity"
Cohesion: 0.08
Nodes (20): TransactionEntity, Account, AccountId, Amount, BudgetId, CategoryId, Date, FlagColor (+12 more)

### Community 17 - "CancellationToken"
Cohesion: 0.13
Nodes (6): LedgerService, LedgerException, StatusCode, Change, Entries, CreatedPairCategory

### Community 18 - "Authentication.cs"
Cohesion: 0.13
Nodes (17): Milestone 2a final verification (2026-10-23), ChangePasswordRequest, LoginRequest, OpenBudgetUser, CreatedAt, IsBootstrapAdministrator, IsSuspended, LastLoginAt (+9 more)

### Community 19 - "RegisterViewEntity"
Cohesion: 0.15
Nodes (13): RegisterViewEntity, BudgetId, ColumnWidthsJson, DatePreset, FromMonth, Scope, ShowFlags, ShowImages (+5 more)

### Community 21 - "vitest"
Cohesion: 0.05
Nodes (35): @testing-library/react, @testing-library/user-event, vitest, plans, Activity, AgentToken, AgentTokens(), BudgetOption (+27 more)

### Community 23 - "routes.ts"
Cohesion: 0.11
Nodes (36): accountSlugFor(), accountSlugs(), AppPage, AppRoute, budgetIdFromLocation(), budgetKeyFor(), BudgetRoute, BudgetSlugAliases (+28 more)

### Community 24 - "API reference"
Cohesion: 0.07
Nodes (27): API reference, Approval and attention, Assignment rollover and earlier-month edits, Authentication, Boundaries, Budget collaboration and Plan history, Cash accounts and reconciliation, Credit accounts and card payments (+19 more)

### Community 25 - "Wishlist slice"
Cohesion: 0.23
Nodes (6): Wishlist slice, Milestone 13 Wishlist acceptance map (2026-10-30), Wishlist purchase transactions, WishlistModel, WishlistPurchaseRules, WishlistPurchaseRulesTests

### Community 26 - "BudgetDbContext"
Cohesion: 0.03
Nodes (57): MCP server and live updates, ApplicationSettingEntity, Key, Value, BudgetDbContext, Accounts, ActiveBudgetId, AdminAuditLogs (+49 more)

### Community 27 - "TestApp"
Cohesion: 0.13
Nodes (13): Agent access (MCP) and live updates acceptance map, CreateOwnedBudgetRequest, OwnedBudgetDto, CreateAgentTokenRequest, Program, BudgetOwnershipApiTests, McpServerApiTests, McpTestKit (+5 more)

### Community 28 - "CreditPayoffTarget.tsx"
Cohesion: 0.11
Nodes (20): CreditPaymentDetails(), mutate(), card, progress, renderPanel(), CreditPayoff, currentMonth(), kindLabels (+12 more)

### Community 30 - "BudgetChangeInterceptor"
Cohesion: 0.07
Nodes (12): BudgetChangeEvent, BudgetChangeInterceptor, BudgetChangePublisher, Subscription, BudgetId, Channel, Id, UserId (+4 more)

### Community 31 - "Milestone 2a — Multi-User Authentication & Administration"
Cohesion: 0.09
Nodes (21): 10. Definition of Done, 1. Authentication & Login, 2. Permanent Administrator Accounts, 3. Registration & User Lifecycle, 4.1 Dashboard, 4.2 User Management, 4.3 Application Settings, 4. Separate Administration Panel (+13 more)

### Community 32 - "AccountsPage"
Cohesion: 0.14
Nodes (16): AccountsPage(), applyView(), beginColumnResize(), clearFilters(), finishColumnResize(), moveColumnResize(), mutate(), persistColumnWidths() (+8 more)

### Community 33 - "Testing and acceptance coverage"
Cohesion: 0.12
Nodes (16): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 verification (2026-10-22) (+8 more)

### Community 34 - "Agent Instructions"
Cohesion: 0.14
Nodes (13): Agent Instructions, Before making changes, Coding Guidelines, Delegating requests to worktrees and subagents, Development Principles, Documentation, Git, Project - OpenBudget (+5 more)

### Community 35 - "JsonElement"
Cohesion: 0.23
Nodes (3): McpResultExtensions, McpTestClient, Http

### Community 36 - "docs/README.md"
Cohesion: 0.20
Nodes (5): Coding conventions, Documentation, Highlights, Project status, Quick start

### Community 38 - "AgentTokenEntity"
Cohesion: 0.10
Nodes (24): AdminAuditLogEntity, Action, ActorUserId, CreatedAt, Id, TargetUserId, AgentTokenEntity, Budgets (+16 more)

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.18
Nodes (11): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Plan interaction extension (2026-10-22) (+3 more)

### Community 41 - "WishlistPage.tsx"
Cohesion: 0.06
Nodes (57): BucketPanel(), DragSource, DropTarget, History(), Money, Props, BucketEditing, BucketNameInput() (+49 more)

### Community 42 - "LongTermPlanning.tsx"
Cohesion: 0.06
Nodes (51): openUpcoming(), DisclosureChevron(), Split, ActivityDialog, ActivityKind, ActivitySelection, actualLabel(), actualMetrics (+43 more)

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.22
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

### Community 44 - "MigrationBuilder"
Cohesion: 0.04
Nodes (20): InitialCreate, BudgetCore, NamedBudget, AccountRegisterPresentation, AccountViewOptions, AccountNotes, PayeeManagement, CategoryMoves (+12 more)

### Community 45 - "system_text_json"
Cohesion: 0.07
Nodes (6): OpenBudget.Api.Realtime, OpenBudget.Api.Mcp, McpServerText, AgentTokenClaims, Route, BudgetChangeKinds

### Community 46 - "Milestone 4 — Scheduled transactions and cash-flow awareness"
Cohesion: 0.29
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 4 — Scheduled transactions and cash-flow awareness, Product references, User value, Vertical slice

### Community 47 - "RenameFlagsRequest"
Cohesion: 0.29
Nodes (7): 1. Add the GitHub workflows, 2. Configure Coolify, 3. Create the Coolify webhook, 4. Add GitHub secrets, 5. Publish a release, GitHub Release to Coolify, Useful checks

### Community 48 - "package.json"
Cohesion: 0.05
Nodes (32): jsdom, @testing-library/jest-dom, @types/react, @types/react-dom, typescript, vite, @vitejs/plugin-react, dependencies (+24 more)

### Community 50 - ".GetYearAsync"
Cohesion: 0.10
Nodes (16): LongTermPlanningCategoryEntity, BudgetId, CategoryId, Month, PlannedBudgetOverride, PlannedExpenses, LongTermPlanningCategoryDto, LongTermPlanningCategoryMonthDto (+8 more)

### Community 51 - "WishlistItemEntity"
Cohesion: 0.13
Nodes (15): WishlistItemEntity, BucketId, BudgetId, Cost, CreatedAt, Description, Id, Name (+7 more)

### Community 52 - "SafeTransactionImageUrlFetcher"
Cohesion: 0.13
Nodes (11): ImageFetchException, ITransactionImageUrlFetcher, SafeTransactionImageUrlFetcher, TransactionImageUrlFetcherOptions, AllowNonDefaultPort, MaxRedirects, Timeout, StubFetcher (+3 more)

### Community 53 - ".Calculate"
Cohesion: 0.14
Nodes (7): CategoryTargetDefinition, Context, MonthEnd, MonthIndex, StartIndex, CategoryTargetRulesTests, InvalidRequests

### Community 54 - "LoanAccountPage.tsx"
Cohesion: 0.07
Nodes (46): AccountType, formatMoney(), ActivityList(), CategoryActions, Chevron(), ContractSchedule(), ContractSummary(), LoanAccountPage() (+38 more)

### Community 55 - "WishlistApiTests"
Cohesion: 0.41
Nodes (5): WishlistBucketRequest, WishlistOverviewDto, WishlistVersionRequest, OwnedBudgetListDto, WishlistApiTests

### Community 56 - "AppRouting.test.tsx"
Cohesion: 0.11
Nodes (6): Category, emitBudgetChanged(), FakeEventSource, initialBudgets, legacyKey, path()

### Community 57 - "Milestone 5 — Credit cards and debt payoff planning"
Cohesion: 0.29
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 5 — Credit cards and debt payoff planning, Product references, User value, Vertical slice

### Community 58 - "Milestone 6 — Focused plan views and reports"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 6 — Focused plan views and reports, Product references, User value, Vertical slice

### Community 59 - "Milestone 8 — Identity, multiple budgets, and household sharing"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 8 — Identity, multiple budgets, and household sharing, Product reference, User value, Vertical slice

### Community 60 - "parseMoney"
Cohesion: 0.07
Nodes (53): Mortgages, loans and contracts, Current feature inventory, Loan accounts, creditPaymentGroupId, LoanAccountType, loanAccountTypes, parseMoney(), EditAccountForm() (+45 more)

### Community 61 - "Milestone 9 — Cross-device experience and offline resilience"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 9 — Cross-device experience and offline resilience, Product reference, User value, Vertical slice

### Community 62 - "Milestone 10 — Optional linked-account import"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies and release gate, Goal, Milestone 10 — Optional linked-account import, Product references, User value, Vertical slice

### Community 63 - "IReadOnlyDictionary"
Cohesion: 0.11
Nodes (9): 5. Security model (ISO 27001 mapping), Residual risks, ApiDispatcher, ApiResponse, Error, IsSuccess, BodyDetection, CanHaveBody (+1 more)

### Community 64 - "Milestone: Annual Long-Term Budget Planning"
Cohesion: 0.29
Nodes (6): Acceptance Criteria, Budget propagation rule, Core behavior, Goal, Implementation notes, Milestone: Annual Long-Term Budget Planning

### Community 65 - "YNAB Web app: reverse-engineering notes"
Cohesion: 0.29
Nodes (6): Account-linking code observed in the bundle, Evidence and scope, Frontend structure, Lazy modules and browser storage, Limits and follow-up, YNAB Web app: reverse-engineering notes

### Community 66 - ".Project"
Cohesion: 0.08
Nodes (7): ContractTermsRequest, Flows, LoanRules, Pairing, Projection, NeverPaidOff, LoanRulesTests

### Community 67 - "Milestone 0 — Local application foundation"
Cohesion: 0.33
Nodes (5): Acceptance criteria, Goal, Milestone 0 — Local application foundation, User and developer outcome, Vertical slice

### Community 68 - "WishlistApp.test.tsx"
Cohesion: 0.18
Nodes (7): Access, checking, json(), month(), progress, purchaseEntry, withAccounts()

### Community 71 - "McpToolDefinition"
Cohesion: 0.18
Nodes (7): AgentPermission, Admin, Budget, Reporting, McpPermissions, McpToolDefinition, McpPermissionMatrixTests

### Community 72 - "DateOnly"
Cohesion: 0.14
Nodes (8): CategoryTargetProgressDto, CategoryTargetRules, ContractItem, Total, ContractPosition, Remaining, MembershipSubscriptionMetadata, LoanAccounts

### Community 73 - ".Validate"
Cohesion: 0.21
Nodes (3): ImageFileNameSanitizer, TestImages, TransactionImageValidationTests

### Community 74 - "BudgetCarryForwardTests"
Cohesion: 0.17
Nodes (11): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+3 more)

### Community 75 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

### Community 76 - "ScheduleEntity"
Cohesion: 0.05
Nodes (46): AccountDto, SplitDto, TransactionDto, TransactionRequest, SplitEntity, Amount, CategoryId, Memo (+38 more)

### Community 77 - "Payees.tsx"
Cohesion: 0.06
Nodes (55): Cash ledger slice, react-dom, AccountForm(), editor(), TransferForm(), CategoryOption, isCreditAccount(), isLoanAccount() (+47 more)

### Community 78 - "Documentation"
Cohesion: 0.67
Nodes (3): Current release shape, Documentation, Documents

### Community 79 - ".GetManagedPayeesAsync"
Cohesion: 0.36
Nodes (5): PayeeDto, PayeeRuleDto, PayeeRules, PayeesDto, PayeeUpdateDto

### Community 81 - "vitest"
Cohesion: 0.06
Nodes (57): react, AccountHeader(), balanceSummary(), ClearedIndicator(), ClearedSymbol(), reconciliationText(), RegisterIcon(), account (+49 more)

### Community 82 - "LongTermPlanningDto"
Cohesion: 0.34
Nodes (5): Milestone 11 acceptance map, LongTermPlanningDto, LongTermPlanningApiTests, PlanningConflictBody, ImageResponseExtensions

### Community 83 - "Milestone 13 — Wishlist"
Cohesion: 0.11
Nodes (19): Category targets, Target progress, Wishlist, API, OpenBudget API and local services, Implemented: category targets (2026-10-30), Acceptance criteria, Buckets and Plan integration (+11 more)

### Community 84 - "IReadOnlyDictionary"
Cohesion: 0.18
Nodes (9): Backdated assignment corrections, Category targets, Credit accounts, Payee management, Transaction images, Wishlist, BudgetCarryForward, Changes (+1 more)

### Community 85 - ".HandleAuthenticateAsync"
Cohesion: 0.22
Nodes (3): AgentTokenSecrets, GeneratedToken, AgentTokenSecretsTests

### Community 86 - "Milestone 12 — Icons and suggested icons"
Cohesion: 0.22
Nodes (9): Acceptance criteria, Dependencies, Goal, Milestone 12 — Icons and suggested icons, Open questions, Product references, User value, Vertical slice (+1 more)

### Community 87 - "CategoryEntity"
Cohesion: 0.08
Nodes (25): CategoryEntity, Activity, Assigned, Available, BudgetId, BudgetMonth, CreditOverspending, GroupId (+17 more)

### Community 88 - "IReadOnlyList"
Cohesion: 0.54
Nodes (3): PlanActionDraft, PlanActionRules, PlanActionRulesTests

### Community 90 - "McpToolCatalog"
Cohesion: 0.16
Nodes (9): S, McpToolCatalog, Effect, Destructive, Read, Write, All, CommandIdArg (+1 more)

### Community 91 - "AccountEmailSettingsService"
Cohesion: 0.12
Nodes (10): AccountEmailSettingsService, IAccountEmailSender, SmtpAccountEmailSender, SmtpConnectionSettings, SmtpSettingsDto, UpdateSmtpSettingsRequest, TestAccountEmailSender, FailSending (+2 more)

### Community 92 - ".CallToolAsync"
Cohesion: 0.12
Nodes (4): McpToolHandlers, AgentTokenIdentity, SchedulePostingService, SchedulePostingWorker

### Community 93 - "OpenBudget.Api"
Cohesion: 0.08
Nodes (6): OpenBudget.Tests, OpenBudget.Api, OpenBudget.UnitTests, SendTestEmailRequest, RecurrenceTests, RegisterViewTests

### Community 94 - "CategoryTargetEntity"
Cohesion: 0.12
Nodes (16): CategoryTargetEntity, Amount, Behavior, BudgetId, CategoryId, CreatedAt, DayOfMonth, DueDate (+8 more)

### Community 96 - "ModelBuilder"
Cohesion: 0.04
Nodes (53): BudgetPlanEntity, CreatedAt, Currency, Id, IsArchived, Months, Name, OwnerUserId (+45 more)

### Community 97 - ".MapAuthenticationEndpoints"
Cohesion: 0.22
Nodes (3): AuthenticationSetup, TransactionImageEndpoints, TransactionImageUpload

### Community 98 - "Task"
Cohesion: 0.22
Nodes (12): BudgetUpdateDto, CategoryGroupUpdateDto, CategoryUpdateDto, AuditPageDto, CreateAccountRequest, CreditPayoffDto, CreditPayoffProgressDto, CreditPayoffTargetDto (+4 more)

### Community 99 - "ScheduledTransactions"
Cohesion: 0.14
Nodes (5): Limitations and direction, Scheduling and flag storage, ScheduledTransactions, TransactionFlags, ScheduledApproval

### Community 100 - "McpToolContext"
Cohesion: 0.18
Nodes (5): McpToolContext, Arguments, BudgetId, CancellationToken, Token

### Community 101 - "Current PostgreSQL schema"
Cohesion: 0.20
Nodes (9): `Budgets`, Calculations and month lifecycle, `CategoryGroups` and `Categories`, Current PostgreSQL schema, Data model, Identity and account lifecycle, Ledger tables, Long-Term Planning (+1 more)

### Community 102 - "LoanTermsEntity"
Cohesion: 0.09
Nodes (22): LoanTermsEntity, AccountId, BalanceConfirmedAt, BudgetId, Contract, ContractMonths, ContractStartDate, CreatedAt (+14 more)

### Community 103 - "WishlistPurchaseApiTests"
Cohesion: 0.34
Nodes (3): DeleteTransactionRequest, WishlistPurchaseApiTests, WriteSnapshot

### Community 104 - ".SaveChangesAsync"
Cohesion: 0.27
Nodes (6): BudgetInvitationDto, BudgetPersonDto, BudgetSharingDto, BudgetSharingService, InviteBudgetMemberRequest, PlanActionDto

### Community 105 - "TransactionImageApiTests"
Cohesion: 0.41
Nodes (6): AccountsDto, RegisterDto, DeleteTransactionImageRequest, ImportTransactionImageUrlRequest, TransactionImageListDto, TransactionImageApiTests

### Community 106 - ".ToDtoAsync"
Cohesion: 0.15
Nodes (7): Architecture and current status, Category targets, Design limits to address, Implemented user path, Runtime topology, Technologies, LongTermPlanningPlanValues

### Community 108 - "liveUpdates.test.tsx"
Cohesion: 0.30
Nodes (6): BudgetChange, LiveUpdateOptions, reconnectDelay(), FakeEventSource, setup(), useBudgetLiveUpdates()

### Community 109 - "BudgetEntity"
Cohesion: 0.16
Nodes (9): WishlistBucketEntity, BudgetId, CreatedAt, Id, IsDeleted, Name, SortOrder, UpdatedAt (+1 more)

### Community 111 - "MCP server for personal AI agents"
Cohesion: 0.20
Nodes (10): Agent tokens, MCP and live events, 1. Create an agent token, 2. Connect a client, 3. Using the tools, 4. Permission matrix, 6. Real-time updates, 7. Limitations, MCP server for personal AI agents (+2 more)

### Community 112 - "TransactionImageService"
Cohesion: 0.13
Nodes (10): Storage, AuditDto, CreditSummaryDto, DeleteCreditPayoffTargetRequest, SaveCreditPayoffTargetRequest, TransactionImageDto, TransactionImageService, ITransactionImageStore (+2 more)

### Community 113 - "BudgetMemberEntity"
Cohesion: 0.33
Nodes (6): BudgetMemberEntity, BudgetId, InvitedByUserId, JoinedAt, Role, UserId

### Community 115 - "TransactionImageEntity"
Cohesion: 0.14
Nodes (14): TransactionImageEntity, BudgetId, ContentType, CreatedAt, CreatedByUserId, FileName, Height, Id (+6 more)

### Community 116 - "LedgerCommandEntity"
Cohesion: 0.20
Nodes (10): LedgerCommandEntity, AfterJson, BeforeJson, BudgetId, CreatedAt, Fingerprint, Id, Kind (+2 more)

### Community 117 - "AuthGate"
Cohesion: 0.24
Nodes (9): AuthBrand(), AuthGate(), login(), refreshSession(), Session, isSafeReturnPath(), publicPaths, rememberReturnPath() (+1 more)

### Community 118 - ".UpdateMonthAsync"
Cohesion: 0.13
Nodes (12): BudgetUpdateResult, PlanActionEntity, ActorUserId, Amount, BudgetId, CreatedAt, FromCategoryName, Id (+4 more)

### Community 119 - "CategoryDto"
Cohesion: 0.25
Nodes (6): BudgetPlanDto, BudgetPlanUpdateDto, BudgetPlanUpdateResult, CategoryDto, CategoryGroupDto, CategoryTargetDto

### Community 120 - "BudgetPlanEntity"
Cohesion: 0.14
Nodes (14): AgentAccessModel, AgentAuditLogEntity, BudgetId, CreatedAt, Id, Outcome, StatusCode, Success (+6 more)

### Community 121 - "BudgetInvitationEntity"
Cohesion: 0.29
Nodes (7): BudgetInvitationEntity, BudgetId, CreatedAt, Email, Id, InvitedByUserId, NormalizedEmail

### Community 122 - "SmtpSettingsEntity"
Cohesion: 0.22
Nodes (9): SmtpSettingsEntity, EnableSsl, EncryptedPassword, From, Host, Id, Port, UpdatedAt (+1 more)

### Community 123 - "WishlistSettingsEntity"
Cohesion: 0.29
Nodes (7): WishlistSettingsEntity, BudgetId, IsEnabled, UpdatedAt, Version, WishlistState, ActiveBuckets

### Community 124 - "App.test.tsx"
Cohesion: 0.14
Nodes (8): App(), RoutedPage(), Month, next(), saved(), weeklyProgress, weeklyTarget, isAdminPath()

### Community 125 - "AdminPanel"
Cohesion: 0.33
Nodes (8): AdminPanel(), action(), inviteUser(), saveEmailSettings(), saveMembership(), saveMode(), UserEntitlementOverrides(), load()

### Community 126 - "useAppRouter.ts"
Cohesion: 0.22
Nodes (10): listeners, longTermYearStore, useRoutedLongTermYear(), routeNotices, RouteTarget, titleForState(), AppRouterOptions, currentPath() (+2 more)

### Community 127 - "EntitlementSettings.tsx"
Cohesion: 0.28
Nodes (7): Entitlement, EntitlementSettings(), toggle(), featureLabel(), featureLabels, Plan, useEntitlementKeys()

### Community 128 - "AccountEntity"
Cohesion: 0.10
Nodes (12): CreditCardRules, Flows, AccountEntity, AccountType, BudgetId, Id, IsClosed, IsFavorite (+4 more)

### Community 129 - "TransactionImagesDialog"
Cohesion: 0.29
Nodes (11): Security model, Transaction image attachments, TransactionImagesDialog(), addFiles(), commandFor(), drop(), go(), importUrl() (+3 more)

### Community 132 - "AdminPanel.tsx"
Cohesion: 0.22
Nodes (7): AdminAudit, AdminSummary, AdminUser, EmailSettings, MembershipDraft, MembershipPlan, modes

### Community 133 - "Developer setup"
Cohesion: 0.22
Nodes (9): Build commands, Current development boundaries, Database lifecycle, Developer setup, First run, GitHub pull requests and releases, Prerequisites, Request flow (+1 more)

### Community 134 - ".Validate"
Cohesion: 0.21
Nodes (3): ImageMetadataStripper, ImageValidationException, TransactionImageValidator

### Community 135 - "UserMembershipEntity"
Cohesion: 0.22
Nodes (9): UserMembershipEntity, BillingProvider, BudgetLimitOverride, ExpiresOn, ExternalSubscriptionId, MembershipPlanId, StartsOn, UserId (+1 more)

### Community 136 - "WishlistContracts.cs"
Cohesion: 0.39
Nodes (6): WishlistBucketDto, WishlistBucketOrderRequest, WishlistItemDto, WishlistItemOrderRequest, WishlistProgressDto, WishlistPurchaseDto

### Community 139 - "Milestone 3 — Goals, templates, and guided assignment"
Cohesion: 0.29
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 3 — Goals, templates, and guided assignment, Product references, User value, Vertical slice

### Community 141 - "URL routing"
Cohesion: 0.40
Nodes (5): URL routing, URL routing (2026-10-30), budget(), account(), slugify()

### Community 143 - "LedgerBalanceRow"
Cohesion: 0.67
Nodes (3): LedgerBalanceRow, Balance, Id

## Knowledge Gaps
- **429 isolated node(s):** `IsSuspended`, `IsBootstrapAdministrator`, `RequiresApproval`, `CreatedAt`, `LastLoginAt` (+424 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1067 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **22 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Wishlist slice` connect `Wishlist slice` to `AccountsPage`, `DateTimeOffset`, `.Project`, `WishlistPage.tsx`, `.ToDtoAsync`, `BudgetEntity`, `Payees.tsx`, `CancellationToken`, `Milestone 13 — Wishlist`, `.UpdateMonthAsync`, `WishlistService`, `LoanAccountPage.tsx`, `parseMoney`?**
  _High betweenness centrality (0.175) - this node is a cross-community bridge._
- **What connects `IsSuspended`, `IsBootstrapAdministrator`, `RequiresApproval` to the rest of the system?**
  _429 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `BudgetApplication` be split into smaller, more focused modules?**
  _Cohesion score 0.03252470799640611 - nodes in this community are weakly interconnected._
- **Why does `BudgetDbContext` connect `BudgetDbContext` to `AccountEntity`, `CategoryTargetService`, `UserMembershipEntity`, `MembershipEntitlements.cs`, `IResult`, `TransactionEntity`, `CancellationToken`, `Authentication.cs`, `RegisterViewEntity`, `WishlistService`, `TestApp`, `AgentTokenEntity`, `.GetYearAsync`, `WishlistItemEntity`, `WishlistApiTests`, `BudgetCarryForwardTests`, `ScheduleEntity`, `Milestone 13 — Wishlist`, `IReadOnlyDictionary`, `CategoryEntity`, `AccountEmailSettingsService`, `.CallToolAsync`, `OpenBudget.Api`, `CategoryTargetEntity`, `ModelBuilder`, `.MapAuthenticationEndpoints`, `LoanTermsEntity`, `WishlistPurchaseApiTests`, `.SaveChangesAsync`, `.ToDtoAsync`, `RegisterViewDto`, `BudgetEntity`, `AgentTokenAuthenticationHandler`, `TransactionImageService`, `BudgetMemberEntity`, `TransactionImageEntity`, `LedgerCommandEntity`, `.UpdateMonthAsync`, `BudgetInvitationEntity`, `SmtpSettingsEntity`, `WishlistSettingsEntity`?**
  _High betweenness centrality (0.125) - this node is a cross-community bridge._
- **Should `DateTimeOffset` be split into smaller, more focused modules?**
  _Cohesion score 0.047519217330538085 - nodes in this community are weakly interconnected._
- **Why does `MCP server and live updates` connect `BudgetDbContext` to `.ToDtoAsync`, `liveUpdates.test.tsx`, `AgentTokenAuthenticationHandler`, `Milestone 13 — Wishlist`, `McpToolCatalog`, `.CallToolAsync`, `BudgetChangeInterceptor`, `IReadOnlyDictionary`?**
  _High betweenness centrality (0.082) - this node is a cross-community bridge._
- **Should `CategoryTarget.tsx` be split into smaller, more focused modules?**
  _Cohesion score 0.07770582793709528 - nodes in this community are weakly interconnected._