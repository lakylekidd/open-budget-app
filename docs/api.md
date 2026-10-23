# API reference

Local Aspire development exposes the UI at `http://127.0.0.1:5173` and the API at `http://127.0.0.1:5080`. Vite proxies `/api` and `/health` to the API.

## `GET /health`

Checks PostgreSQL connectivity. Success: `{ "status": "ok", "database": "connected" }`. Returns 503 when PostgreSQL is unavailable.

## Authentication

Outside the Testing environment, API requests require an authenticated Identity session. Budget-management, monthly Plan, ledger and long-term planning APIs are available to authenticated users and enforce ownership of the selected active budget. Requests without a valid owned-budget selection fall back to `default`, whose legacy routes remain administrator-only. Sign-in creates an HttpOnly, SameSite=Strict cookie; login failures return the same 401 response for unknown, invalid, locked, or suspended accounts. Identity lockout is configured for five failed attempts and 15 minutes.

| Method/path | Behavior |
|---|---|
| `POST /api/auth/login` | Accepts `{ "userNameOrEmail", "password", "rememberMe" }`; returns the signed-in user's ID, username, email and administrator flag. |
| `GET /api/auth/session` | Returns the current session identity or 401. |
| `POST /api/auth/logout` | Invalidates the current cookie session. |
| `GET /api/auth/profile` | Returns the signed-in user's profile summary. |
| `PUT /api/auth/profile` | Accepts `{ "userName": "new-name" }`, updates the signed-in user's unique username, refreshes the session cookie and records a profile audit event. Email remains unchanged and must use its verification flow. |
| `POST /api/auth/change-password` | Accepts current and new passwords; updates the password, audits the action without credential data, and invalidates all sessions using the prior security stamp. |
| `GET /api/auth/registration-mode` | Publicly reports Disabled, ApprovalRequired, or Open. Defaults to Disabled. |
| `POST /api/auth/register` | Creates an account only when registration is enabled and SMTP is configured; sends a two-hour email-verification link. |
| `POST /api/auth/confirm-email` | Accepts user ID and URL-safe verification token. |
| `POST /api/auth/forgot-password` | Sends a reset link when the address belongs to a verified account; always returns the same response. |
| `POST /api/auth/reset-password` | Resets a password using a two-hour, URL-safe Identity token; the token can be used once and reset completion is audited without token or password data. |
| `PUT /api/admin/settings/registration-mode` | Administrator-only; accepts Disabled, ApprovalRequired, or Open. |
| `POST /api/admin/users/{id}/approve` | Administrator-only; approves a user after email verification. |
| `POST /api/admin/users/{id}/reject` | Administrator-only; rejects a pending registration and records an audit event. |
| `GET /api/admin/users?q={search}&status={status}` | Administrator-only; searches up to 100 users and returns safe account metadata/status. Status filter accepts Active, Pending, Locked or Suspended. |
| `POST /api/admin/users/invite` | Administrator-only; accepts `{ "userName": "alex", "email": "alex@example.com", "membershipPlanId": "free" }`, creates an unverified account, assigns membership, audits the invite and emails a two-hour verification link. The API never returns a password or token. After verification, the invited user sets a password through Forgot password. Email delivery must be configured. |
| `GET /api/admin/dashboard` | Administrator-only; returns user status and total/active budget counts, users by membership tier, the five newest registrations (username, date and status), and the ten newest administration/security audit events. It does not include users' budget or financial data. |
| `POST /api/admin/users/{id}/suspend` / `reactivate` | Administrator-only; changes account access and records an audit event. The bootstrap administrator cannot be suspended. |
| `POST /api/admin/users/{id}/unlock` | Administrator-only; clears Identity lockout without changing suspension status. |
| `POST /api/admin/users/{id}/password-reset` | Administrator-only; emails a two-hour Identity reset link to the user's verified email and audits initiation. The response never contains a reset token or password. |
| `GET /api/admin/users/{id}/history` | Administrator-only; returns the latest 50 audit events for that user, newest first. Unknown users return 404. |
| `GET /api/admin/membership-plans/{id}/entitlements` | Administrator-only; lists the feature keys and enabled defaults for a membership tier. |
| `PUT /api/admin/membership-plans/{id}/entitlements` | Administrator-only; replaces the tier's entitlement defaults with `{ "entitlements": [{ "entitlementKey": "ledger.export", "isEnabled": true }] }`. Keys must be unique and use lowercase letters, digits, dots, underscores or hyphens. |
| `PUT /api/admin/users/{id}/entitlements/{key}` | Administrator-only; accepts `{ "isEnabled": true }` to create or update that user's allow/deny override. |
| `DELETE /api/admin/users/{id}/entitlements/{key}` | Administrator-only; clears the user override so the assigned tier default applies again. |
| `GET /api/admin/audit` | Administrator-only; returns the latest 100 administration audit events. |

