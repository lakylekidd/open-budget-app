# Graph Report - ynab-clone  (2026-10-30)

## Corpus Check
- 318 files · ~284,286 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 19 file(s) not represented in the graph (top: .css 10, .ttf 4, (none) 3)

## Summary
- 3500 nodes · 11355 edges · 163 communities (138 shown, 25 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 1119 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `ae58dfd9`
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
- App.tsx
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
- AdminPanel.tsx
- AccountsPage
- Testing and acceptance coverage
- Agent Instructions
- JsonElement
- docs/README.md
- TransactionImages.tsx
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
- vitest
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
- .OnModelCreating
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
- LongTermPlanningDto
- Milestone 13 — Wishlist
- BudgetCarryForward
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
- LedgerCommandEntity
- Authentication.cs
- Task
- ScheduledTransactions
- McpToolContext
- Current PostgreSQL schema
- LoanTermsEntity
- WishlistPurchaseApiTests
- .SaveChangesAsync
- TransactionImageApiTests
- Architecture and current status
- RegisterViewDto
- liveUpdates.test.tsx
- BudgetEntity
- AgentTokenAuthenticationHandler
- MCP server for personal AI agents
- TransactionImageService
- BudgetMemberEntity
- BudgetEntity
- TransactionImageEntity
- AgentTokens.tsx
- AuthGate
- .UpdateMonthAsync
- CategoryDto
- BudgetPlanEntity
- BudgetInvitationEntity
- SmtpSettingsEntity
- IReadOnlyDictionary
- App.test.tsx
- DateOnly
- LedgerContracts.cs
- ModelBuilder
- AccountEntity
- TransactionImagesDialog
- PlanTransactionList.tsx
- Theory
- Loan accounts
- Developer setup
- .Validate
- UserMembershipEntity
- WishlistContracts.cs
- .Validate
- .UpdateMonthAsync
- Milestone 3 — Goals, templates, and guided assignment
- devDependencies
- MoneyInput.tsx
- TimeProvider
- LedgerBalanceRow
- .EditMonthAsync
- CreditPayoffTargetEntity
- PayeeEntity
- CreditCardRulesTests
- CategoryTargetDefinition
- ReconciliationEntity
- SchedulePostingService
- AccountRegisterPresentation
- CategoryDto
- MembershipPlanEntitlementEntity
- UserEntitlementOverrideEntity
- Effect
- AccountNotes
- CategoryMoves
- MembershipEntitlements
- CreditAccounts
- WishlistPurchaseTransaction

## God Nodes (most connected - your core abstractions)
1. `OpenBudget.Api` - 119 edges
2. `BudgetDbContext` - 115 edges
3. `MutationDto` - 104 edges
4. `BudgetApplication()` - 102 edges
5. `LedgerService` - 97 edges
6. `OpenBudget.Api.Migrations` - 71 edges
7. `LedgerApiTests` - 70 edges
8. `BudgetDto` - 62 edges
9. `LedgerException` - 59 edges
10. `TransactionEntity` - 55 edges

## Surprising Connections (you probably didn't know these)
- `Target progress` --references--> `CategoryDto`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/BudgetContracts.cs
- `Importing an accepted normalized row` --references--> `TransactionRequest`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/LedgerContracts.cs
- `Buckets and Plan integration` --references--> `LedgerGate`  [INFERRED]
  docs/milestones/milestone-13-wishlist.md → src/OpenBudget.Api/LedgerGate.cs
- `Concurrency and persistence` --references--> `LedgerGate`  [INFERRED]
  docs/milestones/milestone-13-wishlist.md → src/OpenBudget.Api/LedgerGate.cs
- `Wishlist purchase transactions (2026-10-30)` --references--> `focusNextField()`  [INFERRED]
  docs/milestones/milestone-13-wishlist.md → src/OpenBudget.UI/src/ledger/focusNext.ts

## Import Cycles
- None detected.

## Communities (163 total, 25 thin omitted)

### Community 0 - "BudgetApplication"
Cohesion: 0.05
Nodes (42): BudgetApplication(), addFunds(), applyAssignmentChanges(), applyAutoAssignOption(), applyMove(), applyRoute(), cancelClosePlans(), canLeaveCurrentView() (+34 more)

### Community 1 - "DateTimeOffset"
Cohesion: 0.10
Nodes (7): Agent tokens and MCP audit, IdentityFoundation, AdminLifecycle, BudgetOwnershipAndMembership, AdminEmailSettings, BudgetSharingAndPlanHistory, AgentTokensAndAudit

### Community 2 - "CategoryTargetService"
Cohesion: 0.28
Nodes (4): CategoryTargetCommandResult, CategoryTargetResultDto, DeleteCategoryTargetRequest, CategoryTargetService

### Community 3 - "CategoryTarget.tsx"
Cohesion: 0.08
Nodes (35): BehaviorPicker(), choose(), keys(), CategoryTargetCard(), CategoryTargetEditor(), describe(), Money, MoneyField() (+27 more)

### Community 4 - "requestJson"
Cohesion: 0.11
Nodes (15): ManagePayees(), close(), done(), edit(), rule(), update(), PayeeList, PayeePicker() (+7 more)

### Community 5 - "http"
Cohesion: 0.13
Nodes (18): ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL, ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL, ASPNETCORE_ENVIRONMENT, DOTNET_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables (+10 more)

### Community 6 - "compilerOptions"
Cohesion: 0.11
Nodes (17): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+9 more)

