# Milestone 7 — File import, matching, and portable data

**Status:** Planned; payee rule resolution and accepted normalized-row API are implemented prerequisites.

## Goal

Let a user safely import statement files into the transaction ledger and export a portable copy of their budget data.

## Dependencies

Milestone 2 provides accounts, ledger transactions, and transaction matching. Import is also useful after Milestones 5 and 8, but linked-account connectivity is not required. CSV formats and export schema must be documented and versioned.

## User value

A person can bring in a bank statement, review and correct parsed rows before they affect balances, then export their budget and transaction data.

## Vertical slice

- **UI:** File upload with file limits and privacy message; column mapping; preview and row validation; duplicate/match review; split/category assignment; import confirmation/progress/result; budget and transaction export controls.
- **API/domain:** Parse supported delimited formats; normalize dates, amounts, payees, and optional cleared status; detect duplicates using stable fingerprints plus account/date/amount/payee evidence; match imported rows to existing manual entries; create unmatched transactions only after approval.
- **Persistence:** Import batch, file fingerprint, normalized rows, row decisions, created transaction IDs, and audit trail. Persist enough state to retry safely and avoid duplicate inserts.
- **Portability:** Versioned export includes the data necessary to restore budgets, categories, accounts, target settings, and transaction history; document exclusions and sensitive fields.

## Acceptance criteria

1. Upload accepts only supported file types and configured size limits; invalid files are rejected before import and never change budget data.
2. The preview identifies mapped date, payee, amount, and optional fields; users can correct mappings and see representative normalized rows before proceeding.
3. Invalid rows show row-specific errors and can be corrected or explicitly excluded; valid rows remain reviewable without being committed prematurely.
4. Duplicate candidates are surfaced with comparison details and an explicit keep, match, or skip decision; the service does not silently discard a possibly distinct transaction.
5. Matching an imported row to a manual transaction enriches/clears the existing entry instead of creating a duplicate; unmatched accepted rows create one ledger transaction.
6. Import confirmation is atomic or reports row-level committed outcomes with a safe resume path; retrying the same confirmed batch does not duplicate transactions.
7. Account balances, category activity, and reports update through the same ledger path used by manual transactions.
8. A user can cancel before confirmation and verify that no transaction or balance changed.
9. Export is scoped to the selected budget and chosen date range/options; it includes a schema version and stable currency/date conventions.
10. A supported export can be imported into a fresh budget with relationships preserved and no duplicate rows; unsupported or intentionally omitted data is listed in the export documentation.
11. File content is stored only as long as needed for review/retry according to the documented retention policy; errors and logs avoid exposing full sensitive statement data.
12. Accessible progress, cancellation, validation, and completion feedback is provided for large imports.
13. The supported file formats, encoding, date/amount parsing, matching algorithm, size limits, and privacy behavior are documented.

## Product references

[File-based import](https://support.ynab.com/en_us/file-based-import-a-guide-Bkj4Sszyo), [adding transactions](https://support.ynab.com/en_us/how-to-add-transactions-in-ynab-HyDwA_byi).


## Implemented prerequisite (2026-10-21)

`POST /api/transactions/import` accepts one validated normalized row with the ledger command protocol, applies persisted literal Is/Contains merchant rules and category policies, records source/result audit and creates an unapproved entry atomically. Hidden payees still match. Exact command replay prevents retry duplication; new command IDs do not perform inferred duplicate detection. This does not complete file upload, mapping, preview, statement duplicate/manual matching, batch confirmation, export or restore acceptance criteria above.
