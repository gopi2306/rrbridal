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
| `store_daily_expenses` | Petty cash expenses (existing) |

## Expected cash formula

```
expectedCash = openingCash + netCashInHand - depositsToBank - cashWithdrawals
```

`netCashInHand` already deducts daily expenses and cash refunds from bill/return activity.

## UI

- **Day Close** nav tab — visible on every till.
- **Dashboard → Day close** — manager rollup of all counters (POS 1).
- Header chip **Day: Open / Closed / Not opened** — links to Day Close page.

## Central API

`GET /api/dashboard/store/day-close?storeId=&businessDate=YYYY-MM-DD&posCounter=`

Returns counter session rows and store totals from synced `store_day_closes`.

### Full report export (CSV / Excel)

**WPF POS**

- **Dashboard → Day close** — **Download full report** (respects business date and counter filter).
- **Day Close** page — **Download full report** (current counter only).

Save dialog supports `*.csv` or `*.xlsx`. Default filename: `day-close-{storeId}-{yyyy-MM-dd}[-pos{n}].csv|xlsx`.

**Central API**

`GET /api/dashboard/store/day-close/export?format=csv|xlsx&storeId=&businessDate=YYYY-MM-DD&posCounter=`

Returns a file download with the same section layout as WPF.

```bash
curl -O -J "http://localhost:3000/api/dashboard/store/day-close/export?format=csv&storeId=store-001&businessDate=2024-06-17"
```

**Report sections:** METADATA, SUMMARY (reconciliation), COUNTER_ROLLUP, BILLS, RETURNS, ADJUSTMENTS, EXPENSES, CASH_MOVEMENTS, CREDIT_NOTE_CASHOUTS (if any), DENOMINATIONS (if closed), STOCK_EXCEPTIONS (if any).

Bills include cash/card/UPI/credit-note amounts and credit note number(s). Returns include credit note numbers when issued.

## Business date note

Sessions use explicit `businessDate` (`YYYY-MM-DD`). Bill/return day-close **reports** still filter by `createdAtUtc` local calendar day; near-midnight mismatches are possible and should be reviewed on the Day Close screen.
