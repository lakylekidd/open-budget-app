# Enable Banking account linking investigation

**Provider:** Enable Banking Account Information Service (AIS), read-only. Payment initiation is outside this feature.

## Integration flow

1. Register an Enable Banking application in its Control Panel. Keep the RSA private key in a server secret mount, register the public certificate, and allowlist the exact HTTPS callback URL. Sandbox registration is separate from production registration. Production activation, terms, privacy URL, data-protection contact and any account allowlisting must be completed with Enable Banking.
2. The API signs short-lived five-minute RS256 JWTs using the application ID as `kid`, `iss=enablebanking.com`, and `aud=api.enablebanking.com`. Never send the private key to the browser.
3. The authenticated user chooses country and bank. The API starts `POST /auth` with the transaction scope needed for this feature, a random one-time `state`, the callback URL and a server-generated anonymized PSU identifier. The browser is redirected to Enable Banking's returned authorization URL.
4. The callback verifies and consumes the state before exchanging the authorization `code` through `POST /sessions`. Save the returned session ID, bank/country, access expiry and each account UID and `identification_hash` under the active budget and owner. The session may contain several bank accounts.
5. Let the user map each returned bank account to an existing OpenBudget account or create a new one. Do not impose a unique bank-per-budget rule: one bank session may yield several accounts, and a user may create multiple sessions with the same bank. The provider's account UID is session-specific; its `identification_hash` is the cross-session identity to use when reconnecting.
6. Sync transactions from `GET /accounts/{uid}/transactions`, passing `date_from` with a small overlap window and following every `continuation_key`. Upsert with the linked account plus provider transaction identity/reference. Preserve pending-to-booked transitions and never advance the checkpoint until all pages have been committed.
7. Keep provider rows in a review queue until accepted, dismissed or verified as a match. For each new bank row, candidate matching uses the same internal account, exact signed amount, a bounded date window, and payee text similarity. A suggested match is always user-confirmed. Confirming a match links the bank row to the existing transaction and must not create a second financial entry. Accepting as new creates an ordinary ledger transaction through the existing ledger service.
8. A background worker claims due connections using a database lease, calls the provider, stores each page idempotently, and schedules the next attempt using that connection's selected interval. Transient 429/5xx/network failures use exponential backoff; invalid consent, a revoked session or `access.valid_until` expiry moves the connection to Reconnect required and stops scheduled requests. The user can run Sync now, change interval, reconnect the existing session, or disconnect.

## Sync interval

The interval controls how often OpenBudget checks for new data; it cannot make a bank publish data more frequently. Store it per provider session, with an allowed range such as 15 minutes through 24 hours and a one-hour default. Pick an interval that respects Enable Banking and institution rate limits. Keep the latest successful sync time, next eligible sync time, retry time and short safe error code/status. Never expose request bodies, credentials, access tokens, account identifiers or transaction payloads in logs.

Enable Banking's `access.valid_until` limits the session itself. The user must authorize again after expiry; a worker cannot silently extend bank consent. Read the institution's current `maximum_consent_validity` when constructing authorization requests rather than assuming a universal PSD2 duration. The connection screen requests transaction access; balance access is not requested because this feature does not sync bank balances.

## OpenBudget changes required

- Add budget-scoped provider connections, per-account mappings and staged imported bank rows, with migration constraints for unique provider identities and no uniqueness constraint on bank name.
- Add encrypted-at-rest Enable Banking session IDs, app-key secret configuration, short-lived signed client JWTs, state replay protection, trusted callback validation, consent lifecycle and provider disconnect handling.
- Add a hosted sync worker with a per-connection cadence, lease/locking, retry/backoff, bounded transaction overlap, provider pagination and durable checkpoints.
- Add account mapping, connection status, last-sync/reauthorization state and an import-review UI. Keep pending imports out of account balances until accepted; matching an existing transaction must never duplicate it.
- Add fuzzy payee suggestions with an explainable score and a manual choice. Keep exact payee/category history as the stronger rule; do not silently rename or categorize a payment from a low-confidence similarity result.
- Add audit records and integration tests for owner/budget isolation, callback state, secret protection, duplicate pages, repeated sessions from the same bank, multiple accounts from a session, pending-to-booked updates, fuzzy match confirmation, retries, expired consent and disconnect.
- Add connection data to export/deletion workflows before enabling a shared production service. Confirm account ownership, regional eligibility, costs, user consent wording, retention and incident response with Enable Banking before production.

## Current scope and operational prerequisite

This change implements the application-side flow and configurable worker. A real live connection still requires an Enable Banking app ID, RSA key, exact HTTPS callback registration and production activation. Without those deployment secrets and the provider's production approval, only sandbox and deterministic integration-fixture verification are possible.

## Provider references

- [Enable Banking API reference](https://enablebanking.com/docs/api/reference/) — RS256 application authentication, authorization/session APIs, ASPSP access limits, transaction pagination and session deletion.
- [Enable Banking quick start](https://enablebanking.com/docs/api/quick-start/) — application registration, sandbox setup, callback flow and sample AIS requests.
- [Enable Banking FAQ](https://enablebanking.com/docs/faq/) — session expiry and reauthorization behavior.
