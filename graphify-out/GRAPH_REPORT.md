# Graph Report - ynab-clone  (2026-10-30)

## Corpus Check
- 314 files · ~278,674 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 18 file(s) not represented in the graph (top: .css 9, .ttf 4, (none) 3)

## Summary
- 3495 nodes · 11191 edges · 143 communities (122 shown, 21 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 1080 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `1976ce2b`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- BudgetApplication
- DateTimeOffset
- CategoryTargetService
- CategoryTarget.tsx
- App.tsx
- http
- compilerOptions
- http
- LoanRules
- OpenBudget.IntegrationTests.csproj
- Fact
- MembershipEntitlements.cs
- PostgresFixture.cs
- Task
- TransactionImages.tsx
- CancellationToken
- Authentication.cs
- RegisterViewEntity
- WishlistRulesTests
- AccountsPage.tsx
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
- ScheduleOccurrenceEntity
- Testing and acceptance coverage
- Agent Instructions
- JsonElement
- docs/README.md
- TestApp
- AdminAuditLogEntity
- Milestone 1 — Reliable budget core and month lifecycle
- Milestone 2 — Accounts and transaction register
- WishlistPage.tsx
- LongTermPlanning.tsx
- Milestone 7 — File import, matching, and portable data
- MigrationBuilder
- system_text_json
- Milestone 4 — Scheduled transactions and cash-flow awareness
- RenameFlagsRequest
- vitest
- microsoft_entityframeworkcore
- .GetYearAsync
- WishlistItemEntity
- SafeTransactionImageUrlFetcher
- .Calculate
- LoanAccountPage.tsx
- BudgetUpdateDto
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
- List
- .Calculate
- vitest
- McpToolCatalog
- Milestone 13 — Wishlist
- .UpdateMonthAsync
- .HandleAuthenticateAsync
- Milestone 12 — Icons and suggested icons
- CategoryEntity
- IReadOnlyList
- .MapAuthenticationEndpoints
- McpToolCatalog
- AccountEmailSettingsService
- .CallToolAsync
- OpenBudget.Api
- LedgerContracts.cs
- LedgerContracts.cs
- ModelBuilder
- AgentTokens.tsx
- Task
- ScheduledTransactions
- McpToolContext
- Current PostgreSQL schema
- LoanTermsEntity
- WishlistPurchaseApiTests
- JsonObject
- TransactionImageApiTests
- devDependencies
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
- PlanActionEntity
- BudgetPlanEntity
- BudgetPlanEntity
- BudgetInvitationEntity
- SmtpSettingsEntity
- WishlistSettingsEntity
- CreditPayoffTargetEntity
- AdminPanel
- CategoryGroupEntity
- EntitlementSettings.tsx
- AccountEntity
- UserEntitlementOverrides
- LedgerRulesTests
- Theory
- AdminPanel.tsx
- Developer setup
- TransactionImageValidator
- scripts
- Effect
- .Validate
- AgentTokensAndAudit
- LongTermPlanningIncomeEntity
- LongTermPlanningStateEntity
- MembershipPlanEntity
- UserEntitlementOverrideEntity

## God Nodes (most connected - your core abstractions)
1. `OpenBudget.Api` - 119 edges
2. `BudgetDbContext` - 112 edges
3. `BudgetApplication()` - 101 edges
4. `MutationDto` - 86 edges
5. `OpenBudget.Api.Migrations` - 71 edges
6. `LedgerApiTests` - 69 edges
7. `BudgetDto` - 60 edges
8. `LedgerException` - 58 edges
9. `LedgerService` - 55 edges
10. `AccountsPage()` - 53 edges

## Surprising Connections (you probably didn't know these)
- `Enable/disable semantics` --references--> `Wishlist`  [INFERRED]
  docs/milestones/milestone-13-wishlist.md → src/OpenBudget.Api/Migrations/20261030124559_Wishlist.Designer.cs
- `Entitlement and enablement` --references--> `Wishlist`  [INFERRED]
  docs/milestones/milestone-13-wishlist.md → src/OpenBudget.Api/Migrations/20261030124559_Wishlist.Designer.cs
- `Target progress` --references--> `CategoryDto`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/BudgetContracts.cs
- `Importing an accepted normalized row` --references--> `TransactionRequest`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/LedgerContracts.cs
- `Wishlist purchase transactions (2026-10-30)` --references--> `focusNextField()`  [INFERRED]
  docs/milestones/milestone-13-wishlist.md → src/OpenBudget.UI/src/ledger/focusNext.ts

## Import Cycles
- None detected.

## Communities (143 total, 21 thin omitted)

### Community 0 - "BudgetApplication"
Cohesion: 0.05
Nodes (39): BudgetApplication(), addFunds(), applyAssignmentChanges(), applyAutoAssignOption(), applyMove(), applyRoute(), cancelClosePlans(), canLeaveCurrentView() (+31 more)

### Community 1 - "DateTimeOffset"
Cohesion: 0.04
Nodes (22): Owned budgets and memberships, AdminAuditDto, AdminDashboardDto, AdminInviteUserRequest, AdminMembershipCountDto, AdminMembershipRequest, AdminRegistrationDto, AdminUserDto (+14 more)

### Community 2 - "CategoryTargetService"
Cohesion: 0.08
Nodes (26): BudgetPlanDto, BudgetPlanUpdateDto, BudgetPlanUpdateResult, CategoryDto, CategoryGroupDto, CategoryTargetCommandResult, CategoryTargetDto, CategoryTargetEntity (+18 more)

### Community 3 - "CategoryTarget.tsx"
Cohesion: 0.07
Nodes (40): BehaviorPicker(), choose(), keys(), CategoryTargetCard(), CategoryTargetEditor(), submit(), describe(), Money (+32 more)

### Community 4 - "App.tsx"
Cohesion: 0.06
Nodes (33): accountSectionId(), accountSections, App(), BudgetPlan, BudgetState, Category, CategoryGroup, CategoryTransactionDialog (+25 more)

### Community 5 - "http"
Cohesion: 0.13
Nodes (18): ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL, ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL, ASPNETCORE_ENVIRONMENT, DOTNET_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables (+10 more)

### Community 6 - "compilerOptions"
Cohesion: 0.11
Nodes (17): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+9 more)