The Identity cookie is HttpOnly, SameSite=Strict and Secure in shared environments. State-changing requests with an Origin or Referer must match the forwarded request scheme and host; authenticated state-changing requests in production require one of those headers. The API processes forwarded scheme/host headers from the private Compose proxy boundary so HTTPS termination is reflected in cookie and CSRF checks. Registration and reset email delivery require SMTP configuration; shared environments also require the trusted `AccountEmail__PublicOrigin` HTTPS setting. Registration and recovery email requests are limited to five per minute per client IP. Identity verification and password reset tokens expire after two hours. Registration mode changes and user lifecycle actions commit together with their audit entry. The `/admin` UI supports user search, approval/rejection, password reset, suspension/reactivation, unlock, registration settings and recent audit history.

The `/admin` UI also supports email invitations, membership assignment and per-user account history. Invitees verify their email, then use Forgot password to choose their initial password.

## Owned budgets and memberships

`GET /api/budgets?includeArchived=false` lists the authenticated user's budgets and effective membership limit. New registrations receive a membership but no budget; users explicitly create a budget or choose a template when that flow is available. `POST /api/budgets` creates a budget with `{ "name": "Household", "currency": "EUR" }`. `PUT /api/budgets/{id}` renames an owned budget with `{ "name": "New name", "version": 1 }`. `POST /api/budgets/{id}/archive` and `/restore` retain data while changing active-budget count; the legacy `default` budget cannot be archived during the transition. `DELETE /api/budgets/{id}` permanently removes an owned budget and its Plan and financial data when sent `{ "version": 1 }`; the UI requires confirmation, stale versions return 409, and `default` cannot be deleted. Unknown or foreign budget IDs return 404. New budgets are owner-scoped; creation and restore serialize by user before checking the membership limit. The seeded Free, Plus and Pro tiers allow 1, 3 and unlimited active budgets. Registration assigns Free, while the configured bootstrap administrator receives Pro. An individual limit override is stored on the membership row.

Administrators can read `GET /api/admin/membership-plans` and update a user's membership with `PUT /api/admin/users/{id}/membership` using `{ "membershipPlanId": "plus", "budgetLimitOverride": 2, "startsOn": "2026-10-01", "expiresOn": "2027-10-01", "billingProvider": "provider-name", "externalSubscriptionId": "provider-subscription-id" }`. Subscription metadata fields are optional; dates must be ordered, provider is limited to 80 characters and external ID to 160. Use `null` for the override to apply the tier's limit. Updates are role-protected and written to the administration audit log. These fields do not connect to or charge a payment provider.

Feature checks can use the reusable `RequireEntitlement("ledger.export")` endpoint requirement. A user's explicit allow/deny override wins over the assigned membership plan's entitlement; missing plan entries deny access. Entitlement configuration and overrides are audited, and no feature is restricted by a plan until an endpoint opts into a requirement.

The budget-scoped Plan endpoints are also available to the owner:

| Method/path | Behavior |
|---|---|
| `GET /api/budgets/{id}/months/{YYYY-MM}` | Reads or initializes a month inside an owned budget. |
| `PUT /api/budgets/{id}/months/{YYYY-MM}` | Saves that budget's monthly Plan snapshot with optimistic concurrency. |

Foreign or archived budget IDs return 404. Monthly rows, groups and categories use composite budget/month keys; carry-forward queries stay within the selected budget. The UI loads this list, allows budget creation, selection, rename, archive, restore and confirmed deletion, and persists the selected ID in local storage and the `openbudget.activeBudgetId` cookie. For authenticated financial requests, the API verifies that this cookie names an active budget owned by the current user. Account, transaction, register, transfer, schedule, payee, flag, reconciliation, ledger audit and long-term planning endpoints then operate within that budget. Requests without a valid owned-budget selection fall back to `default`, whose legacy APIs remain administrator-only.

## `GET /api/budget`

Returns the default budget identity:

```json
{ "id": "default", "name": "My Budget", "currency": "EUR", "version": 1 }
```

## `PUT /api/budget`

Renames the default budget using optimistic concurrency. Request `{ "name": "Household", "version": 1 }`. Names are required and limited to 120 characters. A successful response includes the incremented version. A stale version returns 409 with `{ "error": "...", "current": { ... } }`.

## `GET /api/budget/{month}`

Reads or initializes a month using `YYYY-MM`. The first requested month receives a deterministic blank starter category structure. An unseen later month is created from the nearest prior month: category available balances become opening balances, and the prior Ready to Assign becomes opening Ready to Assign. The operation is idempotent under repeated or concurrent reads.

The response includes `month`, `note`, `version`, `openingReadyToAssign`, legacy `fundsAdded`, server-owned `ledgerIncome`, calculated `readyToAssign`, and ordered groups/categories. Each category includes `assigned`, total legacy-plus-ledger `activity`, calculated `available`, target and archive/display fields.

The Plan summary's **Expected Income** value displays this month's `ledgerIncome`; it does not use a separate fixed estimate.

## `PUT /api/budget/{month}`

