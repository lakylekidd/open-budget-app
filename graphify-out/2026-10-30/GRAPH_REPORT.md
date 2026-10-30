# Graph Report - ynab-clone  (2026-10-30)

## Corpus Check
- 313 files · ~272,535 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 18 file(s) not represented in the graph (top: .css 9, .ttf 4, (none) 3)

## Summary
- 3490 nodes · 11044 edges · 143 communities (118 shown, 25 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 1063 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `91127311`
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
- App.tsx
- OpenBudget.IntegrationTests.csproj
- Fact
- MembershipEntitlements.cs
- PostgresFixture.cs
- Task
- List
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
- .CreateAsync
- Milestone 1 — Reliable budget core and month lifecycle
- Milestone 2 — Accounts and transaction register
- WishlistPage.tsx
- BudgetDbContext.cs
- Milestone 7 — File import, matching, and portable data
- MigrationBuilder
- system_text_json
- Milestone 4 — Scheduled transactions and cash-flow awareness
- RenameFlagsRequest
- vitest
- microsoft_entityframeworkcore
- .Calculate
- WishlistService
- SafeTransactionImageUrlFetcher
- .Calculate
- LoanAccountPage.tsx
- TestApp
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
- PayeeRuleDto
- .Calculate
- AgentTokens.tsx
- McpToolDefinition
- LedgerGate
- PlanActionEntity
- Theory
- Milestone 12 — Icons and suggested icons
- CategoryEntity
- .UpdateMonthAsync
- AccountsPage
- JsonObject
- SmtpSettingsEntity
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
- LedgerContracts.cs
- TransactionImageApiTests
- CategoryTargetEntity
- App.test.tsx
- .BudgetId
- Current PostgreSQL schema
- JsonElement
- TransactionImageService
- ImageMetadataStripper
- TransactionImagesDialog
- devDependencies
- AppRouting.test.tsx
- liveUpdates.test.tsx
- BudgetInvitationEntity
- AccountEntity
- .ToDtoAsync
- evaluateMoneyExpression
- MCP server for personal AI agents
- WishlistApp.test.tsx
- InlineTransactionEditor.tsx
- IReadOnlyList
- LedgerRulesTests
- .RenameFlagsAsync
- Developer setup
- UserMembershipEntity
- ModelBuilder
- .Describe
- .Validate
- CreditPayoffTargetEntity
- Milestone 3 — Goals, templates, and guided assignment
- AdminAuditLogEntity
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
- `Target progress` --references--> `CategoryDto`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/BudgetContracts.cs
- `Importing an accepted normalized row` --references--> `TransactionRequest`  [INFERRED]
  docs/api.md → src/OpenBudget.Api/LedgerContracts.cs
- `Current feature inventory` --references--> `LoanTerms`  [INFERRED]
  docs/architecture.md → src/OpenBudget.UI/src/ledger/loans.ts
- `Enable/disable semantics` --references--> `Wishlist`  [INFERRED]
  docs/milestones/milestone-13-wishlist.md → src/OpenBudget.Api/Migrations/20261030124559_Wishlist.Designer.cs
- `Entitlement and enablement` --references--> `Wishlist`  [INFERRED]
  docs/milestones/milestone-13-wishlist.md → src/OpenBudget.Api/Migrations/20261030124559_Wishlist.Designer.cs

## Import Cycles
- None detected.

## Communities (143 total, 25 thin omitted)

### Community 0 - "BudgetApplication"
Cohesion: 0.05
Nodes (37): BudgetApplication(), addFunds(), applyAssignmentChanges(), applyAutoAssignOption(), applyMove(), applyRoute(), cancelClosePlans(), canLeaveCurrentView() (+29 more)

### Community 1 - "DateTimeOffset"
Cohesion: 0.06
Nodes (16): BudgetInvitationDto, BudgetPersonDto, BudgetSharingDto, InviteBudgetMemberRequest, PlanActionDto, IdentityFoundation, AdminLifecycle, BudgetOwnershipAndMembership (+8 more)

### Community 2 - "CategoryTargetService"
Cohesion: 0.24
Nodes (5): CategoryTargetCommandResult, CategoryTargetDto, CategoryTargetResultDto, DeleteCategoryTargetRequest, CategoryTargetService

### Community 3 - "CategoryTarget.tsx"
Cohesion: 0.08
Nodes (34): BehaviorPicker(), choose(), keys(), CategoryTargetCard(), CategoryTargetEditor(), describe(), Money, MoneyField() (+26 more)

### Community 4 - "api.ts"
Cohesion: 0.09
Nodes (25): Dialog, checking, savings, Accounts, accountTypeSections, Audit, Entry, Register (+17 more)

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
Cohesion: 0.07
Nodes (31): accountSections, App(), BudgetPlan, BudgetState, Category, CategoryGroup, CategoryTransactionDialog, currency (+23 more)

### Community 9 - "OpenBudget.IntegrationTests.csproj"
Cohesion: 0.08
Nodes (24): net10.0, Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), coverlet.collector (6.0.4), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.13), Microsoft.AspNetCore.Mvc.Testing (10.0.13), Microsoft.EntityFrameworkCore.Design (10.0.13), Microsoft.EntityFrameworkCore.Relational (10.0.13) (+16 more)

