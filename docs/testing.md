# Testing and acceptance coverage

Every feature or behavior change includes automated tests in the same change, as required by `AGENTS.md`. Assert observable rules, responses, persisted state and user workflows. Do not substitute an in-memory database for PostgreSQL integration coverage.

## Commands and isolation

```powershell
dotnet test OpenBudget.slnx
npm --prefix src/OpenBudget.UI test
dotnet build OpenBudget.slnx
npm --prefix src/OpenBudget.UI run build
```

.NET tests use xUnit. Integration tests use `WebApplicationFactory<Program>`, real HTTP handlers/services, EF migrations and disposable PostgreSQL 17 via Testcontainers. Each scenario creates its own database inside the container, with no connection to the Aspire database/volume. Docker must be available; missing infrastructure is not silently skipped. Container disposal cleans scenario data.

UI tests use Vitest, jsdom and Testing Library with controlled API responses. These complement real database tests rather than claiming mocked responses prove persistence. Cover valid/invalid input, money/date boundaries, reload, failure atomicity, stale/concurrent writes, replay and recovery where applicable. Add regression tests with bug fixes.

`.github/workflows/test.yml` builds and runs all three suites on pushes and pull requests. The Linux runner supplies Docker for PostgreSQL integration; CI results must pass before merging.

## Milestone 2 acceptance map