Saves the editable monthly snapshot. The route month must match `body.month`. The request accepts `note`, `version`, `fundsAdded`, groups, and category assignments/metadata. It does not accept `activity`, `available`, or opening balance fields; those remain server-owned. Amounts use decimal precision of two places. Existing groups/categories must be retained and archived rather than deleted. New categories must start with zero assigned. Ordering values and identifiers must be unique.

Successful writes increment the version and return authoritative calculated totals. Changes to assignments also update every initialized later month's opening category/Ready to Assign amounts atomically, retaining those months' own assignments and activity. Affected later versions increment, so previously loaded snapshots must reload before writing. A stale version returns 409 with the latest budget so the UI can reload or reapply. Invalid requests return 400. `fundsAdded` must equal its preserved legacy value; record new income through a cash account transaction. Monthly initialization/writes and ledger commands share a database lock/transaction. Ledger changes increase affected month versions.

Category IDs must be unique across the month and remain stable when a category moves between active groups. Keep every existing category ID in the submitted snapshot; changing its containing group changes membership and sort order while preserving its assignment, activity and available balance. Group/category drag ordering is saved through this same versioned endpoint. Archived categories/groups must be restored before moving a category.

## Long-Term Planning

`GET /api/long-term-planning/{year}` returns all 12 months for a year, the latest existing active group/category structure, saved planning values, and actual values from persisted monthly snapshots. Reading a year never initializes a missing budget month. Missing planning income/expenses begin at zero. Missing actual assigned/expense values are null; actual available carries forward from the nearest earlier stored month when one exists.

`GET /api/long-term-planning/{year}/activity/{YYYY-MM}` returns the scheduled occurrences behind that month's expected income, expected expenses, and planned category expenses. It uses the same recurrence, pause, transfer, and pending override rules as the annual totals. Each row has a stable projected occurrence ID and transaction details. The UI gets actual category activity from the existing paginated `GET /api/transactions` endpoint. An invalid or out-of-year month returns 400.

Each month summary contains `expectedIncome`, `expectedExpenses`, `remaining` (income minus expenses), `remainingToBeAssigned` (income minus the sum of planned category budgets), `hasExpectedIncome` and `hasActualData`. Expected income and expenses are read-only sums of active, unpaused scheduled account transactions for that month; transfers are excluded. Recurrence, end date, count, and pending occurrence overrides are respected. Category-month `plannedExpenses` is the sum of scheduled outflows assigned to that category, including splits. Other category-month values are `plannedBudgetOverride`, effective `plannedBudget`, `plannedBudgetBeforeMonth`, calculated `plannedAvailable`, and read-only `actualAssigned`, net `actualExpenses` (the negation of signed activity), and `actualAvailable`. A null override inherits the latest earlier override; an explicit zero is retained as an override.

`PUT /api/long-term-planning/{year}` saves one category budget override using `{ "version": 1, "month": "2027-06", "field": "plannedBudgetOverride", "categoryId": "groceries", "amount": 600.00 }`. Set `amount` to null to remove an override. Expected income and planned expenses are calculated from schedules and cannot be written through this endpoint. Budget values must be non-negative decimal amounts within the `numeric(14,2)` range. The month must belong to the route year, and past-month writes are rejected using the server's local calendar date.

All planning values share an optimistic `version`. A successful write returns the refreshed annual plan and increments the version when values change. A stale version returns 409 with the current plan; invalid requests return 400 without partial writes. Forecast persistence is separate from monthly budget snapshots and does not change monthly assignments, activity, or available balances.

## Boundaries

Cash ledger endpoints use the authenticated user's validated active budget; legacy `/api/budget` identity and month routes remain administrator-only and target `default`. Credit/debt and bank links are future milestones. See [data model](data-model.md) for migration and calculations.

## Ledger command protocol

Every ledger write includes `commandId` (nonempty UUID) and `version` (current revision for the active budget from an accounts/register response). Success returns `{ "id": "uuid", "version": 2 }`. Use a new command ID for each action; retry a lost response with the identical path/body, original version and command ID. Exact replay returns the original result without applying it again, even after the ledger revision changes. Reusing an ID for different input returns 409.

Stale writes return 409 `{ "error": "..." }`; reload and review before sending a new command. Validation returns 400 and missing resources 404. A failure rolls back financial rows, projected months, reconciliation and audit receipts together. Transaction/opening dates use `YYYY-MM-DD`, from 1900-01-01 through 9998-12-31. Money inputs have at most two decimal places and an absolute maximum of 999,999,999.99.

## Cash accounts and reconciliation

| Method/path | Additional body fields / result |
|---|---|
| `GET /api/accounts` | `{ version, accounts: [{ id, name, isClosed, balance, clearedBalance, unclearedBalance }] }`, including closed accounts |
| `POST /api/accounts` | Required `name` (max 120), `date`, `openingBalance`. A nonzero opening balance creates one cleared opening entry; a zero opening balance creates no transaction. |
| `PUT /api/accounts/{id}` | `name`, `isClosed`. Close only at zero balance with no uncleared entries or uncategorized spending; an error explains how to resolve it. Reopen through the same endpoint. |
| `POST /api/accounts/{id}/reconcile` | `statementBalance`, equal to the cleared-plus-reconciled balance. Saves a snapshot and marks currently cleared entries reconciled. |

