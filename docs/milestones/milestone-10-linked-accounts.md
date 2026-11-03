# Milestone 10 — Optional linked-account import

**Status:** In progress — Enable Banking adapter, consent flow, background sync, account mapping, and transaction review are implemented; sandbox verification and production onboarding remain.

## Goal

Allow a user to opt in to a supported financial-data provider, review imported transactions, and disconnect with consent and credentials handled safely.

## Dependencies and release gate

Milestone 8 identity, tenant isolation, consent, secure token storage, account deletion/export, and security audit are prerequisites. Milestone 2 provides the transaction ledger and matching path; Milestone 7's import review concepts should be reused. Enable Banking is the selected provider for the first adapter. Production use still requires the app registration, redirect allowlist, production approval, regional coverage, legal terms, cost, and data-retention review documented in [the investigation](../enable-banking-investigation.md).

## User value

A person can connect an institution, see synchronization status and candidate transactions, decide what enters the budget, and revoke access.

## Vertical slice

- **UI:** Institution/provider selection; clear consent, scope, and last-sync details; connect/reconnect/disconnect; account mapping; import review; match/skip/accept; provider failure and reconnect messages.
- **API/domain:** Provider adapter boundary; OAuth/consent callback; scheduled/manual sync; stable provider transaction identity; pending-to-posted matching; deduplication; review queue; retry/backoff and outage handling. This feature imports data only; it does not initiate payments or transfers.
- **Persistence/security:** Encrypted provider tokens and consent metadata scoped to user/budget; sync cursors; provider transaction IDs and review decisions; audit events for connect, sync, consent changes, and disconnect. Never return provider secrets to the browser or logs.
- **Operations:** Secret rotation, provider webhook/signature validation if used, background-job visibility, alerting, data deletion, and support diagnostics that avoid exposing financial details.

## Current implementation notes

- A budget can have multiple connections to the same bank and each Enable Banking session may contain multiple accounts. Each linked provider account maps independently to one OpenBudget account.
- Sync intervals are stored per connection (15 minutes through 24 hours; default one hour). A hosted worker checks due connections each minute, uses a 15-minute lease, resumes with a five-day overlap, paginates provider responses, and applies exponential retry delays. The selected interval only controls how often OpenBudget asks Enable Banking; it cannot make a bank publish fresher data.
- Bank transactions are staged in `BankImports`, so they do not change ledger balances until accepted. Exact signed amount, dates within four days, and fuzzy payee similarity produce a possible-match suggestion. A link glyph appears in the register's left information cell while a match awaits the user's verification. Verifying records the link against the existing transaction and avoids a duplicate; accepting separately creates a normal imported ledger entry atomically with the review decision. When adding a new transaction, the user can keep the bank's payee or reuse the most similar existing payee above the 72% threshold.
- Enable Banking authorization requires the registered app ID, server-side RSA key and HTTPS redirect URL. Reauthorization refreshes the current provider session while preserving account mappings and previously staged/accepted imports. No production credentials or sandbox account are committed to the repository. Live provider verification is still outstanding.

## Acceptance criteria

1. Only an authenticated, authorized budget member can start or manage a connection; selected institution and data scopes are shown before the user grants consent.
2. OAuth/state/redirect handling rejects forged, expired, replayed, or mismatched callbacks and does not expose access tokens to the browser URL or logs.
3. Provider tokens are encrypted at rest, scoped to the right user/connection, inaccessible from client APIs, and removed or revoked on disconnect according to provider capabilities.
4. The connection screen shows institution, linked accounts, connection status, consent/reauthorization state, and last successful sync.
5. Sync operations are idempotent using stable provider identifiers and cursor checkpoints; retrying a page/batch cannot create duplicate transactions.
6. Pending and posted provider records are matched so a posted transaction updates the pending record rather than creating a second entry.
7. Imported rows enter a review/matching queue and do not affect account balances or budget activity until accepted or matched under the documented policy.
8. Accepting, matching, or dismissing an item is auditable and retry-safe; accepted transactions flow through the normal ledger calculation path.
9. Provider outages, rate limits, expired consent, partial sync, and invalid payloads produce recoverable states and do not advance the cursor past unprocessed data.
10. Disconnect immediately prevents future sync, revokes provider access where supported, deletes connection secrets, and preserves already accepted ledger transactions.
11. User export and deletion workflows account for provider connection metadata and stored tokens; logs and telemetry exclude credentials and minimize transaction details.
12. A user can reconnect after expired consent without duplicating previously imported data.
13. No payment initiation, transfer execution, or unsupported provider action is exposed through this milestone.
14. Provider selection, supported regions/institutions, costs, consent terms, data retention, and incident response are documented before production release.
15. The complete flow is demonstrated with provider sandbox fixtures, including callback security, incremental sync, pending-to-posted update, retry, outage, consent expiry, and disconnect.

## Product references

[Linked accounts](https://support.ynab.com/en_us/linked-accounts-in-ynab-B1991f2Cc), [how direct import works](https://support.ynab.com/en_us/how-direct-import-works-H1IGYLgnxl). Provider availability and terms vary by region and can change; validate them during implementation planning.