### Community 12 - "Fact"
Cohesion: 0.19
Nodes (11): Milestone 2 acceptance map, MutationDto, ReconcileRequest, SplitDto, StatusRequest, TransferRequest, UpdateAccountRequest, SavePayeesRequest (+3 more)

### Community 13 - "MembershipEntitlements.cs"
Cohesion: 0.11
Nodes (13): `BudgetPlans`, MembershipPlanEntitlementEntity, EntitlementKey, IsEnabled, MembershipPlanId, UserEntitlementOverrideEntity, EntitlementKey, IsEnabled (+5 more)

### Community 15 - "Task"
Cohesion: 0.09
Nodes (14): IAccountEmailSender, SmtpAccountEmailSender, BudgetOwnershipService, OwnedBudgetDto, BudgetRequestContext, BudgetId, BudgetSharingService, LedgerProjection (+6 more)

### Community 16 - "List"
Cohesion: 0.19
Nodes (8): BudgetUpdateDto, CategoryGroupUpdateDto, CategoryUpdateDto, CreateOwnedBudgetRequest, CreateAccountRequest, TransactionDto, LongTermPlanningActivityDto, BudgetOwnershipApiTests

### Community 17 - "CancellationToken"
Cohesion: 0.07
Nodes (9): LedgerService, LedgerException, StatusCode, LedgerRules, Change, LedgerService, Entries, CreatedPairCategory (+1 more)

### Community 18 - "Authentication.cs"
Cohesion: 0.05
Nodes (36): Milestone 2a final verification (2026-10-23), AccountEmailContent, AccountEmailSettingsService, AccountEmailTemplates, SendTestEmailRequest, SmtpConnectionSettings, SmtpSettingsDto, UpdateSmtpSettingsRequest (+28 more)

### Community 19 - "RegisterViewEntity"
Cohesion: 0.12
Nodes (15): RegisterViewDto, RegisterViewEntity, BudgetId, ColumnWidthsJson, DatePreset, FromMonth, Scope, ShowFlags (+7 more)

### Community 20 - "TransactionEntity"
Cohesion: 0.06
Nodes (35): PayeeEntity, BudgetId, CategoryMode, DefaultCategoryId, Id, IsHidden, Name, NormalizedName (+27 more)

### Community 21 - "WishlistRulesTests"
Cohesion: 0.10
Nodes (12): Milestone 13 Wishlist acceptance map (2026-10-30), Wishlist purchase transactions, WishlistBucketEntity, BudgetId, CreatedAt, Id, IsDeleted, Name (+4 more)

