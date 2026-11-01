# MCP server for personal AI agents

OpenBudget hosts a [Model Context Protocol](https://modelcontextprotocol.io) server inside the API process so the budget owner's own AI agents (Claude Code, Claude Desktop or any MCP client that supports remote Streamable HTTP servers) can read and manage budgets. Changes an agent makes are committed through the same application services as the web app and appear live in open browser sessions.

| Item | Value |
|---|---|
| Endpoint | `POST /mcp` (Streamable HTTP, stateless; JSON or SSE responses) |
| SDK | Official C# SDK `ModelContextProtocol.AspNetCore` **2.2.0** (released 2026-08-13) |
| Authentication | `Authorization: Bearer obk_…` agent token only — session cookies are rejected |
| Transport security | HTTPS required outside Development/Testing (`400` otherwise); production traffic arrives through the nginx proxy with `X-Forwarded-Proto` |
| Rate limit | 120 requests per token per 60 s by default (`Mcp:RateLimit:PermitLimit`, `Mcp:RateLimit:WindowSeconds`); `429` with `Retry-After` |
| Request size | 1 MiB maximum body |

## 1. Create an agent token

1. Sign in to OpenBudget and open **Profile** (avatar, top right) → **Agent tokens**.
2. Enter a name (for example "Laptop assistant"), choose a permission level and — for Budget or Reporting tokens — tick the budgets the agent may use. Only budgets you own or edit are offered. Admin is only offered to administrators.
3. Choose an expiry (default 90 days, maximum 365) and select **Create token**. Ticking **No expiration** is an explicit opt-in (never the default) and shows a warning; see [Non-expiring tokens and renewal](#non-expiring-tokens-and-renewal).
4. Copy the token immediately. It is shown once; OpenBudget stores only its SHA-256 hash and a short display prefix (`obk_` + 8 characters).
5. Use **Recent activity** to see each tool call the token made (tool, budget, outcome, time), **Renew** (with confirmation) to replace the secret, and **Revoke** (with confirmation) to cut access immediately.

Treat a token like a password: store it in your client's secret store or an environment variable, never in source control, shared config or chat transcripts. For server-side automation in Azure, keep it in **Azure Key Vault** and read it with a Managed Identity at run time.

The REST API behind the screen (cookie session only — an agent token cannot manage tokens):

| Method/path | Behavior |
|---|---|
| `GET /api/auth/agent-tokens` | The signed-in user's tokens (never the secret or hash): id, name, prefix, permission, budgets, created/expires/last-used/revoked/renewed timestamps and status (`active`, `expired`, `revoked`). `expiresAt` is `null` for a token created or renewed with `neverExpires: true`. |
| `POST /api/auth/agent-tokens` | `{ "name", "permission": "Admin"|"Budget"|"Reporting", "budgetIds": [...], "expiresInDays": 1-365, "neverExpires": true|false }` → `201 { "token": "obk_…", "details": {…} }` with `Cache-Control: no-store`. Omitting both lifetime fields gives the 90-day default; only an explicit `"neverExpires": true` creates a non-expiring token, and sending it together with `expiresInDays` is `400`. `403` when a non-administrator requests Admin; `400` for invalid names, expiry, or any budget the user cannot access (one generic message, so other users' budgets are not disclosed); `409` beyond 25 active tokens. |
| `POST /api/auth/agent-tokens/{id}/renew` | Optional body `{ "expiresInDays": 1-365 }` or `{ "neverExpires": true|false }`. Rotates the secret of the caller's own unrevoked (active or expired) token: `200 { "token": "obk_…", "details": {…} }` with `no-store`; the previous secret stops working immediately. Permission and budgets are kept. Without a body the expiry mode is kept (a dated token gets a fresh 90-day expiry; a non-expiring token stays non-expiring). `404` for another user's or an unknown token; `409` for a revoked token, a concurrent renew/revoke, or reactivating an expired token beyond 25 active tokens; `400` for an invalid lifetime. |
| `POST /api/auth/agent-tokens/{id}/revoke` | Revokes immediately (`204`); `404` for another user's token. |
| `GET /api/auth/agent-tokens/{id}/activity?limit=50` | Recent MCP audit rows for the token (owner only). |

Token creation, renewal and revocation are also written to the administrator security audit without any secret: `agent-token.create.<level>`, `agent-token.renew` and `agent-token.revoke`, with a `.no-expiry` suffix on create/renew when the resulting token does not expire (for example `agent-token.create.budget.no-expiry`).

### Non-expiring tokens and renewal

A non-expiring token is a long-lived credential: if it leaks (shell history, a synced config file, a chat transcript) it keeps working until you notice and act, instead of lapsing on its own. Prefer a dated expiry, especially for Admin tokens. If you opt in to **No expiration**:

- Keep the token in a secret store (OS keychain, the client's secret store, or Azure Key Vault read through a Managed Identity for server-side automation), never in source control.
- **Renew it regularly**, for example every 90 days, and immediately after any suspected exposure or when a device or person that held it changes. Renewal generates a new secret for the same token (same name, permission and budgets) and the old secret stops working at once, so update the agent's configuration with the new value straight away.
- Review **Recent activity** and the last-used time for unexpected use, and **Revoke** tokens you no longer need. A revoked token cannot be renewed; create a new one.

Renewal is deliberately not replay-safe: OpenBudget stores only the hash, so a lost renew response cannot be replayed to recover the secret; renew again. Concurrent renewals, or a renewal racing a revocation, are resolved by a conditional update, so at most one returned secret is ever valid (the others get `409`).

## 2. Connect a client

Replace `https://budget.example.com` with your deployment origin and `${OPENBUDGET_TOKEN}` with a token kept in your environment or secret store. In local Development the API is reachable directly at `http://127.0.0.1:5080/mcp` (the Vite dev server does not proxy `/mcp`).

**Claude Code** (remote HTTP server with a header):

```bash
claude mcp add --transport http openbudget https://budget.example.com/mcp \
  --header "Authorization: Bearer ${OPENBUDGET_TOKEN}"
```

or in a project/user `.mcp.json`:

```json
{
  "mcpServers": {
    "openbudget": {
      "type": "http",
      "url": "https://budget.example.com/mcp",
      "headers": { "Authorization": "Bearer ${OPENBUDGET_TOKEN}" }
    }
  }
}
```

**Claude Desktop** (`claude_desktop_config.json`, using the `mcp-remote` bridge for clients that only speak stdio):

```json
{
  "mcpServers": {
    "openbudget": {
      "command": "npx",
      "args": ["-y", "mcp-remote", "https://budget.example.com/mcp", "--header", "Authorization:${OPENBUDGET_AUTH}"],
      "env": { "OPENBUDGET_AUTH": "Bearer <paste-token-here-locally-never-commit>" }
    }
  }
}
```

Any other client: send JSON-RPC 2.0 `POST /mcp` with `Accept: application/json, text/event-stream`, `Content-Type: application/json`, the bearer header and (after `initialize`) `MCP-Protocol-Version`.

## 3. Using the tools

- Start with `budgets_list`; every budget-scoped tool takes that `budgetId`.
- Money is a decimal in the budget currency with at most two decimal places; outflows are negative.
- Writes use optimistic concurrency. Pass the `version` you last read (Plan month version for Plan tools, ledger version for ledger tools, target or planning versions where described) to make stale writes fail with a `conflict` error that includes `current` data when the service provides it — or omit `version` and the tool reads the current one itself.
- Ledger writes accept an optional `commandId` (UUID). Retrying with the same `commandId` and identical arguments — including the same explicit `version` — replays the original receipt instead of writing twice; reusing it for different arguments is a conflict. (The receipt fingerprint covers the expected version, so a retry that omits `version` after the ledger moved on is reported as a conflict, not replayed.)
- Account types (`accounts_create`/`accounts_update` `accountType`, required on create, never defaulted) are the web app's list: cash `checking`, `savings`, `cash`; credit `creditCard`, `lineOfCredit`; mortgages and loans `mortgage`, `autoLoan`, `studentLoan`, `personalLoan`, `medicalDebt`, `otherDebt`, `contract`. Cash and credit accounts take a signed `openingBalance`. Loan types take `loan` instead, as the "Add Unlinked Account" loan form does: `balanceOwed` (positive), `interestRate` (annual %, up to three decimals), `minimumPayment` (> 0, includes escrow) and optional `escrowAmount`; `contract` takes `loan.contract` (months, start date, device and service costs, optional discount) and no rate or payment. Either kind may pair an existing category (`pairedCategoryId`) or create one (`newPairedCategory`). There is no term field: the payoff month is projected from the payment (`loans_get`, `loans_simulate`). A missing, unknown or inconsistent type/terms combination returns `invalid_arguments`/`invalid_request` naming the valid values, and nothing is written.
- Destructive tools (and destructive options such as `transactions_bulk_update` with `action: "delete"`, `transactions_review` with `reject`, `payees_save` with `delete`, `schedules_action` with `end`/`skip`) require `"confirm": true`. Without it they return `confirmation_required` and change nothing. Agents must ask the user first.
- Results carry `structuredContent` (JSON) plus a short text summary and the same JSON as text. Errors set `isError: true` with `{ "error": { "code", "status", "message", "current"? } }`; codes are `invalid_arguments`, `invalid_request`, `confirmation_required`, `not_found`, `forbidden`, `conflict`, `unavailable`, `internal_error`.
- `tools/list` only returns tools the token may call. Each tool advertises `readOnlyHint`, `destructiveHint`, `idempotentHint` and `openWorldHint: false`, and `_meta["openbudget/permissions"]` lists the levels that may call it. Calling a tool outside the token's level is answered exactly like an unknown tool (JSON-RPC `-32602`).

## 4. Permission matrix

| Level | Scope |
|---|---|
| **Reporting** | Read-only, listed budgets only: budgets, Plan months, categories/targets, accounts and balances, loan details and payoff simulations, transactions (filters/pagination), payees, flags, scheduled transactions, long-term planning, Wishlist, receipt image metadata, credit summaries and payoff targets, ledger audit receipts, Recent Moves. No writes at all. |
| **Budget** | Listed budgets only: read Plan months, targets and Recent Moves, set a category's assigned amount and move money between categories. No ledger, structure, target, sharing or user changes. |
| **Admin** | Everything the creator can do in the app: all Reporting and Budget tools plus ledger, Plan structure, targets, long-term planning, budget lifecycle, sharing, user administration and application settings, across every budget the creator can access. Only administrators can create Admin tokens. |

Budget-scoped calls check, on every call, that the budget is in the token's list (Admin: any budget) **and** that the creator still owns or edits that active budget. Unknown, foreign, archived and out-of-scope budgets all return the same `not_found` result. Owner-only operations (rename/archive/delete budgets, sharing) still require the creator to be the owner; administrator operations still evaluate the endpoint's Administrator role policy.

Every tool:

| Tool | Purpose | Reporting | Budget | Admin | Kind |
|---|---|:-:|:-:|:-:|---|
| `budgets_list` | List budgets | ✓ | ✓ | ✓ | read-only |
| `budgets_create` | Create budget | — | — | ✓ | write |
| `budgets_rename` | Rename budget | — | — | ✓ | write |
| `budgets_archive` | Archive budget | — | — | ✓ | destructive (confirm) |
| `budgets_restore` | Restore budget | — | — | ✓ | write |
| `budgets_delete` | Delete budget | — | — | ✓ | destructive (confirm) |
| `plan_get_month` | Get Plan month | ✓ | ✓ | ✓ | read-only |
| `plan_set_assigned` | Set category assignment | — | ✓ | ✓ | write |
| `plan_move_money` | Move money between categories | — | ✓ | ✓ | write |
| `plan_recent_actions` | Recent Plan moves | ✓ | ✓ | ✓ | read-only |
| `plan_set_note` | Set Plan month note | — | — | ✓ | write |
| `category_groups_create` | Create category group | — | — | ✓ | write |
| `category_groups_update` | Update category group | — | — | ✓ | write |
| `categories_create` | Create category | — | — | ✓ | write |
| `categories_update` | Update category | — | — | ✓ | write |
| `category_groups_move` | Reorder category group | — | — | ✓ | write |
| `categories_move` | Reorder category / move to another group | — | — | ✓ | write |
| `category_targets_get` | Get category target | ✓ | ✓ | ✓ | read-only |
| `category_targets_save` | Create or update category target | — | — | ✓ | write |
| `category_targets_delete` | Delete category target | — | — | ✓ | destructive (confirm) |
| `accounts_list` | List accounts | ✓ | — | ✓ | read-only |
| `accounts_create` | Create account | — | — | ✓ | write |
| `accounts_update` | Update account | — | — | ✓ | write |
| `accounts_reorder` | Reorder accounts (every account ID exactly once) | — | — | ✓ | write |
| `accounts_delete` | Delete account (refused while it has transactions other than its opening balance, active schedules or payment-category money) | — | — | ✓ | destructive (confirm) |
| `accounts_credit_summary` | Credit account monthly summary | ✓ | — | ✓ | read-only |
| `reconcile_account` | Reconcile account | — | — | ✓ | write |
| `transactions_list` | List transactions | ✓ | — | ✓ | read-only |
| `transactions_create` | Create transaction (optional `wishlistItemId` records a Wishlist purchase; payee and memo are kept as given) | — | — | ✓ | write |
| `transactions_import` | Import transaction | — | — | ✓ | write |
| `transactions_update` | Update transaction | — | — | ✓ | write |
| `transactions_delete` | Delete transaction | — | — | ✓ | destructive (confirm) |
| `transactions_set_status` | Set transaction cleared status | — | — | ✓ | write |
| `transactions_review` | Approve or reject transaction | — | — | ✓ | write; destructive options need confirm |
| `transactions_bulk_update` | Bulk update transactions | — | — | ✓ | write; destructive options need confirm |
| `transactions_set_flag` | Flag transaction | — | — | ✓ | write |
| `transaction_images_list` | List a transaction's receipt images (metadata only) | ✓ | — | ✓ | read-only |
| `transaction_images_delete` | Delete a receipt image | — | — | ✓ | destructive (confirm) |
| `transfers_create` | Create transfer (optional `categoryId`: required between a tracking and a budget account, optional on a cash→loan payment) | — | — | ✓ | write |
| `transfers_update` | Update transfer (same optional `categoryId`) | — | — | ✓ | write |
| `ledger_audit_list` | Ledger audit history | ✓ | — | ✓ | read-only |
| `payees_list` | List payees | ✓ | — | ✓ | read-only |
| `payees_save` | Update payees | — | — | ✓ | write; destructive options need confirm |
| `flags_list` | List flags | ✓ | — | ✓ | read-only |
| `flags_rename` | Rename flags | — | — | ✓ | write |
| `credit_payoff_target_get` | Get credit payoff target | ✓ | — | ✓ | read-only |
| `credit_payoff_target_save` | Save credit payoff target | — | — | ✓ | write |
| `credit_payoff_target_delete` | Delete credit payoff target | — | — | ✓ | destructive (confirm) |
| `schedules_list` | List scheduled transactions | ✓ | — | ✓ | read-only |
| `schedules_create` | Create scheduled transaction | — | — | ✓ | write |
| `schedules_update` | Update scheduled transaction | — | — | ✓ | write |
| `schedules_action` | Act on scheduled occurrence | — | — | ✓ | write; destructive options need confirm |
| `schedules_set_flag` | Flag scheduled transaction | — | — | ✓ | write |
| `schedules_process_due` | Post due scheduled transactions | — | — | ✓ | write |
| `long_term_planning_get_year` | Get long-term plan | ✓ | — | ✓ | read-only |
| `long_term_planning_get_activity` | Get planned activity | ✓ | — | ✓ | read-only |
| `long_term_planning_set_override` | Set planned budget override | — | — | ✓ | write |
| `wishlist_get` | Get Wishlist (buckets, items, progress) | ✓ | — | ✓ | read-only |
| `wishlist_set_enabled` | Enable or disable Wishlist | — | — | ✓ | write; disabling needs confirm |
| `wishlist_buckets_create` | Create Wishlist bucket | — | — | ✓ | write |
| `wishlist_buckets_rename` | Rename Wishlist bucket | — | — | ✓ | write |
| `wishlist_buckets_delete` | Delete Wishlist bucket | — | — | ✓ | destructive (confirm) |
| `wishlist_buckets_reorder` | Reorder Wishlist buckets | — | — | ✓ | write |
| `wishlist_items_create` | Add Wishlist item | — | — | ✓ | write |
| `wishlist_items_update` | Update/move Wishlist item | — | — | ✓ | write |
| `wishlist_items_reorder` | Reorder Wishlist items | — | — | ✓ | write |
| `wishlist_items_set_status` | Mark item purchased/removed/active | — | — | ✓ | write; removing needs confirm |
| `sharing_get` | Get budget sharing | — | — | ✓ | read-only |
| `sharing_invite` | Invite budget member | — | — | ✓ | write |
| `sharing_remove_member` | Remove budget member | — | — | ✓ | destructive (confirm) |
| `sharing_cancel_invitation` | Cancel budget invitation | — | — | ✓ | destructive (confirm) |
| `admin_dashboard_get` | Admin dashboard | — | — | ✓ | read-only |
| `admin_audit_list` | Admin audit log | — | — | ✓ | read-only |
| `users_list` | List users | — | — | ✓ | read-only |
| `users_history` | User account history | — | — | ✓ | read-only |
| `users_invite` | Invite user | — | — | ✓ | write |
| `users_set_membership` | Set user membership | — | — | ✓ | write |
| `users_approve` | Approve registration | — | — | ✓ | write |
| `users_reject` | Reject registration | — | — | ✓ | destructive (confirm) |
| `users_suspend` | Suspend user | — | — | ✓ | destructive (confirm) |
| `users_reactivate` | Reactivate user | — | — | ✓ | write |
| `users_unlock` | Unlock user | — | — | ✓ | write |
| `users_send_password_reset` | Send password reset | — | — | ✓ | write |
| `users_set_entitlement` | Override user entitlement | — | — | ✓ | write |
| `users_clear_entitlement` | Clear user entitlement override | — | — | ✓ | write |
| `membership_plans_list` | List membership tiers | — | — | ✓ | read-only |
| `membership_plan_entitlements_get` | Get tier entitlements | — | — | ✓ | read-only |
| `membership_plan_entitlements_save` | Save tier entitlements | — | — | ✓ | write |
| `admin_settings_registration_mode_get` | Get registration mode | — | — | ✓ | read-only |
| `admin_settings_registration_mode_set` | Set registration mode | — | — | ✓ | write |
| `admin_settings_email_get` | Get email delivery settings | — | — | ✓ | read-only |
| `admin_settings_email_update` | Update email delivery settings | — | — | ✓ | write |
| `admin_settings_email_send_test` | Send test email | — | — | ✓ | write |

Not exposed (by design): signing in, registration, profile/username and password changes, agent token management, role grants (the app has no role-management endpoint; the Administrator role is assigned by bootstrap configuration), register column preferences, and Auto-Assign (a browser-side calculation over Plan data — agents can compute amounts from `plan_get_month` target progress and call `plan_set_assigned`).

### UI parity

Every user-facing capability of the web app and every API endpoint must be reachable through MCP, or be listed here as excluded with the reason (see `AGENTS.md`). `MISSING` rows are gaps still to close; `pending` rows wait for the named in-flight change.

| Capability (UI area → API) | MCP tool(s) |
|---|---|
| Budget list, create, rename, archive, restore, delete (budget switcher, Budgets dialog → `/api/budgets`) | `budgets_list`, `budgets_create`, `budgets_rename`, `budgets_archive`, `budgets_restore`, `budgets_delete` |
| Plan month read, assign, move money, note (Plan → `/api/budgets/{id}/months/{month}`) | `plan_get_month`, `plan_set_assigned`, `plan_move_money`, `plan_set_note` |
| Recent Moves (→ `/plan-actions`) | `plan_recent_actions` |
| Category groups and categories: create, rename, icon, snooze, hide/unhide | `category_groups_create`, `category_groups_update`, `categories_create`, `categories_update` |
| Drag-and-drop ordering of groups and categories, move category to another group | `category_groups_move`, `categories_move` |
| Category targets (→ `/categories/{categoryId}/target`) | `category_targets_get`, `category_targets_save`, `category_targets_delete` |
| Auto-Assign | Not exposed (browser-side calculation; see above) |
| Accounts list and balances (sidebar → `GET /api/accounts`) | `accounts_list` |
| Add account — Checking, Savings, Cash, Credit Card, Line of Credit (→ `POST /api/accounts`) | `accounts_create` |
| Add account — Mortgage, Auto Loan, Student Loan, Personal Loan, Medical Debt, Other Debt, Contract with loan terms and category pairing | `accounts_create` (`loan`) |
| Edit account: rename, notes, favorite, close/reopen, working balance, type within family, loan terms | `accounts_update`, `loans_save_terms` |
| Delete account | `accounts_delete` |
| Account ordering | `accounts_reorder` |
| Loan page: details, projection, activity, contract schedule (→ `/loan`) | `loans_get` |
| Loan payoff simulator (→ `/loan/simulation`) | `loans_simulate` |
| Loan "Update Balance" (→ `/loan/balance`) | `loans_confirm_balance` |
| Loan category pairing picker (→ `/api/loan-pairings`) | `loans_pairings_list` |
| Credit card payment details (→ `/credit-summary`, `/credit-activity`) | `accounts_credit_summary`, `accounts_credit_activity` |
| Credit payoff target (→ `/payoff-target`) | `credit_payoff_target_get`, `credit_payoff_target_save`, `credit_payoff_target_delete` |
| Reconcile (→ `/reconcile`) | `reconcile_account` |
| Register: list/filter/search transactions (→ `GET /api/transactions`) | `transactions_list` |
| Add/edit/delete transaction, splits, repeat | `transactions_create`, `transactions_update`, `transactions_delete` |
| Imported-transaction entry (→ `/api/transactions/import`) | `transactions_import` |
| Cleared status, approve/reject, flag, bulk selection actions | `transactions_set_status`, `transactions_review`, `transactions_set_flag`, `transactions_bulk_update` |
| Transfers | `transfers_create`, `transfers_update` |
| Receipt images: list, delete | `transaction_images_list`, `transaction_images_delete` |
| Receipt images: upload file | Not exposed: binary multipart upload does not fit the 1 MiB JSON-RPC body; use the web app |
| Receipt images: import from URL | Not exposed — **security decision needed**: makes the server fetch an agent-supplied URL (outbound request/SSRF surface, already rate limited for users); agents could be steered to it by prompt injection |
| Receipt image download (→ `/api/transaction-images/{id}`) | Not exposed: binary content; metadata is in `transaction_images_list` |
| Ledger audit history (→ `/api/ledger/audit`) | `ledger_audit_list` |
| Payees management (→ `/api/payees/manage`) | `payees_list`, `payees_save` |
| Flags palette (→ `/api/flags`) | `flags_list`, `flags_rename` |
| Scheduled transactions: list, create, edit, pause/resume/end/snooze/skip/post, flag, post due | `schedules_list`, `schedules_create`, `schedules_update`, `schedules_action`, `schedules_set_flag`, `schedules_process_due` |
| Long-Term Planning: year, planned activity, budget overrides | `long_term_planning_get_year`, `long_term_planning_get_activity`, `long_term_planning_set_override` |
| Wishlist: overview, enable/disable, buckets, items, ordering, status | `wishlist_get`, `wishlist_set_enabled`, `wishlist_buckets_*`, `wishlist_items_*` |
| Wishlist "Record purchase" with a ledger transaction (→ `/purchase-transaction`) | `transactions_create` (`wishlistItemId`) |
| Budget Together: members, invitations (→ `/sharing`, `/invitations`, `/members`) | `sharing_get`, `sharing_invite`, `sharing_remove_member`, `sharing_cancel_invitation` |
| Live updates (→ `/api/budgets/{id}/events`) | Not exposed: cookie-only SSE for browsers; MCP is stateless |
| Register view options and column widths (→ `/api/register-views`) | Not exposed: per-user display preferences |
| Theme (→ `/api/me/preferences`) | Not exposed: per-user display preference |
| Sign in/out, register, confirm email, forgot/reset password, session (→ `/api/auth/*`) | Not exposed: authentication flows |
| Profile, username and password change (→ `/api/auth/profile`, `/change-password`) | Not exposed: account credential changes |
| Agent tokens: create, list, revoke, activity (→ `/api/auth/agent-tokens`) | Not exposed: an agent must not mint, extend or revoke credentials (renewal and non-expiring tokens (issue #15) are likewise not exposed to MCP) |
| Admin: dashboard, audit, users (invite/approve/reject/suspend/reactivate/unlock/password reset/history/membership), entitlement overrides, tier entitlements, registration mode, email settings | `admin_*`, `users_*`, `membership_plan*` tools (Admin tokens of administrators only) — **security decision needed**, see below |
| Admin: entitlement key list, a user's entitlement overrides (→ `/api/admin/entitlement-keys`, `/api/admin/users/{id}/entitlements`) | Not exposed (read-only admin views; intentionally not added while the admin tool set is under review) |
| Legacy single-budget Plan routes (`/api/budget`, `/api/budget/{month}`) | Not exposed: superseded by the budget-scoped routes above |

**Needs the owner's decision (not changed here):** the catalog already lets Admin tokens perform administrator user and entitlement management (`users_set_membership`, `users_set_entitlement`, `membership_plan_entitlements_save`, `users_send_password_reset`, `admin_settings_email_update`, `admin_settings_registration_mode_set`). These are privilege-relevant: an Admin agent token can change other users' memberships/entitlements, open registration or redirect outgoing email. Consider removing them from MCP or putting them behind a separate, explicitly granted level. Also decide whether receipt URL import should ever be agent-accessible.

## 5. Security model (ISO 27001 mapping)

| Control | Implementation |
|---|---|
| Access control, least privilege (A.5.15, A.8.3) | Three fixed levels; per-token budget allow-list; tool list and every call filtered server-side by the permission matrix (`McpPermissions`, `McpToolCatalog`). Non-Admin calls run with the creator's principal **stripped of role claims**, so an administrator's Budget/Reporting token cannot reach administrator endpoints even indirectly. |
| Identity and authentication (A.5.16, A.5.17, A.8.5) | 256-bit CSPRNG secrets, `obk_` prefix, SHA-256 hash with a unique index, constant-time comparison, shape check before lookup, generic `401` with `WWW-Authenticate: Bearer`. Dated expiry is the default; no expiration requires an explicit opt-in, and renewal rotates the secret by replacing the stored hash atomically. Each request re-validates the token and its creator: revoked, expired (server clock; a `null` expiry exists only for explicitly non-expiring tokens), suspended, unapproved, unverified, locked-out, deleted (cascade) or — for Admin — no-longer-administrator creators are denied immediately. |
| Same application paths (A.8.25, A.8.28) | Tools never touch the database. `ApiDispatcher` invokes the existing minimal-API endpoint in-process with the creator's principal and an explicitly selected budget, so validation, ownership checks, `LedgerGate` locking, optimistic versions, command receipts, Plan action history and admin audit are identical to the web app. Endpoint authorization metadata is evaluated before each handler. |
| CSRF / session separation | `/mcp` only accepts the bearer scheme; the session cookie is ignored. Agent tokens are not accepted on any `/api` route (they use the default cookie scheme). |
| Input validation (A.8.28) | Strict JSON-schema validation of every argument (types, required, `additionalProperties: false`, lengths, patterns, enums, UUID/date formats, money precision and bounds) before any service runs; services validate again. Validation messages name the argument, never echo values. |
| Logging and monitoring (A.8.15, A.8.16) | Every tool call writes an `AgentAuditLogs` row: token id, user id, tool name, budget id, success, outcome code, HTTP-equivalent status and timestamp. Arguments, results, financial values and tokens are never stored or logged; failures log only the tool name and exception type. Ledger command receipts remain the financial audit trail. Forward application logs to Azure Monitor / Log Analytics with access restricted to operators. |
| Cryptography and secrets (A.8.24, A.5.34) | Only hashes at rest. Production secrets (database connection, SMTP, bootstrap admin, any automation token) belong in Azure Key Vault accessed through a Managed Identity with the narrowest role (`Key Vault Secrets User` on the specific vault); nothing is committed to source control. TLS is required in transit outside Development; PostgreSQL storage should use Azure encryption at rest. |
| Availability (A.8.6) | Per-token fixed-window rate limit, 1 MiB request cap, stateless transport (no server session state), bounded SSE subscriber queues and at most 10 event streams per user. |

### Residual risks

- A leaked token grants its level of access until it is revoked, renewed or expires; there is no IP or client binding. Keep expiries short for Admin tokens. A token created with **No expiration** never lapses on its own, so its exposure window is bounded only by the owner renewing or revoking it; renew such tokens on a fixed schedule.
- Requests with invalid tokens are rejected before the per-token limiter, so they are not rate limited by token; each costs one indexed hash lookup. Front the deployment with a WAF/edge rate limit (for example Azure Front Door) if exposed publicly.
- Prompt injection: data in payees, memos or notes is returned to the agent verbatim. Destructive tools need `confirm: true`, but an agent that ignores instructions could still perform non-destructive writes within its level. Prefer Reporting or Budget tokens.
- Changing a password does not revoke agent tokens (they are separate credentials); suspension, role loss or revocation does.
- Admin tokens inherit future capabilities of the creator's role.
- Accepted risk (owner decision, 2026-10-31): Admin-level agent tokens keep the user-administration tools (`users_set_membership`, `users_set_entitlement`, `membership_plan_entitlements_save`, password-reset email, registration and email settings). A prompt-injected agent holding an Admin token could change other users' access or redirect outgoing email. Mitigate with short Admin token expiries and by not giving Admin tokens to agents that read untrusted text. Receipt import from a URL (`/images/from-url`) is deliberately not exposed (server-side request forgery and prompt-injection risk).
- The rate limiter and event hub are per process (see limitations).
- HTTPS enforcement trusts `X-Forwarded-Proto` from the reverse proxy (as the rest of the API does). The TLS-terminating edge in front of nginx must set it and must not accept plain HTTP for `/mcp`.

## 6. Real-time updates

`GET /api/budgets/{id}/events` is a cookie-authenticated server-sent events stream for the budget's owner and editors (`404` otherwise, `429` past 10 streams per user). A `retry: 5000` line and heartbeat comments every 25 s keep proxies open; access is re-checked on every heartbeat and before forwarding membership/budget changes, and the stream ends when it is lost.

```text
event: change
data: {"budgetId":"…","kind":"plan","month":"2026-10","version":7,"at":"2026-10-30T12:00:00+00:00"}
```

`kind` is `plan`, `ledger`, `targets`, `planning` or `admin`; `month` and `version` are present when known. Events never contain amounts, names or other financial details. An EF Core interceptor records which budgets and areas a `SaveChanges` touched and publishes only after the surrounding database transaction commits (or after an auto-committed save); rolled-back work is never published. Ledger writes also emit `plan` events for recalculated months (collapsed to one budget-wide event beyond 24 months).

The web app subscribes with `EventSource` (`useBudgetLiveUpdates`), reconnects with exponential backoff (1 s → 30 s), coalesces bursts, and:

- `plan`/`targets` for the open month: re-fetches the month unless the event is the session's own save (version not newer). If the user has unsaved Plan edits, an "Updated elsewhere — reload" notice appears instead; **Reload** opens the existing conflict dialog (Load latest / Reapply my changes).
- `ledger`: refreshes the sidebar accounts and the open register, unless a register edit or dialog is open (the register's 30 s poll retries).
- `admin`: refreshes the budget list and name.

## 7. Limitations

- **Single instance:** the event hub and the rate limiter are in-process. A scaled-out deployment needs a backplane for events (for example Azure SignalR Service or Azure Cache for Redis pub/sub) and a distributed rate limiter; until then sessions only see changes committed by the same instance.
- Stateless Streamable HTTP only: no server-initiated MCP notifications, resources or prompts; `tools/list` is not paginated (99 tools).
- `transactions_update`, `transfers_update` and `schedules_update` replace the whole record (read it first); `accounts_update` merges omitted fields from the current account.
- Long-Term Planning pages do not live-refresh yet (`planning` events are emitted); they reload on navigation.
