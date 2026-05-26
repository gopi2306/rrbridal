# Centralized inventory (warehouse + store)

The central API maintains **location-aware** stock in the `inventoryledgerentries` collection (Mongoose model `InventoryLedgerEntry`). Each row is a quantity movement (`qtyDelta`) tagged with:

- **`locationKind`**: `warehouse` or `store` (rows without this field are treated as **warehouse** for backward compatibility).
- **`storeId`**: required for `store` rows; identifies the destination store (same id family as purchase intents / transfers).

## How quantities are updated

| Source | Effect |
|--------|--------|
| Goods receipt posted | Positive `qtyDelta` at **warehouse** (`GoodsReceiptPosted`) |
| Transfer **in** → `in_transit` | Negative `qtyDelta` at **warehouse** (`StockTransferDispatched`) |
| Transfer **in** → `completed` | Negative in-transit; positive at **store** for `toStoreId` (`StockTransferReceived`) |
| Transfer **in** → `cancelled` | Reverse in-transit to **warehouse** (`StockTransferCancelled`) |
| Transfer **out** → `in_transit` | Negative `qtyDelta` at **store** for `fromStoreId`; positive in-transit |
| Transfer **out** → `completed` | Negative in-transit; positive at **warehouse** (`StockTransferReceived`) |
| Transfer **out** → `cancelled` | Reverse in-transit to **store** (`StockTransferCancelled`) |

While a transfer is `in_transit` / `awaiting_intake`, quantity sits in the **in_transit** bucket (not at the source site). See [stock-transfers.md](./stock-transfers.md).

## Warehouse + store grid API

`GET /inventory/grid`

Query parameters:

| Param | Description |
|-------|-------------|
| `search` | Optional; filters products by SKU, barcode, name (same rules as product list). |
| `storeId` | Optional; when set, **`storeQty`** is on-hand for that store only. When omitted, **`storeQty`** is the sum across all stores. |
| `limit` | Max rows (default 200, max 500). |

Response rows (one per product in the result set):

- `sku`, `productId`, `upcEanCode` (barcode)
- `product` — full product document with populated master refs (same shape as `GET /products` filter rows)
- `warehouseQty` — sum of ledger at warehouse for that SKU
- `inTransitQty` — quantity in the in-transit bucket
- `storeQty` — per `storeId` filter or all stores
- `mrp`, `storePrice` — convenience copies from `product` (`storePrice` falls back to `sellingPrice` when unset)

Products with no ledger rows show zero quantities.

## Document numbers

SKU and procurement document numbers (PO, intent, return, RCV, GRN) are auto-generated from configurable prefixes and sequences. See [document-numbers.md](./document-numbers.md).
