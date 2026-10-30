# Milestone 3 — Goals, templates, and guided assignment

**Status:** In progress — monthly Auto-Assign actions and category targets (create/edit/delete, Weekly/Monthly/Yearly/Custom, server-calculated monthly progress, Underfunded filter and Auto-Assign integration) are implemented; pause/snooze semantics, templates and recommendation preview/apply workflows remain planned.

## Goal

Let people define what categories need to accomplish, reuse category setups, and review explainable assignment recommendations.

## Dependencies

Milestone 1 provides assignment rules, month lifecycle, and category persistence. Milestone 2 is needed for targets whose progress depends on actual spending/activity; targets based only on planned contribution can be delivered with this slice and clearly label their inputs.

## User value

A person can set aside money for upcoming bills and longer-term savings, understand the amount needed this month, and apply recommendations they have reviewed.

## Vertical slice

- **UI:** Target editor and type-specific fields; progress/underfunded indicators; pause/snooze/resume; category templates; auto-assign preview listing each recommendation and reason; select, apply, undo, and error states.
- **API/domain:** Target types for a target balance by date, recurring spending/needed-for-spending, and monthly contribution. Calculate required contribution from target parameters, current category available, previous contributions, and the defined period. Provide preview and apply commands with the same calculation version. Templates snapshot reusable category/group and target settings.
- **Persistence:** Target definitions and effective dates; template definitions; explicit assignment commands and audit records. Applying a preview persists assignments atomically and remains compatible with concurrency versioning.
- **Rules:** Preview never mutates data. Snoozing and pausing have explicit semantics. Recommendations never exceed ready-to-assign funds and explain any unmet target amount.

## Acceptance criteria

1. A user can create, edit, pause, resume, and remove a target; valid target fields depend on the selected target type and invalid combinations are rejected by the API.
2. The target editor explains required amount, due date/cadence, and any assumptions used to calculate the monthly contribution.
3. Target progress and required contribution update when assignments, category available amounts, target dates, or relevant activity change.
4. A target due date in the past or a target with zero/invalid cadence returns a useful validation state and does not create a malformed target.
5. A paused/snoozed target is excluded from recommendations according to its documented behavior and retains its history/settings for resumption.
6. A user can save a category template and apply it to a budget; duplicate names and category conflicts are handled with explicit choices rather than silent overwrites.
7. Auto-assign preview reports the recommended category amounts, available funds, remaining unfilled needs, and a human-readable reason for each recommendation.
8. Requesting a preview does not change assignments or create audit entries.
9. Applying selected recommendations uses the preview's version; if funds or assignments changed since preview, the API rejects stale application and the UI refreshes the preview.
10. Applying recommendations never assigns more than ready-to-assign funds and keeps all budget totals reconciled.
11. Applying, undoing, or partially accepting recommendations produces the same persisted result after refresh and restart.
12. Target calculations use exact money precision and are deterministic for the same budget, target, and month inputs.
13. Keyboard and screen-reader users can edit targets, inspect recommendation reasons, and apply/cancel the preview without relying on color alone.
14. The API documents target calculation semantics, rounding, pause behavior, template behavior, and recommendation ordering.

## Product references

[Using targets](https://support.ynab.com/how-to-use-targets-rk5kkI9ks), [getting started with targets](https://support.ynab.com/en_us/getting-started-with-targets-ryAEP08xC), [auto-assign](https://support.ynab.com/en_us/auto-assign-a-guide-r1gBNbBJo).

## Implemented: category targets (2026-10-30)

- **Acceptance 1 (partial):** create, edit and remove a target per regular category; type-specific fields are validated by the API (`400`, no partial writes). Pause/resume is not implemented; snoozed categories are excluded from the Underfunded filter and Auto-Assign target needs.
- **Acceptance 2-3:** the card shows the cadence, due date and needed/assigned/to-go amounts; progress is recalculated from the month's Assigned/Available on every month read, so it follows assignments, activity and target edits.
- **Acceptance 4:** non-repeating custom targets due before today, out-of-range dates, zero/invalid amounts and repeat intervals outside 1-99 are rejected.
- **Acceptance 12:** decimal calculations with round-up-to-cent spreading are deterministic; see [API](../api.md#target-progress) for formulas and worked examples.
- Writes are versioned per target, serialized by `LedgerGate`, replay-safe by command ID and audited in the ledger command receipts. Payment categories are rejected.
- Tests: `CategoryTargetRulesTests` (unit), `CategoryTargetApiTests` (PostgreSQL HTTP), `CategoryTarget.test.tsx` and `App.test.tsx` (UI workflow).

Remaining for this milestone: pause/snooze/resume, category templates, and recommendation preview/apply with reasons and versioned apply.
