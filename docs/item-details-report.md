# Item Details Report API

Central API endpoints for **item-wise** purchase (PO + GRN), **SOH** (stock on hand snapshot), and **sales** (bill lines + returns) across **all stores and warehouse**, from the beginning of history unless date filters are applied.

Base path: `/api/reports/item-details`

## Endpoints

### JSON report

```
GET /api/reports/item-details
```

Returns a JSON object with:

- `purchases.poLines` — one row per purchase order line
- `purchases.grnLines` — one row per posted goods receipt line
- `soh` — current stock on hand per SKU (warehouse + in-transit + store)
- `sales` — one row per bill line; returns appear with negative qty and `isReturn: true`
- `summary` — counts and totals

### Excel export

```
GET /api/reports/item-details/export
```

Downloads a multi-sheet `.xlsx` workbook:

| Sheet | Contents |
|-------|----------|
| PO_Lines | Purchase order lines |
| GRN_Lines | Posted GRN / receipt lines |
| SOH | Current stock snapshot |
| Sales | Bill and return lines |
| Summary | Filters, totals, truncation notes |

Each data sheet is capped at **10,000 rows**. Use filters to narrow large datasets.

## Query parameters

| Parameter | Description |
|-----------|-------------|
| `from` | Start date inclusive (`YYYY-MM-DD`). Omit for all history from start. |
| `to` | End date inclusive (`YYYY-MM-DD`). Omit for up to today. |
| `sku` | Exact SKU filter |
| `search` | SKU or product name contains |
| `storeId` | Narrow **sales** to one store; SOH shows that store’s qty column |
| `brandId` | Brand code or id |
| `supplierId` | Supplier id or code |
| `limit` | Rows per section in JSON response (default `1000`, max `10000`) |
| `offset` | Pagination offset per section (default `0`) |

## Examples

All history (first page):

```
GET /api/reports/item-details
```

Date range with Excel:

```
GET /api/reports/item-details/export?from=2026-01-01&to=2026-06-30
```

Single SKU across purchase, SOH, and sales:

```
GET /api/reports/item-details?sku=SKU-001&limit=5000
```

Store-scoped sales:

```
GET /api/reports/item-details?storeId=store-001
```

## Data sources

| Section | Collection | Notes |
|---------|------------|-------|
| PO lines | `purchaseorders` | All PO statuses; date from `poDate` or `createdAt` |
| GRN lines | `goodsreceipts` | Only `status: posted` |
| SOH | `inventoryledgerentries` + `products` | **Current snapshot** — not historical SOH as-of-date |
| Sales | `store_invoices` | Lines parsed from `payload.lines` |
| Returns | `store_sale_returns` | Negative qty lines with `isReturn: true` |

## Response columns (summary)

**PO lines:** PO No, PO Date, Status, Supplier, SKU, Product, Brand, Ordered qty, Cost, Net cost, Net amount

**GRN lines:** Receipt No, GRN No, PO No, Receipt date, Supplier, SKU, Product, Ordered qty, Received qty, Outcome

**SOH:** SKU, Product, Brand, Category, Warehouse qty, In transit, Store qty, Total SOH, Sales qty, Remaining qty (Total SOH − Sales qty), Cost, MRP, Selling price, Store price

**Sales:** Store, Bill/return no, Invoice no, Bill date, Return flag, SKU, Product, Brand, Qty, Rate, Amount, Salesman, Payment summary

## Notes

- SOH reflects **ledger balances now**, not stock at a past date.
- Sales reduce local store stock on POS; central store ledger may differ until transfers/sync complete.
- Large exports may be truncated; check `summary.truncated` in JSON or the Summary sheet in Excel.
