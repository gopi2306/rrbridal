# Inventory Adjustments API

Manual per-SKU stock corrections for **store** or **warehouse** locations. Each adjustment posts signed `qtyDelta` rows to the central `inventory_ledger` with `sourceType: InventoryAdjustmentPosted`.

Stock audit/tally variance is informational only — use this API (or WPF sync) to correct book stock.

## Create adjustment

```
POST /api/inventory-adjustments
```

### Store example

```json
{
  "locationKind": "store",
  "storeCode": "store-001",
  "reason": "Damaged goods write-off",
  "lines": [
    { "sku": "SKU-000235", "qtyDelta": -2, "note": "Water damage" }
  ]
}
```

### Warehouse example

```json
{
  "locationKind": "warehouse",
  "locationCode": "wh-main",
  "reason": "Cycle count correction",
  "lines": [
    { "sku": "SKU-000235", "newQty": 10 }
  ]
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `locationKind` | yes | `store` or `warehouse` |
| `storeCode` | when store | Active store code |
| `locationCode` | when warehouse | Active warehouse location code |
| `reason` | yes | Header reason (max 500 chars) |
| `lines[]` | yes | At least one line |
| `lines[].sku` | yes | Product SKU |
| `lines[].qtyDelta` | one of | Signed change (+ increase, − decrease) |
| `lines[].newQty` | one of | Target on-hand; delta computed from ledger |
| `lines[].note` | no | Per-line note |

Rules:

- Each line must have non-zero resulting delta.
- Resulting on-hand cannot be negative (v1).
- SKU must exist in product master.
- Document number: `IA-000001` (configurable via document numbers admin).

### Response

```json
{
  "id": "...",
  "adjustmentNo": "IA-000001",
  "locationKind": "store",
  "storeId": "store-001",
  "source": "central_admin",
  "reason": "Damaged goods write-off",
  "status": "posted",
  "lines": [
    {
      "sku": "SKU-000235",
      "qtyBefore": 12,
      "qtyDelta": -2,
      "qtyAfter": 10,
      "note": "Water damage"
    }
  ],
  "createdAt": "2026-07-11T15:00:00.000Z"
}
```

## List adjustments

```
GET /api/inventory-adjustments?storeCode=store-001&page=1&limit=20&search=SKU-000235
```

| Parameter | Description |
|-----------|-------------|
| `storeCode` | Filter store adjustments |
| `locationCode` | Filter warehouse adjustments |
| `locationKind` | `store` or `warehouse` |
| `search` | Adjustment no, reason, or SKU |
| `page`, `limit` | Pagination (max limit 100) |

## Get by id

```
GET /api/inventory-adjustments/:id
```

## WPF sync

Store adjustments created on the **WPF billing app** are pushed as `InventoryAdjustmentCreated` sync events. Store adjustments from this REST API are pulled to other tills as `StoreInventoryAdjusted`.

See [sync-protocol.md](./sync-protocol.md).

## Ledger

Each line creates one ledger entry:

- `sourceType`: `InventoryAdjustmentPosted`
- `sourceId`: inventory adjustment Mongo `_id`
- `locationKind`: `store` or `warehouse`
- `storeId` / `locationCode` as appropriate
- `qtyDelta`: signed quantity change
