# Graph Report - ynab-clone  (2026-10-30)

## Corpus Check
- 313 files · ~272,815 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 18 file(s) not represented in the graph (top: .css 9, .ttf 4, (none) 3)

## Summary
- 3491 nodes · 11045 edges · 143 communities (114 shown, 29 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 1063 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `80c75688`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- BudgetApplication
- DateTimeOffset
- CategoryTargetService
- CategoryTarget.tsx
- api.ts
- http
- compilerOptions
- http
- AccountEmail.cs
- OpenBudget.IntegrationTests.csproj
- Fact
- MembershipEntitlements.cs
- PostgresFixture.cs
- Task
- BudgetUpdateDto
- CancellationToken
- Authentication.cs
- RegisterViewEntity
- TransactionEntity
- WishlistRulesTests
- IReadOnlyDictionary
- routes.ts
- API reference
- WishlistItemEntity
- BudgetDbContext
- Milestone 2a — Multi-User Authentication & Administration
- CreditPayoffTarget.tsx
- Task
- BudgetChangeInterceptor
- MembershipEntitlements.cs
- BudgetContracts.cs
- Testing and acceptance coverage
- Agent Instructions
- DateOnly
- docs/README.md
- TestApp
- AgentTokenAuthenticationHandler
- Milestone 1 — Reliable budget core and month lifecycle
- Milestone 2 — Accounts and transaction register
- WishlistPage.tsx
- LongTermPlanning
- Milestone 7 — File import, matching, and portable data
- MigrationBuilder
- system_text_json
- Milestone 4 — Scheduled transactions and cash-flow awareness
- RenameFlagsRequest
- package.json
- microsoft_entityframeworkcore
- .GetYearAsync
- WishlistService
- SafeTransactionImageUrlFetcher
- .Calculate
- LoanAccountPage.tsx
- .ScheduleActionAsync
- LongTermPlanning.tsx
- Milestone 5 — Credit cards and debt payoff planning
- Milestone 6 — Focused plan views and reports
- Milestone 8 — Identity, multiple budgets, and household sharing
- parseMoney
- Milestone 9 — Cross-device experience and offline resilience
- Milestone 10 — Optional linked-account import
- IReadOnlyDictionary
- Milestone: Annual Long-Term Budget Planning
- YNAB Web app: reverse-engineering notes
- LoanApiTests
- Milestone 0 — Local application foundation
- react
- WishlistApiTests
- CategoryTargetRules
- .Validate
- BudgetCarryForwardTests
- OpenBudget product roadmap
- ScheduleEntity
- Payees.tsx
- Documentation
- List
- .Calculate
- vitest
- McpToolDefinition
- LedgerGate
- PlanActionEntity
- TransactionImageValidationTests
- Milestone 12 — Icons and suggested icons
- CategoryEntity
- .UpdateMonthAsync
- AccountsPage
- JsonObject
- AccountEmailSettingsService
- .CallToolAsync
- OpenBudget.Api
- AgentTokenEntity
- LedgerContracts.cs
- .Configure
- .IsBlocked
- Task
- ScheduledTransactions
- McpToolContext
- WishlistBucketEntity
- LoanTermsEntity
- WishlistPurchaseApiTests
- JsonElement
- TransactionImageApiTests
- CategoryTargetEntity
- WishlistRulesTests
- JsonObject
- Current PostgreSQL schema
- JsonElement
- TransactionImageService
- ImageMetadataStripper
- TransactionImagesDialog
- TransactionImageUrlFetcherOptions
- AppRouting.test.tsx
- AuthGate
- BudgetInvitationEntity
- AccountEntity
- .ToDtoAsync
- useAppRouter.ts
- LongTermPlanning.test.tsx
- CategoryGroupEntity
- Wishlist
- IReadOnlyList
- Theory
- CategoryMoves
- Developer setup
- UserMembershipEntity
- BudgetPlanEntity
- CreditPayoffTargets
- .Validate
- LedgerService
- TransactionImages
- LedgerBalanceRow
- LedgerModel.cs
- AccountRegisterPresentation

## God Nodes (most connected - your core abstractions)
1. `OpenBudget.Api` - 119 edges
2. `BudgetDbContext` - 107 edges
3. `BudgetApplication()` - 100 edges
4. `LedgerService` - 74 edges
5. `OpenBudget.Api.Migrations` - 71 edges
6. `LedgerApiTests` - 69 edges
7. `MutationDto` - 68 edges
8. `LedgerException` - 58 edges
9. `BudgetDto` - 52 edges
10. `TransactionEntity` - 49 edges

## Surprising Connections (you probably didn't know these)
- `Enable/disable semantics` --references--> `Wishlist`  [INFERRED]
  docs/milestones/milestone-13-wishlist.md → src/OpenBudget.Api/Migrations/20261030124559_Wishlist.Designer.cs
- `Entitlement and enablement` --references--> `Wishlist`  [INFERRED]
  docs/milestones/milestone-13-wishlist.md → src/OpenBudget.Api/Migrations/20261030124559_Wishlist.Designer.cs
- `Target progress` --references--> `CategoryDto`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/BudgetContracts.cs
- `Importing an accepted normalized row` --references--> `TransactionRequest`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/LedgerContracts.cs
- `Credit accounts` --references--> `CreditPayoffTargets`  [INFERRED]
  docs/data-model.md → src/OpenBudget.Api/Migrations/20261030093344_CreditPayoffTargets.Designer.cs

## Import Cycles
- None detected.

## Communities (143 total, 29 thin omitted)

### Community 0 - "BudgetApplication"
Cohesion: 0.03
Nodes (73): accountSections, App(), BudgetApplication(), addFunds(), applyAssignmentChanges(), applyAutoAssignOption(), applyMove(), applyRoute() (+65 more)

### Community 1 - "DateTimeOffset"
Cohesion: 0.07
Nodes (15): Agent tokens and MCP audit, BudgetInvitationDto, BudgetPersonDto, BudgetSharingDto, PlanActionDto, IdentityFoundation, AdminLifecycle, BudgetOwnershipAndMembership (+7 more)

### Community 2 - "CategoryTargetService"
Cohesion: 0.25
Nodes (5): CategoryTargetCommandResult, CategoryTargetDto, CategoryTargetResultDto, DeleteCategoryTargetRequest, CategoryTargetService

### Community 3 - "CategoryTarget.tsx"
Cohesion: 0.08
Nodes (33): BehaviorPicker(), choose(), keys(), CategoryTargetCard(), describe(), Money, TargetCategory, progress (+25 more)

### Community 4 - "api.ts"
Cohesion: 0.07
Nodes (52): Cash ledger slice, Wishlist slice, AccountSidebarRow(), editor(), Dialog, checking, savings, accountFamily() (+44 more)

### Community 5 - "http"
Cohesion: 0.13
Nodes (18): ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL, ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL, ASPNETCORE_ENVIRONMENT, DOTNET_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables (+10 more)

### Community 6 - "compilerOptions"
Cohesion: 0.11
Nodes (17): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+9 more)