### Community 7 - "http"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 8 - "LoanRules"
Cohesion: 0.15
Nodes (3): LoanRules, Projection, NeverPaidOff

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.07
Nodes (25): Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), ModelContextProtocol.AspNetCore (2.2.0), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3) (+17 more)

### Community 12 - "Fact"
Cohesion: 0.19
Nodes (9): Milestone 2 acceptance map, DeleteTransactionRequest, MutationDto, ReconcileRequest, StatusRequest, SavePayeesRequest, ScheduleActionRequest, ScheduleRequest (+1 more)

### Community 13 - "MembershipEntitlements.cs"
Cohesion: 0.24
Nodes (3): EntitlementAuthorizationHandler, EntitlementRequirement, EntitlementService

### Community 15 - "Task"
Cohesion: 0.09
Nodes (17): API, IAccountEmailSender, SmtpAccountEmailSender, BudgetOwnershipService, BudgetRequestContext, BudgetId, BudgetInvitationDto, BudgetPersonDto (+9 more)

### Community 16 - "TransactionImages.tsx"
Cohesion: 0.18
Nodes (14): TransactionRow(), ApiError, entry, image(), openDialog(), transactionImageLabel(), TransactionImagesButton(), acceptedImageTypes (+6 more)

### Community 17 - "CancellationToken"
Cohesion: 0.08
Nodes (16): LedgerService, SetFlagRequest, AccountDto, LedgerException, StatusCode, LedgerRules, Change, LedgerBalanceRow (+8 more)

### Community 18 - "Authentication.cs"
Cohesion: 0.12
Nodes (20): Milestone 2a final verification (2026-10-23), ChangePasswordRequest, ForgotPasswordRequest, LoginRequest, OpenBudgetUser, CreatedAt, IsBootstrapAdministrator, IsSuspended (+12 more)

### Community 19 - "RegisterViewEntity"
Cohesion: 0.15
Nodes (13): RegisterViewEntity, BudgetId, ColumnWidthsJson, DatePreset, FromMonth, Scope, ShowFlags, ShowImages (+5 more)

### Community 20 - "WishlistRulesTests"
Cohesion: 0.21
Nodes (3): WishlistItemRequest, WishlistRules, WishlistRulesTests

