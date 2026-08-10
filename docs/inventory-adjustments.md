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

## Physical inventory Excel import

The physical-count workbook treats **Phy Qty as the final store on-hand quantity**. The service calculates:

`qtyDelta = Phy Qty - current ledger quantity`

Blank `Phy Qty` cells are ignored, allowing counters to fill only products that were physically counted. Rows whose physical quantity already equals book stock are reported as skipped.

### Download a store template

```http
GET /api/inventory-adjustments/import/excel/template?storeCode=store-001
```

Response: `physical-inventory-store-001.xlsx`, containing every active product:

| SKU | Item Name | Phy Qty |
|-----|-----------|---------|
| SKU-000235 | Bridal Wear | *(blank)* |

`SKU` is authoritative. `Item Name` is for operator verification and a mismatch is returned as a warning.

### Dry-run preview

```http
POST /api/inventory-adjustments/import/excel?storeCode=store-001&reason=Physical%20stock%20count&dryRun=true
Content-Type: multipart/form-data

file=<completed .xlsx>
```

Supported aliases include `Physical Qty`, `Physical Quantity`, and `New Qty`. Maximum file size is 10 MB and maximum data rows is 10,000.

The dry run validates the complete workbook without changing stock:

```json
{
  "dryRun": true,
  "readyToCommit": true,
  "totalRows": 2,
  "adjusted": 1,
  "skipped": 1,
  "failed": 0,
  "errors": [],
  "lines": [
    {
      "row": 2,
      "sku": "SKU-000235",
      "itemName": "Bridal Wear",
      "qtyBefore": 12,
      "newQty": 10,
      "qtyDelta": -2,
      "qtyAfter": 10,
      "unchanged": false
    }
  ]
}
```

Commit must be blocked when `readyToCommit` is false. Hard errors include missing/duplicate/unknown SKU, negative/non-numeric physical quantity, inactive store, and malformed headers.

### Commit

Repeat the same upload with `dryRun=false` and a stable client-generated batch ID:

```http
POST /api/inventory-adjustments/import/excel?storeCode=store-001&reason=Physical%20stock%20count&dryRun=false&batchId=20260806-store001-count1
```

The whole validated file is posted as one multi-line inventory adjustment. `batchId` is required and idempotent per store: retrying the same batch after a timeout returns the existing adjustment without posting ledger entries again.

Successful responses also include `adjustmentId` and `adjustmentNo`.

### Browser integration for the external central admin

```javascript
// Template
const template = await fetch(
  `/api/inventory-adjustments/import/excel/template?storeCode=${encodeURIComponent(storeCode)}`
);
const blob = await template.blob();

// Preview or commit
const form = new FormData();
form.append("file", file);
const query = new URLSearchParams({
  storeCode,
  reason,
  dryRun: String(dryRun),
  ...(dryRun ? {} : { batchId }),
});
const result = await fetch(`/api/inventory-adjustments/import/excel?${query}`, {
  method: "POST",
  body: form,
}).then((response) => response.json());
```

The WPF Dashboard exposes the same workflow through **Download count template** and **Import physical counts**. Online mode posts centrally without changing local Mongo; Offline mode sets local cached quantities and queues one multi-line `InventoryAdjustmentCreated` event.

## Ledger

Each line creates one ledger entry:

- `sourceType`: `InventoryAdjustmentPosted`
- `sourceId`: inventory adjustment Mongo `_id`
- `locationKind`: `store` or `warehouse`
- `storeId` / `locationCode` as appropriate
- `qtyDelta`: signed quantity change
