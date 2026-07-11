# Sync protocol (Store ↔ Central)

This document defines the offline-first sync protocol used by the **store client** (with local MongoDB) to synchronize with the **Central NestJS API** (MongoDB).

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

## Store sync run order (client run)

Each **Run sync once** performs **pull then push** (then best-effort masters, store users, and salesmen):

1. **Pull** — applies `StockTransferAwaitingStoreIntake` (local `stockQty` + pending `StockTransferReceived` outbox), product deltas, completed-transfer deltas, etc.
2. **Push** — sends pending outbox events including `StockTransferReceived`, so central can move the transfer from `awaiting_intake` to **`completed`** in the **same** run.
3. **Best-effort pull** — `GET /api/store-users` and `GET /api/salesmen?storeId=` refresh local `store_users` and `store_salesmen` caches (not outbox events).

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

Raised by the store client after it receives a central stock transfer into the local MongoDB inventory cache.

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

- The event `storeId` must match the transfer’s store: `toStoreId` for **transfer in**, `fromStoreId` for **transfer out**.
- Lines must match the central transfer lines by SKU and quantity.
- Central treats a transfer that is already `completed` as applied and does not post inventory ledger entries again.
- For a transfer in `awaiting_intake`, central moves it to `completed`:
  - **In:** in_transit −, store + (central ledger).
  - **Out:** in_transit −, warehouse + (central ledger). The store client has already decremented local stock on pull.

### Store billing document numbers

Local format (per counter / device): `{yyyyMMdd}-{storeLast3}-{posCounter}-{seq:D4}` where `storeLast3` is derived from `storeId` (e.g. `store-001` → `001`). Returns prefix `RET-`, adjustments `ADJ-`.

Central stores the store’s `billNo` / `returnNo` / `adjustmentNo` as the business number and enforces **unique `(storeId, invoiceNo)`** (and the same pattern for returns/adjustments/credit notes).

### Event: `InvoiceCreated`

Raised when a **posted** store bill is saved locally (`store_bills`). Payments are embedded in the payload; standalone `PaymentRecorded` is **not** sent for normal checkout (avoids duplicates).

`payload` includes at minimum: `billNo`, `storeId`, `deviceId`, `posCounter`, `lines[]`, `payments[]`, tax totals, `payable`, `status: posted`, customer fields, optional `creditNoteNo` / `creditApplied`.

Central: collection `store_invoices`, idempotent by `sourceEventId` (`eventId`). Duplicate `billNo` for the same store → `rejected`.

**Inventory:** Central also posts `StoreInvoicePosted` ledger rows at the store (`locationKind: store`, negative `qtyDelta` per sold line). Lines in `stockExceptions` with `stockDecremented: false` are skipped. Idempotent by `eventId` — duplicate sync does not double-post stock.

### Event: `SaleReturnCreated` / `SaleExchangeCreated`

Raised after a sale return or exchange is posted locally. `payload` includes `returnNo`, `originalBillNo`, lines, totals, `returnMode`, optional `creditNoteNo`.

Central: `store_sale_returns` with unique `(storeId, returnNo)`.

**Inventory:** `SaleReturnCreated` posts `StoreSaleReturnPosted` (positive qty at store). `SaleExchangeCreated` also posts `StoreSaleExchangePosted` (negative qty for replacement lines). Idempotent by `eventId`.

### Event: `AdjustmentBillCreated`

Raised after an adjustment bill is posted. `payload` includes `adjustmentNo`, `originalBillNo`, `lines`, `originalPayable`, `adjustedPayable`, `diffPayable`.

Central: `store_adjustments` with unique `(storeId, adjustmentNo)`.

### Event: `InventoryAdjustmentCreated`

Raised when store staff posts a **manual inventory adjustment** from the WPF dashboard (per-SKU stock correction). Not related to `AdjustmentBillCreated` (financial bill adjustment).

`payload` shape:

```json
{
  "adjustmentNo": "IADJ-20260711-0001",
  "locationKind": "store",
  "reason": "Cycle count correction",
  "lines": [
    { "sku": "SKU-000235", "qtyDelta": 3, "note": "Found extra unit" }
  ]
}
```

Rules:

- `locationKind` must be `store` for WPF events.
- `lines[]` required; each line needs non-empty `sku` and non-zero `qtyDelta`.
- Central creates `inventory_adjustments` with `source: wpf_sync`, posts `InventoryAdjustmentPosted` ledger rows, idempotent by `eventId` (`sourceEventId`).
- Duplicate `eventId` → `duplicate`.

