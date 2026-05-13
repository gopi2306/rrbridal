# Centralized inventory (warehouse + store)

The central API maintains **location-aware** stock in the `inventoryledgerentries` collection (Mongoose model `InventoryLedgerEntry`). Each row is a quantity movement (`qtyDelta`) tagged with:

- **`locationKind`**: `warehouse` or `store` (rows without this field are treated as **warehouse** for backward compatibility).
- **`storeId`**: required for `store` rows; identifies the destination store (same id family as purchase intents / transfers).

## How quantities are updated

| Source | Effect |
|--------|--------|
| Goods receipt posted | Positive `qtyDelta` at **warehouse** (`GoodsReceiptPosted`) |
| Stock transfer → `in_transit` | Negative `qtyDelta` at **warehouse** (`StockTransferDispatched`) |
| Stock transfer → `completed` | Positive `qtyDelta` at **store** for `toStoreId` (`StockTransferReceived`) |
| Stock transfer → `cancelled` from `in_transit` or `awaiting_intake` | Positive `qtyDelta` at **warehouse** to reverse dispatch (`StockTransferCancelled`) |

While a transfer is between `in_transit` and `awaiting_intake`, stock is no longer counted at the warehouse but not yet counted at the store (in-transit gap), matching a simple two-location report.

## Warehouse + store grid API

`GET /inventory/grid`

Query parameters:

| Param | Description |
|-------|-------------|
| `search` | Optional; filters products by SKU, barcode, name (same rules as product list). |
| `storeId` | Optional; when set, **`storeQty`** is on-hand for that store only. When omitted, **`storeQty`** is the sum across all stores. |
| `limit` | Max rows (default 200, max 500). |

Response rows (one per product in the result set):

- `sku`, `barcode`, `product` (item name)
- `warehouseQty` — sum of ledger at warehouse for that SKU
- `storeQty` — per `storeId` filter or all stores
- `mrp`, `storePrice` (falls back to `sellingPrice` if `storePrice` unset)

Products with no ledger rows show zero quantities.
