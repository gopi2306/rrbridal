# Purchase order Excel import

Upload an `.xlsx` with **sku** + **quantity** to create one purchase order for all rows. Costs / GST / MRP / selling are filled from product master via PO refresh (not from the spreadsheet).

## Template

| Method | Path |
|--------|------|
| `GET` | `/api/purchase-orders/import/excel/template` |

Sheet name: `PurchaseOrder`. Columns: `sku`, `quantity` (alias `qty` accepted on upload).

## Endpoints

| Method | Path | Behavior |
|--------|------|----------|
| `POST` | `/api/purchase-orders/import/excel` | Create one PO from all rows; refresh lines from product master. **Does not** move stock. |
| `POST` | `/api/purchase-orders/import/excel/receive` | Create PO → goods receipt → post to warehouse inventory → set PO status `received`. |

### Multipart fields

| Field | Required | Notes |
|-------|----------|--------|
| `file` | yes | `.xlsx` / `.xls`, max 10MB |
| `supplierId` | yes | Supplier Mongo ObjectId |
| `supplierName` | no | Snapshot name; defaults from supplier master when omitted |
| `mainLocationId` | no | Passed through to PO header |
| `branchId` | no | Passed through to PO header |
| `mainDivisionId` | no | Passed through to PO header |

### Query

| Param | Notes |
|-------|--------|
| `dryRun=true` \| `1` | Validate only; no PO / GR / stock writes |

### Excel rules

- Header row required; case-insensitive headers; `quantity` or `qty`.
- Duplicate SKUs in the file are **summed** into one PO line.
- Quantity must be a number **> 0**.
- One upload = **one** purchase order.

### Product / SKU validation

| Endpoint | Missing product |
|----------|-----------------|
| `/excel` (PO only) | Line kept on PO; listed in `warnings` |
| `/excel/receive` | Row error; **no** PO/GR created if any SKU is missing |

Invalid rows, missing `supplierId`, or unknown supplier also return `errors` and skip writes.

## Response

```json
{
  "dryRun": false,
  "totalRows": 8,
  "lineCount": 8,
  "poId": "...",
  "poNo": "PO-...",
  "grId": "...",
  "receiptNo": "...",
  "posted": true,
  "warnings": ["SKU BSH-9999: product not found"],
  "errors": [{ "row": 3, "sku": "...", "message": "..." }]
}
```

`grId`, `receiptNo`, and `posted` are set only on the receive endpoint after a successful post.

## Related

- [purchase-orders.md](./purchase-orders.md) — PO CRUD and refresh
- [inventory.md](./inventory.md) — stock after goods receipt post
