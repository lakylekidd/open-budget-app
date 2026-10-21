# Milestone 9 — Cross-device experience and offline resilience

**Status:** Planned

## Goal

Make core budgeting workflows usable on mobile and resilient to intermittent connectivity, with explicit synchronization and conflict behavior.

## Dependencies

Milestones 1 and 2 must provide command-based budget and transaction writes with concurrency controls. For signed-in multi-device use, complete Milestone 8 first. Avoid caching data from multiple users in a shared browser profile without user-bound encryption and purge behavior.

## User value

A person can check and update a budget on a small screen, continue limited work while disconnected, and understand when changes have synchronized.

## Vertical slice

- **UI:** Responsive navigation, Plan and register essentials, installable web app shell, connection/sync indicator, pending-change queue, per-item retry/conflict resolution, and accessible offline/reconnect states.
- **API/domain:** Change feed or sync cursor; idempotent commands; conflict response with server version and changed fields; bounded history/retention behavior; device registration/revocation where needed.
- **Persistence/client storage:** Persist only explicitly supported offline data; queue operations with stable IDs; protect locally cached sensitive data; purge on sign-out, account switch, device revocation, or retention expiry.
- **Conflict policy:** Define behavior per operation. Non-conflicting edits can rebase; incompatible money changes require a user-visible resolution. Never use last-write-wins for financial mutations without showing the lost/overwritten values.

## Acceptance criteria

1. At supported mobile breakpoints, a user can navigate and complete the defined Plan and register workflows without horizontal page overflow or inaccessible controls.
2. The app can be installed and reopened with a clear indication of which supported information is cached.
3. When disconnected, supported edits enter a durable pending queue with stable operation IDs; unsupported actions explain that a connection is required and do not appear to succeed.
4. Reconnecting syncs each queued operation at most once and shows per-item success, validation failure, or conflict state.
5. A server retry or client restart during synchronization does not duplicate transactions, assignments, or transfers.
6. Two devices changing different fields/items can synchronize without discarding either change.
7. Two devices making conflicting edits to the same financial value receive a clear conflict screen with server and local values and explicit keep/reapply choices.
8. A conflicted or rejected operation remains recoverable until the user resolves or discards it; synchronization does not silently drop it.
9. Sign-out, user switch, or device revocation clears or cryptographically isolates cached data and queued mutations according to the documented policy.
10. Sensitive cache encryption/key handling and behavior when browser storage is unavailable or cleared are documented; no secrets are placed in local storage as plain text.
11. Screen reader, keyboard, focus, contrast, reduced-motion, and touch-target behavior is verified for primary offline and online workflows.
12. A second device eventually displays the same server-confirmed budget state and sync position after successful synchronization.
13. Operational telemetry can distinguish sync failures and conflict rates without recording transaction descriptions, credentials, or other unnecessary financial details.

## Product reference

[YNAB features](https://www.ynab.com/features) describes use across devices and offline access; OpenBudget should implement its own sync and offline model.
