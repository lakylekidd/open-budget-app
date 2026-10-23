# OpenBudget product roadmap

This roadmap turns product workflows documented publicly by YNAB into independently deliverable vertical slices. Each milestone includes the user experience, API/domain behavior, persistence, and an end-to-end acceptance scenario. The roadmap is a product direction for OpenBudget; it does not rely on YNAB source code, private endpoints, or proprietary implementation details.

## Delivery principles

- Deliver a complete user path across UI, API, domain rules, persistence, and operational feedback in every milestone.
- Keep the budget ledger and calculations on the server as soon as transactions arrive; the UI should render authoritative results.
- Preserve an auditable history for money-moving operations and use idempotency/concurrency controls for writes.
- Keep imports, external connections, and household sharing opt-in, reviewable, and protected by explicit authorization.
- Every milestone must include migration/backfill strategy, accessible responsive UI, error and empty states, and concise documentation.

## Milestones

| # | Milestone | Short description | Status | Details |
|---:|---|---|---|---|
| 0 | Local application foundation | Plan UI, .NET API, PostgreSQL persistence, and Aspire development orchestration. | Current / implemented | [Milestone 0](milestones/milestone-00-foundation.md) |
| 1 | Reliable budget core and month lifecycle | Authoritative assignments, available-to-assign calculations, month carry-forward, and conflict-safe writes. | Current / implemented | [Milestone 1](milestones/milestone-01-budget-core.md) |
| 2 | Accounts and transaction register | Cash accounts, transaction ledger, transfers, reconciliation, and budget activity calculations. | Current / implemented | [Milestone 2](milestones/milestone-02-accounts-register.md) |
| 2a | Multi-user authentication and administration | Secure sign-in, administrator controls, configurable registration, independent budgets, and membership limits. | Current / implemented | [Milestone 2a](milestones/milestone-02a-multiuser-auth-and-admin.md) |
| 3 | Goals, templates, and guided assignment | Targets, progress, category templates, and explainable auto-assignment. | Planned | [Milestone 3](milestones/milestone-03-goals-and-assignment.md) |
| 4 | Scheduled transactions and cash-flow awareness | Recurring entries, upcoming/overdue review, and posting occurrences once. | Scheduled register slice implemented | [Milestone 4](milestones/milestone-04-scheduled-transactions.md) |
| 5 | Credit cards and debt payoff planning | Credit-card payment reservations and loan payoff planning/projections. | Planned | [Milestone 5](milestones/milestone-05-credit-and-debt.md) |
| 6 | Focused plan views and reports | Action-focused category views and ledger-backed spending, income, and net-worth reports. | Planned | [Milestone 6](milestones/milestone-06-views-and-reports.md) |
| 7 | File import, matching, and portable data | Validated CSV import, duplicate/match review, and portable budget/transaction exports. | Planned | [Milestone 7](milestones/milestone-07-file-import.md) |
| 8 | Identity, multiple budgets, and household sharing | Secure sign-in, isolated budgets, invitations, membership roles, and sharing. | Planned | [Milestone 8](milestones/milestone-08-identity-and-sharing.md) |
| 9 | Cross-device experience and offline resilience | Responsive installable UI, offline changes, synchronization, and conflict handling. | Planned | [Milestone 9](milestones/milestone-09-cross-device-offline.md) |
| 10 | Optional linked-account import | Consent-based provider connection, transaction sync/review, reconnect, and disconnect. | Planned; provider availability to be assessed | [Milestone 10](milestones/milestone-10-linked-accounts.md) |
| 11 | Annual long-term budget planning | Full-year planned-versus-actual forecast grid with effective-month overrides, carry-forward, and year navigation. | Current / implemented | [Milestone 11](milestones/milestone-11-anual-budget-planning.md) |

## Cross-cutting completion criteria

Apply these to every milestone that changes persisted behavior:

- Database migrations are forward-safe and document any data transformation.
- Server-side domain rules are the source of truth; requests are validated and money uses fixed-precision decimal values.
- Writes have clear retry/idempotency semantics and prevent silent lost updates.
- UI covers loading, empty, validation, permission, failure, and recovery states.
- Sensitive data is minimized, secrets remain outside source control, and authorization is tested at the API boundary.
- Product behavior and API contracts are documented, including known limitations.
- Every feature must include unit and integration tests appropriate to its rules/persistence boundaries, UI workflow tests, and running-application acceptance verification. See [testing](testing.md) and `AGENTS.md`.

Annual planning: Long-Term Planning stores expected income, category expenses and effective-month budget overrides separately from monthly budget snapshots. Its annual grid calculates planned available and summaries with decimal-safe cents, displays read-only ledger-backed actual values, and keeps past months locked. See [Milestone 11](milestones/milestone-11-anual-budget-planning.md) for the API, persistence, and acceptance coverage.

## Source review

Feature scope above was derived from public YNAB product and support pages, checked on 2026-10-21. These pages are product references, not commitments that OpenBudget will reproduce every YNAB behavior. Recheck availability and details before starting each milestone.


Payee management extension: the account register now includes grouped payee selection and persistent merchant category/renaming policies. Milestone 7 has an accepted-row import service/API prerequisite; its file-upload, preview, matching and export criteria remain planned.


Budget core correction: historical assignment edits now propagate category/Ready to Assign opening deltas through existing later months, using the same decimal carry helper as ledger changes. Later own assignments/activity are retained. Pending month saves and invalid assigned input are protected in Plan. The August/September EUR50/EUR35/EUR15/EUR65 scenario and historical corrections have unit, PostgreSQL HTTP, UI and isolated live verification.

Register selection actions: a floating bottom bar and reference-style More menu now support atomic selected-row commands, duplicate/repeat/memo/move/delete, named flags and selected-row CSV export. Import Match/Unmatch remain planned; export of all filtered/account history remains outside this selected-row slice.

Plan interaction extension: category activity and upcoming scheduled transactions open drilldown lists and focus the selected item in its account register, including reconciled transactions. Plan categories can be created under a group, reordered, or moved between groups while preserving budget and transaction identity. Category bars visualize remaining funds, spending and overspending; scheduled shortfalls show their projected effect. Existing uncovered overspending carry-forward remains covered by the budget-core behavior.