### Community 22 - "IReadOnlyDictionary"
Cohesion: 0.18
Nodes (4): Changes, HasChanges, CreditCardRules, Flows

### Community 23 - "routes.ts"
Cohesion: 0.08
Nodes (46): AuthBrand(), AuthGate(), login(), refreshSession(), Session, accountSlugFor(), accountSlugs(), AppPage (+38 more)

### Community 24 - "API reference"
Cohesion: 0.07
Nodes (27): API reference, Approval and attention, Assignment rollover and earlier-month edits, Authentication, Boundaries, Budget collaboration and Plan history, Cash accounts and reconciliation, Credit accounts and card payments (+19 more)

### Community 25 - "WishlistItemEntity"
Cohesion: 0.11
Nodes (17): WishlistItemEntity, BucketId, BudgetId, Cost, CreatedAt, Description, Id, Name (+9 more)

### Community 26 - "BudgetDbContext"
Cohesion: 0.04
Nodes (38): MCP server and live updates, BudgetDbContext, Accounts, ActiveBudgetId, AdminAuditLogs, ApplicationSettings, BudgetInvitations, BudgetMembers (+30 more)

### Community 27 - "Milestone 2a — Multi-User Authentication & Administration"
Cohesion: 0.10
Nodes (20): 10. Definition of Done, 1. Authentication & Login, 2. Permanent Administrator Accounts, 3. Registration & User Lifecycle, 4.1 Dashboard, 4.2 User Management, 4.3 Application Settings, 4. Separate Administration Panel (+12 more)

### Community 28 - "CreditPayoffTarget.tsx"
Cohesion: 0.08
Nodes (27): Invitation, Person, Sharing, CreditPaymentDetails(), mutate(), card, progress, renderPanel() (+19 more)

### Community 30 - "BudgetChangeInterceptor"
Cohesion: 0.05
Nodes (22): LedgerCommandEntity, AfterJson, BeforeJson, BudgetId, CreatedAt, Fingerprint, Id, Kind (+14 more)

### Community 31 - "MembershipEntitlements.cs"
Cohesion: 0.12
Nodes (22): AdminAudit, AdminPanel(), action(), inviteUser(), saveEmailSettings(), saveMembership(), saveMode(), AdminSummary (+14 more)

### Community 32 - "BudgetContracts.cs"
Cohesion: 0.23
Nodes (5): BudgetPlanDto, BudgetPlanUpdateDto, BudgetPlanUpdateResult, CategoryDto, CategoryGroupDto

### Community 33 - "Testing and acceptance coverage"
Cohesion: 0.12
Nodes (16): Account register presentation verification (2026-10-21), Account View Options verification (2026-10-21), Assignment rollover regression verification (2026-10-21), Commands and isolation, Floating selection bar and actions (2026-10-22), Grouped category picker verification (2026-10-21), Inline register, scheduling, account editing and approval verification (2026-10-21), Milestone 11 verification (2026-10-22) (+8 more)

### Community 34 - "Agent Instructions"
Cohesion: 0.15
Nodes (12): Agent Instructions, Before making changes, Coding Guidelines, Development Principles, Documentation, Git, Project - OpenBudget, Roadmap (+4 more)

### Community 35 - "DateOnly"
Cohesion: 0.08
Nodes (9): ContractItem, Total, ContractPosition, Remaining, CashLedger, MembershipSubscriptionMetadata, CategoryTargets, LoanAccounts (+1 more)

### Community 36 - "docs/README.md"
Cohesion: 0.20
Nodes (5): Coding conventions, Documentation, Highlights, Project status, Quick start

### Community 38 - ".CreateAsync"
Cohesion: 0.09
Nodes (11): AgentAccessSetup, AgentTokenIdentity, AgentActivityDto, AgentTokenBudgetDto, AgentTokenDto, AgentTokenService, CreatedAgentTokenDto, McpBudgetIds (+3 more)