### Event: `DailyExpenseCreated`

Raised when a **daily cash expense slip** is posted locally (`store_daily_expenses`). Simple fields: `expenseNo`, `storeId`, `deviceId`, `posCounter`, `businessDate` (`YYYY-MM-DD` IST calendar date), `description`, `amount` (cash, > 0), `status: posted`, `createdAtUtc`.

Central: `store_daily_expenses`, idempotent by `sourceEventId` (`eventId`). Duplicate `expenseNo` for the same store → `rejected`.

### Event: `DaySessionOpened`

Raised when a counter **opens** the business day (`store_day_sessions`, `status: open`). Payload includes `sessionId`, `storeId`, `posCounter`, `deviceId`, `businessDate`, `openingCash`, `openedBy`, `openedAtUtc`.

Central: `store_day_closes` (session records), idempotent by `sourceEventId`. Duplicate `(storeId, businessDate, posCounter)` on open is ignored if already present.

### Event: `DaySessionClosed`

Raised when a counter **closes** the day after cash hand-over. Payload includes denomination breakdown (`cashDenominations[]`), `expectedCash`, `actualCashCounted`, `cashDifference`, `closeSnapshot`, `closedBy`, `closedAtUtc`.

Central: `store_day_closes`, idempotent by `sourceEventId`. Duplicate close for same `(storeId, businessDate, posCounter)` → `rejected`.

### Event: `CashMovementCreated`

Raised for **bank deposits** (`deposit_to_bank`) or **cash withdrawals** (`cash_withdrawal`) posted to `store_cash_movements`.

Central: `store_cash_movements`, idempotent by `sourceEventId`. Duplicate `movementNo` per store → `rejected`.

### Event: `CreditNoteCreated`

Raised when a customer credit note is created from a return (local `customer_credit_notes`). `payload` includes `creditNoteNo`, `returnNo`, `originalBillNo`, `amount`, `remainingAmount`, customer phone/code, `storeId`.

Central: `store_credit_notes`, unique `(storeId, creditNoteNo)`. Duplicate create → treated as already applied.

### Event: `CreditNoteApplied`

Raised when credit is applied on a bill (`ConsumeAsync`). `payload`: `creditNoteNo`, `billNo`, `amountApplied`, `remainingAmount`, `status` (`available` | `consumed`).

Central: idempotent by `sourceEventId` on each application; updates `remainingAmount` and application history.

### Event: `InvoiceCodPaymentReceived`

Raised when staff records **COD payment received** on the Online Sales screen (local `store_bills` updated with `onlineCod.status: received`, `payments[]`, and transaction reference).

`payload` includes: `billNo`, `storeId`, `salesChannel: online`, `onlineCod` (status, amount, `transactionNo`, `receivedAtUtc`, `receivedBy`, `receivedPaymentMode`), `payments[]`, `paymentMode`.

Central: finds existing `store_invoices` by `(storeId, billNo)` and merges payment fields into `payload`. Idempotent by `sourceEventId` (`eventId`). See [online-cod-orders.md](./online-cod-orders.md).

## Central → Store (pull)
Endpoint: `GET /api/sync/pull?storeId=...&sinceCursor=...&sinceTransferCursor=...&sincePromotionCursor=...&sinceAdjustmentCursor=...&limit=...`

Parameters:
- `storeId`: required
- `sinceCursor`: optional, pass `0` for first sync (product `_id` cursor)
- `sinceTransferCursor`: optional, pass `0` for bootstrap window (see below)
- `sincePromotionCursor`: optional, pass `0` for first promotion sync
- `sinceAdjustmentCursor`: optional, pass `0` for bootstrap window (store inventory adjustments, last 90 days)
- `limit`: optional (server clamps to max)

Response:
- `cursor`: new product cursor after returned product deltas
- `transferCursor`: new transfer cursor after returned completed-transfer batch
- `promotionCursor`: new promotion scheme cursor
- `adjustmentCursor`: new inventory adjustment cursor
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

Server builds `updates` as: **all `ProductUpserted`**, then **all `StockTransferAwaitingStoreIntake`**, then **all `StockTransferCompleted`**, then **all `StoreInventoryAdjusted`**, then promotion scheme updates. The store applies them in that order in one pass.

### Local `local_products_cache` consistency (SKU vs `centralProductId`)

Transfers increment stock by **SKU**. Product sync upserts by **`centralProductId`**. If a transfer created a SKU-only stub before the canonical product row existed, the store may have held quantity on a duplicate row. On each `ProductUpserted`, the store client **merges** stock from other `local_products_cache` documents with the same `sku` that are not the canonical `centralProductId` into the upserted row and **deletes** those duplicate documents. Transfer intake also **collapses** multiple rows with the same SKU into the row that has `centralProductId` when present.

