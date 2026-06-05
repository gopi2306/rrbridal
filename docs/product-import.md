# Product bulk import

Central API for importing products from **CSV** or **Excel** (`.xlsx`). Master references in the file use **display names** (e.g. `Bridal Wear`, `Sharma Textiles`), not MongoDB IDs. Missing masters can be created automatically. Products are **upserted** by **`sku`** or **`itemName`** (description): if either matches an existing product → update; otherwise → create.

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/products/import/excel/template` | Download `product-import-template.xlsx` |
| `POST` | `/api/products/import/excel` | Upload `.xlsx` only |
| `POST` | `/api/products/import` | Upload `.csv` or `.xlsx` |

### Query parameters (POST)

| Param | Default | Description |
|-------|---------|-------------|
| `dryRun` | `false` | Parse and validate only; no DB writes |
| `createMissingMasters` | `true` | Create department/category/supplier/etc. when name not found |

### Multipart field

- `file` — the spreadsheet file

### Response

```json
{
  "totalRows": 10,
  "created": 7,
  "updated": 2,
  "failed": 1,
  "mastersCreated": { "Supplier": 1, "Category": 1 },
  "errors": [{ "row": 5, "sku": "SKU-001", "message": "..." }],
  "dryRun": false
}
```

## Excel layout

| Rule | Value |
|------|--------|
| Sheet name | `Products` (or first sheet) |
| Row 1 | Column headers |
| Row 2+ | Product rows |

## Required columns

| Column | Description |
|--------|-------------|
| `itemName` | Product name / description; upsert key when SKU is empty or not found |
| `supplierName` | Supplier display name |
| `departmentName` | Department display name |
| `categoryName` | Category display name (scoped by department when duplicate names exist) |

## Recommended

| Column | Description |
|--------|-------------|
| `sku` | Upsert key (checked first); omit to match by `itemName` only or auto-generate SKU on create |

## Optional columns

Master name columns: `subCategoryName`, `manufacturerName`, `brandName`, `colourName`, `productStatusName`, `hsnName`, `gstUomName`, `uomSubName`, `weightSizeName`, `weightUnitName`, `offerGroupName`, `skuTypeName`, `skuOrderGroupName`, `indentTypeName`, `batchExpiryDetailName`, `itemPrepStatusName`, `packedConfirmationName`, `poQtyPolicyName`, `sellByName`, `batchSelectionName`.

Weight/size on the product (`weightAndSizeId`): use `weightSizeName` (display name, e.g. `Heavy`), or `weightSizeCode` (e.g. `ws-002`), or `weightAndSizeId` (24-char MongoDB ObjectId). Header aliases include `Weight Size` and `weight and size id`.

Scalars: `shortName`, `alias`, `itemProductType`, `gstCode`, `gstPercent`, `upcEanCode`, `decimalPoint` (default **4** — see [money-precision.md](./money-precision.md)), `costPrice`, `marginPercent`, `mrp`, `sellingPrice`, `storePrice`, `unit`, `isActive`, `itemDiscountAllowed`, `isWeighable`, and other numeric fields from the template.

Header aliases are supported (e.g. `Item Name`, `Description` → `itemName`).

## Typical workflow

1. Download template: `GET /api/products/import/excel/template`
2. Fill rows in Excel (delete or replace the example row)
3. Upload: `POST /api/products/import/excel` with `file=@product-import-template.xlsx`

## Examples

```bash
# Download template
curl -o product-import-template.xlsx \
  http://localhost:3000/api/products/import/excel/template

# Import (dry run)
curl -X POST "http://localhost:3000/api/products/import/excel?dryRun=true" \
  -F "file=@product-import-template.xlsx"

# Import
curl -X POST http://localhost:3000/api/products/import/excel \
  -F "file=@my-products.xlsx"
```

## Store sync

After import, stores receive products on the next sync pull as `ProductUpserted` events (see [sync-protocol.md](./sync-protocol.md)).

## Errors

- **Duplicate master name** — import picks the oldest active master when several share a name (common for `Default Batch` after seed + import). Clean up extras in admin if you need a specific record.
- **Missing required column** — row skipped with message in `errors`.
- **Ambiguous category** — use unique category names per department or pre-create categories.
