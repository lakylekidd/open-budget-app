# API reference

Base URL in local Aspire development: `http://127.0.0.1:5080`. The Vite UI proxies `/api` and `/health` to this endpoint.

## `GET /health`

Checks PostgreSQL connectivity.

Successful response:

```json
{ "status": "ok", "database": "connected" }
```

Returns HTTP 503 if the database is unavailable.

## `GET /api/budget/{month}`

Reads one month using the `YYYY-MM` route format. If no budget exists, the API inserts a sample budget for that month and returns it. This is a development bootstrap behavior; a real onboarding flow should replace it before production.

Example response (abbreviated):

```json
{
  "month": "2026-10",
  "note": "",
  "groups": [
    {
      "id": "home",
      "name": "Home",
      "sortOrder": 0,
      "categories": [
        {
          "id": "rent",
          "name": "Rent & mortgage",
          "assigned": 1450.00,
          "activity": -1450.00,
          "available": 0.00,
          "target": 1450.00,
          "icon": "⌂",
          "snoozed": false,
          "sortOrder": 0
        }
      ]
    }
  ]
}
```

## `PUT /api/budget/{month}`

Atomically replaces the full snapshot for the month. The route month must match `body.month`. Group and category ordering is controlled by `sortOrder`.

```json
{
  "month": "2026-10",
  "note": "",
  "groups": [
    {
      "id": "home",
      "name": "Home",
      "sortOrder": 0,
      "categories": [
        {
          "id": "rent",
          "name": "Rent & mortgage",
          "assigned": 1450.00,
          "activity": -1450.00,
          "available": 0.00,
          "target": 1450.00,
          "icon": "⌂",
          "snoozed": false,
          "sortOrder": 0
        }
      ]
    }
  ]
}
```

The endpoint bounds the number of groups/categories and validates identifiers, names, note length, and non-negative target amounts. It permits negative category assignments. It does not yet perform domain-level money validation or protect against concurrent writes.

## Error behavior

- HTTP 400 for an invalid month, mismatched route/body month, duplicate IDs, or invalid input.
- HTTP 503 from `/health` when PostgreSQL cannot be reached.
- Other persistence errors use ASP.NET Core's normal error handling. A production API should add structured problem details and a global exception policy.

## Future contract direction

Replace whole-document writes with resource-oriented endpoints for accounts, transactions, categories, assignments, targets, and imports. Use stable identifiers, concurrency versions, idempotency keys for imported transactions, and explicit authorization before multi-user access.
