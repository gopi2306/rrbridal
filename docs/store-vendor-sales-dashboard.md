# Store vendor-wise sales dashboard API

Aggregated **Vendorwise sales** tab data for the store admin dashboard (`/dashboard/store` → Vendorwise sales). Filters retail sales to lines whose SKU belongs to the selected supplier (`products.supplierNameId`).

General store sales (all vendors) remain on [`GET /api/dashboard/store/sales`](store-sales-dashboard.md).

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/dashboard/store/sales/vendors` | **All vendors** tab — vendor table, invoice list, returns; includes **No vendor mapped** row |
| `GET` | `/api/dashboard/store/sales/vendor` | Single-vendor drill-down (`supplierId` required) |

### Query parameters

| Param | Required | Default | Description |
|-------|----------|---------|-------------|
| `supplierId` | **Yes** | — | Supplier Mongo `_id` (`products.supplierNameId`) |
| `storeId` | No | First active store | Store code |
| `period` | No | `today` | `today` \| `week` \| `month` \| `year` \| `custom` |
| `from` | If `custom` | — | `YYYY-MM-DD` |
| `to` | If `custom` | — | `YYYY-MM-DD` |
| `year` | No | Current year | For `month` / `year` |
| `month` | No | Current month (1–12) | For `period=month` |
| `topProductLimit` | No | `5` | Top SKUs (1–20) |
| `invoiceLimit` | No | `20` | Recent vendor-linked bills (1–100) |
| `returnDetailLimit` | No | `20` | Vendor-linked return rows (1–100) |

**Vendor dropdown:** use existing `GET /api/suppliers` to populate the vendor selector.

## Data sources

| Collection | Usage |
|------------|--------|
| `store_invoices` | Bill lines filtered by SKU → supplier |
| `store_sale_returns` | Return lines filtered by SKU → supplier |
| `products` | `sku` → `supplierNameId`, `costPrice`, `categoryId` |
| `suppliers` | Vendor name for response |
| `categories` | Category names for category mix |

Date ranges use **IST business-day** boundaries (same as store sales dashboard).

## Attribution rules

### Single vendor (`/vendor`)

- Only invoice/return **lines** whose SKU exists in product master **and** `supplierNameId` matches `supplierId` are counted.
- Bills with no vendor lines are excluded from invoice count.

### All vendors (`/vendors`)

- Every bill in the period appears in `recentInvoices` (including bills with unmapped SKUs).
- Each invoice row has `mappedQty`, `unmappedQty`, and `hasUnmapped`.
- SKUs with no `supplierNameId` (or unknown SKU) roll into vendor row **`__unmapped__`** / **No vendor mapped**.
- `summary.salesQty` includes mapped + unmapped units (net of returns).
- `grossSales` = sum of line gross (rate × qty, or amount) for vendor lines.
- `netSales` = sum of line net selling values for vendor lines.
- Margin uses line `costPrice` with product master fallback.

## Response sections

### `summary`

| Field | Meaning |
|-------|---------|
| `grossSales` | Vendor line gross before discounts |
| `netSales` | Vendor line net selling value |
| `returnValue` | Vendor return line totals |
| `returnsCount` | Return docs with at least one vendor line |
| `invoices` | Bills with at least one vendor line |
| `itemsSold` | Vendor invoice qty − vendor return qty |
| `totalCostValue` / `totalSellingValue` / `salesMargin` / `marginPercentage` | Net of returns on vendor lines |

### `marginInsights`

| Field | Meaning |
|-------|---------|
| `marginPercentage` | On net vendor sales |
| `avgSalePerUnit` | `totalSellingValue / itemsSold` |
| `avgCostPerUnit` | `totalCostValue / itemsSold` |
| `avgInvoiceValue` | `netSales / invoices` |
| `returnsQty` / `returnsValue` | Vendor return units and value |

### `salesDetails`

Daily (or monthly) buckets: invoices, items, gross, net, returns.

### `returnBreakdown`

Returns-focused daily buckets: `returns`, `returnValue`.

### `topProducts`

Best sellers for this vendor: `sku`, `description`, `units`, `salesAmount`, `margin`, `percent`.

### `categoryMix`

Quantity share by product category for vendor lines sold.

### `recentInvoices`

Latest bills containing vendor items: vendor-only `qty`, `costValue`, `salesAmount`, `margin`.

### `returns`

Vendor-linked returns: `returnNo`, `originalBillNo`, `qty`, `returnValue`, `lineCount`.

## Examples

```bash
# List vendors for dropdown
curl "http://localhost:3000/api/suppliers"

# Vendor-wise sales today
curl "http://localhost:3000/api/dashboard/store/sales/vendor?storeId=store-001&supplierId=SUPPLIER_MONGO_ID&period=today"

# This week
curl "http://localhost:3000/api/dashboard/store/sales/vendor?storeId=store-001&supplierId=SUPPLIER_MONGO_ID&period=week"

# Custom range
curl "http://localhost:3000/api/dashboard/store/sales/vendor?storeId=store-001&supplierId=SUPPLIER_MONGO_ID&period=custom&from=2026-06-01&to=2026-06-11"
```

Replace `SUPPLIER_MONGO_ID` with the `_id` from `GET /api/suppliers`.
