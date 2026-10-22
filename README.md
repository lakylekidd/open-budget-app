# OpenBudget

OpenBudget is a local-first budgeting application inspired by publicly documented budgeting workflows. The current implementation provides React and TypeScript Plan and Long-Term Planning screens backed by a .NET 10 API and PostgreSQL, orchestrated locally with .NET Aspire.

All Accounts provides cash accounts, a searchable register, splits, linked transfers, reconciliation and an audit history. Account balances and new budget activity come from the ledger. Add an account with its opening balance, record negative amounts for spending and positive amounts for income/refunds, and leave income uncategorized to add it to Ready to Assign.

The Plan header shows a grey `All Money Assigned` card with a checkmark when Ready to Assign is €0.00.

Choose **Long-Term Planning** in the sidebar to view all twelve months for the current year. Expected income and expenses come from active scheduled transactions across all accounts. Click those totals or a category's Expenses value to inspect the underlying activity, then click a transaction to open and select it in its account. Each month shows **Remaining to Be Assigned**, calculated as expected income minus planned category budgets, alongside remaining income after scheduled expenses. Enter planned category budgets for the current or future months. Budget overrides apply from that month onward until a later override; planned availability rolls into the following month. Past months are read-only. The screen compares the forecast with existing monthly assigned, activity, and available values without changing the regular Plan or creating missing budget months.

Plan category activity opens the month's matching transactions. Selecting a transaction opens its account register with that transaction highlighted, including reconciled rows. Click a category to see its available balance and scheduled transactions; upcoming spending shows the balance after those forecasts. Add categories from the group plus button, and drag categories between groups or groups/categories to reorder them. Progress bars show the remaining funded amount in green and uncovered overspending in red. Unfunded overspending carries into later months.

Checkboxes throughout Plan, the register and dialogs use the same compact purple checked style with a white tick.

Typing a payee filters saved payees and transfer accounts by name. Save a transaction with a new name to create a saved payee; it is then available in subsequent transactions.

Selecting transactions opens a floating bar at the bottom of the register. More offers clearing, duplication, repeating schedules, memo edits, moving ordinary transactions to another account, CSV export and confirmed deletion. Bulk writes are atomic and replay-safe. Match/Unmatch remain disabled until imported-transaction matching is implemented.

Select an account in the sidebar for its register: Cleared + Uncleared = Working Balance, with separate outflow, inflow and running-balance columns. Click a transaction's `c` to toggle cleared status; green locks identify reconciled history. Use Reconcile to confirm the statement balance, and View for date presets, month ranges and column visibility. Apply saves every View selection per account on the server, so it follows the account on another PC connected to the same OpenBudget server. Cancel discards the draft. More contains category/status filters, transfers, audit history, and actions for the selected row. Clicking a row highlights it; clicking it again edits it inline. Add Transaction inserts a row with Save/Cancel and Save and add another. While editing inline, Tab moves between fields and skips the adjacent dropdown arrows; inputs keep a single border without an extra focus outline. Accounts are currently unlinked/manual; automatic bank sync is a later milestone.

## Run the application

From the repository root:

```powershell
dotnet run --project src/OpenBudget.AppHost
```

Aspire starts PostgreSQL, the API, and the Vite UI. Open the `ui` endpoint shown in the Aspire dashboard; the stable local URL is `http://127.0.0.1:5173/`.

Requirements: .NET 10 SDK, Node.js/npm, and Docker Desktop. PostgreSQL uses a persistent volume. On a fresh machine, set the local AppHost password as described in [Developer setup](docs/developer-guide.md).

## Project layout

- [`docs/`](docs/README.md): developer documentation and product roadmap.
- [`src/OpenBudget.UI/`](src/OpenBudget.UI/): React and TypeScript frontend.
- [`src/OpenBudget.Api/`](src/OpenBudget.Api/): .NET 10 Minimal API, EF Core model, and migrations.
- [`src/OpenBudget.AppHost/`](src/OpenBudget.AppHost/AppHost.cs): Aspire orchestration for PostgreSQL, API, and UI.
- [`OpenBudget.slnx`](OpenBudget.slnx): .NET solution.
- [`tests/`](tests/): domain unit tests and HTTP/PostgreSQL integration tests.

## Build

```powershell
dotnet build OpenBudget.slnx
npm --prefix src/OpenBudget.UI run build
```

See the [documentation index](docs/README.md) for architecture, API, data model, and roadmap details.

## Test

```powershell
dotnet test OpenBudget.slnx
npm --prefix src/OpenBudget.UI test
```

Integration tests require Docker and start disposable PostgreSQL 17 with a fresh database per scenario; they never use the Aspire database. See [testing and verification](docs/testing.md).

Account editing includes nickname, notes, and a Working Balance field. Changing the balance creates a cleared, audited adjustment entry rather than overwriting history. A reconciled edit requires an explicit warning acknowledgement, marks the affected entry cleared and invalidates the retained reconciliation snapshot; reconcile again after reviewing the change. Reconciled deletion remains blocked.

Choose Repeat in the inline date calendar for recurring transactions. Future entries are scheduled forecasts until due; past/today entries post normally and an explicit Repeat also creates their next occurrence. Expand Scheduled Transactions to review projected balances; select a scheduled row and use More for Enter Now, Skip, Snooze one day, Pause/Resume, or End schedule. On their due day, schedules post automatically as unapproved actual transactions and advance to the next date. Click the `i` to Approve/Reject; clearing also approves. White sidebar dots appear only for accounts with unapproved transactions, and the review banner includes transactions needing approval or a category. Enter Now posts a future occurrence early. The inline editor can change only the next occurrence or the future rule. Clicking the flag opens six colors and Edit Flags; colors and shared custom flag names persist on the server. Hovering a sidebar account shows its edit pencil; the selected account stays highlighted. Bank linking, import and register undo/redo remain future features.


The account register's Payee dropdown groups account transfers and saved merchant names. Manage Payees (dropdown footer or More) saves names, hidden state, category choices and Is/Contains import-renaming rules in PostgreSQL for every client of the same server. Undo/Redo affects the dialog draft; Done persists it. Used payees can be hidden; deletion preserves transaction/schedule history by allowing only unused unreferenced names. Transfer names follow account edits. Accepted normalized-row imports apply these rules through `POST /api/transactions/import` and enter the existing approval workflow; full File Import upload/preview/matching remains a later milestone.


Transaction category dropdowns show category groups with indented children and each category's available balance for the transaction's month. They include search, Ready to Assign, Split, account transfer choices, and New Category. Balances reload when the dropdown opens or the date changes. Creating a category preserves the transaction draft and saves it into the chosen existing budget group.


Budget assignments carry their remaining available money into later months: assigning EUR50 in August and spending EUR35 leaves EUR15 for September; assigning EUR50 in September makes EUR65 available. Editing August later updates all existing later opening balances while preserving each later month's own assignment. Plan waits for saves before month navigation and rejects invalid assignment input. This is covered by unit, real PostgreSQL HTTP and UI regression tests, including concurrent edits and transaction rollback.
