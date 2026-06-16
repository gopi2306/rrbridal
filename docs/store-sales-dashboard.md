# Store sales dashboard API

Aggregated **Sale** tab data for the store admin dashboard (`/dashboard/store` → Sale). Inventory KPIs remain on [`GET /api/dashboard/store`](store-dashboard.md). Vendor-filtered sales: [`GET /api/dashboard/store/sales/vendor`](store-vendor-sales-dashboard.md).

## Endpoint

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/dashboard/store/sales` | Sales KPIs, datewise breakdown, payment mix, top products, returns, credit notes |

### Query parameters

| Param | Default | Description |
|-------|---------|-------------|
| `storeId` | First active store | Store code |
| `period` | `today` | `today` \| `week` \| `month` \| `year` \| `custom` |
| `from` | — | `YYYY-MM-DD`; required when `period=custom` |
| `to` | — | `YYYY-MM-DD`; required when `period=custom` |
| `year` | Current UTC year | For `month` / `year` |
| `month` | Current UTC month (1–12) | For `period=month` |
| `topProductLimit` | `5` | Top SKUs (1–20) |
| `returnDetailLimit` | `20` | Max return detail rows |
| `creditNoteLimit` | `20` | Max credit-note detail rows |

## Data sources

| Collection | Content |
|------------|---------|
| `store_invoices` | Synced bills; totals/lines in `payload` |
| `store_sale_returns` | Returns/exchanges; totals in `payload` |
| `store_credit_notes` | Structured credit notes with `applications[]` |
| `store_daily_expenses` | Daily cash expense slips from WPF sync |

See [sync-protocol.md](sync-protocol.md) for payload shapes from the store client sync.

## Response sections

### `summary` (KPI cards)

| Field | Meaning |
|-------|---------|
| `grossSales` | Sum of invoice gross (`payableBeforeCredit` or equivalent) |
| `netSales` | Sum invoice `payable` minus sum `returnTotal` |
| `invoices` | Bill count |
| `avgBasket` | `netSales / invoices` (rounded) |
| `itemsSold` | Gross invoice line qty (returns not subtracted) |
| `returnsCount` | Return/exchange documents |
| `returnValue` | Sum `returnTotal` |
| `discountsTotal` | Sum item + cash discounts on bills |
| `creditNotesIssued` | Credit notes created in period |
| `creditNotesIssuedAmount` | Sum CN `amount` |
| `creditAppliedOnBills` | Sum `creditApplied` on invoices |
| `creditRemainingOutstanding` | Sum CN `remainingAmount` (issued in period) |
| `dailyExpensesTotal` | Sum of daily cash expenses in period (`payload.businessDate`, IST) |
| `dailyExpensesCount` | Count of posted expense slips in period |
| `cashInHand` | Bill cash − `cashRefundForReturns` + exchange top-up cash − `dailyExpensesTotal` |
| `returnCashRefundTotal` | Cash refunded on sale returns (`returnMode: cash_refund`, uses `cashRefunded` when set) |
| `creditNoteCashoutTotal` | Cash paid out from credit note remaining balance |
| `cashRefundForReturns` | `returnCashRefundTotal` + `creditNoteCashoutTotal` |

### `salesDetails`

Datewise (or monthwise for `year` / long custom ranges) table: invoices, items, gross, net, returns.

### `paymentMix`

Share by payment mode (`UPI`, `Card`, `Cash`, `Credit`) from `payload.payments[]`.

### `topProducts`

Best sellers by units from invoice lines.

### `returns` (advanced)

Per return: `returnNo`, `kind`, `originalBillNo`, `returnMode`, `reason`, totals, `cashRefunded`, `creditBalance`, line count, customer, `occurredAt`.

### `creditNotes` (extra detail)

`summary` plus `items[]` with full application history (`billNo`, `amountApplied`, `appliedAt`).

## Examples

```bash
# Today
GET /api/dashboard/store/sales?storeId=store-001&period=today

# This month (May 2026)
GET /api/dashboard/store/sales?storeId=store-001&period=month&year=2026&month=5

# Custom range
GET /api/dashboard/store/sales?storeId=store-001&period=custom&from=2026-05-24&to=2026-05-30
```

Dates use **IST** for `businessDate` on expenses; bills/returns use `payload.createdAtUtc` (UTC ISO) with document `createdAt` as fallback.

## Swagger

Tag: `dashboard`
