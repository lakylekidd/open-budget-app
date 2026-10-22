# Documentation

This documentation describes the current OpenBudget implementation and its planned product evolution. It is written for developers joining the project or picking up a future milestone.

## Documents

- [Developer setup and workflow](developer-guide.md) — tools, secrets, Aspire startup, builds, and database migrations.
- [Architecture and current status](architecture.md) — runtime topology, feature inventory, and implementation boundaries.
- [API reference](api.md) — current HTTP endpoints and request/response behavior.
- [Data model](data-model.md) — current PostgreSQL schema and the future domain model direction.
- [Testing and acceptance coverage](testing.md) — unit, HTTP/PostgreSQL integration and UI tests, commands, and the live smoke scenario.
- [Coding conventions](conventions.md) — current implementation and testing patterns.
- [Product roadmap](roadmap.md) — milestone index with status, summaries, and links to the detailed vertical-slice documents in [milestones/](milestones/).
- [Reverse-engineering notes](reference-analysis.md) — bounded observations from the reference web app, with evidence limits.

## Current release shape

The application provides monthly Plan state, an annual Long-Term Planning forecast, and a cash-account transaction ledger persisted in PostgreSQL and orchestrated by Aspire. Plan supports category activity drill-down, scheduled spending details, category creation, drag ordering/moves and funded/spent/overspent progress bars. Long-Term Planning projects income and expenses from scheduled account transactions, applies effective-month category budget overrides, and compares forecasts against read-only monthly actuals. Accounts, transactions, splits, transfers, cleared/reconciled state and audit history are implemented. Legacy prototype balances remain explicit migration baselines. Inline editing, scheduled register review, named flags, account notes and balance adjustments are implemented. Reflect, credit/debt and bank links remain future milestones.

The roadmap treats the YNAB site as a source of product capabilities and user workflows, not as a source-code or private API contract. Published features and support articles can change; roadmap links were checked on 2026-10-21.