| Behavior | Automated coverage |
|---|---|
| Account opening, rename, API restart, exactly-once opening | `Opening_balance_rename_restart_and_command_replay_are_persistent`; account form UI |
| Close/reopen constraints and historical rows | `Closing_requires_zero_resolved_balance_preserves_history_and_allows_reopening` |
| Spending/income/refunds/splits, totals, edits and deletion audit | `Categorized_spending_income_refunds_splits_edits_and_deletes_reconcile`; transaction/split form UI |
| Uncategorized spending reduces Ready to Assign until categorized | `Uncategorized_spending_reduces_ready_to_assign_until_categorized`; attention UI |
| Paired, budget-neutral transfer creation/edit/deletion/replay | `Transfers_are_paired_atomic_neutral_editable_deletable_and_idempotent`; transfer UI |
| Combined filters, stable pages and empty results | `Combined_filters_and_pagination_are_stable_include_splits_and_distinguish_empty_results`; filters/pagination UI |
| Reconciliation and immutable entries, including opposite transfer leg | `Reconciliation_requires_zero_difference_and_locks_history`; `Reconciled_transfer_cannot_be_edited_or_deleted_through_the_other_leg`; reconciliation UI |
| Concurrent writers, stale Plan saves, conflicting command reuse | `Concurrent_writes_and_reused_command_ids_return_conflict_without_lost_edits`; conflict/reload UI |
| Rollback after intermediate saves, including future months | `Simulated_persistence_failure_rolls_back_both_transfer_legs_budget_and_receipt`; `Simulated_spending_failure_rolls_back_activity_income_and_future_carry_forward` |
| Historical/date/account edits, future carry-forward and restart | `Historical_edits_propagate_into_existing_later_months_and_survive_restart`; `Moving_a_transaction_between_months_and_accounts_reverses_original_activity` |
| Move a category between groups without losing assignment/activity or transaction labels | `Moving_a_category_between_groups_preserves_its_budget_state_and_transaction_identity` |
| Plan category activity, account focus, exclusive row-background and Assigned-input selection, checkbox multi-selection and Assigned focus, group and selected-subcategory rename popovers, selected-month upcoming activity in category details and its transaction popup, Available move/cover-overspending dialogs, zero Available disabled state, selected-month upcoming shortfall highlighting (including exact-zero and future-month boundaries), drag ordering and progress display | `App.test.tsx`; `planHelpers.test.ts` |
| Auto-Assign current-month scheduled shortfalls, all-category vs selected-category scope, previous-month actuals, trailing 12-month averages, reset actions, Ready to Assign limits, and manual category assignment | `planAutoAssign.test.ts`; `App.test.tsx` |
| Upgrade preserving legacy activity/balances | `Upgrade_preserves_legacy_activity_and_available_values` applies old migrations, seeds legacy rows, then upgrades |
| Server-owned calculations and invalid/unknown/archived input | `Plan_cannot_fabricate_income_or_change_server_owned_activity`; `Invalid_requests_do_not_change_balance_budget_version_or_audit`; `Archived_categories_invalid_transfers_and_missing_commands_do_not_write` |
| Money precision, exact splits, refund/transfer rules, cleared balances | `LedgerRulesTests` and money-input unit tests |
| Loading/error/empty, safe replay after reload, audit/deletion confirmation, keyboard closure | `AccountsPage.test.tsx` and `TransactionForm.test.tsx` |
| Inline transaction Tab order skips dropdown arrows and proceeds to the next field | `AccountsPage.test.tsx` |
| Independent administrator/user cookie sessions, login, logout, lockout, username/profile change, password change/reset invalidation across concurrent sessions, audit privacy and protection of the shared legacy budget | `AuthenticationApiTests.Login_session_logout_and_lockout_use_identity_cookie_and_persisted_user`; `AuthenticationApiTests.Admin_user_lifecycle_is_role_protected_and_audited`; `AuthenticationApiTests.Admin_password_reset_sends_expiring_link_without_disclosing_token_or_existing_password`; `Suspended_users_cannot_login`; `ProfileSettings.test.tsx`; `AuthGate.test.tsx` |
| Production bootstrap administrator uses configured credentials, receives administrator/Pro membership, owns the legacy budget and its financial rows, can change credentials securely, and keeps the changed password across startup reruns | `AuthenticationApiTests.Production_bootstrap_admin_uses_configured_credentials_and_preserves_existing_data_on_restart` |
| Non-Development startup rejects missing bootstrap credentials without provisioning the Development-only `admin` account | `AuthenticationApiTests.Non_development_bootstrap_requires_configured_credentials_and_does_not_create_development_admin` |
| Identity and budget-scope migrations preserve legacy assignments, account/transaction rows, and assign the default budget to the configured bootstrap administrator | `AuthenticationApiTests.Identity_and_budget_scope_migrations_preserve_legacy_financial_data_and_assign_it_to_development_admin` |
| Owner-only budget/month reads and writes, including administrator denial of another user's private Plan data; legacy ledger isolation, unique month snapshots, budget switching, persisted selection, create/rename/archive/restore/delete UI and versioned deletion | `AuthenticationApiTests.Admin_user_lifecycle_is_role_protected_and_audited`; `BudgetOwnershipApiTests.Legacy_ledger_recalculation_and_planning_reads_do_not_cross_owned_budget_snapshots`; `BudgetOwnershipApiTests.Owner_can_permanently_delete_a_budget_and_its_plan_only_with_current_version`; `App.test.tsx` |
| Membership downgrade retains existing budgets while blocking creation over the new limit | `AuthenticationApiTests.Admin_user_lifecycle_is_role_protected_and_audited` |

## Milestone 2a final verification (2026-10-23)