### Community 7 - "http"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 8 - "App.tsx"
Cohesion: 0.06
Nodes (35): accountSectionId(), accountSections, App(), BudgetPlan, BudgetState, Category, CategoryGroup, CategoryTransactionDialog (+27 more)

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.07
Nodes (25): Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13), ModelContextProtocol.AspNetCore (2.2.0), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3) (+17 more)

### Community 12 - "Fact"
Cohesion: 0.19
Nodes (9): Milestone 2 acceptance map, MutationDto, ReconcileRequest, StatusRequest, UpdateAccountRequest, SavePayeesRequest, ScheduleActionRequest, ScheduleRequest (+1 more)

### Community 13 - "MembershipEntitlements.cs"
Cohesion: 0.12
Nodes (11): `BudgetPlans`, AdminAuditLogEntity, Action, ActorUserId, CreatedAt, Id, TargetUserId, EntitlementAuthorizationHandler (+3 more)

### Community 15 - "IResult"
Cohesion: 0.18
Nodes (4): BudgetOwnershipService, BudgetRequestContext, BudgetId, WishlistPurchases

### Community 16 - "TransactionEntity"
Cohesion: 0.09
Nodes (20): TransactionEntity, Account, AccountId, Amount, BudgetId, CategoryId, Date, FlagColor (+12 more)

### Community 17 - "CancellationToken"
Cohesion: 0.13
Nodes (6): LedgerService, LedgerException, StatusCode, Change, Entries, CreatedPairCategory

### Community 18 - "Authentication.cs"
Cohesion: 0.12
Nodes (19): Milestone 2a final verification (2026-10-23), ChangePasswordRequest, ForgotPasswordRequest, LoginRequest, OpenBudgetUser, CreatedAt, IsBootstrapAdministrator, IsSuspended (+11 more)

### Community 19 - "RegisterViewEntity"
Cohesion: 0.15
Nodes (13): RegisterViewEntity, BudgetId, ColumnWidthsJson, DatePreset, FromMonth, Scope, ShowFlags, ShowImages (+5 more)

### Community 20 - "WishlistRulesTests"
Cohesion: 0.18
Nodes (3): WishlistItemRequest, WishlistRules, WishlistRulesTests

### Community 21 - "AccountsPage.tsx"
Cohesion: 0.08
Nodes (30): DisclosureChevron(), Dialog, registerColumnOrder(), checking, savings, show(), accountFamily(), Accounts (+22 more)

### Community 23 - "routes.ts"
Cohesion: 0.09
Nodes (46): URL routing, account(), accountSlugFor(), accountSlugs(), AppPage, AppRoute, budgetIdFromLocation(), budgetKeyFor() (+38 more)

### Community 24 - "API reference"
Cohesion: 0.07
Nodes (30): API reference, Approval and attention, Assignment rollover and earlier-month edits, Authentication, Boundaries, Budget collaboration and Plan history, Cash accounts and reconciliation, Category targets (+22 more)

### Community 25 - "Wishlist slice"
Cohesion: 0.25
Nodes (4): Milestone 13 Wishlist acceptance map (2026-10-30), Wishlist purchase transactions, WishlistPurchaseRules, WishlistPurchaseRulesTests

### Community 26 - "BudgetDbContext"
Cohesion: 0.05
Nodes (38): 6. Real-time updates, BudgetDbContext, Accounts, ActiveBudgetId, AdminAuditLogs, ApplicationSettings, BudgetInvitations, BudgetMembers (+30 more)

