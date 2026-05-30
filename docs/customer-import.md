# Customer bulk import

Central API for importing customers from **CSV** or **Excel** (`.xlsx`). Customers are **upserted by `customerCode`** when present; otherwise by **`phone`** (exact match); otherwise a new row is created.

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/customers/import/excel/template` | Download `customer-import-template.xlsx` |
| `POST` | `/api/customers/import/excel` | Upload `.xlsx` only |
| `POST` | `/api/customers/import` | Upload `.csv` or `.xlsx` |

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
  "errors": [{ "row": 5, "customerCode": "CUST-001", "message": "..." }],
  "dryRun": false
}
```

## Excel layout

| Rule | Value |
|------|--------|
| Sheet name | `Customers` (or first sheet) |
| Row 1 | Column headers |
| Row 2+ | Customer rows |

## Required columns

| Column | Description |
|--------|-------------|
| `name` | Customer display name |

## Recommended

| Column | Description |
|--------|-------------|
| `customerCode` | Upsert key (e.g. `CUST-001`); unique in database |

## Optional columns

| Column | Description |
|--------|-------------|
| `phone` | Mobile; used as upsert key when `customerCode` is omitted |
| `email` | Email (validated if present) |
| `gstin` | GSTIN |
| `addressLine1` | Address line 1 |
| `addressLine2` | Address line 2 |
| `city` | City |
| `state` | State |
| `pincode` | PIN / pincode |
| `isActive` | `true`/`false`/`1`/`0`/`yes`/`no` |

Header aliases: `customer code`, `mobile`, `email id`, `gst`, `pin`, etc.

## Upsert behavior

1. If `customerCode` is set → match by exact code; update or create.
2. Else if `phone` is set → match by exact phone; error if multiple customers share the same phone.
3. Else → create new customer (`name` required).

On update, only columns present in the file are applied; omitted columns keep existing values.

## Filter API

Paginated customer search for admin grids:

| Method | Path |
|--------|------|
| `POST` | `/api/customers/filter` |

Request body supports `search`, exact filters (`customerCode`, `name`, `phone`, `email`, `gstin`, `city`, `state`, `pincode`, `isActive`), and pagination (`page`, `limit`, `sortBy`, `sortOrder`).

```json
{
  "search": "priya",
  "page": 1,
  "limit": 20,
  "sortBy": "updatedAt",
  "sortOrder": "desc"
}
```

Response: `{ "data", "total", "page", "limit", "totalPages" }`.

## Swagger

Import tag: `customers-import`  
Customers tag: `customers`