- Solution build: zero warnings and errors.
- .NET tests: 95 unit and 83 PostgreSQL integration tests passed, including required non-Development bootstrap configuration.
- UI: 178 tests passed; TypeScript/Vite production build passed.
- Deployment configuration: `docker compose config --quiet` passed with the required PostgreSQL and bootstrap-admin values supplied.
- Aspire smoke: API health reported a connected database and the UI returned HTTP 200.
| Membership subscription metadata validates ordered dates and persists start, expiration, provider and external ID through the administrator API/UI | `AuthenticationApiTests.Admin_user_lifecycle_is_role_protected_and_audited`; `AdminPanel.test.tsx` |
| Membership entitlements default to deny, tier settings and user allow/deny overrides drive authorization requirements, clearing an override restores the tier default, and admin changes are audited | `MembershipEntitlementApiTests.Plan_entitlements_and_user_overrides_drive_reusable_authorization_requirements`; `EntitlementKeyTests` |
| Registration disabled by default, email verification, password recovery and single-use reset tokens | `AuthenticationApiTests.Registration_is_disabled_by_default_and_open_registration_requires_email_confirmation`; `AuthGate.test.tsx` |
| New users receive membership but no budget until they explicitly create one | `AuthenticationApiTests.Registration_is_disabled_by_default_and_open_registration_requires_email_confirmation` |
| Administrator role boundary, dashboard status/budget/tier counts, recent registrations and security events, user search/status filters, registration mode, approval, suspension/reactivation, bootstrap protection, unlock behavior and audit | `AuthenticationApiTests.Admin_user_lifecycle_is_role_protected_and_audited`; `AdminPanel.test.tsx` |
| Administrator invitation creates an unverified account with the selected membership, sends an expiring verification link without exposing credentials, enforces the role boundary, audits the action and removes the account when email delivery fails | `AuthenticationApiTests.Administrator_invites_verified_email_user_without_exposing_a_password_or_token`; `AdminPanel.test.tsx` |
| Administrator can inspect a bounded account history containing only that user's lifecycle events; other roles and unknown IDs are denied | `AuthenticationApiTests.Admin_user_lifecycle_is_role_protected_and_audited`; `AdminPanel.test.tsx` |
| Administrator password reset sends the expiring Identity token only by email, never reveals a password/token in the API response, and records initiation | `AuthenticationApiTests.Admin_password_reset_sends_expiring_link_without_disclosing_token_or_existing_password`; `AdminPanel.test.tsx` |

`FailureInjector` throws on the final receipt/audit save after financial rows have already been saved within the open transaction. Fresh HTTP/database assertions prove rollback, rather than just observing an exception.

## Running-application smoke scenario

Use a disposable database and synthetic cash accounts for money-changing smoke checks. Do not alter the user's existing budget.

1. Build/run API/UI, open All Accounts, verify empty state, add Checking (100) and Savings (0).
2. Record categorized spending of -10 and split spending of -20. Check account balances and Plan category activity.
3. Transfer 25 to Savings; verify paired amounts and unchanged budget income/spending. Edit an unreconciled entry and refresh to confirm persistence.
4. Clear entries; verify a nonzero statement difference blocks reconciliation, then confirm zero difference and verify entries lock.
5. Check audit, combined filters/page controls, compact navigation, dialog focus/keyboard closure and validation.
6. Restart against the same disposable database and verify balances/history. Remove only test resources, rebuild/restart Aspire, and check health, accounts and the served UI.

Report actual results, distinguish automated coverage from manual smoke checks, and state any blocked verification. A milestone is complete only after its acceptance path is verified.

## Milestone 2 verification (2026-10-21)

- Local .NET suite: 16 unit and 17 PostgreSQL integration tests passed, none skipped. Solution build passed with zero warnings/errors.
- UI suite: 23 tests passed; TypeScript/Vite production build passed.
- Browser smoke against a disposable PostgreSQL database: opening accounts, categorized and split spending, Plan activity, paired transfer, editing, clearing, zero-difference reconciliation/locks, audit, refresh and API-restart persistence verified. Compact navigation checked at 390 px and viewport restored.
- Smoke balances after editing/reconciliation: Checking 44, Savings 25, total cash 69; spending activity 31, initial income 100. Disposable data was removed independently of the development volume.
- CI is configured to run the same suites; no remote CI run is claimed by this local verification.
- Rebuilt/restarted Aspire with the existing development volume: API health/database connected, accounts revision 1 with no smoke accounts, and the OpenBudget UI returned HTTP 200 at port 5173.

## Account register presentation verification (2026-10-21)

The reference-style account UI adds tests for the balance equation, integer-cent aggregation, truthful connected/unlinked display, real reconciliation age, cleared circles/reconciled locks, filtering without changing totals, and persisted favorites. HTTP/PostgreSQL regression tests cover full-history running balances before filters/pages, per-account partitioning, same-date ordering, deleted entries, edits, transfer account labels, historical category labels, account metadata persistence/validation and reconciliation time. The current suite has 16 domain unit, 19 PostgreSQL integration and 30 UI tests.