### Community 27 - "TestApp"
Cohesion: 0.12
Nodes (14): Agent access (MCP) and live updates acceptance map, OwnedBudgetDto, SaveCategoryTargetRequest, CreateAgentTokenRequest, Program, CategoryTargetApiTests, McpServerApiTests, McpTestKit (+6 more)

### Community 28 - "CreditPayoffTarget.tsx"
Cohesion: 0.09
Nodes (24): Invitation, Person, Sharing, CreditPaymentDetails(), mutate(), card, progress, renderPanel() (+16 more)

### Community 30 - "BudgetChangeInterceptor"
Cohesion: 0.07
Nodes (12): BudgetChangeEvent, BudgetChangeInterceptor, BudgetChangePublisher, Subscription, BudgetId, Channel, Id, UserId (+4 more)

### Community 31 - "AdminPanel.tsx"
Cohesion: 0.05
Nodes (43): 10. Definition of Done, 1. Authentication & Login, 2. Permanent Administrator Accounts, 3. Registration & User Lifecycle, 4.1 Dashboard, 4.2 User Management, 4.3 Application Settings, 4. Separate Administration Panel (+35 more)

### Community 32 - "AccountsPage"
Cohesion: 0.08
Nodes (23): AccountsPage(), applyView(), beginColumnResize(), clearFilters(), finishColumnResize(), moveColumnResize(), mutate(), persistColumnWidths() (+15 more)

### Community 33 - "Testing and acceptance coverage"
Cohesion: 0.13
Nodes (15): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 verification (2026-10-22) (+7 more)

### Community 34 - "Agent Instructions"
Cohesion: 0.14
Nodes (13): Agent Instructions, Before making changes, Coding Guidelines, Delegating requests to worktrees and subagents, Development Principles, Documentation, Git, Project - OpenBudget (+5 more)

### Community 35 - "JsonElement"
Cohesion: 0.22
Nodes (3): McpResultExtensions, McpTestClient, Http

### Community 36 - "docs/README.md"
Cohesion: 0.15
Nodes (10): API, OpenBudget API and local services, Coding conventions, Credit card payment Activity, Calculations and month lifecycle, Data model, Documentation, Highlights (+2 more)

### Community 37 - "TransactionImages.tsx"
Cohesion: 0.10
Nodes (22): ApiError, Entry, LedgerDialog(), Category, Month, NewCategoryDialog(), save(), close (+14 more)

### Community 38 - "AgentTokenEntity"
Cohesion: 0.12
Nodes (18): AgentTokenEntity, Budgets, CreatedAt, ExpiresAt, Id, LastUsedAt, Name, Permission (+10 more)

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.17
Nodes (11): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Plan interaction extension (2026-10-22) (+3 more)

### Community 41 - "WishlistPage.tsx"
Cohesion: 0.06
Nodes (54): BucketPanel(), DragSource, DropTarget, History(), Money, Props, BucketEditing, BucketNameInput() (+46 more)

### Community 42 - "LongTermPlanning.tsx"
Cohesion: 0.06
Nodes (51): Agent tokens, MCP and live events, change(), openUpcoming(), ActivityDialog, ActivityKind, ActivitySelection, actualLabel(), actualMetrics (+43 more)

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.22
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

### Community 44 - "MigrationBuilder"
Cohesion: 0.08
Nodes (9): InitialCreate, BudgetCore, NamedBudget, LongTermPlanning, AccountLifecycle, BudgetFinancialIsolation, RegisterColumnWidths, TransactionSplitDetails (+1 more)

### Community 45 - "system_text_json"
Cohesion: 0.05
Nodes (9): OpenBudget.Api.Realtime, OpenBudget.Api.Mcp, BudgetLimitDto, DeleteOwnedBudgetRequest, RenameOwnedBudgetRequest, McpServerText, AgentTokenClaims, Route (+1 more)

### Community 46 - "Milestone 4 — Scheduled transactions and cash-flow awareness"
Cohesion: 0.29
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 4 — Scheduled transactions and cash-flow awareness, Product references, User value, Vertical slice

### Community 47 - "RenameFlagsRequest"
Cohesion: 0.25
Nodes (7): 1. Add the GitHub workflows, 2. Configure Coolify, 3. Create the Coolify webhook, 4. Add GitHub secrets, 5. Publish a release, GitHub Release to Coolify, Useful checks

### Community 48 - "vitest"
Cohesion: 0.05
Nodes (31): jsdom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript, vite (+23 more)