### Community 21 - "AccountsPage.tsx"
Cohesion: 0.05
Nodes (70): Cash ledger slice, DisclosureChevron(), AccountForm(), AccountsPage(), applyView(), beginColumnResize(), clearFilters(), editor() (+62 more)

### Community 22 - "WishlistService"
Cohesion: 0.15
Nodes (10): WishlistBucketDto, WishlistBucketOrderRequest, WishlistItemDto, WishlistItemOrderRequest, WishlistProgressDto, WishlistPurchaseDto, WishlistResult, WishlistService (+2 more)

### Community 23 - "routes.ts"
Cohesion: 0.09
Nodes (46): URL routing, account(), accountSlugFor(), accountSlugs(), AppPage, AppRoute, budgetIdFromLocation(), budgetKeyFor() (+38 more)

### Community 24 - "API reference"
Cohesion: 0.07
Nodes (27): API reference, Approval and attention, Assignment rollover and earlier-month edits, Authentication, Boundaries, Budget collaboration and Plan history, Cash accounts and reconciliation, Credit accounts and card payments (+19 more)

### Community 25 - "Wishlist slice"
Cohesion: 0.13
Nodes (4): Wishlist slice, WishlistPurchaseRules, WishlistPurchases, WishlistPurchaseRulesTests

### Community 26 - "BudgetDbContext"
Cohesion: 0.05
Nodes (38): MCP server and live updates, BudgetDbContext, Accounts, ActiveBudgetId, AdminAuditLogs, ApplicationSettings, BudgetInvitations, BudgetMembers (+30 more)

### Community 27 - "TestApp"
Cohesion: 0.19
Nodes (10): Agent access (MCP) and live updates acceptance map, CreateAgentTokenRequest, Program, McpServerApiTests, McpTestKit, TestApp, Clock, ConnectionString (+2 more)

### Community 28 - "CreditPayoffTarget.tsx"
Cohesion: 0.09
Nodes (24): Invitation, Person, Sharing, CreditPaymentDetails(), mutate(), card, progress, renderPanel() (+16 more)

### Community 30 - "BudgetChangeInterceptor"
Cohesion: 0.07
Nodes (12): BudgetChangeEvent, BudgetChangeInterceptor, BudgetChangePublisher, Subscription, BudgetId, Channel, Id, UserId (+4 more)

### Community 31 - "Milestone 2a — Multi-User Authentication & Administration"
Cohesion: 0.11
Nodes (18): 10. Definition of Done, 1. Authentication & Login, 2. Permanent Administrator Accounts, 3. Registration & User Lifecycle, 4.1 Dashboard, 4.2 User Management, 4.3 Application Settings, 4. Separate Administration Panel (+10 more)

### Community 32 - "ScheduleOccurrenceEntity"
Cohesion: 0.15
Nodes (11): ScheduleOccurrenceEntity, BudgetId, DeferredDate, Disposition, DueDate, Id, OverrideJson, ScheduleId (+3 more)

### Community 33 - "Testing and acceptance coverage"
Cohesion: 0.07
Nodes (23): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 verification (2026-10-22) (+15 more)

### Community 34 - "Agent Instructions"
Cohesion: 0.14
Nodes (13): Agent Instructions, Before making changes, Coding Guidelines, Delegating requests to worktrees and subagents, Development Principles, Documentation, Git, Project - OpenBudget (+5 more)

### Community 35 - "JsonElement"
Cohesion: 0.20
Nodes (3): McpResultExtensions, McpTestClient, Http

### Community 36 - "docs/README.md"
Cohesion: 0.19
Nodes (6): OpenBudget API and local services, Coding conventions, Documentation, Highlights, Project status, Quick start

### Community 38 - "AdminAuditLogEntity"
Cohesion: 0.33
Nodes (6): AdminAuditLogEntity, Action, ActorUserId, CreatedAt, Id, TargetUserId

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.18
Nodes (11): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Plan interaction extension (2026-10-22) (+3 more)

### Community 41 - "WishlistPage.tsx"
Cohesion: 0.09
Nodes (37): addedAgo(), isWebLink(), loadWishlist(), reorder(), sendWishlistCommand(), WishlistAccess, WishlistBucket, WishlistConflictError (+29 more)