Browser comparison uses disposable sample accounts, including `0 + (-500) = -500` and mixed cleared/reconciled history. The reference images supply layout and state semantics only; personal transaction records are not copied into application data or fixtures. Import, ledger undo/redo and scheduling controls remain disabled pending their milestones. Automatic sync is not simulated.

Final checks passed: all 65 automated tests, TypeScript/Vite build, and solution build (zero warnings/errors). Browser checks confirmed that toggling a -125.50 transaction moves totals from `0 + (-500)` to `125.50 + (-625.50)` while Working Balance remains -500; reconciled filtering also preserves those full-account totals. Reconcile blocks a nonzero statement difference and enables confirmation at zero. Layout was checked at the reference panel width and 390 px; the browser size was restored. Disposable test data was removed and Aspire restarted successfully with connected database health, UI HTTP 200, and no smoke accounts in the development database.

## Account View Options verification (2026-10-21)

- Domain validation covers every preset, custom range ordering, optional bounds, invalid formats and supported year boundaries.
- Real PostgreSQL/HTTP tests cover defaults without writes, all saved selectors/switches, per-account and All Accounts isolation, a new client/API restart, exact retry, invalid/missing accounts, concurrent/stale edits, and unchanged financial revisions/audit/balances. Reconciled exclusion is verified before pagination with full-history running balances and totals preserved.
- UI tests cover calendar/year transitions, leap days, rolling presets, draft/Apply/Cancel/Escape, reversed ranges, failed saves, conflict reload, blocked preference loading with recovery, account switching/reloading, server filter arguments, matching header/row column visibility, and View All preserving column switches.
- Automated results: 30 domain unit tests, 22 HTTP/PostgreSQL integration tests, 43 UI tests; all pass with none skipped. Solution and UI production builds pass.
- Live browser smoke uses only an isolated disposable PostgreSQL database. Verified custom September 2025–October 2036 selectors and switches after a fresh page load, reconciled-row/column hiding and unchanged `1500 + (-125.50) = 1374.50` account totals. Separate account retains its default options.

## Inline register, scheduling, account editing and approval verification (2026-10-21)

- 56 unit tests, 36 real HTTP/PostgreSQL integration tests and 67 UI tests pass (159 total; none skipped). Frontend production build passes. UI suites use two workers and a 3-second asynchronous assertion window to avoid CPU contention during concurrent .NET/database builds.
- Calendar/recurrence tests cover every reference Repeat option, month-end anchors, leap years, twice-monthly rules, fixed-week intervals, supported-date exhaustion and deterministic identities. UI tests cover inline Add (no dialog), first-click selection/second-click edit, Save and add another, split validation, failed-save exact replay, future scheduling, confirmed reconciled edits, visible-column alignment, flags/renaming, sidebar selection/edit controls, account notes/balance adjustment validation, and absence of the removed selection strip.
- Database tests cover forecast isolation, restart persistence, rule and one-off edits, skips/snoozes/pause/end/count, paired transfers/splits, pagination, closed/archived validation, concurrent posting, failed transaction/audit rollback, exact retry, retained reconciliation invalidation for both transfer legs, zero opening edits, palette persistence, and adjustment/no-op/replay/rollback.
- An injected clock verifies future-vs-due processing, server-local timezone boundaries, snooze/catch-up, concurrent worker identities, unapproved counts/attention union, Approve, clearing-as-approval, rejection reversal without re-generation, and independent transfer-leg approval.
- Browser checks against a labelled disposable PostgreSQL database verified creating a monthly forecast inline without changing actual totals, explicit posting, renaming/retaining a flag, confirmed reconciled edits changing totals and requiring reconciliation again, the sidebar edit shortcut, and the unlinked Edit Account dialog saving notes and a 25.00 cleared adjustment. A due subscription posted with an `i`, the next monthly date, a one-item banner and the account dot; clearing removed approval attention. Calendar bounds fit the 390px viewport; temporary viewport overrides were restored. Reference YNAB transactions were inspected without saving changes or copying personal records into fixtures.
- Named smoke resources are independent of the development volume. Final supported Aspire startup/health and clean build results are recorded after cleanup; no remote CI result is claimed.

