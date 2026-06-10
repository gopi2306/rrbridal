# Stock Audit API

Store stock audit lines comparing **ordered** (book on-hand at audit start) vs **scanned** (physical count) quantities.

## List audit lines

```
GET /api/stock-audit?storeCode=store-001&page=1&limit=20
```

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `storeCode` | yes | — | Active store code |
| `search` | no | — | SKU or product name |
| `page` | no | `1` | Page number |
| `limit` | no | `20` | Page size (max `100`) |

When no open audit exists for the store (`draft` or `in_progress`), the API creates one by snapshotting current store on-hand quantities (`orderedQty` from ledger, `scannedQty` = `0`).

### Response

```json
{
  "storeCode": "store-001",
  "auditId": "...",
  "auditNo": "SA-000001",
  "status": "in_progress",
  "data": [
    {
      "sku": "SKU-000235",
      "productName": "PP-11248 SEMI BRIDAL XL",
      "productSubtitle": "SEMI BRIDAL - XL",
      "orderedQty": 12,
      "scannedQty": 10,
      "varianceQty": -2,
      "gstPercent": 18,
      "costPrice": 7300,
      "mrp": 27598,
      "sellingPrice": 13799,
      "storePrice": 13799
    }
  ],
  "total": 5,
  "page": 1,
  "limit": 20,
  "totalPages": 1
}
```

## Export

```
GET /api/stock-audit/export?format=xlsx&storeCode=store-001
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `format` | yes | `xlsx`, `csv`, or `pdf` |
| `storeCode` | yes | Store code |
| `search` | no | Same filter as list |

Maximum **10,000** rows per export.

## Examples

```bash
curl "http://localhost:3000/api/stock-audit?storeCode=store-001&page=1&limit=20&search=bridal"
curl -O -J "http://localhost:3000/api/stock-audit/export?format=pdf&storeCode=store-001"
```
