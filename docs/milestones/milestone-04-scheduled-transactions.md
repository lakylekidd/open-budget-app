# Milestone 4 — Scheduled transactions and cash-flow awareness

**Status:** Planned

## Goal

Represent expected recurring activity as reviewable scheduled items and create actual ledger entries only when the user confirms an occurrence.

## Dependencies

Milestone 2 provides accounts and the transaction register. Milestone 3 may supply target context, but is not required to create or post a scheduled item.

## User value

A person can see bills and income that are coming due, prepare for them, and decide what to do when each occurrence arrives.

## Vertical slice

- **UI:** Scheduled-item creation/editing; upcoming/overdue list or calendar; occurrence review with post, skip, edit-once, and snooze actions; recurring rule detail; clear distinction between forecast and posted transaction.
- **API/domain:** Recurrence rules for daily/weekly/biweekly/monthly/yearly and custom intervals; next occurrence, end date/count, timezone/date behavior, payee, account, amount, category/split. Generate occurrence identities deterministically; one-off edits do not silently rewrite the parent rule.
- **Persistence:** Rule and occurrence records, occurrence disposition, generated transaction link, and audit history. Posting creates a ledger transaction idempotently and atomically marks the occurrence posted.
- **Budget behavior:** Forecasts are not included in actual account balance, category activity, or reports until posted. The UI may show their projected effect separately.

## Acceptance criteria

1. A user can create, edit, pause, resume, and end a recurrence rule with validation for interval, date, amount, account, payee, and category.
2. Weekly, biweekly, monthly, and yearly schedules generate the expected dates across month ends, leap day, year boundaries, and configured time zones.
3. The upcoming/overdue view shows each due occurrence once and clearly identifies its source rule and due date.
4. Editing a single occurrence changes only that occurrence; editing the recurring rule changes future occurrences according to a documented effective date.
5. Skipping or snoozing an occurrence does not create a register transaction and does not accidentally skip later occurrences.
6. Posting an occurrence creates exactly one transaction and marks the occurrence posted, even if the client retries after a timeout.
7. Posted occurrences flow through the normal register/account/category calculation paths; unposted forecasts do not change actual balances or actual reports.
8. Deleting or pausing a rule preserves already posted transactions and the occurrence history.
9. If the account or category was archived after the rule was created, the user receives a resolvable validation state before posting.
10. The API supports filtering upcoming items by budget/account/date and returns useful pagination for long schedules.
11. Loading, empty, offline, validation, and post-failure states allow the user to recover without creating duplicate occurrences.
12. Recurrence and timezone behavior is documented and covered by deterministic boundary-date examples.

## Product references

[Adding transactions](https://support.ynab.com/en_us/how-to-add-transactions-in-ynab-HyDwA_byi), [YNAB features](https://www.ynab.com/features).
