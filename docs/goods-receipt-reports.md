# Goods receipt vendor reports

Excel export of vendor-wise procurement metrics based on **goods receipts** (not purchase order dates). Pricing is joined from linked purchase order lines, with product-master fallback.

## Endpoint

```
GET /goods-receipts/reports/vendor-summary/export
```

Returns an `.xlsx` file with two sheets:

| Sheet | Contents |
|-------|----------|
| **Vendor wise** | One row per vendor |
| **Summary** | One row of grand totals |

### Query parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `receiptDateFrom` | No | — | Goods receipt date from (inclusive), ISO `YYYY-MM-DD`. Filters `createdAt`. |
| `receiptDateTo` | No | — | Goods receipt date to (inclusive end of day), ISO `YYYY-MM-DD`. |
| `supplierId` | No | — | Limit to a single vendor (`supplier.supplierId`). |
| `status` | No | `posted` | `posted` or `draft`. Default excludes drafts. |

### Example

```
GET /goods-receipts/reports/vendor-summary/export?receiptDateFrom=2026-01-01&receiptDateTo=2026-06-30
```

Response headers:

- `Content-Type`: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- `Content-Disposition`: attachment filename `vendor-receipt-report-{scope}-{date}.xlsx`

## Sheet 1 — Vendor wise

| Column | Meaning |
|--------|---------|
| Vendor | Supplier name from the goods receipt |
| Cost price (with tax) | Weighted average unit landed cost: total cost value ÷ qty |
| Selling price | Weighted average unit selling price: total selling value ÷ qty |
| Qty | Sum of `receivedQty` on valid lines |
| Total cost value | Σ (unit net cost × received qty) per line |
| Total S.P. value | Σ (unit selling × received qty) per line |
| Margin | Total S.P. value − Total cost value |
| Margin % | `(Margin ÷ Total cost value) × 100` (blank when cost value is zero) |

## Sheet 2 — Summary

| Column | Meaning |
|--------|---------|
| Qty | Grand total quantity |
| Total cost value | Σ (unit net cost × qty) |
| Total S.P. value | Σ (unit selling × qty) |
| Margin | Total S.P. value − Total cost value |
| Margin % | `(Margin ÷ Total cost value) × 100` (blank when cost value is zero) |

## Line inclusion

- Only lines with `outcome === 'valid'` (same rule as posting to inventory).
- Lines with `receivedQty <= 0` are skipped.

## Pricing join

For each goods receipt line:

1. If the receipt has `poId`, load the purchase order and match a line by `productId`, then `sku`.
2. Use PO line **`netCost`** (unit cost including tax) and **`selling`**. If `netCost` is not stored on the PO line, it is computed from `cost`, tax, and discount fields using the same formula as PO refresh.
3. If no PO or no matching line, load the product master by SKU and compute `netCost` using the same formula as PO refresh (`purchase-order-line-calculator.ts`: tax on base only).

Goods receipt lines do not store cost or selling; they only store SKU and received quantity.

## Why goods receipt (not PO date)?

- **Goods receipt** reflects what was actually received and when (`createdAt` for receipt date filtering).
- **Purchase order** supplies negotiated cost and selling rates at line level.
- **Inventory** reflects current stock, not receipt history, and has no vendor on export.

## Related code

- [`goods-receipt-report.service.ts`](../central-backend/src/modules/goods-receipts/goods-receipt-report.service.ts)
- [`goods-receipt-report.controller.ts`](../central-backend/src/modules/goods-receipts/goods-receipt-report.controller.ts)
- [`purchase-order-line-calculator.ts`](../central-backend/src/modules/purchase-orders/purchase-order-line-calculator.ts)