### Community 7 - "http"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 8 - "AccountEmail.cs"
Cohesion: 0.18
Nodes (8): AccountEmailContent, AccountEmailTemplates, IAccountEmailSender, SmtpAccountEmailSender, TestAccountEmailSender, FailSending, IsConfigured, Messages

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.08
Nodes (24): net10.0, Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), coverlet.collector (6.0.4), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13) (+16 more)

### Community 12 - "Fact"
Cohesion: 0.19
Nodes (10): Milestone 2 acceptance map, MutationDto, ReconcileRequest, StatusRequest, TransferRequest, UpdateAccountRequest, SavePayeesRequest, ScheduleActionRequest (+2 more)

### Community 13 - "MembershipEntitlements.cs"
Cohesion: 0.08
Nodes (19): `BudgetPlans`, AdminAuditLogEntity, Action, ActorUserId, CreatedAt, Id, TargetUserId, MembershipPlanEntitlementEntity (+11 more)

### Community 15 - "Task"
Cohesion: 0.12
Nodes (9): BudgetLimitDto, BudgetOwnershipService, DeleteOwnedBudgetRequest, OwnedBudgetDto, RenameOwnedBudgetRequest, BudgetRequestContext, BudgetId, BudgetSharingService (+1 more)

### Community 16 - "BudgetUpdateDto"
Cohesion: 0.12
Nodes (11): Milestone 11 acceptance map, BudgetUpdateDto, CategoryGroupUpdateDto, CategoryUpdateDto, CreateOwnedBudgetRequest, InviteBudgetMemberRequest, CreateAccountRequest, BudgetOwnershipApiTests (+3 more)