Closed accounts retain history and must be reopened before writes. Reconciliation compares the current cleared ledger balance rather than a date cutoff. There is no automatic adjustment: clear/correct entries until the difference is zero, then explicitly confirm. Reconciled entries cannot be deleted or have their status toggled directly. Transaction/transfer edits require explicit `confirmReconciledEdit=true` when either affected leg is reconciled. Confirmed edits retain the reconciliation row with `isInvalidated=true`, mark reconciled legs cleared, and store before/after audit (including invalidated snapshot IDs). The account reports its latest non-invalidated reconciliation, or null until reconciled again. Closed accounts still require reopening.

Account responses also include `accountType` (`checking`, `savings`, `cash`), `isFavorite`, read-only `connectionStatus`, nullable UTC `lastReconciledAt`, and `needsCategoryCount`. Create requests optionally accept `accountType` (default `checking`). Updates optionally accept `accountType` and `isFavorite`; omission preserves their values. Existing accounts are migrated to checking/nonfavorite without changing ledger balances. Connection status currently returns `unlinked` because automatic provider sync is not implemented; callers cannot mark an account connected through the account-edit API. Last reconciliation time comes from the latest persisted confirmation, and category-attention counts use the complete account ledger.

Header calculations are `clearedBalance + unclearedBalance = balance` (Working Balance). Cleared includes both cleared and reconciled entries. Filters never alter those account totals.

## Register and payees

`GET /api/transactions` combines optional `accountId`, `transferId` (UUIDs), `search` (case-insensitive literal payee/memo substring, max 160), inclusive ISO `from`/`to`, `categoryId` (matches single category or split), and `status` (`uncleared`, `cleared`, `reconciled`). `needsAttention=true` selects the union of unapproved entries and spending needing a category. Optional `includeReconciled=false` excludes reconciled entries before counting/pagination; default is true. It combines with all other filters and never changes account totals or full-history running balances. Special `categoryId=uncategorized` selects negative non-opening/non-transfer entries without categories. Start date must not exceed end date.

Pagination: `page` 1–1,000,000 (default 1), `pageSize` 1–100 (default 50). Returns `{ version, page, pageSize, total, items, accounts }`. The embedded accounts response shares the register's database snapshot/revision. Ordering is date descending then UUID ascending; pages remain stable while the revision is unchanged. Unknown account filters return empty results. Deleted entries are excluded; closed-account history remains visible.

An optional `payeeId` UUID filters by the exact saved payee before pagination.

Items contain `id`, `accountId`, `date`, signed `amount`, `payee`, `memo`, nullable `categoryId`, `status`, nullable shared `transferId`, `isOpening`, `splits: [{ categoryId, amount }]`, and `needsAttention`. `GET /api/payees` returns visible saved name suggestions in name order (hidden payees excluded); trimmed names are reused case-insensitively and retained after deletion of an entry.

Register items additionally include server-calculated `runningBalance`, `transferAccountName`, historical `categoryName` (group/category label), and `isLocked` for protected status/delete actions. `canEdit` is false when an affected account is closed; otherwise reconciled edits can proceed with the explicit confirmation. Items also contain nullable `flagColor` and `scheduleId`, `isApproved`, and `needsCategory`. `needsAttention` is the union of unapproved state and category attention; income does not need a category merely because it needs approval. Running balances use all nondeleted entries in the entry's account before filters/pagination. The chronological window is date ascending, UUID descending, the exact reverse of the displayed date-descending/UUID-ascending ordering. Each amount is counted once, including transfers and opening entries. Search, category/status/date filters and pages do not reset a running balance. A transfer requires confirmation for editing when either leg is reconciled, and cannot be deleted in that state; closed legs prevent editing/deletion; individual unreconciled legs can still be cleared independently.

| Method/path | Additional body fields |
|---|---|
| `POST /api/transactions` | `accountId`, `date`, signed nonzero `amount`, optional `payee` (max 160), `memo` (max 1000), nullable `categoryId`, `status` (`uncleared`/`cleared`), optional `splits` |
| `PUT /api/transactions/{id}` | Same fields; replaces editable state and recalculates original/destination accounts and months. Opening entries stay in their original account without categories/splits. |
| `POST /api/transactions/{id}/status` | `status` (`uncleared`/`cleared`); can clear an individual transfer leg. Use reconciliation for reconciled status. |
| `POST /api/transactions/{id}/delete` | Only command ID/version. Soft deletes the entry, or both transfer legs. |

Negative amounts are outflows; positive amounts are inflows. Categorized inflows refund their category. Uncategorized income increases Ready to Assign; uncategorized spending reduces it and needs attention until categorized. No category is fabricated. Categories must be active in the transaction month. Splits replace the single `categoryId`, use nonzero signed amounts and sum exactly to the total (max 50 parts).