### Community 39 - "Milestone 1 — Reliable budget core and month lifecycle"
Cohesion: 0.20
Nodes (9): Acceptance criteria, Assignment rollover correction (2026-10-21), Delivered in this slice, Dependencies, Goal, Milestone 1 — Reliable budget core and month lifecycle, Product references, User value (+1 more)

### Community 40 - "Milestone 2 — Accounts and transaction register"
Cohesion: 0.18
Nodes (11): Acceptance criteria, Delivered behavior and verification, Dependencies, Goal, Grouped category dropdown extension (2026-10-21), Milestone 2 — Accounts and transaction register, Payee management extension (2026-10-21), Plan interaction extension (2026-10-22) (+3 more)

### Community 41 - "WishlistPage.tsx"
Cohesion: 0.09
Nodes (35): isWebLink(), loadWishlist(), reorder(), sendWishlistCommand(), WishlistAccess, WishlistBucket, WishlistConflictError, wishlistGroupId (+27 more)

### Community 42 - "BudgetDbContext.cs"
Cohesion: 0.07
Nodes (27): ApplicationSettingEntity, Key, Value, BudgetMemberEntity, BudgetId, InvitedByUserId, JoinedAt, Role (+19 more)

### Community 43 - "Milestone 7 — File import, matching, and portable data"
Cohesion: 0.22
Nodes (8): Acceptance criteria, Dependencies, Goal, Implemented prerequisite (2026-10-21), Milestone 7 — File import, matching, and portable data, Product references, User value, Vertical slice

### Community 44 - "MigrationBuilder"
Cohesion: 0.05
Nodes (15): InitialCreate, BudgetCore, NamedBudget, AccountViewOptions, AccountNotes, CategoryMoves, LongTermPlanning, AccountLifecycle (+7 more)

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
Nodes (31): jsdom, react-dom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript (+23 more)

### Community 50 - ".Calculate"
Cohesion: 0.24
Nodes (3): CalculatedLongTermPlanningMonth, LongTermPlanningCalculations, LongTermPlanningTests

### Community 51 - "WishlistService"
Cohesion: 0.20
Nodes (3): WishlistItemRequest, WishlistResult, WishlistService

### Community 52 - "SafeTransactionImageUrlFetcher"
Cohesion: 0.18
Nodes (6): ImageFetchException, SafeTransactionImageUrlFetcher, TransactionImageUrlFetcherOptions, AllowNonDefaultPort, MaxRedirects, Timeout

### Community 54 - "LoanAccountPage.tsx"
Cohesion: 0.07
Nodes (47): AccountType, accountTypeLabel(), formatMoney(), ActivityList(), CategoryActions, Chevron(), ContractSchedule(), ContractSummary() (+39 more)

### Community 55 - "TestApp"
Cohesion: 0.23
Nodes (8): Milestone 11 acceptance map, LongTermPlanningApiTests, PlanningConflictBody, TestApp, Clock, ConnectionString, Email, Failure

### Community 56 - "LongTermPlanning.tsx"
Cohesion: 0.07
Nodes (44): openUpcoming(), ActivityDialog, ActivityKind, ActivitySelection, actualLabel(), actualMetrics, availableTone(), ConflictState (+36 more)

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
Nodes (52): Mortgages, loans and contracts, submit(), AccountForm(), TransferForm(), creditPaymentGroupId, LoanAccountType, loanAccountTypes, parseMoney() (+44 more)

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
Nodes (7): ContractTermsRequest, Flows, LoanRules, Projection, NeverPaidOff, CreditCardRulesTests, LoanRulesTests

### Community 67 - "Milestone 0 — Local application foundation"
Cohesion: 0.33
Nodes (5): Acceptance criteria, Goal, Milestone 0 — Local application foundation, User and developer outcome, Vertical slice