### Community 50 - ".GetYearAsync"
Cohesion: 0.09
Nodes (18): Implemented user path, LongTermPlanningCategoryEntity, BudgetId, CategoryId, Month, PlannedBudgetOverride, PlannedExpenses, LongTermPlanningCategoryDto (+10 more)

### Community 51 - "WishlistItemEntity"
Cohesion: 0.09
Nodes (21): WishlistItemEntity, BucketId, BudgetId, Cost, CreatedAt, Description, Id, Name (+13 more)

### Community 52 - "SafeTransactionImageUrlFetcher"
Cohesion: 0.13
Nodes (11): ImageFetchException, ITransactionImageUrlFetcher, SafeTransactionImageUrlFetcher, TransactionImageUrlFetcherOptions, AllowNonDefaultPort, MaxRedirects, Timeout, StubFetcher (+3 more)

### Community 54 - "LoanAccountPage.tsx"
Cohesion: 0.11
Nodes (33): ReconcileForm(), formatMoney(), parseMoney(), ActivityList(), CategoryActions, Chevron(), ContractSchedule(), ContractSummary() (+25 more)

### Community 55 - "WishlistApiTests"
Cohesion: 0.44
Nodes (4): WishlistBucketRequest, WishlistOverviewDto, WishlistVersionRequest, WishlistApiTests

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
Nodes (50): Mortgages, loans and contracts, submit(), AccountForm(), TransferForm(), creditPaymentGroupId, LoanAccountType, loanAccountTypes, today() (+42 more)

### Community 61 - "Milestone 9 — Cross-device experience and offline resilience"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 9 — Cross-device experience and offline resilience, Product reference, User value, Vertical slice

### Community 62 - "Milestone 10 — Optional linked-account import"
Cohesion: 0.25
Nodes (7): Acceptance criteria, Dependencies and release gate, Goal, Milestone 10 — Optional linked-account import, Product references, User value, Vertical slice

### Community 63 - "IReadOnlyDictionary"
Cohesion: 0.13
Nodes (8): MCP server and live updates, Category targets, 5. Security model (ISO 27001 mapping), Residual risks, LedgerGate, ApiDispatcher, BodyDetection, CanHaveBody

### Community 64 - "Milestone: Annual Long-Term Budget Planning"
Cohesion: 0.29
Nodes (6): Acceptance Criteria, Budget propagation rule, Core behavior, Goal, Implementation notes, Milestone: Annual Long-Term Budget Planning

### Community 65 - "YNAB Web app: reverse-engineering notes"
Cohesion: 0.29
Nodes (6): Account-linking code observed in the bundle, Evidence and scope, Frontend structure, Lazy modules and browser storage, Limits and follow-up, YNAB Web app: reverse-engineering notes

### Community 66 - ".Project"
Cohesion: 0.09
Nodes (4): ContractTermsRequest, Flows, LoanRules, LoanRulesTests

### Community 67 - "Milestone 0 — Local application foundation"
Cohesion: 0.33
Nodes (5): Acceptance criteria, Goal, Milestone 0 — Local application foundation, User and developer outcome, Vertical slice

### Community 68 - "WishlistApp.test.tsx"
Cohesion: 0.18
Nodes (7): Access, checking, json(), month(), progress, purchaseEntry, withAccounts()

### Community 71 - ".OnModelCreating"
Cohesion: 0.10
Nodes (20): ApplicationSettingEntity, Key, Value, LongTermPlanningIncomeEntity, BudgetId, ExpectedIncome, Month, LongTermPlanningStateEntity (+12 more)

### Community 73 - ".Validate"
Cohesion: 0.12
Nodes (5): Transaction image attachments (2026-10-30), SsrfAddressPolicy, ImageFileNameSanitizer, TestImages, TransactionImageValidationTests

### Community 75 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

### Community 76 - "ScheduleEntity"
Cohesion: 0.05
Nodes (43): TransactionDto, SplitEntity, Amount, CategoryId, Memo, Payee, Position, TransactionId (+35 more)

### Community 77 - "Payees.tsx"
Cohesion: 0.07
Nodes (46): Cash ledger slice, Wishlist slice, react, react-dom, editor(), CategoryOption, isCreditAccount(), loadCategories() (+38 more)

### Community 78 - "Documentation"
Cohesion: 0.67
Nodes (3): Current release shape, Documentation, Documents

### Community 79 - "List"
Cohesion: 0.18
Nodes (10): FlagDto, FlagPaletteDto, RenameFlagsRequest, SetFlagRequest, PayeeDto, PayeeRuleDto, PayeeRules, PayeesDto (+2 more)