Final verification: all 159 automated tests passed, solution build reported zero warnings/errors, and the UI production build passed. Removed the labelled disposable smoke container/services, rebuilt and restarted the supported Aspire AppHost against the existing development volume, verified database-connected health and UI HTTP 200, and verified that neither smoke account ID exists in the development database. The browser was refreshed onto the supported All Accounts register with viewport overrides reset.


## Payee management verification (2026-10-21)

The payee extension adds focused validation/matching/history unit tests; PostgreSQL HTTP tests for rename/hidden settings, history and fixed/never category policies, exact-payee filtering, schedule rename, literal rule resolution and specificity/ambiguity, explicit-category preservation, import approval, concurrent revision checks, exact replay and injected save/import rollback; and UI tests for grouped suggestions, stable transfer IDs, search/keyboard selection, modal isolation, rule editing/removal/validation, bulk settings, unused deletion, hide/unhide, draft Undo/Redo, error recovery, conflict reload and identical lost-response retries.

Latest complete suites: **70 domain unit + 43 real PostgreSQL integration + 81 UI component = 194 passing tests**, none skipped. Solution build: zero warnings/errors. TypeScript/Vite production build passed. The earlier 159-test section records the previous register extension rather than this updated total.

Live browser verification used an independent disposable PostgreSQL 17 database (`openbudget-payees-smoke`, label `openbudget.task=payees-smoke`) and generic synthetic merchants/accounts. Verified grouped inline choices, saved category suggestion and transaction save; saved Is/Contains rules and reopened the manager; accepted a normalized imported row and checked canonical payee, active category, unapproved status and balances; restarted the API against that isolated database and confirmed both rules survived. Dialog bounds at a 390px viewport were left 12/right 378. Synthetic screenshots show the grouped dropdown and persisted manager. The test container was removed afterwards, and the normal Aspire stack was rebuilt/restarted with the development volume preserved. No developer ledger rows were created or deleted by these tests.

The accepted-row import API does not complete milestone 7 file parsing/preview, statement duplicate matching or exports. Transfer payee labels are derived from accounts; merchant renaming rules currently target saved merchant payees.


## Grouped category picker verification (2026-10-21)

Latest category extension: **70 domain unit + 44 real PostgreSQL HTTP integration + 101 UI tests = 215 passing tests**, none skipped. Earlier totals record earlier register/payee slices. TypeScript/Vite production build and solution build pass.

New coverage includes ordered active groups, indented children, duplicate names selected by ID, category/group search and keyboard navigation, selected parent labels, authoritative positive/negative/zero and unavailable balance display, Ready to Assign, fresh reads on every open, transaction-month/date-change behavior, failed/loading read recovery with no stale hidden keyboard selection, split validation, and new paired transfer selection through the footer. Category-creation tests cover preserving the snapshot, zero assignment, validation, conflict reload, lost-response readback/retry, no active groups and retaining unsaved transaction fields while creating/selecting/saving the new ID. The PostgreSQL scenario creates/assigns a category, posts spending and a refund, reloads its group/available/RTA and verifies carry-forward into the following month.

Live verification used the isolated `openbudget-categories-smoke` PostgreSQL container (label `openbudget.task=categories-smoke`) with generic sample accounts/transactions, never the development budget. Created Household Supplies from the dropdown, retained the draft memo, saved the transaction's category and reopened the menu: its available balance was -5.99 while the account total remained correct. Verified the grouped menu screenshot and 390px viewport bounds (left 8/right 382). The disposable test container was removed, browser viewport restored, and normal Aspire was rebuilt/restarted with its existing development volume preserved. No backend endpoint/model/migration changed in this extension.


