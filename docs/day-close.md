# Day Close Module

Operational day open/close for store billing tills (WPF POS + local MongoDB), with central sync and dashboard API.

## Workflow

1. **Open day** — Enter opening cash float (`store_day_sessions`, per `storeId` + `businessDate` + `posCounter`).
2. **During the day** — Bills, returns, expenses, and cash movements post normally (blocked if day not open or already closed).
3. **Summary** — System aggregates sales, payment modes, expenses, deposits, and withdrawals.
4. **Cash hand over** — Count physical cash by denomination (₹500…₹1); print thermal slip (F3).
5. **Close day** — Persist expected vs actual cash, difference, and embedded snapshot; lock the till.

## Collections (local MongoDB)

| Collection | Purpose |
|------------|---------|
| `store_day_sessions` | Open/closed session per counter per business date |
| `store_cash_movements` | Bank deposits and cash withdrawals |
| `store_daily_expenses` | GST expense vouchers with one or more payment legs |

## Expected cash formula

```
expectedCash = openingCash + netCashInHand - depositsToBank - cashWithdrawals
```

`netCashInHand` deducts cash refunds and only the **Cash payment legs** of posted expenses. Card, UPI, and Bank Transfer expense legs remain in gross expense reporting but do not reduce the physical drawer. Voided expenses are excluded. Legacy expenses without `payments[]` are treated as fully Cash.

## UI

- **Day Close** nav tab — visible on every till.
- **POS 1 (manager till)** — Day Close summary cards, full report export, and **cash hand over / close day** use **all counters** (store-wide Expected Cash and Morning Cash). Status shows “Store totals (all counters)”; hand-over Counter label is “All counters”. Open day and cash movements remain **this till only**.
- **Other tills** — Day Close summary and close stay scoped to that till’s `posCounter`.
- **Dashboard → Day close** — manager rollup of all counters (POS 1).
- Header chip **Day: Open / Closed / Not opened** — links to Day Close page.

## Central API

`GET /api/dashboard/store/day-close?storeId=store-001&date=2026-06-03&posCounter=`

Query parameters:

| Param | Required | Description |
|-------|----------|-------------|
| `storeId` | no | Store code; defaults to first active store |
| `date` | no | Business day `YYYY-MM-DD`; defaults to today |
| `businessDate` | no | Alias of `date` |
| `posCounter` | no | Single counter; omit for all counters |

Returns counter session rows and store totals from synced `store_day_closes`. Response includes `storeId`, `date`, and `businessDate` (same value).

### Full report export (CSV / Excel)

**WPF POS**

- **Dashboard → Day close** — **Download full report** (respects business date and counter filter).
- **Day Close** page — **Download full report** (POS 1 = all counters; other tills = current counter).

Save dialog supports `*.csv` or `*.xlsx`. Default filename: `day-close-{storeId}-{yyyy-MM-dd}[-pos{n}|-all].csv|xlsx`.

For **All counters** Excel/CSV exports: **SUMMARY_OVERALL** first, then **SUMMARY_POS{n}** for each counter, then detail sheets (COUNTER_ROLLUP, BILLS, …). Single-counter exports keep a single **SUMMARY** sheet.

**Central API**

`GET /api/dashboard/store/day-close/export?format=csv|xlsx&storeId=store-001&date=2026-06-03&posCounter=`

Same filters as the dashboard endpoint (`storeId`, `date` / `businessDate`, `posCounter`).

```bash
curl -O -J "http://localhost:3000/api/dashboard/store/day-close?storeId=store-001&date=2026-06-03"
curl -O -J "http://localhost:3000/api/dashboard/store/day-close/export?format=csv&storeId=store-001&date=2026-06-03"
```

**Report sections:** METADATA, SUMMARY (or SUMMARY_OVERALL + SUMMARY_POS{n} for all-counters), COUNTER_ROLLUP, BILLS, RETURNS, ADJUSTMENTS, EXPENSES, CASH_MOVEMENTS, CREDIT_NOTE_CASHOUTS (if any), DENOMINATIONS (if closed), STOCK_EXCEPTIONS (if any).

Bills include cash/card/UPI/credit-note amounts and credit note number(s). Returns include credit note numbers when issued. Expenses include supplier/invoice details, taxable value, CGST/SGST/IGST, gross amount, payment summary, and cash outflow.

## Business date note

Sessions use explicit `businessDate` (`YYYY-MM-DD`). Bill/return day-close **reports** still filter by `createdAtUtc` local calendar day; near-midnight mismatches are possible and should be reviewed on the Day Close screen.

### Credit (pay-later) collections

Credit bill **invoice count / payable** stay on the bill post date. Cash / Card / UPI (and CN applied at collection) attribute to the **payment received date** (`creditBilling.payments[].receivedAtUtc`), not the bill date.

Example: credit bill on 25-Feb with collection on 27-Aug → tender appears in 27-Aug day close (and expected cash), not 25-Feb. Same-day advance at post stays on the bill day.