### Community 81 - "vitest"
Cohesion: 0.09
Nodes (32): AccountHeader(), balanceSummary(), ClearedIndicator(), ClearedSymbol(), reconciliationText(), RegisterIcon(), account, entry (+24 more)

### Community 82 - "LongTermPlanningDto"
Cohesion: 0.18
Nodes (10): Milestone 11 acceptance map, BudgetUpdateDto, CategoryGroupUpdateDto, CategoryUpdateDto, CreateOwnedBudgetRequest, LongTermPlanningDto, BudgetOwnershipApiTests, LongTermPlanningApiTests (+2 more)

### Community 83 - "Milestone 13 — Wishlist"
Cohesion: 0.12
Nodes (14): Owned budgets and memberships, Acceptance criteria, Buckets and Plan integration, Concurrency and persistence, Dependencies, Deviations from the original brief, Enable/disable semantics, Entitlement and enablement (+6 more)

### Community 84 - "BudgetCarryForward"
Cohesion: 0.12
Nodes (8): Backdated assignment corrections, Credit accounts, Payee management, Transaction images, BudgetCarryForward, PayeeManagement, CreditPayoffTargets, TransactionImages

### Community 85 - ".HandleAuthenticateAsync"
Cohesion: 0.22
Nodes (3): AgentTokenSecrets, GeneratedToken, AgentTokenSecretsTests

### Community 86 - "Milestone 12 — Icons and suggested icons"
Cohesion: 0.22
Nodes (9): Acceptance criteria, Dependencies, Goal, Milestone 12 — Icons and suggested icons, Open questions, Product references, User value, Vertical slice (+1 more)

### Community 87 - "CategoryEntity"
Cohesion: 0.12
Nodes (17): CategoryEntity, Activity, Assigned, Available, BudgetId, BudgetMonth, CreditOverspending, GroupId (+9 more)

### Community 88 - "IReadOnlyList"
Cohesion: 0.54
Nodes (3): PlanActionDraft, PlanActionRules, PlanActionRulesTests

### Community 89 - "AccountEmailContent"
Cohesion: 0.26
Nodes (6): AccountEmailContent, AccountEmailTemplates, TestAccountEmailSender, FailSending, IsConfigured, Messages

### Community 90 - "McpToolCatalog"
Cohesion: 0.17
Nodes (6): Wishlist, S, McpToolCatalog, All, CommandIdArg, LedgerVersionArg

### Community 91 - "AccountEmailSettingsService"
Cohesion: 0.15
Nodes (7): AccountEmailSettingsService, IAccountEmailSender, SendTestEmailRequest, SmtpAccountEmailSender, SmtpConnectionSettings, SmtpSettingsDto, UpdateSmtpSettingsRequest

### Community 92 - ".CallToolAsync"
Cohesion: 0.09
Nodes (9): AgentPermission, Admin, Budget, Reporting, McpToolHandlers, AgentTokenIdentity, McpPermissions, McpToolDefinition (+1 more)

### Community 93 - "OpenBudget.Api"
Cohesion: 0.13
Nodes (3): OpenBudget.Tests, OpenBudget.Api, OpenBudget.UnitTests

### Community 94 - "CategoryTargetEntity"
Cohesion: 0.12
Nodes (16): CategoryTargetEntity, Amount, Behavior, BudgetId, CategoryId, CreatedAt, DayOfMonth, DueDate (+8 more)

### Community 95 - "LedgerContracts.cs"
Cohesion: 0.31
Nodes (3): MembershipEntitlementApiTests, PostgresFixture, TransactionSelectionApiTests

### Community 96 - "LedgerCommandEntity"
Cohesion: 0.10
Nodes (19): FlagDefinitionEntity, BudgetId, Color, Name, LedgerCommandEntity, AfterJson, BeforeJson, BudgetId (+11 more)

### Community 97 - "Authentication.cs"
Cohesion: 0.21
Nodes (9): AdminAuditDto, AdminDashboardDto, AdminInviteUserRequest, AdminMembershipCountDto, AdminMembershipRequest, AdminRegistrationDto, AdminUserDto, AuthenticationSetup (+1 more)

### Community 98 - "Task"
Cohesion: 0.20
Nodes (18): CreateAccountRequest, TransferRequest, ContractDto, ContractScheduleItemDto, LoanActivityItemDto, LoanActivityMonthDto, LoanBalancePointDto, LoanBalanceRequest (+10 more)

