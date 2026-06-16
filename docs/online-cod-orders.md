# Online COD orders (store billing)

Manual **Online COD** bills in the WPF store app: post without collecting payment, track **balance till** until delivery payment is recorded, then sync to central.

## WPF workflow

1. On **Billing**, check **Online COD order** and post the bill (F9). Payment dialog is skipped; bill is saved with `onlineCod.status: pending` and empty `payments[]`.
2. **Online Sales** nav (primary counter): view pending/received orders, **balance till** KPI, and **Record payment** with Cash / UPI / Card + transaction/reference number.
3. **Dashboard** shows **COD balance till** with link to Online Sales. **Day close** shows **Online COD pending (not in cash)** for the selected date.

Pending COD amounts are **not** included in till / expected cash until payment is recorded.

## Bill fields (local `store_bills` + synced `store_invoices.payload`)

| Field | Values |
|-------|--------|
| `salesChannel` | `store` (default) \| `online` |
| `onlineCod.status` | `pending` \| `received` |
| `onlineCod.amount` | Bill payable at post |
| `onlineCod.transactionNo` | Set when payment received |
| `onlineCod.receivedPaymentMode` | `Cash` \| `UPI` \| `Card` |
| `paymentMode` | `OnlineCod` at post; actual mode after receive |
| `payments` | `[]` at post; one leg after receive |

## Sync events

### `InvoiceCreated`

Existing event; payload includes `salesChannel` and `onlineCod` when posted as online COD.

### `InvoiceCodPaymentReceived`

Published when staff records COD payment on **Online Sales**. Central merges `onlineCod`, `payments`, and `paymentMode` into the existing invoice document (idempotent by `eventId`).

## Central API

`GET /api/dashboard/store/online-sales`

| Param | Default | Description |
|-------|---------|-------------|
| `storeId` | First active store | Store code |
| `period` | `today` | `today` \| `week` \| `month` \| `year` \| `custom` |
| `from` / `to` | — | `YYYY-MM-DD` when `period=custom` |
| `status` | `all` | `all` \| `pending` \| `received` |

Response: `summary` (`balanceTill`, `pendingCount`, `receivedCount`, …) and `items[]` (bill rows).

Example:

```bash
curl "http://localhost:3000/api/dashboard/store/online-sales?storeId=store-001&period=week&status=pending"
```

## Related

- [sync-protocol.md](./sync-protocol.md)
- [store-sales-dashboard.md](./store-sales-dashboard.md)
