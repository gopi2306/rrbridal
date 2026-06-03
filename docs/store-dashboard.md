# Store dashboard API

Aggregated data for the bridal boutique admin dashboard (`/dashboard/store` UI).

## Endpoint

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/dashboard/store` | KPIs, store network, category mix, activity, low stock, transfer schedule |

Existing `GET /api/dashboard` and `GET /api/dashboard/warehouse` are unchanged.

### Query parameters

| Param | Default | Description |
|-------|---------|-------------|
| `storeId` | First active store `code` | Store code string; scopes metrics, activity, and transfers; unknown codes return 404 |
| `lowStockLimit` | `10` | Max low-stock rows (1–50) |
| `activityLimit` | `10` | Max merged activity items (1–50) |
| `transferLimit` | `10` | Max inbound transfer schedule rows (1–20) |

## Response

```json
{
  "store": {
    "code": "store-001",
    "name": "RRSN - Falaknuma",
    "subtitle": "Floor and back-room stock, transfers, and replenishment — RRSN - Falaknuma."
  },
  "availableStores": [{ "code": "store-001", "name": "RRSN - Falaknuma" }],
  "metrics": {
    "totalSkus": 312,
    "onShelfUnits": 1894,
    "retailValue": 35900000,
    "inTransitUnits": 142,
    "lowStockSkus": 19,
    "openRequests": 4
  },
  "storeNetwork": [
    {
      "code": "store-001",
      "name": "RRSN - Falaknuma",
      "shelfFillPercent": 94,
      "totalSkus": 312,
      "units": 1894,
      "lowStockSkus": 19
    }
  ],
  "categoryMix": [
    { "categoryId": "...", "categoryName": "Bridal lehengas & sets", "pieces": 688, "percent": 36 }
  ],
  "recentActivity": [
    {
      "id": "...",
      "kind": "transfer",
      "title": "Transfer in STO-2412",
      "description": "Received 28 pieces from central bridal warehouse",
      "occurredAt": "2026-05-26T06:45:00.000Z",
      "status": "COMPLETED"
    }
  ],
  "lowStock": [
    { "sku": "LEH-RD-8842", "productName": "Bridal red lehenga", "quantity": 0, "status": "critical" }
  ],
  "transferSchedule": [
    {
      "transferId": "...",
      "transferNo": "STO-2418",
      "title": "STO-2418",
      "description": "RRSN - Falaknuma · 24 pcs",
      "expectedDate": "2026-05-27",
      "status": "in_transit"
    }
  ]
}
```

Format `metrics.retailValue` as INR on the client (e.g. `₹3.59 Cr`).

## Metric definitions

| Field | Source |
|-------|--------|
| `totalSkus` | Distinct SKUs with store ledger qty &gt; 0 |
| `onShelfUnits` | Sum of `locationKind: store` ledger qty for `storeId` |
| `retailValue` | Sum of `qty × (storePrice ?? sellingPrice)` for active products |
| `inTransitUnits` | Sum of line qty on `warehouse_to_store` transfers to this store in `draft`, `in_transit`, or `awaiting_intake` |
| `lowStockSkus` | SKUs at or below shelf threshold at the store |
| `openRequests` | Purchase intents with status `submitted`, `under_review`, or `approved` |

### Low stock threshold (min shelf)

Uses the first defined value: `minimumShelfFit` → `minStock` → `reorderLevel`.

- **critical**: `quantity <= minStock` (or `<= threshold` if `minStock` unset)
- **low**: at or below threshold but above critical

### Shelf fill % (store network)

`round((totalSkus - lowStockSkus) / totalSkus × 100)`, or `100` when `totalSkus` is 0.

### Activity status

| Status | Source |
|--------|--------|
| `COMPLETED` | Inbound transfer `completed` |
| `PENDING` | Inbound transfer `in_transit` or `awaiting_intake` |
| `OPEN` | Open purchase intent |
| `ALERT` | Top low-stock SKUs (up to 3) |

## Swagger

Tag: `dashboard`