### Diagnostics (empty transfer lines)

If a stock transfer update has no parsable lines (`lines` missing, empty, or invalid `qty`), the store **does not** silently skip: it records a warning into `sync_state.lastError` (visible in Settings sync status) so operators can fix data or report the issue.

### Pull diagnostics summary (Settings)

After each successful pull, the store persists `sync_state.diagnosticsSummary` with: current `STORE_ID`, counts of each `updates[].type` in that response, result of `GET /api/stores/{STORE_ID}` when a bearer token is present (`OK`, `MISSING_fix_STORE_ID` if 404, `unknown_notLoggedIn` if 401), `localTransfersPending` (local `local_stock_transfers` where `stockApplied` is not true), and `localProductsInStock` (rows in `local_products_cache` with `stockQty` > 0). This is shown under **Sync diagnostics** in Settings and helps confirm whether `StockTransferAwaitingStoreIntake` payloads are arriving and whether `STORE_ID` matches central.

### Update: `StockTransferAwaitingStoreIntake`

Central includes transfers whose status is `awaiting_intake` and that belong to the pulling store:

- **Transfer in** (`direction`: `warehouse_to_store` or omitted): `toStoreId` matches `storeId`.
- **Transfer out** (`direction`: `store_to_warehouse`): `fromStoreId` matches `storeId`.

`payload.transfer` includes `direction`, `transferId`, `transferNo`, `lines`, optional `fromStoreId` / `toStoreId`, `stockClassification`, etc.

The store applies these idempotently:

- Save into local `local_stock_transfers` (field `direction` persisted).
- Adjust `local_products_cache.stockQty` per line:
  - **In:** increment (add stock).
  - **Out:** decrement (subtract stock, clamped at 0; pull warning if local qty was short).
- Create a pending `StockTransferReceived` outbox event so central can complete the transfer on the next push.

### Update: `StockTransferCompleted`

Payload shape matches `StockTransferAwaitingStoreIntake` (including `direction`).

Used when central is already **`completed`** (e.g. another till already pushed `StockTransferReceived`). The store:

- Skips if `local_stock_transfers` already has `stockApplied: true` for that `transferId` / `transferNo`.
- Otherwise applies the same stock adjustment as awaiting (**in** = increment, **out** = decrement), sets `stockApplied: true`, `intakeSource: central_completed_pull`.
- **Does not** enqueue `StockTransferReceived` (no second receipt).

### Update: `StoreInventoryAdjusted`

Delivered for **store** inventory adjustments posted on central (`inventory_adjustments`, both `central_admin` REST and `wpf_sync` from other devices).

`payload.adjustment` includes: `adjustmentId`, `adjustmentNo`, optional `sourceEventId`, `reason`, `lines[]` (`sku`, `qtyDelta`, optional `note`).

The store:

- Skips if `local_inventory_adjustments` already contains `sourceEventId` or `centralAdjustmentId`.
- Applies each line’s `qtyDelta` to `local_products_cache.stockQty` (decrement clamped at 0).
- Records intake in `local_inventory_adjustments` for idempotency.

Store persists `adjustmentCursor` in `sync_state` (ObjectId ordering on `inventory_adjustments._id`, bootstrap last 90 days when cursor is `0`).

See [inventory-adjustments.md](./inventory-adjustments.md).

### Sales stock rule

Store billing uses `local_products_cache.stockQty` as the sales availability gate. Products with quantity below one are not added to a bill; the store creates a `PurchaseIntentCreated` reference requisition instead.

## Promotion schemes

Pull query adds **`sincePromotionCursor`** / response **`promotionCursor`** (ObjectId ordering on `promotion_schemes._id`, same pattern as product cursor).

Local collection: **`local_promotion_schemes`** (keyed by central `schemeId` / `_id`).

| Update type | Action |
|-------------|--------|
| `PromotionSchemeUpserted` | Upsert `payload.scheme` when `isActive` and not soft-deleted |
| `PromotionSchemeDeleted` | Remove local row by `payload.schemeId` or deactivate |

Schemes with empty `storeIds` apply to all stores; otherwise `storeIds` must contain the pulling store code.

See [promotion-schemes.md](./promotion-schemes.md) for scheme shape and API examples.

## Receipt settings pull (store billing)

After each **Run sync once** (best-effort), the store client may refresh local thermal receipt configuration from central:

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