### Community 42 - "LongTermPlanning.tsx"
Cohesion: 0.07
Nodes (50): openUpcoming(), ActivityDialog, ActivityKind, ActivitySelection, actualLabel(), actualMetrics, availableTone(), ConflictState (+42 more)

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.22
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

### Community 44 - "MigrationBuilder"
Cohesion: 0.04
Nodes (21): InitialCreate, BudgetCore, NamedBudget, AccountRegisterPresentation, AccountViewOptions, AccountNotes, PayeeManagement, CategoryMoves (+13 more)

### Community 45 - "system_text_json"
Cohesion: 0.06
Nodes (8): OpenBudget.Api.Realtime, OpenBudget.Api.Mcp, BudgetLimitDto, DeleteOwnedBudgetRequest, RenameOwnedBudgetRequest, McpServerText, AgentTokenClaims, BudgetChangeKinds

### Community 46 - "Milestone 4 — Scheduled transactions and cash-flow awareness"
Cohesion: 0.29
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 4 — Scheduled transactions and cash-flow awareness, Product references, User value, Vertical slice

### Community 47 - "RenameFlagsRequest"
Cohesion: 0.29
Nodes (7): 1. Add the GitHub workflows, 2. Configure Coolify, 3. Create the Coolify webhook, 4. Add GitHub secrets, 5. Publish a release, GitHub Release to Coolify, Useful checks

### Community 48 - "vitest"
Cohesion: 0.05
Nodes (29): jsdom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript, vite (+21 more)

### Community 50 - ".GetYearAsync"
Cohesion: 0.08
Nodes (22): Milestone 11 acceptance map, LongTermPlanningCategoryEntity, BudgetId, CategoryId, Month, PlannedBudgetOverride, PlannedExpenses, LongTermPlanningCategoryDto (+14 more)

### Community 51 - "WishlistItemEntity"
Cohesion: 0.13
Nodes (15): WishlistItemEntity, BucketId, BudgetId, Cost, CreatedAt, Description, Id, Name (+7 more)

### Community 52 - "SafeTransactionImageUrlFetcher"
Cohesion: 0.14
Nodes (11): ImageFetchException, ITransactionImageUrlFetcher, SafeTransactionImageUrlFetcher, TransactionImageUrlFetcherOptions, AllowNonDefaultPort, MaxRedirects, Timeout, StubFetcher (+3 more)

### Community 54 - "LoanAccountPage.tsx"
Cohesion: 0.07
Nodes (43): formatMoney(), ActivityList(), CategoryActions, Chevron(), ContractSchedule(), ContractSummary(), LoanAccountPage(), MonthYearPicker() (+35 more)

### Community 55 - "BudgetUpdateDto"
Cohesion: 0.26
Nodes (7): BudgetUpdateDto, CategoryGroupUpdateDto, CategoryUpdateDto, CreateOwnedBudgetRequest, OwnedBudgetDto, BudgetOwnershipApiTests, OwnedBudgetListDto

### Community 56 - "AppRouting.test.tsx"
Cohesion: 0.12
Nodes (5): Category, emitBudgetChanged(), FakeEventSource, initialBudgets, legacyKey

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
Cohesion: 0.08
Nodes (46): Mortgages, loans and contracts, creditPaymentGroupId, LoanAccountType, loanAccountTypes, EditLoanAccountForm(), LoanEditor(), openPairing(), save() (+38 more)

### Community 61 - "Milestone 9 — Cross-device experience and offline resilience"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 9 — Cross-device experience and offline resilience, Product reference, User value, Vertical slice

### Community 62 - "Milestone 10 — Optional linked-account import"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies and release gate, Goal, Milestone 10 — Optional linked-account import, Product references, User value, Vertical slice

### Community 63 - "IReadOnlyDictionary"
Cohesion: 0.16
Nodes (3): ApiDispatcher, BodyDetection, CanHaveBody

### Community 64 - "Milestone: Annual Long-Term Budget Planning"
Cohesion: 0.29
Nodes (6): Acceptance Criteria, Budget propagation rule, Core behavior, Goal, Implementation notes, Milestone: Annual Long-Term Budget Planning