## Transfers and audit

- `POST /api/transfers`: `fromAccountId`, `toAccountId`, `date`, positive `amount`, optional `memo`. Requires different open accounts. Creates equal/opposite entries with a shared transfer ID, both initially uncleared. Transfers never create category spending or income.
- `PUT /api/transfers/{transactionId}`: same fields; accepts either leg ID, edits both together and retains each leg's cleared status. Generic transaction editing cannot alter transfers. The mutation response ID is the shared transfer ID.
- `GET /api/ledger/audit?resourceId={uuid}&page=1&pageSize=50`: `{ page, pageSize, total, items }`, newest revision first, with optional resource filter. Account actions use account ID, transaction actions use entry ID, transfer actions use shared transfer ID. An optional `payeeId` UUID filters by the exact saved payee before pagination.

Items contain `id`, `resourceId`, `kind`, `version`, UTC `createdAt`, `beforeJson` and `afterJson` strings. Every ledger write stores a replay receipt and before/after audit in the same transaction. This local history is not a tamper-proof authorization boundary.

## Persisted register View Options

`GET /api/register-views/{scope}` returns `{ version, datePreset, fromMonth, toMonth, showReconciled, showRunningBalance, showFlags, showImages }`. Scope is the account UUID, or `all` for the independent All Accounts view. Unknown accounts return 404. Unsaved views return version 0, `allDates`, null month bounds and all switches true. A GET does not create a row.

`PUT /api/register-views/{scope}` accepts the same complete object with its expected version. Presets are `thisMonth`, `lastMonth`, `latest3Months`, `thisYear`, `lastYear`, `allDates`, or `custom`. Month bounds are nullable `YYYY-MM` (1900–9998); custom requires both and From <= To. All switches and the stored month selectors are saved atomically. Success returns the incremented view version. A stale edit returns 409; reload saved options, review, and apply a new draft. Exact retry of the immediately preceding identical saved selection returns its existing result, handling a lost response without another write. Invalid input returns 400 without partial changes.

View settings use their own version and the active budget's database gate. They do not increment the financial ledger revision, alter balances/month projections or create financial audit commands. Preferences are shared by clients of the same owned budget, not across budgets.

The UI resolves rolling presets using the viewing browser's current local calendar date. Latest 3 Months includes this month and the preceding two. Custom bounds include the complete first and last months, including leap days. All Dates applies no date bound and retains the stored selectors. Show Reconciled filters on the server; the other switches hide/show existing table columns. Flag and image editing/attachments remain future functionality.

Apply commits the draft; Cancel/Escape discard it. A failed save keeps the draft and the currently applied view, with retry/reload recovery. Loading saved options must succeed before a view can be applied. View All/Reset filters clears transient search/category/status and saves All Dates + Show Reconciled while retaining column switches and stored month selectors. Category/status filters, transfers and audit remain under More.

## Inline account editing and balance adjustment

Account DTOs include `notes` (default empty). `PUT /api/accounts/{id}` additionally accepts optional `notes` (max 1000) and `workingBalance` (valid two-decimal money). Omission preserves notes and requests no adjustment. Under the same ledger lock, a changed working balance creates exactly one cleared transaction for `requested balance - current full account balance`, dated on the server's current local date, with payee `Balance Adjustment`, memo `Account working balance adjustment`, and no category. The adjustment flows through normal income/activity/carry-forward rules. Negative uncategorized adjustments need categorization. Unchanged balance creates no transaction. Names, notes, adjustment, projection, receipt and audit commit/roll back together; exact command replay cannot duplicate the adjustment. Closing still requires a zero resolved balance; a closed account must be reopened to adjust it. Account type remains supported by the API and creation form.

Transaction and transfer writes optionally accept `repeat` (default `never`) and `confirmReconciledEdit` (default false). An explicit non-Never repeat creates/updates a linked future rule in the same transaction. Editing a posted occurrence with Never leaves its future rule intact. Opening entries can retain a zero amount when editing, but cannot be scheduled. UI future entry creation uses the schedule endpoint; the existing transaction API still accepts explicitly posted future dates.

## Scheduled transactions

`GET /api/schedules?accountId={uuid}&from=YYYY-MM-DD&to=YYYY-MM-DD&page=1&pageSize=25` returns `{ version, page, pageSize, total, items }`. Page limits match the register. Each item represents the next occurrence of one live rule: `{ id, occurrenceId, repeat, startDate, isPaused, endDate, count, toAccountId, transaction }`. The transaction shape is a forecast, never an actual register row. It has a deterministic occurrence ID and a projected running balance from the full actual account balance plus active next occurrences before it, before date filters/pagination. Paused items have no projected balance. Date-descending/UUID-ascending ordering mirrors the register. Account filtering includes the receiving side of scheduled transfers with a positive amount. All Accounts displays one source-side row per rule. Forecasts are excluded from account totals, actual activity and actual reports. Listing shows one next occurrence per rule; advancing reviews any overdue backlog one date at a time.