### Community 68 - "react"
Cohesion: 0.07
Nodes (46): react, AccountHeader(), balanceSummary(), ClearedIndicator(), reconciliationText(), RegisterIcon(), account, entry (+38 more)

### Community 71 - "WishlistApiTests"
Cohesion: 0.44
Nodes (4): WishlistBucketRequest, WishlistOverviewDto, WishlistVersionRequest, WishlistApiTests

### Community 72 - "CategoryTargetRules"
Cohesion: 0.23
Nodes (7): CategoryTargetProgressDto, CategoryTargetDefinition, CategoryTargetRules, Context, MonthEnd, MonthIndex, StartIndex

### Community 73 - ".Validate"
Cohesion: 0.19
Nodes (4): ImageFileNameSanitizer, ImageValidationException, TransactionImageValidator, ValidatedImage

### Community 74 - "BudgetCarryForwardTests"
Cohesion: 0.13
Nodes (13): Backdated assignment corrections, BudgetCarryForward, BudgetEntity, BudgetId, FundsAdded, Groups, LedgerIncome, Month (+5 more)

### Community 75 - "OpenBudget product roadmap"
Cohesion: 0.40
Nodes (5): Cross-cutting completion criteria, Delivery principles, Milestones, OpenBudget product roadmap, Source review

### Community 76 - "ScheduleEntity"
Cohesion: 0.06
Nodes (39): LongTermPlanningCategoryDto, LongTermPlanningCategoryMonthDto, LongTermPlanningDto, LongTermPlanningGroupDto, LongTermPlanningMonthDto, LongTermPlanningUpdateDto, LongTermPlanningUpdateResult, LongTermPlanningService (+31 more)

### Community 77 - "Payees.tsx"
Cohesion: 0.08
Nodes (29): CategoryOption, categoryGroups(), CategoryPicker(), choose(), close(), option(), outside(), inflowCategoryId (+21 more)

### Community 78 - "Documentation"
Cohesion: 0.67
Nodes (3): Current release shape, Documentation, Documents

### Community 79 - "PayeeRuleDto"
Cohesion: 0.24
Nodes (6): PayeeDto, PayeeRuleDto, PayeeRules, PayeesDto, PayeeUpdateDto, PayeeRulesTests

### Community 81 - "AgentTokens.tsx"
Cohesion: 0.12
Nodes (11): Activity, AgentToken, AgentTokens(), BudgetOption, formatDate(), Permission, permissionHelp, readJson() (+3 more)

### Community 82 - "McpToolDefinition"
Cohesion: 0.13
Nodes (8): AgentPermission, Admin, Budget, Reporting, McpPermissions, McpToolDefinition, Route, McpPermissionMatrixTests

### Community 83 - "LedgerGate"
Cohesion: 0.11
Nodes (19): Category targets, Target progress, Wishlist, API, OpenBudget API and local services, Implemented: category targets (2026-10-30), Acceptance criteria, Buckets and Plan integration (+11 more)

### Community 84 - "PlanActionEntity"
Cohesion: 0.20
Nodes (10): PlanActionEntity, ActorUserId, Amount, BudgetId, CreatedAt, FromCategoryName, Id, Kind (+2 more)

### Community 85 - "Theory"
Cohesion: 0.17
Nodes (4): Transaction image attachments (2026-10-30), EntitlementKeyTests, RecurrenceTests, TransactionImageValidationTests

### Community 86 - "Milestone 12 — Icons and suggested icons"
Cohesion: 0.22
Nodes (9): Acceptance criteria, Dependencies, Goal, Milestone 12 — Icons and suggested icons, Open questions, Product references, User value, Vertical slice (+1 more)

### Community 87 - "CategoryEntity"
Cohesion: 0.08
Nodes (25): CategoryEntity, Activity, Assigned, Available, BudgetId, BudgetMonth, CreditOverspending, GroupId (+17 more)