## Assignment rollover regression verification (2026-10-21)

**80 domain unit + 54 real PostgreSQL integration + 119 UI tests = 253 passing tests**, none skipped. This adds 38 regression cases to the earlier category-picker baseline. Solution build has zero warnings/errors; TypeScript/Vite production build passes.

- Exact August EUR50 assigned / EUR35 easyEnergy spending -> EUR15 carried into September, then September EUR50 assigned -> EUR65 available. Tested both when future months already exist and when opened afterwards, including a restarted API host and repeated reads.
- Increasing, decreasing, clearing and moving historical assignments; preserving later assignments, activity, notes/archive/labels and legacy openings; unchanged-money saves do not bump future versions.
- Backdated amount, month and category changes; categorized refunds; deletion; budget-neutral transfers; positive/negative/zero balances; reverse month initialization; skipped months; December/January and February 29.
- Historical new categories absent from future snapshots; no copying of source assignment/activity and no dropped carry across an absent category/group.
- Independent decimal prefix-sum checks across repeated historical edits and a 1,000 signed-cent-change unit scenario; no double carry on repeated GETs or stale PUT retries.
- Concurrent past/future month edits and concurrent spending/assignment commands; stale month revisions return corrected current snapshots. Invalid requests cannot partially update future rows or leave an implicitly created rejected month. A save interceptor throws after SQL SaveChanges and verifies source/future rollback, followed by safe retry.
- Plan UI month navigation with a pending save, invalid assigned input never becoming zero, loading-state edit protection, month-local undo history, September EUR65 and reloaded EUR95 after editing August. Amount parser covers decimal comma, precision/range, signed/blank inputs and rejection of malformed/exponent text.

The live browser scenario used the isolated `openbudget-rollover-smoke` PostgreSQL 17 container, labeled `openbudget.task=rollover-smoke`, with one synthetic Household account. September and October were initialized before August's assignment. The UI assigned August EUR50, a synthetic easyEnergy ledger row spent EUR35 in August, and September displayed EUR15 then EUR65 after its own EUR50 assignment. Updating August to EUR80 displayed September EUR95 with Assigned still EUR50; restoring August EUR50 returned September EUR65. Saved the September screenshot, removed only the labeled test container, restored the viewport, rebuilt and restarted normal Aspire with the development volume preserved. No developer budget assignments or transactions were changed by verification.

## Milestone 11 acceptance map

| Behavior | Automated coverage |
|---|---|
| Plan-style month grid with planned/actual subrows and click-to-edit Assigned amounts | `LongTermPlanning.test.tsx` |
| Planned/actual Available colors negative values red and zero-or-positive values green, including category and group totals | `LongTermPlanning.test.tsx` |
| Current-year default, year navigation, past/current/future edit states | `LongTermPlanning.test.tsx` |
| Effective month overrides, later-override protection and override removal | `LongTermPlanningTests` and `longTermPlanningMath.test.ts` |
| Planned available carry across the year boundary with decimal cents | `LongTermPlanningTests`; `LongTermPlanningApiTests.Planned_available_and_actual_available_carry_across_year_boundary_without_creating_actual_months` |
| Expected income/expenses, Remaining, Remaining to Be Assigned, immediate available and group aggregation with collapsed categories | `LongTermPlanning.test.tsx` and `longTermPlanningMath.test.ts` |
| Actuals from existing monthly snapshots, missing-month carry, and no actual-month initialization | `LongTermPlanningApiTests` |
| Separate planning persistence, API restart, no mutation of monthly budgets, stale writes and invalid/historical input | `LongTermPlanningApiTests` |
| Main sidebar navigation, loading failure and retry | `App.test.tsx` and `LongTermPlanning.test.tsx` |
| Annual activity popup, projected occurrence details and account selection | `LongTermPlanning.test.tsx`; `LongTermPlanningApiTests.Scheduled_income_and_expenses_from_all_accounts_drive_read_only_forecasts` |