`POST /api/schedules` and `PUT /api/schedules/{id}` use ledger command IDs/versions and fields `accountId`, `date`, signed `amount`, optional `payee`, `memo`, `categoryId`, `splits`, `repeat`, optional `toAccountId`, `isPaused`, nullable `endDate`/`count`, and nullable `flagColor`. Transfers require a negative source amount, a different open destination and no category/splits. Validation uses normal money/date/category rules. Count is 1–10000. Editing the future rule supersedes its pending override, resets the recurrence anchor/count from its supplied date, increments the occurrence sequence and preserves history. The UI offers an Only this occurrence switch for a single override. Ended rules are retained and cannot be edited/resumed; create a new rule instead.

`POST /api/schedules/{id}/actions` accepts `{ commandId, version, action, occurrenceId, date?, template? }`. A stale occurrence returns 409, even with a fresh ledger version. Actions:

- `post`: Enter Now creates actual uncleared entries (an atomic pair for transfers), links the occurrence and advances the rule. Optional date overrides posting date; the UI uses today for early posting, otherwise the due/snoozed date. Exact retries reuse the receipt; different concurrent commands cannot post the occurrence twice.
- `skip`: retains a skipped occurrence without creating money activity and advances the rule.
- `snooze`: requires a later date, changes only the next occurrence's review/posting date and retains the recurrence anchor.
- `editOnce`: requires a template `{ amount, payee, memo, categoryId, splits }`, optionally a date; preserves the parent rule for subsequent occurrences.
- `pause` / `resume`: preserve history and next occurrence; paused rules cannot post/skip/snooze/edit once.
- `end`: ends the future rule without deleting posted transactions or history.

Posting revalidates open accounts and active categories in the actual posting month. Failures roll back occurrence, ledger entries, projections and receipt together. A background worker checks due schedules at startup and every 30 seconds; production account/register/schedule reads also catch up due entries before returning. Due occurrences post as uncleared/unapproved actual entries, retaining their effective due date and advancing the next scheduled date. Each pass posts at most 32 occurrences and catches further backlog in later passes; invalid/closed-account rules retain a resolvable `postingError` on the scheduled item and do not prevent other rules from posting. All workers/readers share the same deterministic occurrence/command identities and database gate. Testing hosts disable real-time reads/workers and exercise the same processing through `POST /api/schedules/process-due` with an injected clock. That endpoint accepts no client date and processes the server's current local day.

Repeat codes: `never`, `daily`, `weekly`, `everyOtherWeek`, `twiceAMonth`, `every4Weeks`, `monthly`, `everyOtherMonth`, `every3Months`, `every4Months`, `twiceAYear`, `yearly`, `everyOtherYear`. Recurrences use calendar-only dates without timezone instants, avoiding DST shifts. Due-day processing uses the server TimeProvider local timezone, so another PC cannot move the posting boundary. Daily/weekly rules use day offsets. Month/year rules always calculate from the original anchor (Jan 31 → Feb 28 → Mar 31; Feb 29, 2024 yearly → Feb 28, 2025 → Feb 29, 2028). Twice a month pairs anchor days 1–15 with day+15 in the same month; anchors 16–31 pair with day-15 in the next month, clamping to the month's last day. Never completes after one occurrence. End date/count or supported-calendar exhaustion ends a rule. Server-local today is used for due processing and account adjustments; the UI's local today is used for new rows and explicit early posting.

## Transaction flags

`GET /api/flags` returns `{ version, flags: [{ color, name }] }`, ordered red/orange/yellow/green/blue/purple. `PUT /api/flags` accepts the normal command ID/version and all six `{ color, name }` values; names are trimmed, nonempty and at most 40 characters. Names are shared across this budget and clients. Invalid or stale palettes do not partially save.

`POST /api/transactions/{id}/flag` and `POST /api/schedules/{id}/flag` accept `{ commandId, version, color }`; null clears a flag. These metadata commands preserve financial amounts/status/reconciliation, including on reconciled entries, and retain audit/replay semantics. Schedule flags propagate into subsequently posted occurrences. Changing a posted transaction flag does not silently rewrite its future schedule. The flag picker supports 1–6 and 0 to clear while open; labels show the custom name and full title even when a long name is truncated in the table.

## Approval and attention

Account responses include `unapprovedCount` and `attentionCount` (distinct union of unapproved entries and uncategorized spending). Sidebar dots use only unapprovedCount. The top banner uses attentionCount across the full selected account; its View action uses the server `needsAttention=true` filter. Account totals include unapproved actual entries; future forecasts stay excluded.

Automatically posted occurrences have `isApproved=false`, and scheduled transfer legs are independently unapproved. Manual transaction creation, opening entries, balance adjustments and explicit Enter Now are approved. `POST /api/transactions/{id}/review` accepts `{ commandId, version, action: "approve" | "reject" }`. Approve changes only the reviewed entry's approval metadata. Reject soft-deletes the entry (both legs for a transfer) and reverses actual budget/account effects atomically, retaining occurrence/audit history. A rejected occurrence is not regenerated; its rule has already advanced. Reconciled deletion remains blocked. Exact retries use the normal receipt; another stale review returns 409.

