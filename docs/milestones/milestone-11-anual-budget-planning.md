# Milestone: Annual Long-Term Budget Planning

**Status:** Implemented

## Goal

Add a new **Long-Term Planning** screen that gives the user a full-year budgeting forecast in a spreadsheet-style layout. The screen projects income and expenses from scheduled account transactions, lets users plan category budgets across all 12 months, and compares the plan against actual results.

The existing application persists monthly snapshots and carries actual available amounts between initialized months. Long-term forecasts remain a separate model and must not create or clone monthly snapshots to obtain their planning values.

## Core behavior

The screen defaults to the current calendar year and displays January through December as columns in a Plan-style category grid. Users can navigate to the previous or next year.

Month headers use the same dark table treatment as Plan and keep their state visible:

- Past months: gray and read-only.
- Current month: light blue and editable.
- Future months: light green and editable.

At the top of every month column are **Expected Income**, **Expected Expenses**, **Remaining to Be Assigned = Expected Income − planned category budgets**, and **Remaining = Expected Income − Expected Expenses**.

Selecting Expected Income, Expected Expenses, or a read-only category/group Expenses value opens an activity list for that month. Selecting a listed transaction opens its account register with the row selected. Future forecast occurrences appear as selected projected rows in the account register when they are later than the schedule's next occurrence.

Below the summary, the screen displays the same category groups and subcategories used by the main budget. Each subcategory/month cell has two stacked rows: planned on top and actual underneath. The planned row shows Assigned in currency format until clicked, then opens the inline editor; scheduled Expenses and calculated Available remain read-only. Available values are red below zero and green at zero or above. The actual row shows read-only Assigned, Expenses, and Available values. This keeps each month a grid column and makes planned Assigned behave like the monthly Plan input.

Category-group rows show planned totals on the title line and cannot be edited. Individual subcategories contain:

**Plan**
- Planned Budget
- Planned Expenses
- Planned Available

**Actual**
- Actual Budget/Assigned
- Actual Expenses
- Actual Available

Planned Available rolls forward month-to-month:

`Planned Available[m] = Planned Available[m-1] + Planned Budget[m] - Planned Expenses[m]`

Actual Available follows the equivalent actual-value carry-forward.

Planning data should remain separate from actual monthly budget data. The current data model persists Assigned, Activity, and Available as monthly snapshot values, and Activity/Available are currently browser-supplied rather than ledger-derived, so the annual planning model should not make those existing snapshot fields the source of truth for forecast data.

## Budget propagation rule

A planned budget value represents an effective value beginning in a particular month.

For example:

- Setting Groceries to €500 in January results in €500 being planned for January through December.
- Changing Groceries to €600 in June leaves January–May unchanged and results in €600 for June through December.
- Changing the amount again in September results in the September value applying from September onward.

The implementation should therefore support effective-from-month overrides rather than requiring twelve independent values to be manually maintained.

## Acceptance Criteria

1. **Annual navigation:** Opening Long-Term Planning shows the current calendar year. Previous-year and next-year controls change the displayed year and always show all 12 months.

2. **Month states:** Months before the current month are visually muted and cannot be edited. The current month uses a blue accent and future months use a green accent within the Plan-style grid.

3. **Historical years:** When viewing a year entirely in the past, all months are read-only. When viewing a future year, all months are editable.

4. **Category structure:** The screen displays the same ordered category groups and subcategories used by the main budget. Category groups can be collapsed/expanded if the existing category UI supports this behavior.

5. **Category editing:** Category-group totals cannot be edited directly. Planning inputs can only be changed at subcategory level.

6. **Monthly summary:** Each month displays Expected Income, Expected Expenses, Remaining to Be Assigned, and Remaining. Remaining to Be Assigned is Expected Income minus all planned category budgets; Remaining is Expected Income minus Expected Expenses. Both summaries update immediately when a budget override changes.

7. **Planned values:** Every subcategory/month displays Planned Budget, Planned Expenses, and Planned Available.

8. **Actual values:** Every subcategory/month displays the corresponding actual values underneath the planned values. Actual values are read-only on this screen.

