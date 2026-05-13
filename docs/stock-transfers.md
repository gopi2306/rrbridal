# Stock transfers (warehouse → store)

Central API support for **stock transfer** documents, aligned with a warehouse-to-store flow (e.g. challan / dispatch / store intake). This repo does not include the admin web UI; an external front end should call these endpoints.

## Relation to purchase intents

1. A **store** submits a **purchase intent** (requisition), synced to central as `purchase_intents` (see [sync-protocol.md](./sync-protocol.md)).
2. The **warehouse** manually creates a transfer from that intent when ready:

`POST /stock-transfers/from-purchase-intent/:intentId`

- Copies `toStoreId` from the intent and builds **lines** from `intent.lines` (`qty` defaults to each line’s `requestedQty`).
- Optional body: `{ "lineOverrides": [ { "sku": "...", "qty": 5 } ] }` — only SKUs that exist on the intent are allowed; omitted SKUs keep `requestedQty`.
- Rejected if the intent is `rejected`, `cancelled`, or `fulfilled`, or if the intent has no lines.
- Multiple transfers per intent are allowed (partial fulfillments). List them with `GET /stock-transfers?purchaseIntentId=<intentMongoId>`.

## Data model (collection `stock_transfers`)

| Field | Description |
|--------|-------------|
| `transferNo` | Human-readable id, e.g. `TR-1234` |
| `fromKind` | Currently always `warehouse` |
| `toStoreId` | Destination store id (string) |
| `purchaseIntentId` | Set when created from an intent |
| `status` | See lifecycle below |
| `transferDate` | Optional ISO date string |
| `remarks` | Optional |
| `lines[]` | `sku`, optional `description`, `qty` |

## Status lifecycle

Valid transitions (enforced on `POST /stock-transfers/:id/status`):

| Current | May move to |
|---------|-------------|
| `draft` | `in_transit`, `cancelled` |
| `in_transit` | `awaiting_intake`, `cancelled` |
| `awaiting_intake` | `completed` |
| `completed` | _(terminal)_ |
| `cancelled` | _(terminal)_ |

Setting the **same** status as current is a no-op. Terminal states cannot be changed.

Typical flow:

1. `draft` — after `POST /stock-transfers` or `POST /stock-transfers/from-purchase-intent/...`; editable via `PATCH /stock-transfers/:id` (remarks, date, lines).
2. `in_transit` — dispatched from warehouse.
3. `awaiting_intake` — goods arrived; store confirmation pending.
4. `completed` — intake confirmed.

## HTTP API summary

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/stock-transfers` | Ad-hoc transfer (`toStoreId`, `lines`, optional `transferDate`, `remarks`) |
| `POST` | `/stock-transfers/from-purchase-intent/:intentId` | New **draft** transfer from a purchase intent |
| `GET` | `/stock-transfers` | List; query: `toStoreId`, `status`, `purchaseIntentId`, `search` (transferNo) |
| `GET` | `/stock-transfers/:id` | Detail |
| `PATCH` | `/stock-transfers/:id` | Update **draft** only |
| `POST` | `/stock-transfers/:id/status` | Body `{ "status": "..." }` — validated transition |

## Inventory

Stock transfers **post to the inventory ledger** (see [inventory.md](./inventory.md)):

- **Dispatch** (`draft` → `in_transit`): warehouse decreases by line quantities.
- **Complete intake** (`awaiting_intake` → `completed`): destination store increases by line quantities (`toStoreId`).
- **Cancel** from `in_transit` or `awaiting_intake`: warehouse is increased again to reverse dispatch.

Goods receipts posted to inventory increase **warehouse** stock only.
