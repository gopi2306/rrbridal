# Products API (list & filter)

## GET `/api/products`

Lightweight product list (default limit 200, max 500 via `skip`/`limit` on service — expose via query if needed later).

### Query parameters

| Param | Example | Effect |
| ----- | ------- | ------ |
| `search` | `search=lehenga` | Case-insensitive text on itemName, shortName, alias, sku, upcEanCode |
| `sku` | `sku=SKU-001` | **Exact** SKU (trimmed) |
| `skuContains` | `skuContains=SKU-00` | **Partial** SKU match (ignored if `sku` is also set) |
| `supplierNameId` | `507f1f77bcf86cd799439011` | Products for that supplier only (24-char hex ObjectId) |
| `categoryId` | `...` | Products in that category |
| `upcEanCode` | `890...` | Exact barcode |

All provided filters are combined with **AND**.

Invalid `supplierNameId` or `categoryId` (non-empty but not valid ObjectId) returns **400 Bad Request**.

### Supplier-scoped listing

1. Resolve supplier: `GET /api/suppliers` or filter suppliers by name.
2. List products:

```http
GET /api/products?supplierNameId=<supplierMongoId>
```

### SKU lookup

```http
GET /api/products?sku=SKU-001
GET /api/products?skuContains=SKU-00
```

### Combined (supplier + SKU)

```http
GET /api/products?supplierNameId=<id>&sku=SKU-001
```

With seed data (`SEED_TEST_DATA=true`), **Sharma Textiles** supplies lehengas including **SKU-001** (Bridal Red Lehenga).

## POST `/api/products/filter`

Paginated filter with populated master refs. Supports the same text/SKU/supplier ideas:

| Field | Notes |
| ----- | ----- |
| `search` | Same fields as GET |
| `sku` | Exact |
| `skuContains` | Partial (ignored when `sku` set) |
| `supplierNameId` | ObjectId; invalid values are skipped (not 400) |

See `FilterProductDto` for full filter surface (category, brand, price ranges, etc.).

## Related

- [product-import.md](./product-import.md)
- [inventory.md](./inventory.md) — grid uses product `search` via internal list filter
