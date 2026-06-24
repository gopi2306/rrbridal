# Bills API

Read-only APIs for the TruStock **Sales / Bills** screen. Bill data is synced from store billing (WPF) via `InvoiceCreated` events into `store_invoices.payload`.

Base URL:

```
http://localhost:3000/api
```

Swagger: `http://localhost:3000/api/swagger` (tag: `bills`).

Related: [stock-tally.md](./stock-tally.md), [sync-protocol.md](./sync-protocol.md).

---

## List bills

```
GET /api/bills?storeCode=store-001&page=1&limit=20
```

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `storeCode` | no | first active store | Store code |
| `search` | no | — | Bill no, customer name/phone, SKU, barcode, or line description |
| `from` | no | 30 days ago (IST) | Start date `YYYY-MM-DD` |
| `to` | no | today (IST) | End date `YYYY-MM-DD` |
| `page` | no | `1` | Page number |
| `limit` | no | `20` | Page size (max `100`) |
| `status` | no | — | `completed`, `partially_returned`, `returned`, `cancelled` |
| `paymentMode` | no | — | `cash`, `card`, `upi`, `credit`, `mixed` |

When `status` or `paymentMode` is set, results are filtered in memory after the date-bounded query (same pattern as dashboard sales filters). Otherwise pagination is applied at the database level.

### Response

```json
{
  "storeCode": "store-001",
  "storeName": "RR Bridal - Main",
  "from": "2026-05-24",
  "to": "2026-06-23",
  "data": [
    {
      "billNo": "STR-2026-004821",
      "occurredAt": "2026-06-20T14:32:00.000Z",
      "storeCode": "store-001",
      "storeName": "RR Bridal - Main",
      "customerName": "Priya Sharma",
      "itemCount": 3,
      "netAmount": 42599,
      "paymentMode": "UPI",
      "status": "Completed",
      "statusKey": "completed",
      "paymentModeKey": "upi"
    }
  ],
  "total": 142,
  "page": 1,
  "limit": 20,
  "totalPages": 8
}
```

### TruStock Bills table mapping

| TruStock column | API field |
|-----------------|-----------|
| Bill No | `billNo` |
| Date / time | `occurredAt` |
| Store | `storeName` (`storeCode` for filtering) |
| Customer | `customerName` |
| Items | `itemCount` (sum of line quantities) |
| Amount | `netAmount` |
| Payment | `paymentMode` (`Cash`, `Card`, `UPI`, `Credit`, `Mixed`) |
| Status | `status` (`Completed`, `Partially Returned`, `Returned`, `Cancelled`) |

Filter keys for dropdowns: `statusKey`, `paymentModeKey`.

### Status logic

| `statusKey` | Rule |
|-------------|------|
| `cancelled` | Invoice `payload.status` is `void` or `cancelled` |
| `returned` | Sale return exists and returned qty covers all invoice lines |
| `partially_returned` | Sale return exists but not full |
| `completed` | Otherwise |

### Payment mode logic

Derived from `payload` payment totals (`cash`, `card`, `upi`, `creditNote`). More than one non-zero bucket → `Mixed`.

---

## Bill detail

```
GET /api/bills/:billNo?storeCode=store-001
```

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `storeCode` | no | first active store | Store that issued the bill |

Returns `404` when no invoice exists for `{ storeId, invoiceNo }`.

### Response (abbreviated)

```json
{
  "billNo": "STR-2026-004821",
  "billDate": "2026-06-20",
  "occurredAt": "2026-06-20T14:32:00.000Z",
  "storeCode": "store-001",
  "storeName": "RR Bridal - Main",
  "posCounter": "POS-1",
  "customerCode": "CUS-00042",
  "customerName": "Priya Sharma",
  "customerPhone": "9876543210",
  "salesman": "Ravi",
  "holdBills": false,
  "doorDelivery": false,
  "onlineCod": false,
  "stitching": true,
  "isInterState": false,
  "deliveryDate": "2026-06-25",
  "printInvoice": true,
  "status": "Completed",
  "statusKey": "completed",
  "paymentMode": "UPI",
  "paymentModeKey": "upi",
  "totals": {
    "subTotal": 45000,
    "itemDiscount": 500,
    "cashDiscount": 0,
    "schemeDiscount": 0,
    "roundOff": -1,
    "payable": 42599,
    "originalTaxTotal": 8100,
    "revisedSubTotal": 44500,
    "cgstTotal": 4050,
    "sgstTotal": 4050,
    "igstTotal": 0,
    "creditApplied": 0
  },
  "lines": [
    {
      "lineNo": 1,
      "sku": "SKU-000235",
      "description": "PP-11248 SEMI BRIDAL XL",
      "hsn": "6204",
      "qty": 1,
      "rate": 13799,
      "amount": 13799,
      "mrp": 27598,
      "taxPercent": 18,
      "taxAmount": 2484,
      "cgstPercent": 9,
      "sgstPercent": 9,
      "igstPercent": 0,
      "alterationAmount": 500,
      "discountAmount": 0,
      "cashDiscountAmount": 0,
      "schemeDiscountAmount": 0,
      "revisedAmount": 13799,
      "revisedTaxAmount": 2484
    }
  ],
  "payments": [
    {
      "provider": "UPI",
      "amount": 42599,
      "reference": "TXN123456",
      "status": "Success"
    }
  ],
  "linkedReturn": null,
  "linkedAdjustment": null
}
```

### Detail sections

| Section | Fields |
|---------|--------|
| Header | `billNo`, `billDate`, `occurredAt`, store/customer/POS, salesman, flags |
| Totals | `totals.*` — subtotal, discounts, round off, payable, GST splits |
| Lines | `lines[]` — SKU, description, HSN, qty, rate, tax, alteration, discounts |
| Payments | `payments[]` — provider, amount, reference, status |
| Linked docs | `linkedReturn` (`returnNo`, `mode`, `creditNoteNo`), `linkedAdjustment` (`adjustmentNo`) |

Linked documents are resolved from `store_sale_returns` and `store_adjustments` where `payload.originalBillNo` matches the bill.

---

## Examples

```bash
# List last 30 days (default)
curl "http://localhost:3000/api/bills?storeCode=store-001&page=1&limit=20"

# Search by customer
curl "http://localhost:3000/api/bills?storeCode=store-001&search=Priya"

# Date range and status filter
curl "http://localhost:3000/api/bills?storeCode=store-001&from=2026-06-01&to=2026-06-23&status=completed"

# Bill detail (eye icon on row)
curl "http://localhost:3000/api/bills/STR-2026-004821?storeCode=store-001"
```

---

## TruStock integration

The TruStock web app (`/bills`) should:

1. Load the grid from `GET /api/bills` with `storeCode`, `page`, `limit`, and optional `search` / date range.
2. Open the detail drawer from `GET /api/bills/:billNo?storeCode=...` when the user clicks the row eye icon.
3. Pass `status` / `paymentMode` query params when the user applies list filters.

Data originates from WPF sync; new bills appear after the store device posts `InvoiceCreated` to central-backend (see [sync-protocol.md](./sync-protocol.md)).
