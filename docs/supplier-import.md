# Supplier bulk import

Central API for importing suppliers from **CSV** or **Excel** (`.xlsx`). Suppliers are **upserted by name** (case-insensitive exact match): existing name → update; new name → create.

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/suppliers/import/excel/template` | Download `supplier-import-template.xlsx` |
| `POST` | `/api/suppliers/import/excel` | Upload `.xlsx` only |
| `POST` | `/api/suppliers/import` | Upload `.csv` or `.xlsx` |

### Query parameters (POST)

| Param | Default | Description |
|-------|---------|-------------|
| `dryRun` | `false` | Parse and validate only; no DB writes |

### Multipart field

- `file` — the spreadsheet file

### Response

```json
{
  "totalRows": 10,
  "created": 7,
  "updated": 2,
  "failed": 1,
  "errors": [{ "row": 5, "name": "Acme Traders", "message": "..." }],
  "dryRun": false
}
```

## Excel layout

| Rule | Value |
|------|--------|
| Sheet name | `Suppliers` (or first sheet) |
| Row 1 | Column headers |
| Row 2+ | Supplier rows |

## Required columns

| Column | Description |
|--------|-------------|
| `name` | Supplier display name (upsert key) |

Aliases accepted in headers: `supplier`, `supplier name`, `supplierName` → `name`.

## Optional columns

| Column | Description |
|--------|-------------|
| `gstNumber` | GSTIN |
| `gstStateCode` | State code |
| `gstRegistrationType` | e.g. Regular |
| `panNumber` | PAN |
| `businessRelatedType` | Business type |
| `contactPerson` | Primary contact |
| `contactDescription` | Contact notes |
| `mobileNo` | Mobile |
| `emailId` | Email (validated if present) |
| `faxNo` | Fax |
| `offPhoneNo` | Office phone |
| `buildingAddress` | Building / unit |
| `streetAddress` | Street |
| `landmark` | Landmark |
| `country` | Country |
| `state` | State |
| `city` | City |
| `pin` | PIN / pincode |
| `isActive` | `true`/`false`/`1`/`0`/`yes`/`no` |
| `isSupplier` | Same as `isActive` |

## Upsert behavior

- Match is **case-insensitive** on `name` after trim.
- If more than one supplier matches the same name, the row fails with `Multiple suppliers match name "..."`.
- On update, only columns present in the file are applied (`$set`); omitted columns keep existing values.
- Defaults on create: `isActive=true`, `isSupplier=true` (same as manual create API).

## Example workflow

1. `GET /api/suppliers/import/excel/template` — download template.
2. Fill rows (row 2 is an example; replace or add rows).
3. `POST /api/suppliers/import/excel?dryRun=true` — validate.
4. `POST /api/suppliers/import/excel` — import.
5. Re-import the same file with changed optional fields → rows update in place by `name`.

## Swagger

Tag: `suppliers-import`
