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
| `GET /api/admin/settings/email` | Administrator-only; returns the active SMTP connection settings and safe delivery status. It never returns the SMTP password. |
| `PUT /api/admin/settings/email` | Administrator-only; saves `{ host, port, from, username, password, enableSsl }`. Passwords are encrypted at rest and an omitted password retains the existing one. |
| `POST /api/admin/settings/email/test` | Administrator-only; sends an OpenBudget-branded test email to `{ "to": "admin@example.com" }`. |
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

The Identity cookie is HttpOnly, SameSite=Strict and Secure in shared environments. State-changing requests with an Origin or Referer must match the forwarded request scheme and host; authenticated state-changing requests in production require one of those headers. The API processes forwarded scheme/host headers from the private Compose proxy boundary so HTTPS termination is reflected in cookie and CSRF checks. Registration and reset email delivery require SMTP configuration; shared environments also require the trusted `AccountEmail__PublicOrigin` HTTPS setting. Administrators can either configure SMTP in the admin panel or retain the runtime `Smtp__*` configuration fallback. Stored SMTP passwords are encrypted with ASP.NET Data Protection, never returned by the API, and require persisted production Data Protection keys. Registration and recovery email requests are limited to five per minute per client IP. Identity verification and password reset tokens expire after two hours. Registration mode changes and user lifecycle actions commit together with their audit entry. The `/admin` UI supports user search, approval/rejection, password reset, suspension/reactivation, unlock, registration and email-delivery settings, and recent audit history. Verification, invitation, reset, and test messages include both branded HTML and plain-text content.

The `/admin` UI also supports email invitations, membership assignment and per-user account history. Invitees verify their email, then use Forgot password to choose their initial password.

## Owned budgets and memberships

`GET /api/budgets?includeArchived=false` lists the authenticated user's budgets and effective membership limit. New registrations receive a membership but no budget; users explicitly create a budget or choose a template when that flow is available. `POST /api/budgets` creates a budget with `{ "name": "Household", "currency": "EUR" }`. `PUT /api/budgets/{id}` renames an owned budget with `{ "name": "New name", "version": 1 }`. `POST /api/budgets/{id}/archive` and `/restore` retain data while changing active-budget count; the legacy `default` budget cannot be archived during the transition. `DELETE /api/budgets/{id}` permanently removes an owned budget and its Plan and financial data when sent `{ "version": 1 }`; the UI requires confirmation, stale versions return 409, and `default` cannot be deleted. Unknown or foreign budget IDs return 404. New budgets are owner-scoped; creation and restore serialize by user before checking the membership limit. The seeded Free, Plus and Pro tiers allow 1, 3 and unlimited active budgets. Registration assigns Free, while the configured bootstrap administrator receives Pro. An individual limit override is stored on the membership row.

Administrators can read `GET /api/admin/membership-plans` and update a user's membership with `PUT /api/admin/users/{id}/membership` using `{ "membershipPlanId": "plus", "budgetLimitOverride": 2, "startsOn": "2026-10-01", "expiresOn": "2027-10-01", "billingProvider": "provider-name", "externalSubscriptionId": "provider-subscription-id" }`. Subscription metadata fields are optional; dates must be ordered, provider is limited to 80 characters and external ID to 160. Use `null` for the override to apply the tier's limit. Updates are role-protected and written to the administration audit log. These fields do not connect to or charge a payment provider.

