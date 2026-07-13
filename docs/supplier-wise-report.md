# Supplier-wise inventory report API

Supplier overview and product drill-down reports for **on-hand stock only** — rolled up by supplier from the inventory ledger and product master. No sales invoices or returns are queried.

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/inventory/reports/suppliers` | Supplier-wise JSON |
| `GET` | `/api/inventory/reports/suppliers/export` | Supplier-wise Excel (`.xlsx`) |
| `GET` | `/api/inventory/reports/suppliers/:supplierId/products` | Product-wise JSON for one supplier |
| `GET` | `/api/inventory/reports/suppliers/:supplierId/products/export` | Product-wise Excel (`.xlsx`) |

Auth: same as other inventory routes (no JWT required today).

## Query parameters

| Param | Required | Description |
|-------|----------|-------------|
| `scope` | yes | `store` — store on-hand stock; `warehouse` — warehouse on-hand only |
| `storeId` | no | Store code; omit for **all stores** (stock summed across stores) |
| `search` | no | Supplier name or product/SKU contains |
| `brandId` | no | Brand Mongo id |
| `categoryId` | no | Category Mongo id |
| `supplierId` | no | Narrow supplier list (**supplier overview only**; ignored on product drill-down) |

### Examples

Supplier overview — all stores, store stock:

```
GET /api/inventory/reports/suppliers?scope=store
```

One store, warehouse stock:

```
GET /api/inventory/reports/suppliers?scope=warehouse&storeId=store-001
```

Excel export:

```
GET /api/inventory/reports/suppliers/export?scope=store
```

Product drill-down:

```
GET /api/inventory/reports/suppliers/674abc123def456/products?scope=store&storeId=store-001
```

## JSON response

### Supplier overview

```json
{
  "filters": {
    "scope": "store",
    "store": { "code": "all-stores", "name": "All stores", "label": "All stores" }
  },
  "summary": {
    "supplierCount": 3,
    "productCount": 5,
    "stockQty": 1324,
    "totalCostValue": 582400,
    "totalSellingValue": 759600,
    "totalMargin": 177200,
    "marginPercent": 30.42
  },
  "rows": [
    {
      "supplierId": "674abc...",
      "supplierName": "Fresh Farms Pvt Ltd",
      "stockQty": 612,
      "productCount": 2,
      "costValue": 241200,
      "sellingValue": 318400,
      "margin": 77200,
      "marginPercent": 32.01
    }
  ]
}
```

### Product drill-down

```json
{
  "supplier": { "id": "674abc...", "name": "Fresh Farms Pvt Ltd" },
  "filters": { "...": "..." },
  "summary": {
    "productCount": 2,
    "stockQty": 612,
    "costValue": 241200,
    "sellingValue": 318400,
    "margin": 77200,
    "marginPercent": 32.01
  },
  "rows": [
    {
      "sku": "FF-ORG-001",
      "productName": "Organic Tomatoes 1kg",
      "stockQty": 224,
      "costValue": 108200,
      "sellingValue": 142600,
      "margin": 34400,
      "marginPercent": 31.79
    }
  ]
}
```

## Field definitions

| Field | Meaning |
|-------|---------|
| `stockQty` | On-hand quantity from inventory ledger for selected `scope` / `storeId` |
| `costValue` | `stockQty × product.costPrice` (product master) |
| `sellingValue` | `stockQty × product.sellingPrice` (product master) |
| `margin` | `sellingValue - costValue` |
| `marginPercent` | `(margin / costValue) × 100` when cost > 0 |

Rows include only suppliers/products with **stock > 0**.

## Excel export

- Content-Type: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- Filename: `supplier-wise-report-{scope}-{store}-{date}.xlsx`
- Header rows: scope, store
- Maximum **10,000** rows; returns `413 Payload Too Large` if exceeded

### Supplier sheet columns

`Supplier Name | Stock Qty | Products | Cost Value | Selling Value | Margin | Margin %`

### Product sheet columns

`SKU | Product | Stock Qty | Cost Value | Selling Value | Margin | Margin %`

## Admin SPA integration

| UI filter | Query param |
|-----------|-------------|
| Scope = Store | `scope=store` |
| Scope = Warehouse | `scope=warehouse` |
| Store = All stores | omit `storeId` |
| Store = specific | `storeId={store.code}` |
| Export Excel | open `/export` URL as blob download |

Click supplier row → navigate to product view using `GET .../suppliers/{supplierId}/products` with the same filter state.

## Related docs

- [Inventory export](./inventory-export.md) — product grid export (no supplier dimension)
- [Store vendor sales dashboard](./store-vendor-sales-dashboard.md) — sales transaction analytics
