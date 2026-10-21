# Documentation

This documentation describes the current OpenBudget implementation and its planned product evolution. It is written for developers joining the project or picking up a future milestone.

## Documents

- [Developer setup and workflow](developer-guide.md) — tools, secrets, Aspire startup, builds, and database migrations.
- [Architecture and current status](architecture.md) — runtime topology, feature inventory, and implementation boundaries.
- [API reference](api.md) — current HTTP endpoints and request/response behavior.
- [Data model](data-model.md) — current PostgreSQL schema and the future domain model direction.
- [Product roadmap](roadmap.md) — milestone index with status, summaries, and links to the detailed vertical-slice documents in [milestones/](milestones/).
- [Reverse-engineering notes](reference-analysis.md) — bounded observations from the reference web app, with evidence limits.

## Current release shape

The application currently demonstrates the web Plan experience with sample data. Budget state is stored per month in PostgreSQL, and local development is orchestrated by Aspire. Accounts shown in the sidebar, Reflect, most account workflows, and several secondary controls are presentation-only placeholders today.

The roadmap treats the YNAB site as a source of product capabilities and user workflows, not as a source-code or private API contract. Published features and support articles can change; roadmap links were checked on 2026-10-21.