### Community 88 - ".UpdateMonthAsync"
Cohesion: 0.20
Nodes (3): Wishlist slice, BudgetUpdateResult, BudgetService

### Community 89 - "AccountsPage"
Cohesion: 0.13
Nodes (17): AccountsPage(), applyView(), beginColumnResize(), clearFilters(), finishColumnResize(), moveColumnResize(), mutate(), persistColumnWidths() (+9 more)

### Community 91 - "SmtpSettingsEntity"
Cohesion: 0.22
Nodes (9): SmtpSettingsEntity, EnableSsl, EncryptedPassword, From, Host, Id, Port, UpdatedAt (+1 more)

### Community 93 - "OpenBudget.Api"
Cohesion: 0.11
Nodes (4): OpenBudget.Tests, OpenBudget.Api, OpenBudget.UnitTests, RegisterViewTests

### Community 94 - "AgentTokenEntity"
Cohesion: 0.05
Nodes (37): Agent access (MCP) and live updates acceptance map, AgentAccessModel, AgentAuditLogEntity, BudgetId, CreatedAt, Id, Outcome, StatusCode (+29 more)

### Community 95 - "LedgerContracts.cs"
Cohesion: 0.23
Nodes (6): TransactionRequest, ScheduledItemDto, SchedulesDto, MembershipEntitlementApiTests, PostgresFixture, TransactionSelectionApiTests

### Community 96 - ".Configure"
Cohesion: 0.14
Nodes (12): LedgerModel, LedgerStateEntity, BudgetId, Id, Version, ReconciliationEntity, AccountId, BudgetId (+4 more)

### Community 98 - "Task"
Cohesion: 0.16
Nodes (16): ContractDto, ContractScheduleItemDto, LoanActivityItemDto, LoanActivityMonthDto, LoanBalancePointDto, LoanBalanceRequest, LoanDto, LoanPairedCategoryDto (+8 more)

### Community 99 - "ScheduledTransactions"
Cohesion: 0.14
Nodes (5): Limitations and direction, Scheduling and flag storage, ScheduledTransactions, TransactionFlags, ScheduledApproval

### Community 100 - "McpToolContext"
Cohesion: 0.16
Nodes (5): McpToolContext, Arguments, BudgetId, CancellationToken, Token

### Community 101 - "WishlistBucketEntity"
Cohesion: 0.18
Nodes (8): WishlistModel, WishlistSettingsEntity, BudgetId, IsEnabled, UpdatedAt, Version, WishlistState, ActiveBuckets

### Community 102 - "LoanTermsEntity"
Cohesion: 0.09
Nodes (22): LoanTermsEntity, AccountId, BalanceConfirmedAt, BudgetId, Contract, ContractMonths, ContractStartDate, CreatedAt (+14 more)

### Community 104 - "LedgerContracts.cs"
Cohesion: 0.22
Nodes (9): AuditDto, CreditPayoffDto, CreditPayoffProgressDto, CreditPayoffTargetDto, CreditSummaryDto, DeleteCreditPayoffTargetRequest, DeleteTransactionRequest, ReviewTransactionRequest (+1 more)

### Community 105 - "TransactionImageApiTests"
Cohesion: 0.21
Nodes (13): AccountsDto, AuditPageDto, RegisterDto, DeleteTransactionImageRequest, ImportTransactionImageUrlRequest, TransactionImageListDto, ITransactionImageUrlFetcher, StubFetcher (+5 more)

### Community 106 - "CategoryTargetEntity"
Cohesion: 0.11
Nodes (16): CategoryTargetEntity, Amount, Behavior, BudgetId, CategoryId, CreatedAt, DayOfMonth, DueDate (+8 more)

### Community 107 - "App.test.tsx"
Cohesion: 0.17
Nodes (5): Month, next(), saved(), weeklyProgress, weeklyTarget

### Community 108 - ".BudgetId"
Cohesion: 0.08
Nodes (12): Owned budgets and memberships, Category targets, Credit accounts, Loan accounts, Payee management, Transaction images, Wishlist, PayeeManagement (+4 more)

