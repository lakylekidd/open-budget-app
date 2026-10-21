# API reference

Local Aspire development exposes the UI at `http://127.0.0.1:5173` and the API at `http://127.0.0.1:5080`. Vite proxies `/api` and `/health` to the API.

## `GET /health`

Checks PostgreSQL connectivity. Success: `{ "status": "ok", "database": "connected" }`. Returns 503 when PostgreSQL is unavailable.

## `GET /api/budget`

Returns the default budget identity:

```json
{ "id": "default", "name": "My Budget", "currency": "EUR", "version": 1 }
```

## `PUT /api/budget`

Renames the default budget using optimistic concurrency. Request `{ "name": "Household", "version": 1 }`. Names are required and limited to 120 characters. A successful response includes the incremented version. A stale version returns 409 with `{ "error": "...", "current": { ... } }`.

## `GET /api/budget/{month}`

Reads or initializes a month using `YYYY-MM`. The first requested month receives a deterministic blank starter category structure. An unseen later month is created from the nearest prior month: category available balances become opening balances, and the prior Ready to Assign becomes opening Ready to Assign. The operation is idempotent under repeated or concurrent reads.

The response includes `month`, `note`, `version`, `openingReadyToAssign`, `fundsAdded`, calculated `readyToAssign`, and ordered groups/categories. Each category includes `assigned`, `activity`, calculated `available`, target and archive/display fields.

## `PUT /api/budget/{month}`

Saves the editable monthly snapshot. The route month must match `body.month`. The request accepts `note`, `version`, `fundsAdded`, groups, and category assignments/metadata. It does not accept `activity`, `available`, or opening balance fields; those remain server-owned. Amounts use decimal precision of two places. Existing groups/categories must be retained and archived rather than deleted. New categories must start with zero assigned. Ordering values and identifiers must be unique.

Successful writes increment the version and return authoritative calculated totals. A stale version returns 409 with the latest budget so the UI can reload or reapply. Invalid requests return 400. EF Core applies each snapshot update in one database save operation.

## Boundaries

There is one local default budget and no authentication or transaction ledger yet. `activity` is a legacy persisted baseline for compatibility; account transactions become authoritative in Milestone 2. See [data model](data-model.md) for the migration and calculation details.