### Community 17 - "CancellationToken"
Cohesion: 0.09
Nodes (6): LedgerException, StatusCode, Change, LedgerService, Entries, CreatedPairCategory

### Community 18 - "Authentication.cs"
Cohesion: 0.08
Nodes (33): Milestone 2a final verification (2026-10-23), SendTestEmailRequest, UpdateSmtpSettingsRequest, AdminAuditDto, AdminDashboardDto, AdminInviteUserRequest, AdminMembershipCountDto, AdminMembershipRequest (+25 more)

### Community 19 - "RegisterViewEntity"
Cohesion: 0.11
Nodes (16): RegisterViewDto, RegisterViewEntity, BudgetId, ColumnWidthsJson, DatePreset, FromMonth, Scope, ShowFlags (+8 more)

### Community 20 - "TransactionEntity"
Cohesion: 0.06
Nodes (29): PayeeEntity, BudgetId, CategoryMode, DefaultCategoryId, Id, IsHidden, Name, NormalizedName (+21 more)

### Community 21 - "WishlistRulesTests"
Cohesion: 0.15
Nodes (9): WishlistBucketEntity, BudgetId, CreatedAt, Id, IsDeleted, Name, SortOrder, UpdatedAt (+1 more)

### Community 22 - "IReadOnlyDictionary"
Cohesion: 0.21
Nodes (6): Backdated assignment corrections, BudgetCarryForward, Changes, HasChanges, Flows, Flows

### Community 23 - "routes.ts"
Cohesion: 0.13
Nodes (30): accountSlugFor(), accountSlugs(), AppPage, AppRoute, budgetIdFromLocation(), budgetKeyFor(), BudgetRoute, decodeSegment() (+22 more)

### Community 24 - "API reference"
Cohesion: 0.07
Nodes (27): API reference, Approval and attention, Assignment rollover and earlier-month edits, Authentication, Boundaries, Budget collaboration and Plan history, Cash accounts and reconciliation, Credit accounts and card payments (+19 more)

### Community 25 - "WishlistItemEntity"
Cohesion: 0.10
Nodes (18): Milestone 13 Wishlist acceptance map (2026-10-30), Wishlist purchase transactions, WishlistItemEntity, BucketId, BudgetId, Cost, CreatedAt, Description (+10 more)

### Community 26 - "BudgetDbContext"
Cohesion: 0.04
Nodes (50): MCP server and live updates, BudgetDbContext, Accounts, ActiveBudgetId, AdminAuditLogs, ApplicationSettings, BudgetInvitations, BudgetMembers (+42 more)

### Community 27 - "Milestone 2a — Multi-User Authentication & Administration"
Cohesion: 0.10
Nodes (20): 10. Definition of Done, 1. Authentication & Login, 2. Permanent Administrator Accounts, 3. Registration & User Lifecycle, 4.1 Dashboard, 4.2 User Management, 4.3 Application Settings, 4. Separate Administration Panel (+12 more)