### Community 109 - "Current PostgreSQL schema"
Cohesion: 0.22
Nodes (9): Agent tokens and MCP audit, `Budgets`, Calculations and month lifecycle, `CategoryGroups` and `Categories`, Current PostgreSQL schema, Data model, Identity and account lifecycle, Long-Term Planning (+1 more)

### Community 110 - "JsonElement"
Cohesion: 0.11
Nodes (13): ApiResponse, Error, IsSuccess, McpToolCatalog, Effect, Destructive, Read, Write (+5 more)

### Community 112 - "TransactionImageService"
Cohesion: 0.07
Nodes (26): Storage, AccountDto, LedgerBalanceRow, Balance, Id, TransactionImageEndpoints, TransactionImageEntity, BudgetId (+18 more)

### Community 115 - "TransactionImagesDialog"
Cohesion: 0.29
Nodes (11): Security model, Transaction image attachments, TransactionImagesDialog(), addFiles(), commandFor(), drop(), go(), importUrl() (+3 more)

### Community 116 - "devDependencies"
Cohesion: 0.18
Nodes (11): devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react, @testing-library/user-event, @types/react, @types/react-dom, typescript (+3 more)

### Community 118 - "AppRouting.test.tsx"
Cohesion: 0.12
Nodes (6): URL routing, account(), budgets, Category, key, travelKey

### Community 120 - "liveUpdates.test.tsx"
Cohesion: 0.30
Nodes (6): BudgetChange, LiveUpdateOptions, reconnectDelay(), FakeEventSource, setup(), useBudgetLiveUpdates()

### Community 121 - "BudgetInvitationEntity"
Cohesion: 0.29
Nodes (7): BudgetInvitationEntity, BudgetId, CreatedAt, Email, Id, InvitedByUserId, NormalizedEmail

### Community 122 - "AccountEntity"
Cohesion: 0.18
Nodes (8): AccountEntity, AccountType, BudgetId, Id, IsClosed, IsFavorite, Name, Notes

### Community 125 - ".ToDtoAsync"
Cohesion: 0.22
Nodes (8): Architecture and current status, Category targets, Current feature inventory, Design limits to address, Implemented user path, Runtime topology, Technologies, LongTermPlanningPlanValues

### Community 126 - "evaluateMoneyExpression"
Cohesion: 0.33
Nodes (8): parseAssignment(), evaluateMoneyExpression(), factor(), number(), product(), sum(), hasMoneyOperator(), normalize()

### Community 127 - "MCP server for personal AI agents"
Cohesion: 0.20
Nodes (10): Agent tokens, MCP and live events, 1. Create an agent token, 2. Connect a client, 3. Using the tools, 4. Permission matrix, 6. Real-time updates, 7. Limitations, MCP server for personal AI agents (+2 more)

### Community 129 - "InlineTransactionEditor.tsx"
Cohesion: 0.10
Nodes (35): Cash ledger slice, AccountSidebarRow(), editor(), Account, accountFamily(), isCreditAccount(), isLoanAccount(), loadCategories() (+27 more)

### Community 130 - "IReadOnlyList"
Cohesion: 0.39
Nodes (6): WishlistBucketDto, WishlistBucketOrderRequest, WishlistItemDto, WishlistItemOrderRequest, WishlistProgressDto, WishlistPurchaseDto

### Community 132 - ".RenameFlagsAsync"
Cohesion: 0.48
Nodes (4): FlagDto, FlagPaletteDto, RenameFlagsRequest, SetFlagRequest

### Community 133 - "Developer setup"
Cohesion: 0.22
Nodes (9): Build commands, Current development boundaries, Database lifecycle, Developer setup, First run, GitHub pull requests and releases, Prerequisites, Request flow (+1 more)