### Community 65 - "YNAB Web app: reverse-engineering notes"
Cohesion: 0.29
Nodes (6): Account-linking code observed in the bundle, Evidence and scope, Frontend structure, Lazy modules and browser storage, Limits and follow-up, YNAB Web app: reverse-engineering notes

### Community 66 - ".Project"
Cohesion: 0.06
Nodes (24): Architecture and current status, Category targets, Current feature inventory, Design limits to address, Implemented user path, Runtime topology, Security model, Technologies (+16 more)

### Community 67 - "Milestone 0 — Local application foundation"
Cohesion: 0.33
Nodes (5): Acceptance criteria, Goal, Milestone 0 — Local application foundation, User and developer outcome, Vertical slice

### Community 68 - "WishlistApp.test.tsx"
Cohesion: 0.18
Nodes (7): Access, checking, json(), month(), progress, purchaseEntry, withAccounts()

### Community 71 - "McpToolDefinition"
Cohesion: 0.16
Nodes (8): AgentPermission, Admin, Budget, Reporting, McpPermissions, McpToolDefinition, Route, McpPermissionMatrixTests

### Community 72 - "DateOnly"
Cohesion: 0.13
Nodes (12): CategoryTargetProgressDto, CategoryTargetDefinition, CategoryTargetRules, Context, MonthEnd, MonthIndex, StartIndex, ContractItem (+4 more)

### Community 73 - ".Validate"
Cohesion: 0.13
Nodes (6): ImageFileNameSanitizer, ImageMetadataStripper, ImageValidationException, ValidatedImage, TestImages, TransactionImageValidationTests

### Community 75 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

### Community 76 - "ScheduleEntity"
Cohesion: 0.12
Nodes (17): ScheduleEntity, AccountId, AnchorDate, BudgetId, Count, EndDate, FlagColor, Id (+9 more)

### Community 77 - "Payees.tsx"
Cohesion: 0.06
Nodes (45): react, react-dom, CategoryOption, categoryGroups(), CategoryPicker(), choose(), close(), option() (+37 more)

### Community 78 - "Documentation"
Cohesion: 0.67
Nodes (3): Current release shape, Documentation, Documents

### Community 79 - "List"
Cohesion: 0.12
Nodes (14): FlagDto, FlagPaletteDto, RenameFlagsRequest, TransactionDto, LongTermPlanningActivityDto, PayeeDto, PayeeRuleDto, PayeeRules (+6 more)

### Community 81 - "vitest"
Cohesion: 0.09
Nodes (33): AccountHeader(), balanceSummary(), ClearedIndicator(), ClearedSymbol(), reconciliationText(), RegisterIcon(), account, entry (+25 more)

### Community 82 - "McpToolCatalog"
Cohesion: 0.16
Nodes (8): ApiResponse, Error, IsSuccess, McpToolCatalog, All, CommandIdArg, LedgerVersionArg, McpToolOutcome

### Community 83 - "Milestone 13 — Wishlist"
Cohesion: 0.09
Nodes (24): Category targets, Target progress, Wishlist, Acceptance criteria, Dependencies, Goal, Implemented: category targets (2026-10-30), Milestone 3 — Goals, templates, and guided assignment (+16 more)

### Community 84 - ".UpdateMonthAsync"
Cohesion: 0.13
Nodes (7): Changes, HasChanges, BudgetUpdateResult, BudgetService, LedgerProjection, Flows, Pairing

### Community 85 - ".HandleAuthenticateAsync"
Cohesion: 0.25
Nodes (3): AgentTokenSecrets, GeneratedToken, AgentTokenSecretsTests

### Community 86 - "Milestone 12 — Icons and suggested icons"
Cohesion: 0.22
Nodes (9): Acceptance criteria, Dependencies, Goal, Milestone 12 — Icons and suggested icons, Open questions, Product references, User value, Vertical slice (+1 more)

### Community 87 - "CategoryEntity"
Cohesion: 0.12
Nodes (17): CategoryEntity, Activity, Assigned, Available, BudgetId, BudgetMonth, CreditOverspending, GroupId (+9 more)

### Community 89 - ".MapAuthenticationEndpoints"
Cohesion: 0.24
Nodes (6): AccountEmailContent, AccountEmailTemplates, AuthenticationSetup, ApplicationSettingEntity, Key, Value