### Community 28 - "CreditPayoffTarget.tsx"
Cohesion: 0.11
Nodes (22): CreditPaymentDetails(), mutate(), renderPanel(), CreditPayoff, currentMonth(), kindLabels, monthKey(), monthNames (+14 more)

### Community 30 - "BudgetChangeInterceptor"
Cohesion: 0.05
Nodes (22): LedgerCommandEntity, AfterJson, BeforeJson, BudgetId, CreatedAt, Fingerprint, Id, Kind (+14 more)

### Community 31 - "MembershipEntitlements.cs"
Cohesion: 0.06
Nodes (39): Agent tokens, MCP and live events, 1. Create an agent token, 2. Connect a client, 3. Using the tools, 4. Permission matrix, 6. Real-time updates, 7. Limitations, MCP server for personal AI agents (+31 more)

### Community 32 - "BudgetContracts.cs"
Cohesion: 0.21
Nodes (5): BudgetPlanDto, BudgetPlanUpdateDto, BudgetPlanUpdateResult, CategoryDto, CategoryGroupDto

### Community 33 - "Testing and acceptance coverage"
Cohesion: 0.12
Nodes (16): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 verification (2026-10-22) (+8 more)

### Community 34 - "Agent Instructions"
Cohesion: 0.14
Nodes (13): Agent Instructions, Before making changes, Coding Guidelines, Delegating requests to worktrees and subagents, Development Principles, Documentation, Git, Project - OpenBudget (+5 more)

### Community 35 - "DateOnly"
Cohesion: 0.09
Nodes (9): ContractItem, Total, ContractPosition, Remaining, CashLedger, MembershipSubscriptionMetadata, CategoryTargets, LoanAccounts (+1 more)

### Community 36 - "docs/README.md"
Cohesion: 0.20
Nodes (5): Coding conventions, Documentation, Highlights, Project status, Quick start

### Community 37 - "TestApp"
Cohesion: 0.26
Nodes (7): SaveCategoryTargetRequest, CategoryTargetApiTests, TestApp, Clock, ConnectionString, Email, Failure

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.18
Nodes (11): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Plan interaction extension (2026-10-22) (+3 more)

### Community 41 - "WishlistPage.tsx"
Cohesion: 0.09
Nodes (35): addedAgo(), isWebLink(), loadWishlist(), reorder(), sendWishlistCommand(), WishlistAccess, WishlistBucket, WishlistConflictError (+27 more)

### Community 42 - "LongTermPlanning"
Cohesion: 0.15
Nodes (20): openUpcoming(), actualLabel(), availableTone(), inputAmount(), LongTermPlanning(), beginEdit(), commitDraft(), groupValues() (+12 more)

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.22
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

### Community 44 - "MigrationBuilder"
Cohesion: 0.05
Nodes (17): InitialCreate, BudgetCore, NamedBudget, AccountViewOptions, AccountNotes, PayeeManagement, LongTermPlanning, AccountLifecycle (+9 more)

### Community 45 - "system_text_json"
Cohesion: 0.06
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
Nodes (17): LongTermPlanningCategoryEntity, BudgetId, CategoryId, Month, PlannedBudgetOverride, PlannedExpenses, LongTermPlanningCategoryDto, LongTermPlanningCategoryMonthDto (+9 more)

### Community 51 - "WishlistService"
Cohesion: 0.17
Nodes (4): WishlistResult, WishlistService, WishlistState, ActiveBuckets

### Community 52 - "SafeTransactionImageUrlFetcher"
Cohesion: 0.21
Nodes (6): ImageFetchException, SafeTransactionImageUrlFetcher, StubFetcher, Fail, Requests, Response

### Community 53 - ".Calculate"
Cohesion: 0.16
Nodes (7): CategoryTargetDefinition, Context, MonthEnd, MonthIndex, StartIndex, CategoryTargetRulesTests, InvalidRequests

