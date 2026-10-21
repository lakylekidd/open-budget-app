# Milestone 0 — Local application foundation

**Status:** Current / implemented

## Goal

Provide a runnable local product foundation with a Plan screen, a persisted monthly budget snapshot, and one command to start the complete development stack.

## User and developer outcome

A developer can start OpenBudget through Aspire, open the Plan screen in a browser, edit a sample monthly plan, and see those edits after a refresh or application restart. The sample remains explicitly a prototype: this milestone does not claim to provide a transaction ledger, month carry-forward rules, authentication, or multi-user safety.

## Vertical slice

- **UI:** React/TypeScript Plan screen, month navigation, category groups/categories, assigned amount edits, move-money interaction, add/rename category/group interactions, and saved/loading/error feedback.
- **API/domain:** .NET 10 endpoints to load and save a month; validate the month key and request shape; replace a month snapshot atomically. A deterministic sample plan initializes a month that has no saved record.
- **Persistence:** PostgreSQL schema for months, category groups, categories, and the month note; EF Core migration applied at API startup.
- **Operations/docs:** Aspire AppHost starts PostgreSQL, API, and Vite in dependency order; `/health` reports API/database readiness; developer instructions describe prerequisites, secrets, startup, and migration workflow.

## Acceptance criteria

1. A developer with the documented prerequisites can start the complete stack using the documented Aspire command and reach the UI endpoint.
2. Aspire starts the database, waits for it before starting the API, and starts the UI after the API is available.
3. The UI loads a month from the API and displays the persisted category groups and categories.
4. Editing an assigned amount, moving money, adding or renaming a category/group, or editing the month note saves a complete valid snapshot; the UI communicates loading, saving, saved, and failure states.
5. Refreshing the browser and restarting Aspire preserves saved month data in PostgreSQL.
6. Requesting an uninitialized month returns a deterministic sample plan, and repeated requests do not create duplicate month records.
7. `/health` returns an unhealthy response when database connectivity is unavailable and a healthy response when it is available.
8. The schema can be created from the checked-in migration on a clean local database.
9. The application documents that sample initialization, independently stored months, and snapshot replacement are prototype limitations; it does not present them as ledger-derived budgeting behavior.