Setting an entry's status to `cleared` also sets isApproved=true in the same audited command. Unclearing later does not revoke approval. A confirmed reconciled edit also retains approval. Neither approving nor clearing adds another transaction or changes its signed amount. API/domain tests use a deterministic injectable TimeProvider to verify due dates, clock boundaries, retries, rejection reversal, both transfer legs and reopening blocked rules. The UI refreshes idle registers/sidebar data every 30 seconds while preserving active editing/popover drafts.


## Selected transaction actions

`POST /api/transactions/selection` accepts `{ commandId, version, ids, action, status?, color?, memo?, accountId?, repeat?, confirmReconciledEdit? }`. Select 1–100 distinct actual transaction IDs. Actions are `status`, `flag`, `memo`, `move`, `duplicate`, `repeat`, and `delete`. All rows, projection changes and the audit receipt commit atomically under the existing ledger gate. Missing/invalid selections and closed accounts reject the entire batch. Exact retries return the original receipt; stale revisions return 409. Audit retains before/after rows and action parameters.

Status accepts cleared/uncleared; clearing also approves. Flag accepts the six palette colors or null. Memo replaces the selected memos (maximum 1000 characters) and updates both legs of a selected transfer. Move changes ordinary transactions' account IDs, preserving amounts/dates/categories; use linked transfer editing for transfer account changes. Memo/move on reconciled entries require explicit confirmation and invalidate the old reconciliation while retaining its history. Reconciled status/deletion remain locked.

Duplicate copies ordinary entries/splits/flags and complete transfer pairs once, resets status to uncleared, marks copies approved, and does not copy opening-balance or schedule identities. Repeat uses existing recurrence rules to create/update the next scheduled date without adding another actual entry. Delete soft-deletes complete transfer pairs once. Opening balances cannot be duplicated, moved or repeated. Mixed actual/scheduled selections cannot use these actual-entry commands.

The floating selection bar provides count/clear, the existing named flag picker, and an upward More menu with keyboard navigation. C toggles cleared state, Shift+D duplicates, Shift+T opens Repeat, and Delete opens confirmation; shortcuts ignore text inputs/dialogs. Memo, Repeat, Move and Delete use confirmation/form dialogs with pending-write protection. CSV export downloads only visible selected actual rows with UTF-8 text, quoted text cells, decimal amounts and spreadsheet-formula protection; it makes no ledger changes. Match/Unmatch appear disabled because import matching is not implemented. Existing scheduled controls remain available through account More. The bar hides during inline editing.

## Saved payees and renaming rules

`GET /api/payees/manage` returns `{ version, payees }` under the ledger snapshot lock. Each payee has `id`, `name`, `isHidden`, `categoryMode` (`history`, `always`, `never`), nullable `defaultCategoryId` and `effectiveCategoryId`, `transactionCount`, and `rules: [{ operator, value }]`. Counts exclude deleted/opening/transfer entries. Effective history uses the latest three nondeleted ordinary entries, date descending then UUID ascending, counting uncategorized/split entries as no single category. Most frequent category wins; the most recent breaks ties. No history yields no suggestion.

`PUT /api/payees/manage` accepts `{ commandId, version, payees: [{ id, name, isHidden, categoryMode, defaultCategoryId, rules, delete? }] }`. Send only changed payees, 1–500 distinct IDs. Names are trimmed, case-insensitively unique, 1–160 characters. Always mode requires an existing active category. Each payee allows 0–50 nonempty literal rules of 1–160 characters, operator `is` or `contains`. Unknown operators/IDs, duplicate names and invalid categories reject the complete command. Renaming to another existing name is rejected, including name swaps; merging is not implemented.

The command shares ledger revisions, exact replay, stale-write rejection, atomic rollback and before/after audit. Renaming changes the stable payee record, updating historical transaction labels without changing amounts/categories/statuses/reconciliations. Active scheduled templates and pending occurrence overrides using the old name follow the rename. Hidden payees retain history and import rules but disappear from ordinary suggestions. Delete is allowed only if there are no transaction references (including deleted history) and no scheduled reference; otherwise use Hide. Rules and settings are budget-wide PostgreSQL state shared by all clients of the same server.

The register's Payee dropdown offers Selected, Payments and Transfers (all accounts), and Saved Payees. The current account and closed accounts are shown disabled as transfer destinations. Transfer choices use account IDs, even when account names duplicate. Transfer names follow account names; editing a transfer payee's name means editing the account. Saved payees are created by named transaction saves. Selecting one suggests its effective category only if the draft has no category/split and the suggestion is active in that draft's month. Manual free text does not apply import renaming rules, and explicit category choices are preserved.