### Community 99 - "ScheduledTransactions"
Cohesion: 0.14
Nodes (5): Limitations and direction, Scheduling and flag storage, ScheduledTransactions, TransactionFlags, ScheduledApproval

### Community 100 - "McpToolContext"
Cohesion: 0.18
Nodes (5): McpToolContext, Arguments, BudgetId, CancellationToken, Token

### Community 101 - "Current PostgreSQL schema"
Cohesion: 0.20
Nodes (6): `Budgets`, `CategoryGroups` and `Categories`, Current PostgreSQL schema, Identity and account lifecycle, Long-Term Planning, Register view preferences

### Community 102 - "LoanTermsEntity"
Cohesion: 0.09
Nodes (22): LoanTermsEntity, AccountId, BalanceConfirmedAt, BudgetId, Contract, ContractMonths, ContractStartDate, CreatedAt (+14 more)

### Community 104 - ".SaveChangesAsync"
Cohesion: 0.31
Nodes (6): BudgetInvitationDto, BudgetPersonDto, BudgetSharingDto, BudgetSharingService, InviteBudgetMemberRequest, PlanActionDto

### Community 105 - "TransactionImageApiTests"
Cohesion: 0.42
Nodes (6): AccountsDto, AuditPageDto, RegisterDto, DeleteTransactionImageRequest, TransactionImageListDto, TransactionImageApiTests

### Community 106 - "Architecture and current status"
Cohesion: 0.29
Nodes (7): Architecture and current status, Category targets, Design limits to address, Runtime topology, Security model, Technologies, Transaction image attachments

### Community 107 - "RegisterViewDto"
Cohesion: 0.29
Nodes (3): RegisterViewDto, RegisterViewService, RegisterViewTests

### Community 108 - "liveUpdates.test.tsx"
Cohesion: 0.30
Nodes (6): BudgetChange, LiveUpdateOptions, reconnectDelay(), FakeEventSource, setup(), useBudgetLiveUpdates()

### Community 109 - "BudgetEntity"
Cohesion: 0.16
Nodes (10): WishlistBucketEntity, BudgetId, CreatedAt, Id, IsDeleted, Name, SortOrder, UpdatedAt (+2 more)

### Community 110 - "AgentTokenAuthenticationHandler"
Cohesion: 0.13
Nodes (3): AgentAccessSetup, AgentTokenAuthenticationHandler, BudgetEventsEndpoint

### Community 111 - "MCP server for personal AI agents"
Cohesion: 0.33
Nodes (6): 1. Create an agent token, 2. Connect a client, 3. Using the tools, 4. Permission matrix, 7. Limitations, MCP server for personal AI agents

### Community 112 - "TransactionImageService"
Cohesion: 0.11
Nodes (11): Storage, AuditDto, TransactionImageEndpoints, ImportTransactionImageUrlRequest, TransactionImageContent, TransactionImageDto, TransactionImageService, TransactionImageUpload (+3 more)

### Community 113 - "BudgetMemberEntity"
Cohesion: 0.33
Nodes (6): BudgetMemberEntity, BudgetId, InvitedByUserId, JoinedAt, Role, UserId

### Community 114 - "BudgetEntity"
Cohesion: 0.11
Nodes (18): BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month, Note, OpeningReadyToAssign (+10 more)

### Community 115 - "TransactionImageEntity"
Cohesion: 0.15
Nodes (13): TransactionImageEntity, BudgetId, ContentType, CreatedAt, CreatedByUserId, FileName, Height, Id (+5 more)

### Community 116 - "AgentTokens.tsx"
Cohesion: 0.13
Nodes (10): Activity, AgentToken, AgentTokens(), BudgetOption, formatDate(), Permission, permissionHelp, readJson() (+2 more)

### Community 117 - "AuthGate"
Cohesion: 0.26
Nodes (9): AuthBrand(), AuthGate(), login(), refreshSession(), Session, isSafeReturnPath(), publicPaths, rememberReturnPath() (+1 more)

### Community 118 - ".UpdateMonthAsync"
Cohesion: 0.18
Nodes (10): PlanActionEntity, ActorUserId, Amount, BudgetId, CreatedAt, FromCategoryName, Id, Kind (+2 more)

### Community 119 - "CategoryDto"
Cohesion: 0.16
Nodes (12): BudgetPlanDto, BudgetPlanUpdateDto, BudgetPlanUpdateResult, BudgetPlanEntity, CreatedAt, Currency, Id, IsArchived (+4 more)

