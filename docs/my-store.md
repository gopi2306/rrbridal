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

## Example

```bash
curl "http://localhost:3000/api/my-store?storeId=store-001"
```
