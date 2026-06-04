# My Store API

Workspace endpoint for the TruStock **My Store** screen. Returns store profile, inventory KPIs, purchase indents, inbound/outbound transfers, and an inventory grid preview.

## Endpoint

```
GET /api/my-store?storeId=store-001
```

`storeId` is optional; unknown or inactive store codes return `404`. When omitted, the first active store is used.

### Optional query limits

| Parameter | Default | Max |
|-----------|---------|-----|
| `purchaseIndentLimit` | 10 | 50 |
| `transferInLimit` | 10 | 50 |
| `transferOutLimit` | 10 | 50 |
| `inventoryPreviewLimit` | 20 | 100 |

## Response shape

```json
{
  "store": { "code", "name", "address", "phone", "status", "updatedAt" },
  "inventorySummary": { "warehouseQty", "storeQty", "inTransitQty", "retailValue" },
  "purchaseIndents": [],
  "transfersIn": [],
  "transfersOut": [],
  "inventoryPreview": []
}
```

### UI mapping

| UI section | API field |
|------------|-----------|
| Store profile card | `store` |
| Inventory summary KPIs | `inventorySummary` |
| Purchase indents list | `purchaseIndents` |
| Stock transfers in | `transfersIn` |
| Stock transfers out | `transfersOut` |
| Inventory grid preview | `inventoryPreview` |

### Scoping

- **Store profile, indents, transfers, preview**: Scoped to `storeId`.
- **Inventory summary `warehouseQty`**: Global central warehouse on-hand units (ledger `warehouse`).
- **Inventory summary `storeQty` / `retailValue`**: On-hand at the selected store.
- **Inventory summary `inTransitQty` / preview `inTransitQty`**: Pieces on open inbound transfers (`warehouse_to_store` in `draft`, `in_transit`, or `awaiting_intake`) to that store.

## Store inventory grid (paginated)

```
GET /api/my-store/inventory?storeCode=store-001&page=1&limit=20
```

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `storeCode` | yes | — | Store code filter |
| `search` | no | — | SKU, barcode, or product name |
| `page` | no | `1` | Page number |
| `limit` | no | `20` | Page size (max `100`) |

Returns SKUs with on-hand stock at the store, sorted by store quantity (highest first). `inTransitQty` is inbound transfer quantity for that store.

```json
{
  "storeCode": "store-001",
  "data": [
    {
      "sku": "BRD-LHG-001",
      "productName": "Zari Bridal Lehenga",
      "productSubtitle": "RR Style - Bridal Lehenga",
      "barcode": "8901001000012",
      "storeQty": 12,
      "inTransitQty": 8,
      "mrp": 24999,
      "storePrice": 21999
    }
  ],
  "total": 48,
  "page": 1,
  "limit": 20,
  "totalPages": 3
}
```

## Store inventory export

```
GET /api/my-store/inventory/export?format=xlsx&storeCode=store-001
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `format` | yes | `xlsx`, `csv`, or `pdf` |
| `storeCode` | yes | Store code (same as inventory list) |
| `search` | no | Same search filter as inventory list |

Exports all matching rows (not paginated). Maximum **10,000** rows; returns `413` if exceeded.

Filename: `store-inventory-{storeCode}-{date}.{ext}`

## Example

```bash
curl "http://localhost:3000/api/my-store?storeId=store-001"
curl "http://localhost:3000/api/my-store/inventory?storeCode=store-001&page=1&limit=20"
curl -O -J "http://localhost:3000/api/my-store/inventory/export?format=xlsx&storeCode=store-001"
```
