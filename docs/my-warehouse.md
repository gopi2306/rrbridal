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

## Example

```bash
curl "http://localhost:3000/api/my-warehouse?locationCode=loc-001"
```
