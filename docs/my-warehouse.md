# My Warehouse API

Workspace endpoint for the TruStock **My Warehouse** screen. Returns profile, inventory KPIs, goods receipts, purchase orders, outbound transfers, and an inventory preview — all scoped to the selected warehouse where applicable.

## Endpoint

```
GET /api/my-warehouse?locationCode=loc-001
```

`locationCode` is **required**. Unknown or inactive warehouse codes return `404`.

### Optional query limits

| Parameter | Default | Max |
|-----------|---------|-----|
| `goodsReceiptLimit` | 10 | 50 |
| `purchaseOrderLimit` | 10 | 50 |
| `transferOutLimit` | 10 | 50 |
| `inventoryPreviewLimit` | 20 | 100 |

## Response shape

```json
{
  "warehouse": { "code", "name", "address", "phone", "type", "updatedAt" },
  "inventorySummary": { "warehouseQty", "inTransitQty", "stockValue" },
  "goodsReceipts": [],
  "purchaseOrders": [],
  "transfersOut": [],
  "inventoryPreview": []
}
```

### UI mapping

| UI section | API field |
|------------|-----------|
| Warehouse location card | `warehouse` |
| Inventory KPIs | `inventorySummary` |
| Goods receipts list | `goodsReceipts` (`receiptNo`, `mrcNumber`/`grnNumber`, `reference`, `summary`) |
| Purchase orders (inbound pipeline) | `purchaseOrders` |
| Stock transfers out table | `transfersOut` |
| Inventory grid preview | `inventoryPreview` (`costPrice`, `sellingPrice`) |

### Scoping

- **Inventory summary & preview**: Ledger rows filtered by `locationCode` on warehouse/in-transit entries. Legacy rows without `locationCode` count only for the default (first active) warehouse.
- **Transfers out**: `warehouse_to_store` transfers where `fromLocationId` matches the warehouse (with the same legacy-null rule on the default warehouse).
- **Goods receipts & purchase orders**: Global lists (receipts sorted by recency; POs in open pipeline statuses).

## Warehouse inventory grid (paginated)

```
GET /api/my-warehouse/inventory?locationCode=loc-001&page=1&limit=20
```

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `locationCode` | yes | — | Warehouse location filter |
| `search` | no | — | SKU, barcode, or product name |
| `page` | no | `1` | Page number |
| `limit` | no | `20` | Page size (max `100`) |

Returns SKUs with on-hand stock at the warehouse (scoped by `locationCode`), sorted by warehouse quantity (highest first).

```json
{
  "locationCode": "loc-001",
  "data": [
    {
      "sku": "BRD-LHG-001",
      "productName": "Zari Bridal Lehenga",
      "productSubtitle": "RR Style - Bridal Lehenga",
      "barcode": "8901001000012",
      "warehouseQty": 44,
      "inTransitQty": 8,
      "costPrice": 14500,
      "sellingPrice": 22999
    }
  ],
  "total": 120,
  "page": 1,
  "limit": 20,
  "totalPages": 6
}
```

## Warehouse inventory export

```
GET /api/my-warehouse/inventory/export?format=xlsx&locationCode=loc-001
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `format` | yes | `xlsx`, `csv`, or `pdf` |
| `locationCode` | yes | Warehouse location (same as inventory list) |
| `search` | no | Same search filter as inventory list |

Exports all matching rows (not paginated). Maximum **10,000** rows; returns `413` if exceeded.

Filename: `warehouse-inventory-{locationCode}-{date}.{ext}`

## Example

```bash
curl "http://localhost:3000/api/my-warehouse?locationCode=loc-001"
curl "http://localhost:3000/api/my-warehouse/inventory?locationCode=loc-001&page=1&limit=20"
curl -O -J "http://localhost:3000/api/my-warehouse/inventory/export?format=pdf&locationCode=loc-001"
```
