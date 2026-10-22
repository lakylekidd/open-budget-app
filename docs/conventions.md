# Coding conventions

Extend established patterns before adding abstractions/dependencies. The API uses .NET 10, nullable references, async EF I/O and propagated cancellation tokens. Keep public contracts separate from entities. API handlers delegate business behavior to application services.

Use fixed-precision decimal money on the server. Validate dates, sizes, identifiers, ownership and precision before writing. Calculated balances/activity are server-owned. Use version checks, replay receipts and database transactions for related money changes. Never log secrets or financial request bodies.

The frontend uses React function components and strict TypeScript. Extend existing visual patterns. Ledger UI belongs in `src/OpenBudget.UI/src/ledger/`; reuse its typed client, accessible dialog and transaction form. Forms expose loading, validation, empty/error and recovery states, keyboard navigation and compact widths. Preserve uncertain commands until exact replay resolves the outcome.

Native checkbox inputs share `src/OpenBudget.UI/src/checkboxes.css`, imported by the global stylesheet: 13px rounded squares, grey outline when unchecked, purple fill and white tick when checked. Avoid screen-specific appearance/size overrides. Preserve native labels, keyboard interaction, focus indication and disabled state; forced-colors mode uses platform checkbox rendering.

Every feature includes domain unit tests, HTTP/persistence integration tests and UI workflow coverage appropriate to its behavior. Use xUnit, disposable real PostgreSQL, and Vitest/Testing Library. Use isolated synthetic data and verify failure atomicity/recovery. See [testing](testing.md).
