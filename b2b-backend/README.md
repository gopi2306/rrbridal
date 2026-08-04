# RR Bridal Multi-Database B2B Backend

NestJS API that publishes a customer-facing B2B catalog from multiple RR Bridal central MongoDB databases while retaining protected management operations.

## Security warning

Only `/api/storefront/*` is public. Every existing inventory, product, customer, store, purchase-order, bill, report, database, and Swagger management request requires:

```http
x-b2b-admin-key: <B2B_ADMIN_API_KEY>
```

`B2B_ADMIN_API_KEY` must contain at least 24 characters and must never be included in frontend code. Use HTTPS, rate limiting at the gateway, and least-privilege database users in production.

## Requirements and setup

- Node.js 20 or newer
- MongoDB databases that use the current `central-backend` schemas
- Do not configure WPF/POS local cache databases; they are not authoritative and do not contain the central ledger model.

```powershell
Copy-Item .env.example .env
npm install
npm run start:dev
```

Swagger is available at `http://127.0.0.1:3100/api/swagger`.

## Database configuration

`B2B_DATABASES` is a one-line JSON array:

```env
B2B_DATABASES=[{"key":"rrbridal_store","label":"RR Bridal Store","uri":"mongodb://localhost:27017/rrbriadl_store","publicApiBaseUrl":"http://localhost:3000"},{"key":"bilaldev","label":"Bilal Development","uri":"mongodb://localhost:27017/bilaldev","publicApiBaseUrl":"https://bridaldev.rrbazaar.in"}]
```

- `key` is the stable API identifier and must be unique.
- `label` is returned to clients.
- `uri` is never returned by the API.
- `publicApiBaseUrl` is the central-backend origin used to turn relative product media paths into browser URLs.
- Set `"enabled":false` to disable an entry.
- Configuration changes require a restart.
- Each source has its own MongoDB client pool. Failed sources are reported per database during an `all` query.

Use separate credentials per environment. Do not commit real credentials.

## API behavior

All routes use the `/api` prefix.

### Public storefront

- `GET /storefront/products?search=&category=&sort=featured&page=1&limit=24`
- `GET /storefront/categories`
- `GET /storefront/products/:databaseKey/:productIdOrSku`
- `POST /storefront/enquiries`

The catalog returns separate business-labelled offers when the same SKU exists in more than one database. Only products with both `isActive: true` and `isAddedInB2B: true` are returned. Existing products without the B2B flag remain private.

Enquiries contain a retailer profile and source-aware product lines. The API verifies current product visibility, price, and stock, then creates one idempotent `b2b_enquiries` record and upserts the customer in every represented database. A shared `requestId` allows safe retries, and the response reports success or failure per business.

### Discovery and inventory

The routes below and all other non-storefront routes require `x-b2b-admin-key`.

- `GET /databases`
- `GET /databases/health`
- `GET /inventory?databaseKey=all&search=SKU&page=1&limit=50`
- `GET /inventory/summary?databaseKey=bilaldev`
- `GET /inventory/products/:sku?databaseKey=all`
- `POST /inventory/adjustments`

Stock is calculated by summing `qtyDelta` in `inventoryledgerentries`. Adjustments append ledger rows; the service never overwrites a stock balance. Reusing the same `sourceId`, SKU, and location returns the existing adjustment.

```json
{
  "databaseKey": "bilaldev",
  "sku": "SKU-001",
  "qtyDelta": 2,
  "locationKind": "warehouse",
  "locationCode": "main",
  "sourceId": "b2b-adjustment-2026-0001",
  "note": "Approved correction"
}
```

### Business resources

- Products: `GET /products`, `GET /products/:idOrSku`, `PATCH /products/:id`
- Customers: list/detail/create/update/soft-delete under `/customers`
- Stores: list/detail/create/update/soft-delete under `/stores`
- Purchase orders: list/detail/create/update and `PATCH /purchase-orders/:id/status`
- Bills: read-only list/detail under `/bills`
- Reports: `GET /reports/dashboard?databaseKey=all&from=2026-01-01&to=2026-12-31`

Reads default to `databaseKey=all`. Every result contains `databaseKey` and `databaseLabel`. Every mutation requires one explicit `databaseKey`; `all` is rejected to prevent accidental cross-database writes.

An all-database response has this envelope:

```json
{
  "results": [
    {
      "databaseKey": "bilaldev",
      "databaseLabel": "Bilal Development",
      "data": {}
    }
  ],
  "errors": [
    {
      "databaseKey": "bilalqc",
      "databaseLabel": "Bilal QC",
      "error": "Database is unavailable"
    }
  ]
}
```

## Compatibility and limitations

- Direct writes intentionally couple this service to central-backend collection contracts. Deploy schema changes to all configured central databases together.
- Product updates expose pricing, display, active, and B2B publication fields only. Product creation remains in central-backend because it depends on many master references and SKU generation.
- Purchase-order writes preserve the current document shape but do not execute central-backend workflow side effects such as document-number generation, approval audit events, GRN posting, or notifications.
- Bills are read-only because they originate from POS sync.
- Inventory adjustments use an application-level idempotency check. Concurrent writers should use globally unique source IDs.
- Date-range reports use MongoDB `createdAt` timestamps and the invoice payload totals available in each source.

## Verification

```powershell
npm run build
npm test
npm run test:integration
```

Integration tests start one temporary MongoDB server with three databases and verify isolation, B2B publication filtering, duplicate-SKU offers, split/idempotent enquiries, source labels, and partial failure handling.
