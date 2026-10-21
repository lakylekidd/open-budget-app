# Milestone 1 — Reliable budget core and month lifecycle

**Status:** Planned

## Goal

Make a budget internally consistent and persistent across months, replacing cloned sample snapshots with explicit budget state and server-owned calculations.

## Dependencies

Milestone 0 provides the running UI/API/PostgreSQL/Aspire foundation. Complete this milestone before implementing account transactions, which will become the authoritative source of actual cash activity in Milestone 2.

## User value

A person can see how much money is ready to assign, give funds a category, move assignments between categories, and understand what remains available in each month.

## Vertical slice

- **UI:** Budget/month navigation; create or initialize a budget; manage ordered category groups and categories; edit assignments; move funds between categories; show ready-to-assign and available totals; expose loading, validation, save, and conflict recovery states.
- **API/domain:** Explicit budget and monthly budget operations; authoritative calculation of assigned, activity, available, and ready-to-assign values for the features in this milestone; validation of money amounts, month order, category ownership, and assignment/move commands. Define and document how overspending and positive/negative carry-forward behave.
- **Persistence:** Store budget identity, ordered groups/categories, month assignments, carry-forward inputs/results, and a concurrency version. Persist explicit edits rather than treating browser-supplied totals as authoritative.
- **Consistency:** Use decimal/minor-unit money representation. Apply assignment/move commands transactionally. Reject stale writes using a version/ETag and return enough information for the UI to reload and recover.

## Acceptance criteria

1. A user can create/open a budget and see an empty or explicitly initialized plan; no unseen month silently clones the full sample snapshot.
2. Adding, renaming, reordering, archiving, or restoring a category group/category persists and is reflected when the month is reloaded. Archived categories with history remain available in historical months.
3. Assigning money updates the category's assigned and available values and the budget's ready-to-assign value on the server; the UI displays the returned authoritative result.
4. Moving money between categories changes the source and destination assignments by the same amount and leaves total assignments and ready-to-assign unchanged.
5. Invalid amounts, unknown categories, cross-budget identifiers, and writes that violate the documented budget rules are rejected with field/action-specific errors and do not partially persist.
6. Opening a later month applies the documented carry-forward rules exactly once, including positive balances and the selected treatment for overspending; refreshing or requesting it again does not duplicate carry-forward.
7. Carry-forward works across December to January and across leap-year boundaries.
8. Changing a current month's assignment does not rewrite prior months or a later month's explicit assignment.
9. Two clients editing the same budget version cannot silently overwrite each other: the stale write is rejected and the UI offers a reload/reapply path.
10. A successful command is atomic: a simulated persistence failure leaves both assignment rows and calculated totals at their previous committed state.
11. Month navigation preserves each month's assignments and category ordering after refresh and application restart.
12. All displayed totals reconcile from persisted values using the documented money precision; repeated calculation and serialization do not introduce rounding drift.
13. The API contract and migration strategy describe the change from snapshot-supplied totals to server-authoritative budget calculations.

## Product references

[YNAB features](https://www.ynab.com/features), [auto-assign guide](https://support.ynab.com/en_us/auto-assign-a-guide-r1gBNbBJo), [category management](https://support.ynab.com/en_us/adding-removing-and-customizing-categories-a-guide-HJFO5j909).