### Community 54 - "LoanAccountPage.tsx"
Cohesion: 0.07
Nodes (46): formatMoney(), LedgerDialog(), ActivityList(), CategoryActions, Chevron(), ContractSchedule(), ContractSummary(), LoanAccountPage() (+38 more)

### Community 55 - ".ScheduleActionAsync"
Cohesion: 0.13
Nodes (8): SplitEntity, Amount, CategoryId, Memo, Payee, Position, TransactionId, RecurrenceRules

### Community 56 - "LongTermPlanning.tsx"
Cohesion: 0.12
Nodes (21): ActivityDialog, ActivityKind, ActivitySelection, actualMetrics, ConflictState, EditingValue, LongTermPlanningProps, Metric (+13 more)

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
Cohesion: 0.06
Nodes (63): Mortgages, loans and contracts, Current feature inventory, Loan accounts, CategoryTargetEditor(), submit(), MoneyField(), NativeSelect(), Toggle() (+55 more)

### Community 61 - "Milestone 9 — Cross-device experience and offline resilience"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 9 — Cross-device experience and offline resilience, Product reference, User value, Vertical slice

### Community 62 - "Milestone 10 — Optional linked-account import"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies and release gate, Goal, Milestone 10 — Optional linked-account import, Product references, User value, Vertical slice

### Community 63 - "IReadOnlyDictionary"
Cohesion: 0.15
Nodes (5): 5. Security model (ISO 27001 mapping), Residual risks, ApiDispatcher, BodyDetection, CanHaveBody

### Community 64 - "Milestone: Annual Long-Term Budget Planning"
Cohesion: 0.29
Nodes (6): Acceptance Criteria, Budget propagation rule, Core behavior, Goal, Implementation notes, Milestone: Annual Long-Term Budget Planning

### Community 65 - "YNAB Web app: reverse-engineering notes"
Cohesion: 0.29
Nodes (6): Account-linking code observed in the bundle, Evidence and scope, Frontend structure, Lazy modules and browser storage, Limits and follow-up, YNAB Web app: reverse-engineering notes

### Community 66 - "LoanApiTests"
Cohesion: 0.07
Nodes (10): ContractTermsRequest, LoanBalancePointDto, LoanProjectionDto, LoanSimulationDto, NewPairedCategoryRequest, LoanRules, Pairing, Projection (+2 more)

### Community 67 - "Milestone 0 — Local application foundation"
Cohesion: 0.33
Nodes (5): Acceptance criteria, Goal, Milestone 0 — Local application foundation, User and developer outcome, Vertical slice

### Community 68 - "react"
Cohesion: 0.06
Nodes (46): AccountHeader(), balanceSummary(), ClearedIndicator(), reconciliationText(), RegisterIcon(), account, entry, TransactionRow() (+38 more)

### Community 71 - "WishlistApiTests"
Cohesion: 0.38
Nodes (5): WishlistBucketRequest, WishlistOverviewDto, WishlistVersionRequest, OwnedBudgetListDto, WishlistApiTests

### Community 73 - ".Validate"
Cohesion: 0.22
Nodes (3): ImageValidationException, TransactionImageValidator, TestImages

### Community 74 - "BudgetCarryForwardTests"
Cohesion: 0.17
Nodes (11): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+3 more)

### Community 75 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

### Community 76 - "ScheduleEntity"
Cohesion: 0.07
Nodes (27): ScheduleEntity, AccountId, AnchorDate, BudgetId, Count, EndDate, FlagColor, Id (+19 more)

### Community 77 - "Payees.tsx"
Cohesion: 0.07
Nodes (31): react, react-dom, CategoryOption, categoryGroups(), CategoryPicker(), choose(), close(), option() (+23 more)

### Community 78 - "Documentation"
Cohesion: 0.67
Nodes (3): Current release shape, Documentation, Documents

### Community 79 - "List"
Cohesion: 0.14
Nodes (15): SplitDto, TransactionDto, LongTermPlanningActivityDto, PayeeDto, PayeeRuleDto, PayeeRules, PayeesDto, PayeeUpdateDto (+7 more)

