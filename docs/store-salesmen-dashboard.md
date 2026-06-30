# Store salesman-wise sales dashboard API

Aggregated **Salesmen** tab data for the store admin dashboard. Groups retail bills by `payload.salesmanId` / `payload.salesmanCode` / `payload.salesman`.

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/dashboard/store/sales/salesmen` | All salesmen table + recent bills in period |
| `GET` | `/api/dashboard/store/sales/salesman` | Single-salesman drill-down (`salesmanId` required) |

### Query parameters

| Param | Required | Default | Description |
|-------|----------|---------|-------------|
| `storeId` | No | First active store | Store code |
| `period` | No | `today` | `today` \| `week` \| `month` \| `year` \| `custom` |
| `from` | If `custom` | — | `YYYY-MM-DD` |
| `to` | If `custom` | — | `YYYY-MM-DD` |
| `year` | No | Current year | For `month` / `year` |
| `month` | No | Current month (1–12) | For `period=month` |
| `invoiceLimit` | No | `50` | Recent bills (1–200) |
| `salesmanId` | **Yes** on `/salesman` | — | Mongo `_id` from salesman master, or synthetic keys `code:SM001`, `name:Ravi`, or `__legacy__` |

## Data sources

| Collection | Usage |
|------------|--------|
| `store_invoices` | Bill header + lines in `payload` |

Date ranges use **IST business-day** boundaries (same as store sales dashboard).

## Response sections

### `summary`

| Field | Meaning |
|-------|---------|
| `salesmenCount` | Distinct salesman groups in period |
| `invoices` | Posted bills counted |
| `itemsSold` | Sum of line qty |
| `totalBillAmount` | Sum of bill payable |

### `salesmen[]`

| Field | Meaning |
|-------|---------|
| `salesmanId` | Central id or synthetic grouping key |
| `salesmanCode` | From bill payload when present |
| `salesmanName` | Display name |
| `invoices` | Bills for this salesman |
| `itemsSold` | Line qty total |
| `totalBillAmount` | Payable total |

Legacy bills without `salesmanCode` roll into `salesmanId: __legacy__` / name **Legacy bills**.

## Related APIs

- Salesman master: [`GET /api/salesmen`](salesmen-api.md)
- Bills list filter: [`GET /api/bills?salesmanCode=`](bills-api.md)
