# Milestone: Annual Long-Term Budget Planning

## Goal

Add a new **Long-Term Planning** screen that gives the user a full-year budgeting forecast in a spreadsheet-style layout. The screen allows users to plan expected income, expenses, and category budgets across all 12 months while comparing the plan against actual results.

The existing application stores each month independently, while explicit month carry-forward is still planned work. This milestone introduces deterministic forecasting and carry-forward behavior rather than relying on cloned monthly snapshots. 

## Core behavior

The screen defaults to the current calendar year and displays January through December horizontally. Users can navigate to the previous or next year.

Each month is represented as a rounded spreadsheet-style column:

- Past months: gray and read-only.
- Current month: light blue and editable.
- Future months: light green and editable.

At the top of every month column are:

**Expected Income**  
**Expected Expenses**  
**Remaining = Expected Income − Expected Expenses**

Below the summary, the screen displays the same category groups and subcategories used by the main budget.

Category-group rows are calculated totals and cannot be edited. Individual subcategories contain:

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

2. **Month states:** Months before the current month are visually gray and cannot be edited. The current month uses the light-blue treatment. Future months use the light-green treatment. Month columns use a rounded spreadsheet-style visual treatment.

3. **Historical years:** When viewing a year entirely in the past, all months are read-only. When viewing a future year, all months are editable.

4. **Category structure:** The screen displays the same ordered category groups and subcategories used by the main budget. Category groups can be collapsed/expanded if the existing category UI supports this behavior.

5. **Category editing:** Category-group totals cannot be edited directly. Planning inputs can only be changed at subcategory level.

6. **Monthly summary:** Each month displays Expected Income, Expected Expenses, and Remaining. Remaining is recalculated immediately whenever its underlying planning values change.

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

24. **Automated coverage:** Tests cover propagation, overrides, removal of overrides, group aggregation, year-boundary carry-forward, historical-month locking, summary calculations, and persistence.