## Zero Ready to Assign header (2026-10-21)

All 122 UI tests pass, including three new cases covering the zero summary, navigation back to a funded month, and positive/negative one-cent boundaries. TypeScript/Vite production build passes. Verified the actual Plan component in a temporary browser preview with synthetic read responses: the zero card is 225 by 62 pixels, muted grey, with `All Money Assigned` and a circular checkmark. Removed the preview files after capture; no developer budget data changed. This UI-only change does not alter API or persistence workflows; backend suites were not rerun.

## Payee filtering and creation regression (2026-10-21)

125 UI tests and 55 real PostgreSQL HTTP integration tests pass. New cases cover trimmed/case-insensitive queries, exact merchant and full transfer names, clearing a query, no-match guidance, stale keyboard selection, and Save and add another reloading a newly saved payee. The database regression verifies creation and reload, canonical name reuse, identical command replay, invalid-category rollback, injected write failure and safe retry without orphan payees. Production TypeScript/Vite build passes. Live browser verification filtered `man` to the saved Manager payee and showed no suggestions for an unknown name; the draft was cancelled without changing developer data. No backend implementation changed.

## Shared checkbox appearance (2026-10-21)

All 125 existing UI tests and the production build pass after applying the shared checkbox stylesheet and removing local View Options/Manage Payees overrides. Live browser verification confirms 13px sizing and custom appearance in Plan, the register, View Options and Manage Payees; checked inputs display purple with a white tick, unchecked inputs keep the grey outline, and Space toggles a focused option with a visible focus outline. Cancelled all draft preference changes. This is a CSS-only change with no API/persistence changes; backend suites were not rerun.

## Floating selection bar and actions (2026-10-22)

80 domain unit, 62 PostgreSQL HTTP integration and 139 UI tests pass. The solution and UI production builds pass. New selection coverage includes atomic memo/flag/status/move writes and reload, approval on clear, duplicate splits/status/schedule identity, both transfer legs exactly once, next recurrence without actual balance changes, reconciled acknowledgement/invalidation audit, invalid/oversized/missing selections, closed destinations, opening-balance protection, injected rollback, replay and concurrent stale revisions. UI tests cover reference menu/count, selection IDs, flag reuse, forms/confirmation, uncertainty retry, disabled actions, keyboard focus/shortcuts, CSV quoting/precision/formula protection/download, and hiding the bar during inline editing.

Live browser verification confirms the bottom bar and upward menu on the normal register, and 390px layout bounds (bar left 28/right 362, menu left 130/right 362). No financial action was submitted to the developer budget. Backend was rebuilt and Aspire restarted after implementation. Match/Unmatch are visibly disabled until the import matching workflow exists.

Commit-time verification on October 22: the previous complete run passed the counts above, and the final solution/UI builds passed. A focused rerun of the seven selection integration scenarios after adding explicit action parameters to the audit payload could not initialize its PostgreSQL fixture because Docker Desktop's Linux engine pipe was unavailable. No scenario reached its assertions in that rerun. The additional audit-parameter assertion therefore still needs a Docker-backed rerun.

## Milestone 11 verification (2026-10-22)

- Full .NET suite: 84 unit tests and 66 PostgreSQL integration tests passed; no failures or skips. Integration tests apply the planning migration to isolated Testcontainers databases and verify reads, writes, persistence, effective overrides, historical locks, actual values and stale-version conflicts.
- UI suite: 160 tests passed. Solution build completed with zero warnings/errors; UI production build passed; `git diff --check` passed.
- Initial Aspire startup was blocked because the local `Parameters:postgres-password` user secret was unset; no migration ran during that attempt. Follow-up verification confirmed the named database volume was uninitialized, added a generated development-only credential to local user secrets, and restarted Aspire. Database-connected API health and the UI both returned HTTP 200; the annual planning endpoint returned 12 months. Browser inspection confirmed the Plan-style grid, paired planned/actual rows, and editable Assigned fields. No existing budget data was overwritten.
