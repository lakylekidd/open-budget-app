# Milestone 8 — Identity, multiple budgets, and household sharing

**Status:** Planned

## Goal

Protect user data with authenticated identity and budget-level authorization, then allow multiple budgets and invited household collaboration.

## Dependencies

The existing single local budget and persisted data must be migrated or assigned to an initial owner. Complete tenant-scoped authorization before enabling remote access, linked accounts, or shared use. Milestone 10 depends on this milestone.

## User value

A person can securely sign in, keep multiple budgets separate, invite a household member, and control the other member's access.

## Vertical slice

- **UI:** Sign-up/sign-in, sign-out, recovery, budget switcher/create/archive, invite/accept/revoke screens, membership/role management, session-expired handling, account export and deletion flow.
- **API/domain:** Authentication and session lifecycle; user, budget, membership, role, and invitation services; authorization for every budget-scoped operation; invitation expiry/revocation; security and membership audit events.
- **Persistence/security:** User and membership tables; indexes and foreign keys enforcing tenant relationships; secure password/identity-provider integration; protected session/refresh tokens; rate limits and security-event handling. Migrate the local budget to an initial owner without changing financial data.
- **Roles:** Define a minimum role model (owner and member at least), document each permission, and ensure ownership cannot be removed without an explicit transfer workflow.

## Acceptance criteria

1. A user can authenticate, end their session, and recover access using the documented identity flow; credentials/tokens are not written to logs or client-visible errors.
2. A newly created user receives a private budget and cannot enumerate or read another user's budget, account, category, transaction, report, export, or attachment by changing an identifier.
3. Every budget-scoped API route checks authorization server-side; hiding a UI control is never the only permission check.
4. Existing local data is assigned to the configured initial owner through a repeatable migration that preserves budget, account, category, and transaction identifiers/values.
5. An owner can create and revoke an invitation; invitations expire, are single-use, and cannot grant access to a different budget.
6. An invited member can accept or decline and sees only budgets for which membership is active.
7. Each role can perform only its documented operations; unauthorized attempts return a consistent response and generate the required security audit event.
8. Revoking membership terminates access on subsequent requests and invalidates active sessions/credentials scoped to that membership as documented.
9. An owner can transfer ownership before leaving or deleting an account; the service prevents orphaning a shared budget.
10. User data export includes all data covered by the published export contract; account deletion follows retention obligations and clearly reports any data that cannot yet be deleted.
11. Security-sensitive operations (login/recovery, invite, role changes, ownership transfer, deletion) are rate-limited and audited without recording secrets.
12. Tenant isolation is verified for read, write, search, report, import/export, background-job, and identifier-guessing paths.
13. UI handles expired sessions, revoked access, invitation errors, and network failures without losing unsaved work or leaking data across users.
14. Deployment, identity-provider configuration, key rotation, backup, recovery, and privacy boundaries are documented before enabling access outside local development.

## Product reference

[YNAB Together](https://support.ynab.com/en_us/ynab-together-B1nS78Cki).