### Community 91 - "AccountEmailSettingsService"
Cohesion: 0.31
Nodes (4): AccountEmailSettingsService, SmtpConnectionSettings, SmtpSettingsDto, UpdateSmtpSettingsRequest

### Community 92 - ".CallToolAsync"
Cohesion: 0.07
Nodes (11): McpToolHandlers, AgentTokenIdentity, AgentActivityDto, AgentTokenBudgetDto, AgentTokenDto, AgentTokenService, CreatedAgentTokenDto, McpBudgetIds (+3 more)

### Community 93 - "OpenBudget.Api"
Cohesion: 0.08
Nodes (6): OpenBudget.Tests, OpenBudget.Api, OpenBudget.UnitTests, SendTestEmailRequest, RecurrenceTests, RegisterViewTests

### Community 94 - "LedgerContracts.cs"
Cohesion: 0.21
Nodes (10): AuditDto, AuditPageDto, CreditPayoffDto, CreditPayoffProgressDto, CreditPayoffTargetDto, CreditSummaryDto, DeleteCreditPayoffTargetRequest, ReviewTransactionRequest (+2 more)

### Community 95 - "LedgerContracts.cs"
Cohesion: 0.27
Nodes (4): TransactionRequest, MembershipEntitlementApiTests, PostgresFixture, TransactionSelectionApiTests

### Community 96 - "ModelBuilder"
Cohesion: 0.03
Nodes (69): MembershipPlanEntitlementEntity, EntitlementKey, IsEnabled, MembershipPlanId, AccountEntity, AccountType, BudgetId, Id (+61 more)

### Community 97 - "AgentTokens.tsx"
Cohesion: 0.12
Nodes (11): Activity, AgentToken, AgentTokens(), BudgetOption, formatDate(), Permission, permissionHelp, readJson() (+3 more)

### Community 98 - "Task"
Cohesion: 0.20
Nodes (17): CreateAccountRequest, TransferRequest, UpdateAccountRequest, LoanActivityItemDto, LoanActivityMonthDto, LoanBalancePointDto, LoanBalanceRequest, LoanDto (+9 more)

### Community 99 - "ScheduledTransactions"
Cohesion: 0.14
Nodes (5): Limitations and direction, Scheduling and flag storage, ScheduledTransactions, TransactionFlags, ScheduledApproval

### Community 100 - "McpToolContext"
Cohesion: 0.19
Nodes (5): McpToolContext, Arguments, BudgetId, CancellationToken, Token

### Community 101 - "Current PostgreSQL schema"
Cohesion: 0.14
Nodes (14): Backdated assignment corrections, `Budgets`, Calculations and month lifecycle, `CategoryGroups` and `Categories`, Credit accounts, Current PostgreSQL schema, Data model, Identity and account lifecycle (+6 more)

### Community 102 - "LoanTermsEntity"
Cohesion: 0.09
Nodes (22): LoanTermsEntity, AccountId, BalanceConfirmedAt, BudgetId, Contract, ContractMonths, ContractStartDate, CreatedAt (+14 more)

### Community 103 - "WishlistPurchaseApiTests"
Cohesion: 0.11
Nodes (17): Milestone 13 Wishlist acceptance map (2026-10-30), Wishlist purchase transactions, UserMembershipEntity, BillingProvider, BudgetLimitOverride, ExpiresOn, ExternalSubscriptionId, MembershipPlanId (+9 more)

### Community 104 - "JsonObject"
Cohesion: 0.33
Nodes (3): Category targets, Wishlist, S

### Community 105 - "TransactionImageApiTests"
Cohesion: 0.41
Nodes (6): AccountsDto, RegisterDto, DeleteTransactionImageRequest, ImportTransactionImageUrlRequest, TransactionImageListDto, TransactionImageApiTests

### Community 106 - "devDependencies"
Cohesion: 0.18
Nodes (11): devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript (+3 more)

### Community 108 - "liveUpdates.test.tsx"
Cohesion: 0.30
Nodes (6): BudgetChange, LiveUpdateOptions, reconnectDelay(), FakeEventSource, setup(), useBudgetLiveUpdates()

### Community 109 - "BudgetEntity"
Cohesion: 0.10
Nodes (18): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+10 more)

