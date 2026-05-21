# Stock transfers (warehouse ↔ store)

Central API support for **bidirectional** stock transfers. Transfers are **created only on central** (admin / warehouse UI); stores confirm quantities and WPF sync applies local billing stock.

| Direction | `direction` value | Movement |
|-----------|-------------------|----------|
| **Transfer in** | `warehouse_to_store` (default) | Warehouse → store |
| **Transfer out** | `store_to_warehouse` | Store → warehouse |

This repo does not include the admin web UI; an external front end should call these endpoints.

## Relation to purchase intents

**Transfer in only.** A store purchase intent leads to a warehouse transfer:

`POST /stock-transfers/from-purchase-intent/:intentId`

- Copies `toStoreId` from the intent and builds **lines** from `intent.lines`.
- Optional `lineOverrides` per SKU.
- Rejected if the intent is `rejected`, `cancelled`, or `fulfilled`.

## Data model (collection `stock_transfers`)

| Field | Description |
|--------|-------------|
| `transferNo` | Human-readable id, e.g. `TR-1234` |
| `direction` | `warehouse_to_store` or `store_to_warehouse` (legacy rows without field = in) |
| `fromKind` | `warehouse` (in) or `store` (out) |
| `fromLocationId` | Source warehouse location (transfer in) |
| `fromStoreId` | Source store (transfer out) |
| `toStoreId` | Destination store (transfer in) |
| `toLocationId` | Destination warehouse location (transfer out, optional) |
| `purchaseIntentId` | Set when created from an intent (in only) |
| `status` | See lifecycle below |
| `transferDate`, `remarks`, `stockClassification` | Optional |
| `receivedAt`, `receivedBy` | Store confirmation metadata |
| `lines[]` | `sku`, optional `description`, `qty` |

## Status lifecycle

Same status machine for **in** and **out**:

| Status | Set by | Mechanism |
|--------|--------|-----------|
| `draft` | Admin | `POST /stock-transfers` or `PATCH` |
| `in_transit` | Admin | `POST /stock-transfers/:id/status` |
| `awaiting_intake` | Store | `POST /stock-transfers/:id/receive` (full line match) |
| `completed` | Store WPF sync | `StockTransferReceived` push |
| `cancelled` | Admin | `POST /:id/status` from `draft` or `in_transit` |

`in_transit` → `awaiting_intake` only via **receive**, not via status.

### Transfer in (warehouse → store)

1. Admin creates → `draft`.
2. Admin dispatch → `in_transit` (warehouse −, in_transit +).
3. Store receive → `awaiting_intake`.
4. WPF sync → `completed` (in_transit −, store +; local `stockQty` **increases**).

### Transfer out (store → warehouse)

1. Admin creates with `direction: store_to_warehouse`, `fromStoreId` → `draft`.
2. Admin dispatch → `in_transit` (store − on central ledger, in_transit +). Fails if central store stock insufficient.
3. Store confirms dispatch qty → `awaiting_intake` (`storeId` = `fromStoreId`).
4. WPF sync → `completed` (in_transit −, warehouse +; local `stockQty` **decreases**, clamped at 0).

```mermaid
sequenceDiagram
  participant Admin
  participant Central
  participant StoreApp
  participant WPF

  Note over Admin,WPF: Transfer out
  Admin->>Central: POST /stock-transfers store_to_warehouse
  Admin->>Central: POST /:id/status in_transit
  StoreApp->>Central: POST /:id/receive
  WPF->>Central: pull minus local stock
  WPF->>Central: push StockTransferReceived
  Note over Central: completed warehouse plus
```

## HTTP API summary

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/stock-transfers` | Create in or out (see examples) |
| `POST` | `/stock-transfers/from-purchase-intent/:intentId` | Draft **in** from purchase intent |
| `GET` | `/stock-transfers` | List; query: `direction`, `toStoreId`, `fromStoreId`, `status`, `purchaseIntentId`, `search` |
| `GET` | `/stock-transfers/:id` | Detail |
| `PATCH` | `/stock-transfers/:id` | Update **draft** only |
| `POST` | `/stock-transfers/:id/status` | Admin status change |
| `POST` | `/stock-transfers/:id/receive` | Store confirm qty → `awaiting_intake` |

### Example: transfer in create

```json
POST /stock-transfers
{
  "direction": "warehouse_to_store",
  "toStoreId": "store-01",
  "lines": [{ "sku": "SKU-001", "qty": 5 }]
}
```

### Example: transfer out create

```json
POST /stock-transfers
{
  "direction": "store_to_warehouse",
  "fromStoreId": "store-01",
  "lines": [{ "sku": "SKU-001", "qty": 3 }],
  "remarks": "Return to warehouse"
}
```

### Example: store receive

**Transfer in** — `storeId` must match `toStoreId`:

```http
GET /stock-transfers?direction=warehouse_to_store&toStoreId=store-01&status=in_transit
```

**Transfer out** — `storeId` must match `fromStoreId`:

```http
GET /stock-transfers?direction=store_to_warehouse&fromStoreId=store-01&status=in_transit
```

```json
POST /stock-transfers/:id/receive
{
  "storeId": "store-01",
  "receivedBy": "Ravi",
  "lines": [{ "sku": "SKU-001", "qty": 3 }]
}
```

Quantity mismatch returns **400** (`Receipt quantity mismatch for sku …`); status stays `in_transit`.

### WPF sync

See [sync-protocol.md](./sync-protocol.md). Pull payloads include `direction` and `fromStoreId` when applicable.

## Inventory

See [inventory.md](./inventory.md). Summary:

- **In:** dispatch reduces warehouse; complete increases store (central) and local WPF stock.
- **Out:** dispatch reduces store (central); complete increases warehouse; WPF **subtracts** local stock on pull.

## Store sales availability

WPF billing uses `local_products_cache.stockQty`. Transfer **in** adds; transfer **out** subtracts (never below zero). Low stock triggers `PurchaseIntentCreated` for replenishment (transfer in from warehouse).