9. **Group totals:** Each category-group row equals the sum of all visible and hidden subcategories belonging to that group for the same month and metric.

10. **Planned carry-forward:** Planned Available for a month includes the previous month's Planned Available plus the current month's planned budget minus planned expenses. Carry-forward also works across the December → January year boundary.

11. **Actual carry-forward:** Actual Available carries forward from the preceding month using the equivalent actual values and remains consistent with the application's actual budget data.

12. **Initial propagation:** Entering or changing a Planned Budget in month `M` applies that value to month `M` and every later month until another explicit override exists.

13. **Mid-year override:** Creating an override in June changes June onward without modifying January through May.

14. **Existing future override protection:** If September already contains an explicit override and June is subsequently changed, the June value applies June–August only; the September override and values after it remain intact.

15. **Removing an override:** Removing a month's explicit override causes that month to inherit the most recent prior planned value again.

16. **Past-month protection:** Neither direct editing nor propagation from another month may change a month that is already locked as historical.

17. **Persistence:** Planning values and effective-month overrides survive browser refreshes and application restarts.

18. **Year navigation persistence:** Moving away from a year and returning to it reproduces the same forecasts, overrides, carry-forward values, and actual values.

19. **Immediate recalculation:** Editing a planning input updates affected month totals, category-group totals, Expected Expenses, Remaining, Planned Available, and all downstream carry-forward values without requiring a page refresh.

20. **Money precision:** Forecast calculations use the application's documented decimal/minor-unit money strategy and never binary floating-point arithmetic.

21. **Separation from actual budget:** Editing Long-Term Planning never changes historical Assigned, Activity, or Available values in the normal monthly Plan screen.

22. **Responsive annual grid:** Category labels remain identifiable while horizontally scrolling through the year, preferably through a sticky category column and sticky month/header rows.

23. **Loading/error handling:** The screen provides explicit loading and failure states and does not silently replace missing planning data with synthetic sample values.

24. **Automated coverage:** Tests cover the paired planned/actual grid rows, editable Assigned input, propagation, overrides, removal of overrides, group aggregation, year-boundary carry-forward, historical-month locking, summary calculations, and persistence.

## Implementation notes

Long-Term Planning is available from the main navigation. `GET /api/long-term-planning/{year}` returns twelve months, the latest active category structure, saved planning values, and read-only actuals from existing monthly snapshots. The annual read does not initialize missing budget months. `PUT /api/long-term-planning/{year}` saves one month value against a shared optimistic version. Budget overrides are stored in `LongTermPlanningCategories`; they never update the regular monthly budget tables.

Expected income and planned expenses are read-only projections of active scheduled transactions across all accounts. Transfers are excluded, and category expenses include scheduled splits. Planned budget values are effective-month overrides; the latest override applies forward until another override, including across year boundaries. Clearing an override restores the previous effective value. Planned available and month summaries recalculate from decimal values. Past months are locked in both UI and API using the server's local date; current and future months can be edited. Actual assigned, net expenses and available values are read from persisted monthly snapshots and remain read-only. If an actual month is absent, assigned/expense values display as unavailable and its available balance carries from the nearest preceding snapshot without creating a new actual month.

The grid uses ordered active groups/categories from the latest existing monthly snapshot, supports collapsing groups while retaining group totals, and keeps the category labels and month headings visible while scrolling. Its row borders, typography, dark surfaces and Assigned inputs follow the main Plan grid. A blank plan starts with zero estimates and reports that no planning values have been saved; it does not seed sample forecasts.

**Plan integration:** the monthly Plan shows each category's effective planned budget for that month as muted text under its Assigned amount (title/screen-reader label "Planned €X in Long-Term Planning"; group rows show the sum of their planned categories). The value is server-owned `CategoryDto.planned`, the latest override at or before the month, or `null` when none exists (no text is shown; an explicit `0` override is shown). Auto-Assign has a **Planned** option (all categories or the selected ones) that tops each non-snoozed planned category up to its planned amount without lowering assignments, capped by Ready to Assign in category order and saved through the normal versioned Plan save.