### Community 111 - "MCP server for personal AI agents"
Cohesion: 0.17
Nodes (12): Agent tokens, MCP and live events, 1. Create an agent token, 2. Connect a client, 3. Using the tools, 4. Permission matrix, 5. Security model (ISO 27001 mapping), 6. Real-time updates, 7. Limitations (+4 more)

### Community 112 - "TransactionImageService"
Cohesion: 0.16
Nodes (6): Storage, TransactionImageEndpoints, TransactionImageService, TransactionImageUpload, ITransactionImageStore, PostgresTransactionImageStore

### Community 113 - "BudgetMemberEntity"
Cohesion: 0.33
Nodes (6): BudgetMemberEntity, BudgetId, InvitedByUserId, JoinedAt, Role, UserId

### Community 115 - "TransactionImageEntity"
Cohesion: 0.12
Nodes (15): TransactionImageEntity, BudgetId, ContentType, CreatedAt, CreatedByUserId, FileName, Height, Id (+7 more)

### Community 116 - "LedgerCommandEntity"
Cohesion: 0.20
Nodes (10): LedgerCommandEntity, AfterJson, BeforeJson, BudgetId, CreatedAt, Fingerprint, Id, Kind (+2 more)

### Community 117 - "AuthGate"
Cohesion: 0.26
Nodes (9): AuthBrand(), AuthGate(), login(), refreshSession(), Session, isSafeReturnPath(), publicPaths, rememberReturnPath() (+1 more)

### Community 118 - "PlanActionEntity"
Cohesion: 0.20
Nodes (10): PlanActionEntity, ActorUserId, Amount, BudgetId, CreatedAt, FromCategoryName, Id, Kind (+2 more)

### Community 119 - "BudgetPlanEntity"
Cohesion: 0.22
Nodes (9): BudgetPlanEntity, CreatedAt, Currency, Id, IsArchived, Months, Name, OwnerUserId (+1 more)

### Community 120 - "BudgetPlanEntity"
Cohesion: 0.08
Nodes (26): AgentAccessModel, AgentAuditLogEntity, BudgetId, CreatedAt, Id, Outcome, StatusCode, Success (+18 more)

### Community 121 - "BudgetInvitationEntity"
Cohesion: 0.29
Nodes (7): BudgetInvitationEntity, BudgetId, CreatedAt, Email, Id, InvitedByUserId, NormalizedEmail

### Community 122 - "SmtpSettingsEntity"
Cohesion: 0.22
Nodes (9): SmtpSettingsEntity, EnableSsl, EncryptedPassword, From, Host, Id, Port, UpdatedAt (+1 more)

### Community 123 - "WishlistSettingsEntity"
Cohesion: 0.40
Nodes (5): WishlistSettingsEntity, BudgetId, IsEnabled, UpdatedAt, Version

### Community 124 - "CreditPayoffTargetEntity"
Cohesion: 0.22
Nodes (9): CreditPayoffTargetEntity, AccountId, BudgetId, CreatedAt, Kind, MonthlyAmount, PayoffAmount, TargetMonth (+1 more)

### Community 125 - "AdminPanel"
Cohesion: 0.36
Nodes (7): AdminPanel(), action(), inviteUser(), saveEmailSettings(), saveMembership(), saveMode(), load()

### Community 126 - "CategoryGroupEntity"
Cohesion: 0.25
Nodes (8): CategoryGroupEntity, BudgetId, BudgetMonth, Categories, Id, IsArchived, Name, SortOrder

### Community 127 - "EntitlementSettings.tsx"
Cohesion: 0.28
Nodes (7): Entitlement, EntitlementSettings(), toggle(), featureLabel(), featureLabels, Plan, useEntitlementKeys()

### Community 128 - "AccountEntity"
Cohesion: 0.18
Nodes (3): CreditCardRules, Flows, CreditCardRulesTests

### Community 129 - "UserEntitlementOverrides"
Cohesion: 0.25
Nodes (6): `BudgetPlans`, 7. Database & API Changes, API requirements, Proposed entities, EntitlementEndpointExtensions, UserEntitlementOverrides()

### Community 132 - "AdminPanel.tsx"
Cohesion: 0.25
Nodes (7): AdminAudit, AdminSummary, AdminUser, EmailSettings, MembershipDraft, MembershipPlan, modes

