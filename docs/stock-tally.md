# Stock Tally API

Active store scanning session for physical stock counts. Scanned quantities are pushed to the open **stock audit** on save.

## Get session

```
GET /api/stock-tally?storeCode=store-001&page=1&limit=50
```

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `storeCode` | yes | — | Active store code |
| `search` | no | — | Filter scanned lines by SKU, barcode, or product name |
| `page` | no | `1` | Page number |
| `limit` | no | `50` | Page size (max `200`) |

Creates an empty draft tally session when none exists for the store.

### Response

```json
{
  "storeCode": "store-001",
  "tallyId": "...",
  "tallyNo": "ST-000001",
  "status": "draft",
  "skuCount": 3,
  "totalScannedQty": 24,
  "data": [
    {
      "sku": "SKU-000235",
      "productName": "PP-11248 SEMI BRIDAL XL",
      "productSubtitle": "SEMI BRIDAL - XL",
      "barcode": "8901001000012",
      "scannedQty": 10,
      "gstPercent": 18,
      "costPrice": 7300,
      "mrp": 27598,
      "sellingPrice": 13799,
      "storePrice": 13799
    }
  ],
  "total": 3,
  "page": 1,
  "limit": 50,
  "totalPages": 1
}
```

## Scan barcode or SKU

```
POST /api/stock-tally/scan
```

```json
{
  "storeCode": "store-001",
  "barcodeOrSku": "8901001000012",
  "qtyDelta": 1
}
```

Looks up product by `upcEanCode` first, then SKU. Adds a line or increments `scannedQty`. Returns the updated session.

## Update scanned quantity

```
PATCH /api/stock-tally/lines
```

```json
{
  "storeCode": "store-001",
  "sku": "SKU-000235",
  "scannedQty": 12
}
```

Set `scannedQty` to `0` to remove the line from the session.

## Save tally

```
POST /api/stock-tally/save
```

```json
{ "storeCode": "store-001" }
```

Writes all scanned lines to the store's open stock audit (`scannedQty`), marks the tally as saved, and starts a new empty draft session.

```json
{
  "storeCode": "store-001",
  "tallyId": "...",
  "tallyNo": "ST-000001",
  "auditId": "...",
  "auditNo": "SA-000001",
  "linesSaved": 3,
  "savedAt": "2026-06-03T12:00:00.000Z"
}
```

## Examples

```bash
curl "http://localhost:3000/api/stock-tally?storeCode=store-001"
curl -X POST "http://localhost:3000/api/stock-tally/scan" -H "Content-Type: application/json" -d "{\"storeCode\":\"store-001\",\"barcodeOrSku\":\"8901001000012\"}"
curl -X PATCH "http://localhost:3000/api/stock-tally/lines" -H "Content-Type: application/json" -d "{\"storeCode\":\"store-001\",\"sku\":\"SKU-000235\",\"scannedQty\":12}"
curl -X POST "http://localhost:3000/api/stock-tally/save" -H "Content-Type: application/json" -d "{\"storeCode\":\"store-001\"}"
```

See also [stock-audit.md](./stock-audit.md) for the audit comparison view.