### Community 120 - "BudgetPlanEntity"
Cohesion: 0.14
Nodes (14): AgentAccessModel, AgentAuditLogEntity, BudgetId, CreatedAt, Id, Outcome, StatusCode, Success (+6 more)

### Community 121 - "BudgetInvitationEntity"
Cohesion: 0.29
Nodes (7): BudgetInvitationEntity, BudgetId, CreatedAt, Email, Id, InvitedByUserId, NormalizedEmail

### Community 122 - "SmtpSettingsEntity"
Cohesion: 0.22
Nodes (9): SmtpSettingsEntity, EnableSsl, EncryptedPassword, From, Host, Id, Port, UpdatedAt (+1 more)

### Community 123 - "IReadOnlyDictionary"
Cohesion: 0.15
Nodes (7): Changes, HasChanges, CardFlow, PaymentsAndReturns, TotalActivity, CreditCardRules, Flows

### Community 124 - "App.test.tsx"
Cohesion: 0.15
Nodes (7): URL routing (2026-10-30), budget(), Month, next(), saved(), weeklyProgress, weeklyTarget

### Community 125 - "DateOnly"
Cohesion: 0.14
Nodes (7): ContractItem, Total, ContractPosition, Remaining, CashLedger, MembershipSubscriptionMetadata, CategoryTargets

### Community 126 - "LedgerContracts.cs"
Cohesion: 0.15
Nodes (12): AccountDto, CreditActivityDto, CreditPayoffDto, CreditPayoffProgressDto, CreditPayoffTargetDto, CreditSummaryDto, DeleteCreditPayoffTargetRequest, DeleteTransactionRequest (+4 more)

### Community 127 - "ModelBuilder"
Cohesion: 0.14
Nodes (3): AccountViewOptions, BudgetScopedMonths, BudgetDbContextModelSnapshot

### Community 128 - "AccountEntity"
Cohesion: 0.13
Nodes (10): AccountEntity, AccountType, BudgetId, Id, IsClosed, IsFavorite, Name, Notes (+2 more)

### Community 129 - "TransactionImagesDialog"
Cohesion: 0.39
Nodes (9): TransactionImagesDialog(), addFiles(), commandFor(), drop(), go(), importUrl(), keyDown(), remove() (+1 more)

### Community 130 - "PlanTransactionList.tsx"
Cohesion: 0.22
Nodes (11): CreditActivitySummary, money, place(), Placement, PlanTransactionItem, PlanTransactionList(), signedMoney, accounts (+3 more)

### Community 131 - "Theory"
Cohesion: 0.11
Nodes (4): EntitlementKeyTests, LedgerRulesTests, PayeeRulesTests, RecurrenceTests

### Community 132 - "Loan accounts"
Cohesion: 0.18
Nodes (5): Current feature inventory, Loan accounts, LoanAccounts, LoanOptionalPairing, LoanTerms

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
Cohesion: 0.27
Nodes (8): Projection, NeverPaidOff, WishlistBucketDto, WishlistBucketOrderRequest, WishlistItemDto, WishlistItemOrderRequest, WishlistProgressDto, WishlistPurchaseDto

### Community 139 - "Milestone 3 — Goals, templates, and guided assignment"
Cohesion: 0.25
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented: category targets (2026-10-30), Milestone 3 — Goals, templates, and guided assignment, Product references, User value, Vertical slice

### Community 140 - "devDependencies"
Cohesion: 0.18
Nodes (11): devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript (+3 more)

### Community 141 - "MoneyInput.tsx"
Cohesion: 0.31
Nodes (9): evaluateMoneyExpression(), factor(), number(), product(), sum(), hasMoneyOperator(), normalize(), CalculatorIcon() (+1 more)

### Community 142 - "TimeProvider"
Cohesion: 0.20
Nodes (4): TestClock, LocalTimeZone, UtcNow, Zone

### Community 143 - "LedgerBalanceRow"
Cohesion: 0.67
Nodes (3): LedgerBalanceRow, Balance, Id

### Community 144 - ".EditMonthAsync"
Cohesion: 0.38
Nodes (4): ApiResponse, Error, IsSuccess, McpToolOutcome

### Community 145 - "CreditPayoffTargetEntity"
Cohesion: 0.22
Nodes (9): CreditPayoffTargetEntity, AccountId, BudgetId, CreatedAt, Kind, MonthlyAmount, PayoffAmount, TargetMonth (+1 more)

### Community 146 - "PayeeEntity"
Cohesion: 0.22
Nodes (9): PayeeEntity, BudgetId, CategoryMode, DefaultCategoryId, Id, IsHidden, Name, NormalizedName (+1 more)