Manage Payees is available in the dropdown footer and More. Search, group expansion, multi-selection, hide/unhide, safe unused deletion, the three categorization policies, and rule add/remove are supported. Done saves all staged changes atomically; Cancel/X/Escape discards them. Undo/Redo operate on the unsaved draft. Show Transactions filters by exact payee ID within the current account/date view; View All/Reset filters clears that filter. Loading/validation/conflict errors retain the draft; explicit Reload replaces it after a conflict. Lost-response saves retain the identical command for Retry save and block closing/navigation until resolved.

Typing in the Payee field filters both merchant and transfer labels by a trimmed, case-insensitive substring, including exact matches and full transfer names. Clearing the field restores suggestions. Unmatched free text remains valid: saving the transaction creates the payee atomically with the transaction; typing or cancelling creates nothing. Name reuse is trimmed and case-insensitive. Save and add another reloads payees for the next draft. Failed or rejected transactions cannot leave orphan payees, and exact command retries cannot duplicate them.

### Importing an accepted normalized row

`POST /api/transactions/import` accepts the standard `TransactionRequest` command/date/amount/payee/memo/category/status/splits fields. Repeat must be Never; the endpoint only creates new entries. This is the accepted-row application boundary, not a file-upload/parser or matching UI. File Import remains disabled until milestone 7 supplies file preview, duplicate/manual-entry matching and batch confirmation.

The service validates the row, applies payee rules, and writes through the normal ledger transaction/projection/audit path. Matching is trimmed/case-insensitive and literal, with exact matches before contains, then the longest contains text. Equally specific matches for different payees reject the row with no writes so rules can be corrected. Hidden payees can match. An unmatched name reuses/creates its ordinary canonical record. Rules do not modify old imports retroactively or chain through multiple payees. With no explicit category/splits, the effective category is used only when the category and its group are active in the row's month. New rows are unapproved and use normal Approve/Reject/clear controls; actual balances include them. Audit retains the source payee and resulting transaction. Use a stable command ID and identical body per accepted row to prevent duplicate retries; a distinct command means a distinct row. No bank synchronization, statement parsing or inferred duplicate detection is implied by this endpoint.


## Grouped transaction category picker

The inline register and split rows use a searchable grouped picker rather than a native select. Group headings are flush left; their categories are indented, with the server's `available` amount aligned right. The Selected section includes the parent group, and Inflow / Ready to Assign displays the same transaction month's server `readyToAssign`. Positive amounts are green, negative red, zero neutral. The list follows budget ordering and excludes archived categories/groups from ordinary choices. Duplicate display names select by stable category ID.

Amounts come from `GET /api/budget/{transaction month}` with no account-local or draft-derived calculation. Opening the dropdown, changing the transaction date, retrying a read, or creating a category reloads that month's values. Pending reads are cancelled when superseded. Failed/loading reads do not permit selecting stale hidden options. Search matches category and group names; keyboard arrows/Enter select, Escape discards search and keeps the inline draft. Transfer/opening entries retain their protected category behavior.

New Category reads a fresh monthly snapshot, requires a name and active existing group, and uses the existing versioned month PUT with a new stable ID and zero assigned/target. It preserves all existing fields and never sends calculated activity/available values. Name duplicates within the selected active group are rejected in the form. A stale month requires explicit Reload budget, retaining the name. If a response is lost, Retry save reads back the stable new ID before retrying the same snapshot; it never invents another category ID. Closing/navigation is blocked until an uncertain save is resolved. Creating/selecting a category preserves the transaction's unsaved date, payee, memo, amount and splits. Returning to Plan reloads the saved month as before.

The footer reuses the existing Split editor and account transfer choices. Payment/Transfer opens payee account choices; credit-card accounting remains outside the current cash-ledger milestone. Category and Ready to Assign amounts are budget-wide, independent of the account being edited.


## Assignment rollover and earlier-month edits

For a category, Available = prior carried amount + current Assigned + current Activity. Example: August Assigned 50, categorized spending -35 gives Available 15; September starts with Assigned 0 / Available 15; assigning 50 in September gives Available 65. Changing August Assigned to 80 subsequently gives August Available 45 and September Available 95, with September Assigned still 50. Reducing August to 50 restores September Available 65. Assignment is a monthly amount, not a repeated template; only the remaining available money carries.

`PUT /api/budget/{month}` performs the correction under the shared ledger lock and transaction. Invalid/stale requests and save failures do not leave partially updated future months or an implicitly initialized rejected month. Repeating the old monthly version returns 409 with the current snapshot; it never adds the carry twice. A later month's stale write returns its corrected `current` snapshot for reload/reapply. Concurrent ledger changes and assignments serialize under the same lock. New category/group definitions needed to retain incoming historical carry are materialized in initialized later months without copying source assignments/activity or overwriting existing later metadata.

The Plan UI waits for pending assignment saves before changing months; unsaved/failed edits cannot be lost by month navigation. Undo/Redo history resets after loading a different month. Assigned input accepts signed decimal amounts with at most two fractional digits (decimal comma supported; blank clears to zero). Invalid text, exponent notation, excess precision and out-of-range values remain visible with an error and are never silently converted to zero. All persisted available/Ready to Assign totals come back from the server.
