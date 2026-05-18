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
- **Cursor**: opaque position used by store to fetch **product** deltas from central (`sinceCursor` / `cursor`).
- **Transfer cursor**: separate opaque position for **completed** stock transfers (`sinceTransferCursor` / `transferCursor`) so secondary POS machines can apply store inventory after another device already pushed `StockTransferReceived`.

## Topology (single vs multiple billing PCs)

- **One local MongoDB per store (shared network path or single till)**  
  All devices see the same `local_products_cache` and `local_stock_transfers`; the original pull → push flow is enough.

- **Separate local Mongo per PC (typical: each till has its own MongoDB)**  
  Only the machine that pulls while central status is `awaiting_intake` used to get `StockTransferAwaitingStoreIntake`. After another device pushed `StockTransferReceived`, central moves to `completed` and other machines **never** saw that awaiting update. They now consume **`StockTransferCompleted`** pull updates (see below) using `transferCursor`, in addition to the awaiting-intake path.

## Store sync run order (WPF `RunOnceAsync`)

Each **Run sync once** performs **pull then push** (then best-effort masters and store users):

1. **Pull** — applies `StockTransferAwaitingStoreIntake` (local `stockQty` + pending `StockTransferReceived` outbox), product deltas, completed-transfer deltas, etc.
2. **Push** — sends pending outbox events including `StockTransferReceived`, so central can move the transfer from `awaiting_intake` to **`completed`** in the **same** run.

If push ran before pull, the receipt would not exist until the next sync.