### Community 133 - "Developer setup"
Cohesion: 0.22
Nodes (9): Build commands, Current development boundaries, Database lifecycle, Developer setup, First run, GitHub pull requests and releases, Prerequisites, Request flow (+1 more)

### Community 135 - "scripts"
Cohesion: 0.33
Nodes (6): scripts, build, dev, preview, test, test:watch

### Community 136 - "Effect"
Cohesion: 0.50
Nodes (4): Effect, Destructive, Read, Write

### Community 139 - "LongTermPlanningIncomeEntity"
Cohesion: 0.50
Nodes (4): LongTermPlanningIncomeEntity, BudgetId, ExpectedIncome, Month

### Community 140 - "LongTermPlanningStateEntity"
Cohesion: 0.50
Nodes (4): LongTermPlanningStateEntity, BudgetId, Id, Version

### Community 141 - "MembershipPlanEntity"
Cohesion: 0.50
Nodes (4): MembershipPlanEntity, Id, MaxActiveBudgets, Name

### Community 142 - "UserEntitlementOverrideEntity"
Cohesion: 0.50
Nodes (4): UserEntitlementOverrideEntity, EntitlementKey, IsEnabled, UserId

## Knowledge Gaps
- **429 isolated node(s):** `LocalTimeZone`, `UtcNow`, `Zone`, `Arguments`, `BudgetId` (+424 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1082 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **21 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Wishlist slice` connect `Wishlist slice` to `ModelBuilder`, `DateTimeOffset`, `.Project`, `LoanRules`, `Payees.tsx`, `Task`, `CancellationToken`, `Milestone 13 — Wishlist`, `.UpdateMonthAsync`, `WishlistRulesTests`, `WishlistService`, `AccountsPage.tsx`, `LoanAccountPage.tsx`, `parseMoney`?**
  _High betweenness centrality (0.189) - this node is a cross-community bridge._
- **What connects `LocalTimeZone`, `UtcNow`, `Zone` to the rest of the system?**
  _429 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `BudgetApplication` be split into smaller, more focused modules?**
  _Cohesion score 0.04864311315924219 - nodes in this community are weakly interconnected._
- **Why does `BudgetDbContext` connect `BudgetDbContext` to `CategoryTargetService`, `LongTermPlanningIncomeEntity`, `LongTermPlanningStateEntity`, `MembershipPlanEntity`, `UserEntitlementOverrideEntity`, `Task`, `MembershipEntitlements.cs`, `Authentication.cs`, `RegisterViewEntity`, `WishlistService`, `TestApp`, `ScheduleOccurrenceEntity`, `AdminAuditLogEntity`, `.GetYearAsync`, `WishlistItemEntity`, `ScheduleEntity`, `Milestone 13 — Wishlist`, `.UpdateMonthAsync`, `CategoryEntity`, `.MapAuthenticationEndpoints`, `AccountEmailSettingsService`, `.CallToolAsync`, `OpenBudget.Api`, `ModelBuilder`, `LoanTermsEntity`, `WishlistPurchaseApiTests`, `RegisterViewDto`, `BudgetEntity`, `AgentTokenAuthenticationHandler`, `TransactionImageService`, `BudgetMemberEntity`, `TransactionImageEntity`, `LedgerCommandEntity`, `PlanActionEntity`, `BudgetPlanEntity`, `BudgetInvitationEntity`, `SmtpSettingsEntity`, `WishlistSettingsEntity`, `CreditPayoffTargetEntity`, `CategoryGroupEntity`?**
  _High betweenness centrality (0.113) - this node is a cross-community bridge._
- **Should `DateTimeOffset` be split into smaller, more focused modules?**
  _Cohesion score 0.044486215538847115 - nodes in this community are weakly interconnected._
- **Why does `AccountsPage()` connect `AccountsPage.tsx` to `BudgetApplication`, `App.tsx`, `Payees.tsx`, `parseMoney`, `TransactionImages.tsx`, `vitest`, `LoanAccountPage.tsx`, `Wishlist slice`, `CreditPayoffTarget.tsx`?**
  _High betweenness centrality (0.098) - this node is a cross-community bridge._
- **Should `CategoryTargetService` be split into smaller, more focused modules?**
  _Cohesion score 0.083710407239819 - nodes in this community are weakly interconnected._