### Community 80 - ".Calculate"
Cohesion: 0.20
Nodes (3): CreditPayoffRules, CreditPayoffProgressDto, CreditPayoffRulesTests

### Community 81 - "vitest"
Cohesion: 0.04
Nodes (33): @testing-library/react, @testing-library/user-event, vitest, Activity, AgentToken, AgentTokens(), BudgetOption, formatDate() (+25 more)

### Community 82 - "McpToolDefinition"
Cohesion: 0.17
Nodes (7): AgentPermission, Admin, Budget, Reporting, McpPermissions, McpToolDefinition, McpPermissionMatrixTests

### Community 83 - "LedgerGate"
Cohesion: 0.08
Nodes (26): Category targets, Target progress, Wishlist, API, OpenBudget API and local services, Acceptance criteria, Dependencies, Goal (+18 more)

### Community 84 - "PlanActionEntity"
Cohesion: 0.20
Nodes (10): PlanActionEntity, ActorUserId, Amount, BudgetId, CreatedAt, FromCategoryName, Id, Kind (+2 more)

### Community 86 - "Milestone 12 — Icons and suggested icons"
Cohesion: 0.22
Nodes (9): Acceptance criteria, Dependencies, Goal, Milestone 12 — Icons and suggested icons, Open questions, Product references, User value, Vertical slice (+1 more)

### Community 87 - "CategoryEntity"
Cohesion: 0.11
Nodes (20): CategoryEntity, Activity, Assigned, Available, BudgetId, BudgetMonth, CreditOverspending, GroupId (+12 more)

### Community 89 - "AccountsPage"
Cohesion: 0.13
Nodes (17): AccountsPage(), applyView(), beginColumnResize(), clearFilters(), finishColumnResize(), moveColumnResize(), mutate(), persistColumnWidths() (+9 more)

### Community 91 - "AccountEmailSettingsService"
Cohesion: 0.16
Nodes (12): AccountEmailSettingsService, SmtpConnectionSettings, SmtpSettingsDto, SmtpSettingsEntity, EnableSsl, EncryptedPassword, From, Host (+4 more)

### Community 92 - ".CallToolAsync"
Cohesion: 0.11
Nodes (4): McpToolHandlers, AgentTokenIdentity, SchedulePostingService, SchedulePostingWorker

### Community 93 - "OpenBudget.Api"
Cohesion: 0.11
Nodes (3): OpenBudget.Tests, OpenBudget.Api, OpenBudget.UnitTests

### Community 94 - "AgentTokenEntity"
Cohesion: 0.06
Nodes (39): Agent access (MCP) and live updates acceptance map, AgentAccessModel, AgentAuditLogEntity, BudgetId, CreatedAt, Id, Outcome, StatusCode (+31 more)

### Community 95 - "LedgerContracts.cs"
Cohesion: 0.27
Nodes (4): TransactionRequest, MembershipEntitlementApiTests, PostgresFixture, TransactionSelectionApiTests

### Community 96 - ".Configure"
Cohesion: 0.14
Nodes (12): LedgerModel, LedgerStateEntity, BudgetId, Id, Version, ReconciliationEntity, AccountId, BudgetId (+4 more)

### Community 98 - "Task"
Cohesion: 0.19
Nodes (12): ContractDto, ContractScheduleItemDto, LoanActivityItemDto, LoanActivityMonthDto, LoanBalanceRequest, LoanDto, LoanPairedCategoryDto, LoanPairingDto (+4 more)

### Community 99 - "ScheduledTransactions"
Cohesion: 0.14
Nodes (5): Limitations and direction, Scheduling and flag storage, ScheduledTransactions, TransactionFlags, ScheduledApproval

### Community 100 - "McpToolContext"
Cohesion: 0.19
Nodes (5): McpToolContext, Arguments, BudgetId, CancellationToken, Token