### Community 134 - "UserMembershipEntity"
Cohesion: 0.22
Nodes (9): UserMembershipEntity, BillingProvider, BudgetLimitOverride, ExpiresOn, ExternalSubscriptionId, MembershipPlanId, StartsOn, UserId (+1 more)

### Community 135 - "ModelBuilder"
Cohesion: 0.11
Nodes (14): BudgetPlanEntity, CreatedAt, Currency, Id, IsArchived, Months, Name, OwnerUserId (+6 more)

### Community 136 - ".Describe"
Cohesion: 0.44
Nodes (3): PlanActionDraft, PlanActionRules, PlanActionRulesTests

### Community 138 - "CreditPayoffTargetEntity"
Cohesion: 0.22
Nodes (9): CreditPayoffTargetEntity, AccountId, BudgetId, CreatedAt, Kind, MonthlyAmount, PayoffAmount, TargetMonth (+1 more)

### Community 139 - "Milestone 3 — Goals, templates, and guided assignment"
Cohesion: 0.29
Nodes (7): Acceptance criteria, Dependencies, Goal, Milestone 3 — Goals, templates, and guided assignment, Product references, User value, Vertical slice

### Community 140 - "AdminAuditLogEntity"
Cohesion: 0.33
Nodes (6): AdminAuditLogEntity, Action, ActorUserId, CreatedAt, Id, TargetUserId

### Community 141 - "LedgerModel.cs"
Cohesion: 0.40
Nodes (4): FlagDefinitionEntity, BudgetId, Color, Name

## Knowledge Gaps
- **427 isolated node(s):** `BudgetId`, `Notes`, `Id`, `Name`, `IsClosed` (+422 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1090 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **25 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Wishlist slice` connect `.UpdateMonthAsync` to `AccountsPage`, `InlineTransactionEditor.tsx`, `LoanApiTests`, `WishlistBucketEntity`, `.BudgetId`, `CancellationToken`, `LedgerGate`, `WishlistService`, `WishlistRulesTests`, `LoanAccountPage.tsx`, `WishlistItemEntity`, `parseMoney`, `.ToDtoAsync`?**
  _High betweenness centrality (0.222) - this node is a cross-community bridge._
- **What connects `BudgetId`, `Notes`, `Id` to the rest of the system?**
  _427 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `BudgetApplication` be split into smaller, more focused modules?**
  _Cohesion score 0.05026300409117475 - nodes in this community are weakly interconnected._
- **Why does `AccountsPage()` connect `AccountsPage` to `InlineTransactionEditor.tsx`, `api.ts`, `react`, `App.tsx`, `CreditPayoffTarget.tsx`, `Payees.tsx`, `LoanAccountPage.tsx`, `.UpdateMonthAsync`, `parseMoney`?**
  _High betweenness centrality (0.100) - this node is a cross-community bridge._
- **Should `DateTimeOffset` be split into smaller, more focused modules?**
  _Cohesion score 0.06341463414634146 - nodes in this community are weakly interconnected._
- **Why does `BudgetDbContext` connect `BudgetDbContext` to `CategoryTargetService`, `UserMembershipEntity`, `ModelBuilder`, `AdminAuditLogEntity`, `MembershipEntitlements.cs`, `Task`, `CancellationToken`, `Authentication.cs`, `RegisterViewEntity`, `.CreateAsync`, `BudgetDbContext.cs`, `WishlistApiTests`, `BudgetCarryForwardTests`, `ScheduleEntity`, `LedgerGate`, `PlanActionEntity`, `CategoryEntity`, `SmtpSettingsEntity`, `.Configure`, `WishlistPurchaseApiTests`, `TransactionImageService`, `BudgetInvitationEntity`?**
  _High betweenness centrality (0.089) - this node is a cross-community bridge._
- **Should `CategoryTarget.tsx` be split into smaller, more focused modules?**
  _Cohesion score 0.08019323671497584 - nodes in this community are weakly interconnected._