# Global categories

Category groups and categories are the same in every Plan month, past and future. Only money and a few per-month
choices are stored per month. This replaces the earlier per-month copies (each month created by copying the previous,
next or starter-template month), which let months drift apart: a month created from the starter template showed
template categories and lacked the user's own, so category pickers showed different categories depending on the
transaction date.

## Schema

| Table | Key | Holds | Scope |
|---|---|---|---|
| `BudgetCategoryGroups` (`CategoryGroupEntity`) | `(BudgetId, Id)` | `Name`, `SortOrder`, `IsArchived` | budget |
| `BudgetCategories` (`CategoryEntity`) | `(BudgetId, Id)` | `GroupId` (FK to the group), `Name`, `Icon`, `SortOrder`, `IsArchived` | budget |
| `CategoryMonths` (`CategoryMonthEntity`) | `(BudgetId, BudgetMonth, CategoryId)` | `OpeningAvailable`, `Assigned`, `LegacyActivity`, `Activity`, `CreditOverspending`, `Target` (legacy amount), `Snoozed` | month |
| `Budgets` | `(BudgetId, Month)` | unchanged: opening Ready to Assign, legacy funds, ledger income, note, `Version` | month |
| `BudgetPlans.CategoriesInitialized` | | true once the budget's categories exist (starter seeded or migrated) | budget |

Foreign keys: definitions cascade with their budget plan; month state cascades with its month and uses `NO ACTION`
towards its category definition, so a category cannot be removed while any month still holds state for it; a group
cannot be removed while it has categories.

A month without a state row for a category treats it as all zero. Rows are added when a month is loaded for an
operation (`CategoryCatalog.Attach`) or when carried money reaches the month, so a category added later appears in
every month without copying, and carry-forward never drops money.

### Per-month versus budget-wide

| Budget-wide (definition) | Per month (state) |
|---|---|
| ID, name, group, sort order, icon, archived flag (group and category) | assigned, activity (materialized), available (calculated), opening available, legacy activity, credit overspending |
| structured targets (`CategoryTargets`, already keyed by budget and category) | target progress (calculated per month) |
| | snooze, legacy manual `target` amount (carried into newly created months, as before) |

Snooze stays per month because it means "not this month"; the product owner may decide to make it budget-wide later
(one column move).

## Code structure