## Store → Central (push)
Endpoint: `POST /api/sync/push`

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
  "remarks": "optional free text (document-level)",
  "lines": [
    {
      "sku": "SKU-123",
      "requestedQty": 2,
      "barcode": "optional",
      "description": "optional",
      "note": "optional line note",
      "stockClassification": "optional, e.g. Normal Stock",
      "toKind": "optional destination kind hint, e.g. warehouse",
      "toLocationId": "optional 24-char hex Mongo ObjectId of a Location",
      "remarks": "optional per-line remarks"
    }
  ]
}
```

Rules:

- `lines` is required and must contain at least one item.
- Each line must have non-empty `sku` and a positive numeric `requestedQty`.
- When present, `toLocationId` must be a valid 24-character hex Mongo ObjectId string.
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
Endpoint: `GET /api/sync/pull?storeId=...&sinceCursor=...&sinceTransferCursor=...&limit=...`

Parameters:
- `storeId`: required
- `sinceCursor`: optional, pass `0` for first sync (product `_id` cursor)
- `sinceTransferCursor`: optional, pass `0` for bootstrap window (see below)
- `limit`: optional (server clamps to max)

Response:
- `cursor`: new product cursor after returned product deltas
- `transferCursor`: new transfer cursor after returned completed-transfer batch
- `updates[]`: ordered list of updates (see ordering below)

### Product cursor

- Store persists `cursor` in `sync_state`.
- On next pull, store sends `sinceCursor=<last product cursor>`.

### Transfer cursor (completed transfers)

Central returns updates of type `StockTransferCompleted` for transfers with `status: completed` and `toStoreId` matching the store:

- **Bootstrap** (`sinceTransferCursor` is `0` or not a valid ObjectId): returns completed transfers whose `updatedAt` is within the **last 90 days**, oldest first, capped by `limit` (server clamps to 200). This lets a **new** till catch up on recently finished transfers without scanning the entire history.
- **Incremental** (valid ObjectId): returns completed transfers with `_id` greater than `sinceTransferCursor`, oldest first.

Store persists `transferCursor` in `sync_state` and sends it as `sinceTransferCursor` on the next pull.

### Pull update ordering

Server builds `updates` as: **all `ProductUpserted`**, then **all `StockTransferAwaitingStoreIntake`**, then **all `StockTransferCompleted`**. The store applies them in that order in one pass.

### Local `local_products_cache` consistency (SKU vs `centralProductId`)

Transfers increment stock by **SKU**. Product sync upserts by **`centralProductId`**. If a transfer created a SKU-only stub before the canonical product row existed, the store may have held quantity on a duplicate row. On each `ProductUpserted`, the WPF app **merges** stock from other `local_products_cache` documents with the same `sku` that are not the canonical `centralProductId` into the upserted row and **deletes** those duplicate documents. Transfer intake also **collapses** multiple rows with the same SKU into the row that has `centralProductId` when present.

### Diagnostics (empty transfer lines)

If a stock transfer update has no parsable lines (`lines` missing, empty, or invalid `qty`), the store **does not** silently skip: it records a warning into `sync_state.lastError` (visible in Settings sync status) so operators can fix data or report the issue.

### Pull diagnostics summary (WPF Settings)

After each successful pull, the store persists `sync_state.diagnosticsSummary` with: current `STORE_ID`, counts of each `updates[].type` in that response, result of `GET /api/stores/{STORE_ID}` when a bearer token is present (`OK`, `MISSING_fix_STORE_ID` if 404, `unknown_notLoggedIn` if 401), `localTransfersPending` (local `local_stock_transfers` where `stockApplied` is not true), and `localProductsInStock` (rows in `local_products_cache` with `stockQty` > 0). This is shown under **Sync diagnostics** in Settings and helps confirm whether `StockTransferAwaitingStoreIntake` payloads are arriving and whether `STORE_ID` matches central.

### Update: `StockTransferAwaitingStoreIntake`

Central includes store-bound transfers whose status is `awaiting_intake`. The store applies these idempotently:

- Save the transfer into local `local_stock_transfers` (with `intakeSource: awaiting_intake_pull` when finished).
- Increment `local_products_cache.stockQty` once per transfer line.
- Create a pending `StockTransferReceived` outbox event so central can complete the transfer on the next push.

### Update: `StockTransferCompleted`

Payload shape matches `StockTransferAwaitingStoreIntake` (`payload.transfer` with `transferId`, `transferNo`, `lines`, etc.).

Used when central is already **`completed`** (e.g. another till already pushed `StockTransferReceived`). The store:

- Skips if `local_stock_transfers` already has `stockApplied: true` for that `transferId` / `transferNo` (same idempotency as awaiting).
- Otherwise increments `local_products_cache.stockQty` per line, updates `local_stock_transfers` with `stockApplied: true` and `intakeSource: central_completed_pull`.
- **Does not** enqueue `StockTransferReceived` (central must not receive a second receipt).

### Sales stock rule

WPF billing uses `local_products_cache.stockQty` as the sales availability gate. Products with quantity below one are not added to a bill; the store creates a `PurchaseIntentCreated` reference requisition instead.

## Receipt settings pull (store billing)

After each **Run sync once** (best-effort), the WPF app may refresh local thermal receipt configuration from central:

| Endpoint | Purpose |
|----------|---------|
| `GET /api/company-profile` | Company-wide receipt header: trade/legal name, address, GSTIN, phone, logo URL, FSSAI, website, terms, policy lines, up to 3 `receiptQrSlots`, `receiptBarcodeEnabled` (also supported via `extraFields` for older admin payloads) |
| `GET /api/stores/:code/receipt-settings` | Per-store printer defaults: `printerModel`, `billPrinterQueueName`, `receiptCharWidth` (e.g. 48 for 3-inch / 80mm), `alwaysUsePrintDialog`, `paperWidthMm` |

Local file: `%LocalAppData%/RRBridal/StoreBilling/receipt_config.json`. Central overwrites branding fields; printer queue is applied only when the hinted name matches an installed Windows queue on the POS.

Settings UI also exposes **Pull receipt settings from central** (same merge rules).

## Recommended indexes
Central:
- `sync_events.eventId` unique
- `purchase_intents.sourceEventId` unique (sparse, for sync-created intents)
- `sync_events.storeId + _id` for pull queries
Store:
- `outbox_events.eventId` unique
- `outbox_events.status + createdAt` for batching