### Community 101 - "WishlistBucketEntity"
Cohesion: 0.25
Nodes (6): WishlistModel, WishlistSettingsEntity, BudgetId, IsEnabled, UpdatedAt, Version

### Community 102 - "LoanTermsEntity"
Cohesion: 0.09
Nodes (22): LoanTermsEntity, AccountId, BalanceConfirmedAt, BudgetId, Contract, ContractMonths, ContractStartDate, CreatedAt (+14 more)

### Community 104 - "JsonElement"
Cohesion: 0.22
Nodes (3): McpResultExtensions, McpTestClient, Http

### Community 105 - "TransactionImageApiTests"
Cohesion: 0.21
Nodes (9): AccountsDto, AuditPageDto, CreditSummaryDto, DeleteTransactionRequest, RegisterDto, ReviewTransactionRequest, DeleteTransactionImageRequest, ITransactionImageUrlFetcher (+1 more)

### Community 106 - "CategoryTargetEntity"
Cohesion: 0.12
Nodes (16): CategoryTargetEntity, Amount, Behavior, BudgetId, CategoryId, CreatedAt, DayOfMonth, DueDate (+8 more)

### Community 108 - "JsonObject"
Cohesion: 0.27
Nodes (6): Category targets, Credit accounts, Payee management, Transaction images, Wishlist, S

### Community 109 - "Current PostgreSQL schema"
Cohesion: 0.22
Nodes (9): `Budgets`, Calculations and month lifecycle, `CategoryGroups` and `Categories`, Current PostgreSQL schema, Data model, Identity and account lifecycle, Ledger tables, Long-Term Planning (+1 more)

### Community 110 - "JsonElement"
Cohesion: 0.13
Nodes (12): ApiResponse, Error, IsSuccess, McpToolCatalog, Effect, Destructive, Read, Write (+4 more)

### Community 112 - "TransactionImageService"
Cohesion: 0.07
Nodes (31): Storage, AccountDto, AuditDto, TransactionImageEndpoints, TransactionImageBlobEntity, BudgetId, Content, ImageId (+23 more)

### Community 115 - "TransactionImagesDialog"
Cohesion: 0.29
Nodes (11): Security model, Transaction image attachments, TransactionImagesDialog(), addFiles(), commandFor(), drop(), go(), importUrl() (+3 more)

### Community 116 - "TransactionImageUrlFetcherOptions"
Cohesion: 0.17
Nodes (6): McpBudgetIds, BudgetEventsEndpoint, TransactionImageUrlFetcherOptions, AllowNonDefaultPort, MaxRedirects, Timeout

### Community 118 - "AppRouting.test.tsx"
Cohesion: 0.12
Nodes (7): URL routing, account(), budgets, Category, key, travelKey, routeNotices

### Community 120 - "AuthGate"
Cohesion: 0.26
Nodes (9): AuthBrand(), AuthGate(), login(), refreshSession(), Session, isSafeReturnPath(), publicPaths, rememberReturnPath() (+1 more)

### Community 121 - "BudgetInvitationEntity"
Cohesion: 0.14
Nodes (13): BudgetInvitationEntity, BudgetId, CreatedAt, Email, Id, InvitedByUserId, NormalizedEmail, BudgetMemberEntity (+5 more)

### Community 122 - "AccountEntity"
Cohesion: 0.10
Nodes (13): CreditCardRules, AccountEntity, AccountType, BudgetId, Id, IsClosed, IsFavorite, Name (+5 more)

### Community 125 - ".ToDtoAsync"
Cohesion: 0.25
Nodes (7): Architecture and current status, Category targets, Design limits to address, Implemented user path, Runtime topology, Technologies, LongTermPlanningPlanValues

### Community 126 - "useAppRouter.ts"
Cohesion: 0.24
Nodes (9): listeners, longTermYearStore, useRoutedLongTermYear(), RouteTarget, titleForState(), AppRouterOptions, currentPath(), Named (+1 more)

