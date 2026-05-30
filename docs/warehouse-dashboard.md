# Warehouse dashboard API

Aggregated data for the central warehouse admin dashboard (`/dashboard/warehouse` UI).

## Endpoint

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/dashboard/warehouse` | KPIs, stock by category, activity feed, low stock, inbound POs |

Existing `GET /api/dashboard` is unchanged (procurement summary).

### Query parameters

| Param | Default | Description |
|-------|---------|-------------|
| `locationCode` | First active warehouse `Location` | Warehouse label in response (ledger is global today) |
| `lowStockLimit` | `10` | Max low-stock table rows (1–50) |
| `activityLimit` | `10` | Max merged activity items (1–50) |
| `inboundDays` | `7` | Include POs with `deliveryDate` from today through today + N days (1–90) |

## Response

```json
{
  "warehouse": {
    "code": "rrsn-main",
    "name": "RRSN - Main Warehouse",
    "subtitle": "Bridal warehouse stock, receipts, outbound transfers to boutiques, and alerts — RRSN - Main Warehouse."
  },
  "metrics": {
    "totalSkus": 428,
    "stockUnits": 4865,
    "stockValue": 78200000,
    "inTransitUnits": 142,
    "lowStockSkus": 14,
    "pendingActions": 5
  },
  "stockByCategory": [
    { "categoryId": "...", "categoryName": "Bridal lehengas & sets", "pieces": 1820, "percent": 37 }
  ],
  "recentActivity": [
    {
      "id": "...",
      "kind": "grn",
      "title": "GRN-2408 · Threads of Joy Tex",
      "description": "Received 96 pieces · Bridal zari sarees bulk roll",
      "occurredAt": "2026-05-26T05:38:00.000Z",
      "status": "COMPLETED"
    }
  ],
  "lowStock": [
    { "sku": "LEH-RD-8842", "productName": "Bridal red lehenga", "quantity": 3, "status": "critical" }
  ],
  "inboundPipeline": [
    {
      "poId": "...",
      "poNo": "PO-2026-BRD-118",
      "supplierName": "Kanchi Zari House",
      "totalPieces": 54,
      "expectedDate": "2026-05-24"
    }
  ]
}
```

Format `metrics.stockValue` as INR on the client (e.g. `₹7.82 Cr`).

## Metric definitions

| Field | Source |
|-------|--------|
| `totalSkus` | Distinct SKUs with warehouse on-hand &gt; 0 |
| `stockUnits` | Sum of warehouse ledger quantities |
| `stockValue` | Sum of `warehouseQty × costPrice` for active products |
| `inTransitUnits` | Sum of `in_transit` ledger quantities (outbound to stores) |
| `lowStockSkus` | Active products where `warehouseQty ≤ reorderLevel` (fallback `minStock`) |
| `pendingActions` | Draft GRNs + transfers in `draft`, `in_transit`, or `awaiting_intake` |

### Low stock status

- **critical**: `warehouseQty ≤ minStock` (or `≤ reorderLevel` if `minStock` unset)
- **low**: at or below `reorderLevel` but above critical threshold

### Activity `status` values

| Status | Typical source |
|--------|----------------|
| `COMPLETED` | Posted goods receipt |
| `PENDING` | Warehouse→store transfer in transit |
| `OPEN` | Open / approved / partially received PO |
| `ALERT` | Top low-stock SKUs (up to 3) |

### Inbound pipeline

Purchase orders with status `open`, `approved`, or `partially_received` and `deliveryDate` in the query window. `totalPieces` sums line `recdQty + freeQty` (minimum 1 per line when both missing).

## Swagger

Tag: `dashboard`