`CategoryCatalog` (`src/OpenBudget.Api/CategoryCatalog.cs`) holds a budget's tracked definitions for one request.
`db.LoadPlanMonthsAsync(query)` loads months with their state and composes each with the catalog; `BudgetEntity.Groups`
then returns `PlanCategoryGroup`/`PlanCategory` views (definition + that month's state) in display order, so services
keep reading `month.Groups...` while writes to names/order/archive go to the shared definition. System categories are
ensured once per budget: `CreditCardRules.EnsurePaymentCategories`, `LoanRules.EnsureCategories` and
`WishlistRules.EnsureCategories` take the catalog.

## Month creation and the starter template

Months are still created lazily (`BudgetService.GetMonthAsync`). A new month gets a state row for every category:
opening available is carried from the nearest earlier month (positive only, as before), snooze and legacy target are
copied from it; a month before every existing month starts at zero and copies snooze/target from the nearest later
month. The starter template is seeded only when `CategoriesInitialized` is false and the budget has no categories,
then the flag is set; it is never seeded per month and never again after the user deletes categories.

## Editing and versioning

`PUT /api/budgets/{id}/months/{month}` keeps its request/response shape. Names, groups, order, icons and archive state
in the request are applied to the budget's definitions; assigned, snooze and legacy target to that month. When any
definition changes, every month of the budget gets a new `Version`. A session holding another month therefore gets
409 on its next save and reloads instead of writing back stale names or order (the month version remains the single
optimistic concurrency token; no separate structure version is needed). Assignment-only saves change only the months
whose money changed. Groups and categories cannot be removed through this endpoint ("must be archived instead of
removed" still applies); deletion uses the endpoints below.

Live updates: definition changes publish a `plan` event with `month: null` (open sessions reload whatever month they
show); month state changes publish events for their month. Bulk changes collapse as before.

## Deleting a category

- `GET /api/budgets/{id}/categories/{categoryId}/deletion` returns the preview (`CategoryDeletionPreviewDto`): counts of
  transactions (including soft-deleted ones, which keep their history), split lines, schedule templates/overrides,
  months with an assigned amount and their total, the available amount in the current month, payee defaults, loan
  pairings, long-term planning values, whether a target exists, the ledger version and the allowed reassignment
  categories (active, non-archived, not the deleted one, not credit card payment, loan or Wishlist categories).
- `POST /api/budgets/{id}/categories/{categoryId}/delete` with `{ commandId, version, reassignToCategoryId }` is a ledger
  command (replay-safe command ID, ledger version check, audit receipt `category.delete/{categoryId}`), authorized like
  the Plan month routes (owner or editor of an active budget, otherwise 404) and run under `LedgerGate` in one
  database transaction.

A category has history when it has money in any month or any reference above. Then a target category is required
(400 otherwise). In one transaction the server:

1. moves transactions and split lines, schedule templates and pending overrides, payee defaults and loan pairings to the
   target; moves long-term planning values (added together where both have a month);
2. removes the deleted category's structured target (its definition is kept in the audit receipt; targets cannot be
   merged meaningfully);
3. recalculates every month oldest first: each month's assigned amount, legacy activity and non-carried opening baseline
   are added to the target's, activity and credit-card flows are recomputed from the moved transactions, and
   carry-forward and Ready to Assign deltas propagate as for any other change (for example the target now covers
   overspending the deleted category had);
4. verifies the deleted category has no money left in any month (otherwise aborts), removes its state rows and its
   definition, advances every month version, and records a Recent Moves action (`reassigned`, total assigned moved).

A category without history needs no target and is removed directly (still confirmed in the UI). Credit card payment,
loan and Wishlist categories cannot be deleted here; they belong to their account or bucket. Any failure rolls the
whole command back. Group deletion is not part of this change: empty groups can be archived.

The earlier rule "existing groups and categories must be archived instead of removed" is kept for month saves, so no
client can drop history by omitting a category. Deletion is only possible through the reassign flow, which moves
history first and records the change in the ledger audit log (before: definition and target; after: target category,
counts and assigned amount per month) and in Recent Moves.

## Migration

Migration `GlobalCategories` (hand-written body, scaffolded Designer/snapshot) runs in EF's migration transaction at
API startup:

1. renames `CategoryGroups`/`Categories` to `LegacyMonthCategoryGroups`/`LegacyMonthCategories` (kept unchanged as a
   read-only pre-migration copy for audit and manual recovery; drop them in a later release once production is verified);
2. creates the new tables;
3. definitions: per (budget, group) and (budget, category) the row from the **most recent month that contains it**
   wins (name, group, icon, order, archived). This resolves months that were created from the starter template or that
   kept old names; a category that only exists in older months keeps its latest definition (including its archived
   flag). Sort orders are renumbered 0..n per budget (groups) and per budget/group (categories), keeping that order
   (ties: more recent month first, then ID);
4. month state: every legacy row is copied unchanged;
5. sets `CategoriesInitialized` for budgets that have months or categories;
6. verifies inside the transaction that every legacy row has an identical state row and every legacy group/category a
   definition; otherwise it raises an error and the whole migration rolls back.

No ID changes, so transactions, splits, schedules, targets, payees, loans, Wishlist and credit card payment categories
keep their references. Data statements use `ON CONFLICT DO NOTHING` and EF records the migration once, so re-running
(including `dotnet ef migrations script --idempotent`) is safe.

Down rebuilds per-month rows from the global data (every month gets every group and category; months without state get
zeros), drops the new tables and restores the original table names. Category deletions done after the upgrade are not
undone.

### Before deploying to production

1. Take and verify a full PostgreSQL backup of the production database (for example `pg_dump -Fc`) and keep it until the
   new version is verified. Restore-test it on a non-production instance.
2. Stop the old API before the new one starts (single-instance Compose deployment): the old version cannot read the
   renamed tables.
3. After startup, check the API log for the migration and spot-check a few months per budget (names, available
   amounts, Ready to Assign).

Tests: `GlobalCategoriesMigrationTests` seeds several budgets/months including a template-seeded month whose names,
groups and order differ, a category present in only one month and money in every column, then checks the dedupe rules,
exact preservation of every month value, budget isolation, the starter flag, the composed months, and the Down/Up round
trip.

## Decisions for the product owner

- Conflicting historical names/groups/order resolve to the most recent month's definition (documented above).
- Snooze and the legacy manual target stay per month.
- Deleting a category removes its structured target instead of moving it to the reassignment category.
- Long-term planning values of the deleted category are added to the target's.
- Category deletion is limited to user categories; group deletion is not provided yet.