### Community 128 - "CategoryGroupEntity"
Cohesion: 0.25
Nodes (8): CategoryGroupEntity, BudgetId, BudgetMonth, Categories, Id, IsArchived, Name, SortOrder

### Community 130 - "IReadOnlyList"
Cohesion: 0.60
Nodes (4): WishlistBucketDto, WishlistItemDto, WishlistProgressDto, WishlistPurchaseDto

### Community 131 - "Theory"
Cohesion: 0.11
Nodes (5): WishlistPurchaseRules, EntitlementKeyTests, LedgerRulesTests, PayeeRulesTests, RecurrenceTests

### Community 133 - "Developer setup"
Cohesion: 0.22
Nodes (9): Build commands, Current development boundaries, Database lifecycle, Developer setup, First run, GitHub pull requests and releases, Prerequisites, Request flow (+1 more)

### Community 134 - "UserMembershipEntity"
Cohesion: 0.22
Nodes (9): UserMembershipEntity, BillingProvider, BudgetLimitOverride, ExpiresOn, ExternalSubscriptionId, MembershipPlanId, StartsOn, UserId (+1 more)

### Community 135 - "BudgetPlanEntity"
Cohesion: 0.22
Nodes (9): BudgetPlanEntity, CreatedAt, Currency, Id, IsArchived, Months, Name, OwnerUserId (+1 more)

### Community 138 - "LedgerService"
Cohesion: 0.11
Nodes (18): CreditPayoffTargetEntity, AccountId, BudgetId, CreatedAt, Kind, MonthlyAmount, PayoffAmount, TargetMonth (+10 more)

### Community 140 - "LedgerBalanceRow"
Cohesion: 0.67
Nodes (3): LedgerBalanceRow, Balance, Id

### Community 141 - "LedgerModel.cs"
Cohesion: 0.40
Nodes (4): FlagDefinitionEntity, BudgetId, Color, Name

## Knowledge Gaps
- **427 isolated node(s):** `LocalTimeZone`, `UtcNow`, `Zone`, `Arguments`, `BudgetId` (+422 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1090 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **29 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Wishlist slice` connect `api.ts` to `Wishlist`, `LoanApiTests`, `Theory`, `WishlistBucketEntity`, `CancellationToken`, `LedgerGate`, `WishlistService`, `WishlistRulesTests`, `LoanAccountPage.tsx`, `.UpdateMonthAsync`, `AccountsPage`, `parseMoney`, `.ToDtoAsync`?**
  _High betweenness centrality (0.200) - this node is a cross-community bridge._
- **What connects `LocalTimeZone`, `UtcNow`, `Zone` to the rest of the system?**
  _427 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `BudgetApplication` be split into smaller, more focused modules?**
  _Cohesion score 0.03223443223443224 - nodes in this community are weakly interconnected._
- **Why does `AccountsPage()` connect `AccountsPage` to `BudgetApplication`, `api.ts`, `react`, `Payees.tsx`, `parseMoney`, `LoanAccountPage.tsx`, `CreditPayoffTarget.tsx`?**
  _High betweenness centrality (0.081) - this node is a cross-community bridge._
- **Should `DateTimeOffset` be split into smaller, more focused modules?**
  _Cohesion score 0.07057057057057058 - nodes in this community are weakly interconnected._
- **Why does `MCP server and live updates` connect `BudgetDbContext` to `AgentTokenAuthenticationHandler`, `MembershipEntitlements.cs`, `LedgerGate`, `.CallToolAsync`, `.ToDtoAsync`, `BudgetChangeInterceptor`, `IReadOnlyDictionary`?**
  _High betweenness centrality (0.081) - this node is a cross-community bridge._
- **Should `CategoryTarget.tsx` be split into smaller, more focused modules?**
  _Cohesion score 0.07712765957446809 - nodes in this community are weakly interconnected._