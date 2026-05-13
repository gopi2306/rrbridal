# Sync protocol (Store ↔ Central)

This document defines the offline-first sync protocol used by the **Store WPF** app (with local MongoDB) to synchronize with the **Central NestJS API** (MongoDB).

## Design goals
- **Offline-first**: store can create invoices/payments without internet.
- **At-least-once delivery**: store retries safely.
- **Idempotent processing**: central never duplicates records if an event is resent.
- **Auditable**: central keeps an immutable record of accepted events.

## Terms
- **Outbox**: local store collection that persists events to be sent to central.
- **Event**: immutable record describing a business action (invoice created, payment recorded, stock adjusted).
- **Cursor**: opaque position used by store to fetch deltas from central.

## Store → Central (push)
Endpoint: `POST /sync/push`

Payload shape (batch):
- `events[]` where each event contains:
  - `eventId`: UUID (unique)
  - `storeId`, `deviceId`
  - `type`: string (e.g. `InvoiceCreated`, `PaymentRecorded`, `PurchaseIntentCreated`)
  - `createdAt`: ISO timestamp string
  - `payload`: JSON object
  - `hash`: non-empty string (e.g. stable JSON of payload fields); central validates `hash` as required on each event

### Event: `PurchaseIntentCreated`

Raised when a **store** submits a **requisition** (purchase intent) to the central warehouse. Central creates a `purchase_intents` document with `sourceEventId` equal to `eventId` (idempotent per `eventId` / `sourceEventId`).

`payload` shape:

```json
{
  "remarks": "optional free text",
  "lines": [
    {
      "sku": "SKU-123",
      "requestedQty": 2,
      "barcode": "optional",
      "description": "optional",
      "note": "optional line note"
    }
  ]
}
```

Rules:

- `lines` is required and must contain at least one item.
- Each line must have non-empty `sku` and a positive numeric `requestedQty`.
- Optional string fields may be omitted entirely (do not send `null` unless you standardize on that).

Central processing rules:
- Central must enforce a **unique index** on `eventId`.
- If `eventId` already exists → return `duplicate` (do not apply again).
- If validation fails → return `rejected` with reason.
- Otherwise → persist event as `applied` and apply to domain tables/collections.

Response:
- `results[]` with `eventId` and `status` = `applied|duplicate|rejected`.

Store rules:
- Store only marks an outbox event as **synced** if central returns `applied` or `duplicate`.
- If `rejected`, store keeps it as **failed** and shows the reason.

### Event: `StockTransferReceived`

Raised by the **Store WPF** app after it receives a central stock transfer into the local MongoDB inventory cache.

`payload` shape:

```json
{
  "transferId": "central Mongo ObjectId",
  "transferNo": "TR-1234",
  "receivedAt": "2026-05-13T10:00:00.000Z",
  "lines": [
    {
      "sku": "SKU-123",
      "qty": 2
    }
  ]
}
```

Rules:

- The event `storeId` must match the transfer `toStoreId`.
- Lines must match the central transfer lines by SKU and quantity.
- Central treats a transfer that is already `completed` as applied and does not post inventory ledger entries again.
- For a transfer in `awaiting_intake`, central moves it to `completed`, which records `in_transit -qty` and `store +qty` through the existing stock transfer ledger flow.

## Central → Store (pull)
Endpoint: `GET /sync/pull?storeId=...&sinceCursor=...&limit=...`

Parameters:
- `storeId`: required
- `sinceCursor`: optional, pass `0` for first sync
- `limit`: optional (server clamps to max)

Response:
- `cursor`: new cursor after returned updates
- `updates[]`: ordered list of updates/deltas for the store to apply

Cursor rules:
- Store persists `cursor` in `sync_state`.
- On next pull, store sends `sinceCursor=<lastCursor>`.

### Update: `StockTransferAwaitingStoreIntake`

Central includes store-bound transfers whose status is `awaiting_intake`. The store applies these idempotently:

- Save the transfer into local `local_stock_transfers`.
- Increment `local_products_cache.stockQty` once per transfer line.
- Create a pending `StockTransferReceived` outbox event so central can complete the transfer on the next push.

### Sales stock rule

WPF billing uses `local_products_cache.stockQty` as the sales availability gate. Products with quantity below one are not added to a bill; the store creates a `PurchaseIntentCreated` reference requisition instead.

## Recommended indexes
Central:
- `sync_events.eventId` unique
- `purchase_intents.sourceEventId` unique (sparse, for sync-created intents)
- `sync_events.storeId + _id` for pull queries
Store:
- `outbox_events.eventId` unique
- `outbox_events.status + createdAt` for batching