Feature checks can use the reusable `RequireEntitlement("ledger.export")` endpoint requirement. A user's explicit allow/deny override wins over the assigned membership plan's entitlement; missing plan entries deny access. Entitlement configuration and overrides are audited, and no feature is restricted by a plan until an endpoint opts into a requirement. The `wishlist` key gates the [Wishlist](#wishlist) routes; the `Wishlist` migration seeds it as disabled for Free and enabled for Plus and Pro.

The budget-scoped Plan endpoints are available to the owner and invited editors:

| Method/path | Behavior |
|---|---|
| `GET /api/budgets/{id}/months/{YYYY-MM}` | Reads or initializes a month inside an accessible budget. |
| `PUT /api/budgets/{id}/months/{YYYY-MM}` | Saves that budget's monthly Plan snapshot with optimistic concurrency. |

Foreign or archived budget IDs return 404. Monthly rows, groups and categories use composite budget/month keys; carry-forward queries stay within the selected budget. The UI loads owned and shared budgets and persists the selected ID in local storage and the `openbudget.activeBudgetId` cookie. For authenticated financial requests, the API verifies that this cookie names an active budget owned by or shared with the current user. Account, transaction, register, transfer, schedule, payee, flag, reconciliation, ledger audit and long-term planning endpoints then operate within that budget. Requests without a valid accessible-budget selection fall back to `default`, whose legacy APIs remain administrator-only.

## Budget collaboration and Plan history

`GET /api/budgets/{id}/sharing` returns the group manager, joined editors, owner-visible pending invitations, whether the caller can manage access, and the remaining collaborator capacity. The owner can `POST /api/budgets/{id}/invitations` with `{ "email": "member@example.com" }`, remove a joined editor with `DELETE /api/budgets/{id}/members/{userId}`, or cancel a pending invitation with `DELETE /api/budgets/{id}/invitations/{invitationId}`. A verified existing user joins immediately. Otherwise the invitation is retained by normalized email and claimed after a user verifies and signs in with that address. SMTP notification is best-effort; persisted access does not depend on delivery. A budget supports four joined or pending collaborators. Editors can read and update budget-scoped Plan and financial data but cannot rename, archive, delete, or manage sharing.

`GET /api/budgets/{id}/plan-actions` returns up to 100 newest Plan actions with their month, kind, amount, source/destination category, timestamp, and actor name/email. `kind=moved` and `kind=assigned` filter the feed; the assigned filter includes removals. Successful monthly saves append immutable `moved`, `assigned`, `removed`, or `updated` records in the same transaction as the Plan change. Failed and conflicting saves append nothing.

## `GET /api/budget`

Returns the default budget identity:

```json
{ "id": "default", "name": "My Budget", "currency": "EUR", "version": 1 }
```

## `PUT /api/budget`

Renames the default budget using optimistic concurrency. Request `{ "name": "Household", "version": 1 }`. Names are required and limited to 120 characters. A successful response includes the incremented version. A stale version returns 409 with `{ "error": "...", "current": { ... } }`.

## `GET /api/budget/{month}`

Reads or initializes a month using `YYYY-MM`. The first requested month receives a deterministic blank starter category structure. An unseen later month is created from the nearest prior month: positive category available balances become opening balances, while uncovered negative balances reset to zero in the category and reduce opening Ready to Assign. The operation is idempotent under repeated or concurrent reads.

The response includes `month`, `note`, `version`, `openingReadyToAssign`, legacy `fundsAdded`, server-owned `ledgerIncome`, calculated `readyToAssign`, and ordered groups/categories. Each category includes `assigned`, total legacy-plus-ledger `activity`, calculated `available`, the legacy `target` amount, archive/display fields, and the structured `categoryTarget` with its server-calculated `targetProgress` (see [category targets](#category-targets)).

The Plan summary's **Expected Income** value displays this month's `ledgerIncome`; it does not use a separate fixed estimate.

## `PUT /api/budget/{month}`

Saves the editable monthly snapshot. The route month must match `body.month`. The request accepts `note`, `version`, `fundsAdded`, groups, and category assignments/metadata. It does not accept `activity`, `available`, or opening balance fields; those remain server-owned. Amounts use decimal precision of two places. Existing groups/categories must be retained and archived rather than deleted. New categories must start with zero assigned. Ordering values and identifiers must be unique.

Successful writes increment the version and return authoritative calculated totals. Changes to assignments also recalculate every initialized later month's opening category/Ready to Assign amounts atomically, retaining those months' own assignments and activity. The recalculation is chronological because each rollover resets uncovered category overspending to zero and withholds that shortfall from Ready to Assign. Affected later versions increment, so previously loaded snapshots must reload before writing. Group/category ordering and category group membership changes synchronize matching rows in every initialized month; names, archive state, assignments, activity and other month-specific values are not copied. Newly initialized months inherit the latest available order. A stale version returns 409 with the latest budget so the UI can reload or reapply. Invalid requests return 400. `fundsAdded` must equal its preserved legacy value; record new income through a cash account transaction. Monthly initialization/writes and ledger commands share a database lock/transaction. Ledger changes increase affected month versions.

Category IDs must be unique across the month and remain stable when a category moves between active groups. Keep every existing category ID in the submitted snapshot; changing its containing group changes membership and sort order while preserving its assignment, activity and available balance. Group/category drag ordering is saved through this same versioned endpoint and is applied to matching initialized months, so the position remains stable when navigating between months until another reorder is saved. Archived categories/groups must be restored before moving a category.

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

Account responses also include `accountType` (`checking`, `savings`, `cash`, `creditCard`, `lineOfCredit`), `isFavorite`, read-only `connectionStatus`, nullable UTC `lastReconciledAt`, and `needsCategoryCount`. Create requests optionally accept `accountType` (default `checking`). Updates optionally accept `accountType` and `isFavorite`; omission preserves their values. Existing accounts are migrated to checking/nonfavorite without changing ledger balances. Connection status currently returns `unlinked` because automatic provider sync is not implemented; callers cannot mark an account connected through the account-edit API. Last reconciliation time comes from the latest persisted confirmation, and category-attention counts use the complete account ledger.

Header calculations are `clearedBalance + unclearedBalance = balance` (Working Balance). Cleared includes both cleared and reconciled entries. Filters never alter those account totals.

## Credit accounts and card payments

`accountType` also accepts `creditCard` and `lineOfCredit`. An account cannot later switch between cash and credit types (`400`). A credit account's opening balance is the existing debt as a negative amount; it is excluded from Ready to Assign.

Each credit account owns one server-managed payment category, ID `ccp-{accountId}`, named after the account in the first group `credit-card-payments` ("Credit Card Payments"). Monthly Plan saves may assign to these categories but cannot create, move or add other categories to that group (`400`). Transactions and schedules cannot use a payment category (`400`); pay a card with a transfer.

Rules applied by the ledger projection for each month:

| Card activity | Effect |
|---|---|
| Purchase from a funded category (e.g. 40 from Groceries with 100 available) | Groceries activity −40; payment category +40. |
| Purchase from a partially funded category (40, only 25 available) | Groceries −40 (Available −15, reported as `creditOverspending: 15`); payment +25. The 15 becomes card debt: the next month opens Groceries at zero **without** reducing Ready to Assign. Assigning 15 later moves it into the payment category. |
| Refund (+10 to Groceries) | Groceries +10; payment −10. |
| Fee or interest | Enter as categorized spending; follows the purchase rules. |
| Payment (transfer 30 cash → card) | Budget-neutral for cash; payment category −30. |

Credit account transactions without a category or split are rejected with `Credit account transactions require a category.` Opening entries and transfers are exempt.

`GET /api/accounts/{id}/credit-summary/{YYYY-MM}` returns `priorBalance` (before the month), `spendingAndOutflows`, `interestCharges` (currently always 0), `paymentsAndInflows` and `currentBalance` for a credit account. Monthly category DTOs include read-only `creditOverspending`.

### Debt payoff targets

Each credit account may have one debt payoff target on its payment category. Targets are ledger commands: they require `commandId` and the current ledger `version`, replay an identical command, reject a reused command ID with a different body (`409`) or a stale version (`409`), write an audit receipt (`payoffTarget.save/{accountId}`, `payoffTarget.delete/{accountId}`) and increment the ledger version. They never change Plan amounts, category `target` values or ledger balances.

| Method | Route | Body / result |
|---|---|---|
| `GET` | `/api/accounts/{id}/payoff-target/{YYYY-MM}` | `{ accountId, month, version, target, progress }`; `target` and `progress` are `null` when none is saved. |
| `PUT` | `/api/accounts/{id}/payoff-target` | `{ commandId, version, kind, targetMonth?, monthlyAmount? }` creates or replaces the target; returns `{ id, version }`. |
| `POST` | `/api/accounts/{id}/payoff-target/delete` | `{ commandId, version }`; `404` when no target exists. |

`kind` is `payByDate` (requires `targetMonth` `YYYY-MM`, from the current calendar month up to 50 years ahead; no `monthlyAmount`) or `monthlyAmount` (requires a positive `monthlyAmount` with at most two decimals; no `targetMonth`). Saving requires an open credit account; cash accounts and past months return `400`. `target.payoffAmount` is the card debt (`max(-balance, 0)` over all entries) recorded on every save and shown as the "Pay off" headline; `createdAt` is kept across edits and `updatedAt` changes.

`progress` is calculated by the server for the requested Plan month (it initializes that month like `GET /api/budget/{month}`):

| Field | Rule |
|---|---|
| `neededForPayoff` | `max(-card balance through the end of the month, 0)` |
| `availableForPayment` | payment category Available in that month (negative counts as 0 below) |
| `assignedThisMonth` | payment category Assigned in that month |
| `toGo` | `max(needed - max(available, 0), 0)` |
| `payByDate` `monthsRemaining` | months from the viewed month to `targetMonth` inclusive; `1` when the target month has passed (`isPastDue` is then true while money is still to go) |
| `payByDate` `assignThisMonth` | `max(0, roundUpToCent((needed - (available - assigned)) / monthsRemaining) - assigned)`, capped at `toGo` |
| `payByDate` `percent` | `floor(min(available / needed, 1) × 100)`; `100` when nothing is owed |
| `monthlyAmount` `assignThisMonth` | month target `min(monthlyAmount, assigned + toGo)`; `max(monthTarget - assigned, 0)` |
| `monthlyAmount` `percent` | `floor(min(assigned / monthTarget, 1) × 100)`; `100` when the month target is 0 |
| `status` | `funded` when `toGo` is 0, `onTrack` when `assignThisMonth` is 0, otherwise `behind` |

Worked example (October 2026, target March 2027, balance −2,674.47, Available 500.40, Assigned 0): needed 2,674.47, to go 2,174.07, percent 18, six months remaining, assign `ceil(2,174.07 / 6)` = 362.35.

## Mortgages, loans and contracts

Loan account types are `mortgage`, `autoLoan`, `studentLoan`, `personalLoan`, `medicalDebt`, `otherDebt` and `contract`. They are created with the existing `POST /api/accounts` plus a `loan` object; `PUT /api/accounts/{id}` also accepts `loan` together with name, notes, `accountType` (within the loan family; a contract stays a contract) and `workingBalance` (the existing adjustment transaction). An account can never switch between the cash, credit and loan families (`400`).

```json
{ "commandId": "…", "version": 7, "name": "Mortgage (400K @ 3.47%)", "date": "2026-10-21", "openingBalance": -389651.68, "accountType": "mortgage",
  "loan": { "interestRate": 3.47, "minimumPayment": 1789.49, "escrowAmount": 0, "pairedCategoryId": null, "createPairedCategory": true, "contract": null } }
```

| Field | Rule |
|---|---|
| `openingBalance` | Debt as a negative amount (`0` allowed, positive rejected). For contracts send `0` (or the exact server value): the server records the remaining commitment on `date`. |
| `interestRate` | Annual percent, 0–100, at most three decimals. Contracts must send `0`. |
| `minimumPayment` | Positive money (includes escrow/fees). Contracts must send `0` (their payment comes from the schedule). |
| `escrowAmount` | Non-negative money, less than `minimumPayment`; it does not reduce principal. |
| `createPairedCategory` | Pairs the account's own `loan-{accountId}` category in the **Loans** group (created, or restored if archived). |
| `pairedCategoryId` | Pairs an existing active category of the current month instead (not a `ccp-*` card payment category, not another loan's category, not already paired). Both omitted = unpaired. |
| `contract` | Required for `contract`, rejected otherwise: `{ months 1–60, startDate, includesDevice, upfrontCost, monthlyServiceCost, deviceTotal, discountMonths, discountedServiceCost }`. `deviceTotal` must be 0 without a device; a discount needs both `discountMonths` (≤ months) and `discountedServiceCost` (≤ the regular cost). |

| Method | Route | Contract |
|---|---|---|
| `GET` | `/api/accounts/{id}/loan` | `LoanDto`: balance, terms, `originalBalance`, `percentRemaining`, `balanceConfirmedAt`, `estimatedInterestThisMonth`, `pairedCategory { id, name, groupName, available, isArchived, isAutomatic, targetKind, targetMoreNeeded }`, `projection`, month-end `history`, `activity` (newest month first: `endBalance`, `estimatedInterest`, `payments`, `other`) and `contract` (schedule, remaining device/contract cost, end date, total cost). `400` for non-loan accounts, `404` for unknown/other-budget accounts. |
| `GET` | `/api/accounts/{id}/loan/simulation?payment=&payoffMonth=&extra=` | Read-only. `payment` (default the minimum) or `payoffMonth` (`YYYY-MM`, after this month, within 100 years; solves the smallest whole-cent payment) and an optional one-time `extra` this month. Returns `{ balance, minimumPayment, extra, projection }`. Contracts return `400`. Never writes. |
| `PUT` | `/api/accounts/{id}/loan` | `{ commandId, version, terms }` replaces the terms and pairing (ledger command, audited as `loan.terms/{id}`). Closed accounts are rejected. |
| `POST` | `/api/accounts/{id}/loan/balance` | `{ commandId, version, balance? }`. Without `balance` it confirms the current balance (sets `balanceConfirmedAt`, the "Updated" date). With a different `balance` it also records a cleared "Balance Adjustment" transaction for the difference. |

`projection` is `{ monthlyPayment, paymentsRemaining, payoffMonth, interestRemaining, principalRemaining, remainingToPay, neverPaidOff, track[] }`; `track[0]` is the current month. Errors follow the ledger protocol: `400` validation (nothing written), `404` unknown account, `409` stale version or reused command ID.

**Budget treatment.** A transfer from a non-loan account into a loan is a payment: it reduces the debt and counts as activity (spending) in the paired category, exactly like categorized spending, so Ready to Assign is unchanged and the category's Available falls. Unpaired (or paired to a category missing in that month), the payment reduces Ready to Assign instead. A transfer out of a loan (a draw) is the reverse; transfers between two loan accounts are budget-neutral. Loan opening balances and loan-only transactions (interest, fees, balance adjustments) change the debt only, never Ready to Assign. Loan transactions cannot have a category, do not need one, and cannot be imported. In every case budget-account cash = Ready to Assign + Σ Available.

Worked example: Checking 5,000; mortgage −389,651.68 paired with "Loans: Mortgage". Ready to Assign 5,000. Assign 1,789.49 to the loan category (RTA 3,210.51) and transfer 1,789.49 Checking → mortgage: Checking 3,210.51, mortgage −387,862.19, loan category Activity −1,789.49, Available 0, RTA 3,210.51. After unpairing, the same payment reduces RTA to 1,421.02 and the category keeps its 1,789.49 Available (1,421.02 + 1,789.49 = 3,210.51 cash).

**Interest estimate.** `round_half_even(debt × rate / 1200, 2)`, computed on the debt carried into the month (the opening debt in the account's first month). It is shown as "Estimated" and never posted; record actual interest with Add Activity (a negative loan transaction). Example: 390,312.52 × 3.47 / 1200 = 1,128.65.

**Projection.** Starting from the current debt `B` (minus any one-time extra), each month: `I = round_half_even(B × r / 1200, 2)`, `B' = B + I − (payment − escrow)`. The final payment is capped at `B + I`. If a month does not reduce the debt (payment − escrow ≤ interest), or payoff would take more than 1,200 months, the result is `neverPaidOff`. Payment for a payoff date: annuity estimate `B·i·(1+i)^n / ((1+i)^n − 1)` rounded up to the cent, then increased cent by cent until the simulation finishes within `n` months; escrow is added back. Worked examples (decimal, verified by unit tests):

| Scenario | Result |
|---|---|
| €389,651.68 at 3.47%, €1,789.49/month | 344 payments (28 yrs, 8 mos), payoff Jun 2055 from Oct 2026, interest €225,929.45, final payment €1,786.06, remaining to pay €615,581.13 |
| Same, payoff Oct 2046 (240 payments) | €2,253.83/month, interest €151,264.28 |
| Same, €10,000 one-time extra now | 330 payments, interest €209,462.96 |
| €100,000 at 12%, €1,000/month | never paid off (interest is exactly €1,000) |

The reference screenshot shows €225,929.41 interest; our per-month half-even cent rounding gives €225,929.45 (the 4-cent difference is cumulative rounding). Payment counts and payoff month match.

**Contracts.** Item `N` is due on `startDate + (N−1) months` (day clamped). The device total is split into equal floor-to-cent instalments, the last absorbing the remainder; for the first `discountMonths` items the contract (service) cost is `discountedServiceCost`. Remaining commitment at a date = items due on or after it, plus the upfront cost if the contract has not started (counted as device cost when a device is included). The account balance is −(remaining device + remaining contract cost); both parts and the total contract cost are reported separately. Example: 24 months from 31 Jan 2026, upfront 50, service 20 (10 for the first 6 months), device 599.99: instalments 23 × 24.99 + 25.22; total cost 1,069.99; on 30 Oct 2026, 15 items remain: device 375.08 + contract 300.00 = balance −675.08.

## Category targets

A regular Plan category (any category outside the `credit-card-payments` group; `ccp-*` payment categories use [debt payoff targets](#debt-payoff-targets)) may have one structured target per budget. Targets are keyed by the logical category ID, so the same target applies in every month. They never change assignments, Available, Ready to Assign or ledger balances.

Routes use the same access rule as `/api/budgets/{id}/months/{month}` (owner or joined editor of an active budget; others get `404`) and run under the budget's `LedgerGate`. Validation and conflict responses roll the gate transaction back, including a month initialized by that request.

| Method | Path | Body / result |
|---|---|---|
| `GET` | `/api/budgets/{id}/categories/{categoryId}/target/{YYYY-MM}` | `{ categoryId, month, target, progress }`; `target`/`progress` are `null` without a target. |
| `PUT` | `/api/budgets/{id}/categories/{categoryId}/target` | `{ commandId, version, month, kind, amount, behavior, weekday?, dayOfMonth?, dueDate?, repeatEvery?, repeatUnit? }` creates (`version: 0`) or replaces (current `target.version`) the target; returns the result for `month`. |
| `POST` | `/api/budgets/{id}/categories/{categoryId}/target/delete` | `{ commandId, version, month }`; returns the result with `target: null`. |

Validation (`400` without writes): the category must exist in `month` and be visible (not archived, group not archived) and must not be a payment category; `kind` is `weekly`, `monthly`, `yearly` or `custom`; `amount` is greater than zero with at most two decimals (maximum 999,999,999.99); `behavior` is `setAside` or `refill`. Weekly requires `weekday` 0 (Sunday)–6 (Saturday) and `setAside`. Monthly takes optional `dayOfMonth` 1–31 (null or 0 = last day; later days clamp to short months). Yearly and custom require `dueDate` (`YYYY-MM-DD`, years 1900–9998). Only custom takes `repeatEvery` 1–99 with `repeatUnit` `day`/`week`/`month`/`year` (both or neither); a non-repeating custom target cannot be due before the server's local date. Fields that do not belong to the kind are rejected. A missing category returns `404`.

Writes use the target's own optimistic `version` (incremented on each save): a stale version, or deleting a target that no longer exists, returns `409` with `{ error, current }`, where `current` is the latest `{ target, progress }`. Each write appends a command receipt (`categoryTarget.save/{categoryId}` or `categoryTarget.delete/{categoryId}`) with before/after JSON to the ledger audit feed (`GET /api/ledger/audit`); targets do not advance the ledger revision. An identical replay of a `commandId` returns the current target state without a second write; a reused `commandId` with a different body returns `409`. Plan **Recent Moves** remains assignment-only, so target changes are not listed there.

Changing kind, behavior, due date or repeat starts a new funding cycle (`startMonth` becomes the request month); changing only the amount, weekday or day keeps it. Each month DTO category (`GET/PUT /api/budgets/{id}/months/{month}` and the legacy month routes) includes `categoryTarget` (the definition) and `targetProgress` (calculated for that month; `null` for hidden categories). The legacy decimal `target` field is still accepted and returned for compatibility, but the Plan UI no longer edits it.

### Target progress

`targetProgress` is `{ needed, funded, moreNeeded, segments, fundedFraction, status, nextDueDate, monthsRemaining, isPastDue, cadence }`. Let A be the amount, `funded = max(Assigned this month, 0)` and `before = Available − Assigned` (opening balance plus this month's activity).

| Target | `needed` this month |
|---|---|
| Weekly | `A × occurrences of the weekday in the month`; `segments` = occurrences (4 or 5) |
| Monthly, set aside | `A` |
| Monthly, refill | `max(A − before, 0)` (top Available up to A) |
| Yearly / dated, set aside | `max(roundUpToCent((A − Assigned in earlier months of the cycle) / monthsRemaining), 0)` |
| Yearly / dated, refill (fill up) | `max(roundUpToCent((A − before) / monthsRemaining), 0)` |
| Custom repeating every N days/weeks | `A × due dates in the month` (minus `before` when filling up); a month with no due date spreads A towards the next one like a dated target |
| Non-repeating custom after its due date | the whole unfunded remainder (`isPastDue` true; 0 once funded) |

`monthsRemaining` counts the viewed month through the due month inclusive. Yearly targets (and custom targets repeating every N months/years) advance their due date by the period once it has passed; the next cycle starts the month after the previous due month and never before the target's `startMonth`. `moreNeeded = max(needed − funded, 0)`, `funded` in the response is capped at `needed`, `fundedFraction = min(funded / needed, 1)` (truncated to 4 decimals; 1 when nothing is needed) and `status` is `underfunded` while `moreNeeded > 0`, otherwise `funded`. `cadence` is a short schedule description such as `Every Friday` or `Every year by 15-03-2027`.

Worked examples (amounts in EUR):

- Weekly 25 every Friday, October 2026 (five Fridays), Assigned 50: needed 125, more needed 75, 5 segments, fraction 0.4. In November 2026 (four Fridays) needed is 100.
- Monthly set aside 100, Assigned 30, Available 230: needed 100, more needed 70 (carried money does not count).
- Monthly refill up to 150, Assigned 20, Available 80: before = 60, needed 90, more needed 70.
- Yearly 1,000 by 15-03-2027 viewed in October 2026: six months, 1,000 / 6 = 166.666… → 166.67. In December with 166.67 and 100 assigned in October/November: (1,000 − 266.67) / 4 = 183.3325 → 183.34.
- Custom 40 every 2 weeks from 02-10-2026: October has 2, 16 and 30 October → needed 120 in 3 segments; November (13 and 27) → 80.

The Plan **Underfunded** filter shows categories whose `targetProgress.moreNeeded` is above zero (snoozed categories excluded); **Overfunded** shows categories whose Assigned exceeds `needed`. Categories without a structured target keep the previous legacy-`target` rule. **Auto-Assign → Underfunded** assigns, per category, the larger of the scheduled-outflow shortfall and `moreNeeded`, still capped by Ready to Assign.

## Wishlist

Budget-scoped wish lists funded by Plan categories ([Milestone 13](milestones/milestone-13-wishlist.md)). Every route requires the `wishlist` entitlement (`403` otherwise; seeded for Plus and Pro, denied for Free, per-user overrides win) and owner/editor access to an active budget (`404` otherwise). Requests run under the budget's `LedgerGate`; validation errors (`400`), missing resources (`404`) and conflicts (`409`) roll back the whole transaction, including Plan month changes. Every write carries the overview's `version` (0 before the first write) and returns the new overview; a stale version returns `409` with `{ error, current }`.

| Method | Path | Body / behavior |
|---|---|---|
| `GET` | `/api/budgets/{id}/wishlist?month=YYYY-MM` | Overview `{ isEnabled, version, month, readyCount, buckets[] }`. `month` defaults to the server's current month and selects the Available used for progress. |
| `POST` | `/api/budgets/{id}/wishlist/enable` / `disable` | `{ version }`. Enable creates/restores the `wishlist` group and `wl-{bucketId}` categories in every initialized month (initializing the current month first); disable archives the group without moving money. |
| `POST` | `/api/budgets/{id}/wishlist/buckets` | `{ version, name }` creates a bucket (1–60 characters, unique case-insensitively, max 50) and its Plan category. |
| `PUT` | `/api/budgets/{id}/wishlist/buckets/{bucketId}` | `{ version, name }` renames the bucket and its category in every month. |
| `PUT` | `/api/budgets/{id}/wishlist/buckets/order` | `{ version, bucketIds }` (every active bucket exactly once) reorders buckets and their categories. |
| `POST` | `/api/budgets/{id}/wishlist/buckets/{bucketId}/delete` | `{ version }` archives the bucket and category; rejected while the category has non-zero Available in the current, a later or the latest initialized month. |
| `POST` | `/api/budgets/{id}/wishlist/buckets/{bucketId}/items` | `{ version, name, url?, cost, description? }` adds an item at the bottom of the bucket. |
| `PUT` | `/api/budgets/{id}/wishlist/items/{itemId}` | `{ version, name, url?, cost, description?, bucketId? }` edits an active item; a different `bucketId` moves it to the end of that bucket. |
| `PUT` | `/api/budgets/{id}/wishlist/buckets/{bucketId}/items/order` | `{ version, itemIds }` (every active item exactly once) sets the desire order. |
| `POST` | `/api/budgets/{id}/wishlist/items/{itemId}/purchase` / `remove` / `restore` | `{ version }`. Purchase/remove move an active item to history (`purchasedAt`/`removedAt`); restore returns it to the end of the bucket. |

Item validation: name 1–120 characters; cost greater than zero with at most two decimals (max 999,999,999,999.99); description ≤ 1000; optional `url` must be an absolute `http`/`https` URL without whitespace or credentials, ≤ 2048 characters. Bucket and item writes require Wishlist to be enabled for the budget.

Each bucket is `{ id, name, sortOrder, categoryId, available, minCost, maxCost, progress, items, purchased, removed }`: `items` are active items in desire order; `purchased`/`removed` list the latest 50 history entries. `progress` (also returned as `wishlistProgress` on the bucket's category in `GET/PUT /api/budgets/{id}/months/{month}` while Wishlist is enabled) is `{ nextItemId, nextItemName, cost, available, remaining, canPurchase, fundedFraction }` for the top active item: `remaining = max(cost − available, 0)`, `canPurchase = available ≥ cost`, `fundedFraction = available ≤ 0 ? 0 : min(available / cost, 1)`; `null` for a bucket without active items. `readyCount` counts buckets whose next item can be purchased.

Plan month saves keep the Wishlist group server-owned: assigning/moving money and moving the whole group are allowed, but renaming, archiving, adding, removing, reordering or moving `wl-*` categories, renaming/archiving the group, or placing other categories into it returns `400`.

Administrators can list feature keys with `GET /api/admin/entitlement-keys` (known keys such as `wishlist` plus any configured key) and a user's overrides with `GET /api/admin/users/{id}/entitlements`.

## Register and payees

`GET /api/transactions` combines optional `accountId`, `transferId` (UUIDs), `search` (case-insensitive literal payee/memo substring, max 160), inclusive ISO `from`/`to`, `categoryId` (matches single category or split), and `status` (`uncleared`, `cleared`, `reconciled`). `needsAttention=true` selects the union of unapproved entries and spending needing a category. Optional `includeReconciled=false` excludes reconciled entries before counting/pagination; default is true. It combines with all other filters and never changes account totals or full-history running balances. Special `categoryId=uncategorized` selects negative non-opening/non-transfer entries without categories. Start date must not exceed end date.

Pagination: `page` 1–1,000,000 (default 1), `pageSize` 1–100 (default 50). Returns `{ version, page, pageSize, total, items, accounts }`. The embedded accounts response shares the register's database snapshot/revision. Ordering is date descending then UUID ascending; pages remain stable while the revision is unchanged. Unknown account filters return empty results. Deleted entries are excluded; closed-account history remains visible.

An optional `payeeId` UUID filters by the exact saved payee before pagination.

Items contain `id`, `accountId`, `date`, signed `amount`, `payee`, `memo`, nullable `categoryId`, `status`, nullable shared `transferId`, `isOpening`, `splits: [{ categoryId, amount, payee?, memo? }]`, and `needsAttention`. Split payee and memo are optional per-part descriptions; each part remains assigned to an active category and the signed split amounts must equal the parent amount. `GET /api/payees` returns visible saved name suggestions in name order (hidden payees excluded); trimmed names are reused case-insensitively and retained after deletion of an entry.

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

`GET /api/register-views/{scope}` returns `{ version, datePreset, fromMonth, toMonth, showReconciled, showRunningBalance, showFlags, showImages, showScheduledTransactions, columnWidths }`. Scope is the account UUID, or `all` for the independent All Accounts view. Unknown accounts return 404. Unsaved views return version 0, `allDates`, null month bounds, all switches true and an empty `columnWidths` object. A GET does not create a row.

`PUT /api/register-views/{scope}` accepts the same complete object with its expected version. Presets are `thisMonth`, `lastMonth`, `latest3Months`, `thisYear`, `lastYear`, `allDates`, or `custom`. Month bounds are nullable `YYYY-MM` (1900–9998); custom requires both and From <= To. `columnWidths` maps visible column keys (`check`, `info`, `flag`, `image`, `date`, `payee`, `category`, `memo`, `outflow`, `inflow`, `balance`, `status`) to pixel widths from 24 through 1200. All switches, including whether scheduled transactions are expanded, month selectors and column widths are saved atomically. Success returns the incremented view version. A stale edit returns 409; reload saved options, review, and apply a new draft. Exact retry of the immediately preceding identical saved selection returns its existing result, handling a lost response without another write. Invalid input returns 400 without partial changes.

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

The inline register and split rows use searchable grouped pickers rather than native selects. Split parts render as aligned register subrows with payee, category, memo, and outflow/inflow fields; the editor shows the remaining amount to assign and blocks saving until every active category and signed amount totals exactly to the transaction. Split payee and memo persist with the part. Group headings are flush left; their categories are indented, with the server's `available` amount aligned right. The Selected section includes the parent group, and Inflow / Ready to Assign displays the same transaction month's server `readyToAssign`. Positive amounts are green, negative red, zero neutral. The list follows budget ordering and excludes archived categories/groups from ordinary choices. Duplicate display names select by stable category ID.

Amounts come from `GET /api/budget/{transaction month}` with no account-local or draft-derived calculation. Opening the dropdown, changing the transaction date, retrying a read, or creating a category reloads that month's values. Pending reads are cancelled when superseded. Failed/loading reads do not permit selecting stale hidden options. Search matches category and group names; keyboard arrows/Enter select, Escape discards search and keeps the inline draft. Transfer/opening entries retain their protected category behavior.

New Category reads a fresh monthly snapshot, requires a name and active existing group, and uses the existing versioned month PUT with a new stable ID and zero assigned/target. It preserves all existing fields and never sends calculated activity/available values. Name duplicates within the selected active group are rejected in the form. A stale month requires explicit Reload budget, retaining the name. If a response is lost, Retry save reads back the stable new ID before retrying the same snapshot; it never invents another category ID. Closing/navigation is blocked until an uncertain save is resolved. Creating/selecting a category preserves the transaction's unsaved date, payee, memo, amount and splits. Returning to Plan reloads the saved month as before.

The footer reuses the existing Split editor and account transfer choices. Payment/Transfer opens payee account choices; credit-card accounting remains outside the current cash-ledger milestone. Category and Ready to Assign amounts are budget-wide, independent of the account being edited.


## Assignment rollover and earlier-month edits

For a category, Available = prior carried amount + current Assigned + current Activity. Example: August Assigned 50, categorized spending -35 gives Available 15; September starts with Assigned 0 / Available 15; assigning 50 in September gives Available 65. Changing August Assigned to 80 subsequently gives August Available 45 and September Available 95, with September Assigned still 50. Reducing August to 50 restores September Available 65. Assignment is a monthly amount, not a repeated template; only the remaining positive available money carries. If August instead ends at -20, September starts that category at zero and its Ready to Assign is reduced by 20. Assigning money in August to cover that shortfall removes the Ready to Assign deduction without charging the following month twice.

`PUT /api/budget/{month}` performs the correction under the shared ledger lock and transaction. Invalid/stale requests and save failures do not leave partially updated future months or an implicitly initialized rejected month. Repeating the old monthly version returns 409 with the current snapshot; it never adds the carry twice. A later month's stale write returns its corrected `current` snapshot for reload/reapply. Concurrent ledger changes and assignments serialize under the same lock. Future months are corrected in order so threshold crossings between positive category carry and Ready to Assign withholding are reflected at every boundary. New category/group definitions needed to retain incoming positive historical carry are materialized in initialized later months without copying source assignments/activity or overwriting existing later metadata.

The Plan UI waits for pending assignment saves before changing months; unsaved/failed edits cannot be lost by month navigation. Undo/Redo history resets after loading a different month. Assigned input accepts signed decimal amounts with at most two fractional digits (decimal comma supported; blank clears to zero). Invalid text, exponent notation, excess precision and out-of-range values remain visible with an error and are never silently converted to zero. All persisted available/Ready to Assign totals come back from the server.

## Transaction image attachments

Receipts and other images attach to one transaction in the active budget. All routes require an authenticated session (explicit `RequireAuthorization`, also in the Testing host) and resolve the transaction/image through the budget-scoped query filter, so an image in a budget the caller cannot open returns `404`, never `403`. Owners and editor members have the same access. Image changes are non-financial: they are allowed on reconciled transactions and in closed accounts, never bump the ledger version and never recalculate balances.

| Route | Body | Result |
|---|---|---|
| `GET /api/transactions/{id}/images` | — | `{ transactionId, maxImages, images: [{ id, transactionId, fileName, contentType, sizeBytes, width, height, source, createdAt, url }] }`, oldest first |
| `POST /api/transactions/{id}/images` | `multipart/form-data`: `commandId` plus 1–10 `files` parts | Updated list |
| `POST /api/transactions/{id}/images/from-url` | `{ commandId, url }` | Updated list |
| `GET /api/transaction-images/{imageId}` | — | Image bytes |
| `POST /api/transaction-images/{imageId}/delete` | `{ commandId }` | `{ id }` |

`TransactionDto.imageCount` (additive) carries the count for each register row, so the register needs no extra calls.

- **Validation.** Only JPEG, PNG, WebP and GIF are accepted, identified by magic bytes; the client content type and extension are ignored. SVG, HTML, PDF and anything else return `400`. Content whose first KiB contains markup (`<html`, `<script`, `<svg`, `<?xml`, …) after metadata removal is rejected as a polyglot. Header dimensions must be readable, 1–20,000 px per side and at most 100 megapixels; pixels are not decoded. Each image is at most 10 MB (`413`), a request carries at most 10 files and 50 MB (`413`, enforced through the request-body-size feature and multipart form limits, buffered in memory only), and a transaction holds at most 20 images (`409`, checked under the budget ledger lock so concurrent uploads cannot exceed it). A failing file rejects the whole request with no partial writes.
- **Names.** The display name is reduced to ASCII letters, digits, space and `-_().`, path segments are removed, it is at most 100 characters, and the extension is replaced with the detected one. It is never used as a storage key.
- **Privacy.** JPEG APP1 (EXIF/XMP, including GPS), APP13 (IPTC) and COM segments, PNG `eXIf`/`tEXt`/`zTXt`/`iTXt`/`tIME` chunks and WebP `EXIF`/`XMP ` chunks are removed before storage. A JPEG EXIF orientation is re-emitted as a minimal one-tag APP1 so phone photos stay upright. GIF has no EXIF container.
- **Serving.** Bytes are returned with the stored, validated `Content-Type`, `X-Content-Type-Options: nosniff`, `Content-Disposition: inline; filename="<sanitized>"`, `Content-Security-Policy: default-src 'none'; img-src 'self'; sandbox`, `Cross-Origin-Resource-Policy: same-origin` and `Cache-Control: private, max-age=3600`. IDs are random UUIDs. There is no thumbnail endpoint: no image library is used, so thumbnails are the original bytes sized with CSS (the private cache keeps repeated views cheap).
- **URL import.** The URL must be `https://` on port 443, at most 2,048 characters and without credentials. The server fetches it with a dedicated client: no proxy, cookies or decompression; DNS is resolved in `SocketsHttpHandler.ConnectCallback` and the connection is refused when **any** resolved address is loopback, private (RFC 1918), CGNAT, link-local (including `169.254.169.254`), multicast, broadcast/reserved, documentation/benchmark, IPv6 non-global (`::1`, ULA `fc00::/7`, link-local, site-local, NAT64, IPv4-compatible), 6to4/Teredo, or an IPv4-mapped form of a blocked address; the connected remote address is re-checked. Redirects are followed manually (at most 3) and each hop is re-validated. A 10-second overall timeout and a streamed 10 MB cap apply, then the same magic-byte validation. Failures return `422` with one generic message; fetched bodies, upstream errors and resolved addresses are never returned. Only the host is logged, never the path or query string. Imports are rate limited to 20 per user per minute. Exact replay of the command returns the current list without fetching again.
- **Commands and audit.** `commandId` is required. Exact replay returns the current state; reusing an ID for a different request returns `409`. Each add/delete writes a `LedgerCommands` receipt (`transaction.image.add/{transactionId}` or `transaction.image.delete/{imageId}`, resource = transaction ID) at the **current** ledger version, visible through `GET /api/ledger/audit?resourceId={transactionId}`. Receipts hold metadata only — transaction ID, image ID, size, content type, source — never bytes, names or URLs.
- **Lifecycle.** Split transactions keep images on the parent transaction; each transfer leg has its own images. Images of a soft-deleted transaction are kept but hidden: listing, download and delete return `404`. Deleting an image removes its metadata and bytes permanently (the receipt remains). Deleting a whole budget cascades its transactions, images and bytes.

## Agent tokens, MCP and live events

Full details, the tool list and the permission matrix are in [MCP server for personal AI agents](mcp.md).

| Method/path | Behavior |
|---|---|
| `GET /api/auth/agent-tokens` | Cookie session. Lists the signed-in user's agent tokens without secrets or hashes (`no-store`). |
| `POST /api/auth/agent-tokens` | Cookie session. `{ "name", "permission": "Admin"|"Budget"|"Reporting", "budgetIds", "expiresInDays" }` → `201 { "token": "obk_…", "details" }`; the plaintext is returned only here. Admin requires the Administrator role (`403`); Budget/Reporting need 1-50 budgets the user owns or edits (`400` with one generic message otherwise); expiry 1-365 days (default 90); at most 25 active tokens (`409`). |
| `POST /api/auth/agent-tokens/{id}/revoke` | Cookie session. Immediate revocation (`204`); another user's token is `404`. |
| `GET /api/auth/agent-tokens/{id}/activity?limit=50` | Cookie session. Recent MCP calls for the token: tool, budget, success, outcome code and time (no arguments or values). |
| `POST /mcp` | MCP Streamable HTTP (stateless) for agent tokens only: `Authorization: Bearer obk_…`. Cookies are ignored (`401` without a valid bearer). HTTPS required outside Development; per-token rate limit (`429` + `Retry-After`); 1 MiB body limit. |
| `GET /api/budgets/{id}/events` | Cookie session, owner/editor only (`404` otherwise). Server-sent `change` events `{ budgetId, kind, month, version, at }` published after commit; `kind` is `plan`, `ledger`, `targets`, `planning` or `admin`. Heartbeat every 25 s; at most 10 streams per user (`429`). |
