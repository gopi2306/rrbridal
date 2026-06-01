# Product bulk import

Central API for importing products from **CSV** or **Excel** (`.xlsx`). Master references in the file use **display names** (e.g. `Bridal Wear`, `Sharma Textiles`), not MongoDB IDs. Missing masters can be created automatically. Products are **upserted by SKU**: existing SKU → update; new SKU → create.

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
| `itemName` | Product name |
| `supplierName` | Supplier display name |
| `departmentName` | Department display name |
| `categoryName` | Category display name (scoped by department when duplicate names exist) |

## Recommended

| Column | Description |
|--------|-------------|
| `sku` | Upsert key; omit to auto-generate SKU on create |

## Optional columns

Master name columns: `subCategoryName`, `manufacturerName`, `brandName`, `colourName`, `productStatusName`, `hsnName`, `gstUomName`, `uomSubName`, `weightSizeName`, `weightUnitName`, `offerGroupName`, `skuTypeName`, `skuOrderGroupName`, `indentTypeName`, `batchExpiryDetailName`, `itemPrepStatusName`, `packedConfirmationName`, `poQtyPolicyName`, `sellByName`, `batchSelectionName`.

Scalars: `shortName`, `alias`, `itemProductType`, `gstCode`, `gstPercent`, `upcEanCode`, `decimalPoint` (default **4** — see [money-precision.md](./money-precision.md)), `costPrice`, `marginPercent`, `mrp`, `sellingPrice`, `storePrice`, `unit`, `isActive`, `itemDiscountAllowed`, `isWeighable`, and other numeric fields from the template.

Header aliases are supported (e.g. `Item Name` → `itemName`).

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

- **Duplicate master name** — two departments (etc.) with the same name → row fails; fix data or merge masters in admin.
- **Missing required column** — row skipped with message in `errors`.
- **Ambiguous category** — use unique category names per department or pre-create categories.