### Community 148 - "CategoryTargetDefinition"
Cohesion: 0.29
Nodes (5): CategoryTargetDefinition, Context, MonthEnd, MonthIndex, StartIndex

### Community 149 - "ReconciliationEntity"
Cohesion: 0.29
Nodes (7): ReconciliationEntity, AccountId, BudgetId, CreatedAt, Id, IsInvalidated, StatementBalance

### Community 152 - "CategoryDto"
Cohesion: 0.60
Nodes (3): CategoryDto, CategoryGroupDto, CategoryTargetDto

### Community 154 - "MembershipPlanEntitlementEntity"
Cohesion: 0.50
Nodes (4): MembershipPlanEntitlementEntity, EntitlementKey, IsEnabled, MembershipPlanId

### Community 155 - "UserEntitlementOverrideEntity"
Cohesion: 0.50
Nodes (4): UserEntitlementOverrideEntity, EntitlementKey, IsEnabled, UserId

### Community 156 - "Effect"
Cohesion: 0.50
Nodes (4): Effect, Destructive, Read, Write

## Knowledge Gaps
- **431 isolated node(s):** `IsSuspended`, `IsBootstrapAdministrator`, `RequiresApproval`, `CreatedAt`, `LastLoginAt` (+426 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1075 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **25 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Wishlist slice` connect `Payees.tsx` to `AccountsPage`, `.Project`, `WishlistPage.tsx`, `Architecture and current status`, `.UpdateMonthAsync`, `CancellationToken`, `Milestone 13 — Wishlist`, `WishlistItemEntity`, `WishlistRulesTests`, `WishlistService`, `LoanAccountPage.tsx`, `Wishlist slice`, `parseMoney`, `Task`, `IReadOnlyDictionary`?**
  _High betweenness centrality (0.177) - this node is a cross-community bridge._
- **What connects `IsSuspended`, `IsBootstrapAdministrator`, `RequiresApproval` to the rest of the system?**
  _431 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `BudgetApplication` be split into smaller, more focused modules?**
  _Cohesion score 0.046532045654082525 - nodes in this community are weakly interconnected._
- **Why does `BudgetDbContext` connect `BudgetDbContext` to `AccountEntity`, `CategoryTargetService`, `UserMembershipEntity`, `.UpdateMonthAsync`, `MembershipEntitlements.cs`, `TimeProvider`, `IResult`, `TransactionEntity`, `CreditPayoffTargetEntity`, `Authentication.cs`, `PayeeEntity`, `RegisterViewEntity`, `ReconciliationEntity`, `CancellationToken`, `WishlistService`, `MembershipPlanEntitlementEntity`, `UserEntitlementOverrideEntity`, `TestApp`, `Task`, `AgentTokenEntity`, `.GetYearAsync`, `WishlistItemEntity`, `WishlistApiTests`, `IReadOnlyDictionary`, `.OnModelCreating`, `ScheduleEntity`, `CategoryEntity`, `AccountEmailSettingsService`, `.CallToolAsync`, `OpenBudget.Api`, `CategoryTargetEntity`, `LedgerCommandEntity`, `Authentication.cs`, `LoanTermsEntity`, `WishlistPurchaseApiTests`, `.SaveChangesAsync`, `RegisterViewDto`, `BudgetEntity`, `AgentTokenAuthenticationHandler`, `TransactionImageService`, `BudgetMemberEntity`, `BudgetEntity`, `TransactionImageEntity`, `.UpdateMonthAsync`, `CategoryDto`, `BudgetInvitationEntity`, `SmtpSettingsEntity`, `IReadOnlyDictionary`?**
  _High betweenness centrality (0.126) - this node is a cross-community bridge._
- **Should `DateTimeOffset` be split into smaller, more focused modules?**
  _Cohesion score 0.09538461538461539 - nodes in this community are weakly interconnected._
- **Why does `MCP server and live updates` connect `IReadOnlyDictionary` to `McpToolCatalog`, `Architecture and current status`, `liveUpdates.test.tsx`, `AgentTokenAuthenticationHandler`, `BudgetDbContext`, `.CallToolAsync`, `BudgetChangeInterceptor`?**
  _High betweenness centrality (0.087) - this node is a cross-community bridge._
- **Should `CategoryTarget.tsx` be split into smaller, more focused modules?**
  _Cohesion score 0.07770582793709528 - nodes in this community are weakly interconnected._