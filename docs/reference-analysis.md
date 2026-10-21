# YNAB Web app: reverse-engineering notes

Captured 2026-10-21 from the app open in the Codex in-app browser. This report records app structure only and excludes budget names, account labels, transaction details, and amounts.

## Evidence and scope

- REA analyzed seven first-party JavaScript assets observed in the browser (about 2.42 MB): 56 modules, 505,724 visited AST nodes, 1,253 findings, and no parse failures.
- Main bundle: `assets/ynab_web/assets/main-BPlrTCm8.js` (about 1.94 MB).
- Static application analysis Evidence: `ev_4c44814eef17a5b5e9c9f9699dc0a52a1fa55889f36cca867e95bd7ebe74ec4a`.
- REA traced the exact `/budget/:budgetId` route to the main bundle's source module through an `exposes` relationship. That literal is the shared-budget route; it is distinct from the signed-in budget screen's version-and-month route.
- Runtime screen inspection showed the app's main Plan view and its controls. The current view is authenticated and contains private financial data, so this report does not include its user-specific content.

## Frontend structure

The main bundle contains an Ember router and Ember/Embroider output. The module is ESM and REA recorded 856 import relationships for it. The bundle also contains a Vite marker and chunk-loading code; this identifies packaging/runtime clues, not the exact production build pipeline by itself.

The observed route configuration has these main areas:

- `/:budgetVersionId/budget` and `/:budgetVersionId/budget/:monthString` for planning by budget version and month.
- `/:budgetVersionId/reports/spending/totals`, `.../spending/trends`, `.../reports/net-worth`, and `.../reports/income-expense` for reports.
- `/:budgetVersionId/reflect` with spending breakdown, spending trends, spending totals, net worth, income/expense, and age-of-money views.
- `/:budgetVersionId/accounts` with an account ID route.
- `/templates`, template creation/editing, and `/templates/import`.
- Family and group flows for invitations, membership, shared budgets/transfers, group resources, employees, billing, and group reports.
- Home, account setup, plan creation, labs, and a catch-all route.

The Plan view is organized around a month selector, budget status filters, and a category table with Assigned, Activity, and Available columns. It also has category-group actions, a summary panel, auto-assignment controls, and future-month assignments. The sidebar groups accounts by account type and links to Plan, Reflect, and All Accounts.

## Account-linking code observed in the bundle

Static code around the `account-widget/api` service shows flows for creating, stub, and troubleshooting authorization sessions. The same service exposes operations to list budget accounts, fetch one account, update it, and unlink it. In the observed code, account lookup checks an in-memory list before requesting a missing account; unlinking requires an account ID. These are code-level observations, not a claim that each method ran during the browser session.

The bundle contains two literal API-shaped paths: `/api/budgets/${e}/match_debug_events` and `/api/transactions/${e}/images/${t}`. REA's static endpoint trace did not resolve these into graph entities, so treat them as source-string sightings rather than a complete or runtime-verified API map. Other service URLs appear to be assembled or provided through service/configuration code.

## Lazy modules and browser storage

REA retained candidate references to lazy modules whose names suggest category wizards, user changes, event logging, push notifications, feed updates, and support tooling. Those references were unresolved in the downloaded asset set; they do not prove those chunks loaded or ran.

The bundle uses both localStorage and sessionStorage. REA found 21 storage-operation observations, but most key names were not statically recoverable. No stored values were read for this analysis.

## Limits and follow-up

The application and semantic graphs are partial. REA reports about 49,972 semantic unknowns, including dynamic calls and property relationships. It parsed source as inert text and did not execute the bundle. Static route declarations and request-shaped strings do not prove runtime behavior.

The signed-in screen was available for read-only UI inspection, but REA could not reach a local CDP debugging endpoint at `127.0.0.1:9222`. That prevented an authenticated network capture. A fuller behavioral map would need REA connected to the browser's CDP target, then a bounded read-only scenario for key views and their network request shapes. Dynamic chunks should be collected from observed browser assets before drawing conclusions about those features.

REA recorded 25 network-like candidates in the main bundle, but these are mostly method or model names (for example, `budgetMonthDisplayItems` and category_templates), not concrete URL paths. A focused trace linked `budgetMonthDisplayItems` to a get call with low confidence. This supports the presence of request/service interactions but does not identify the server contract.
