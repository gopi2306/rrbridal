# Inventory export API

Download a filtered inventory report (Excel, CSV, or PDF) using the same filters as [`GET /api/inventory/grid`](inventory.md).

## Endpoint

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/inventory/export` | Download inventory report file |

Auth: same as grid (no JWT on inventory routes today).

### Query parameters

| Param | Required | Description |
|-------|----------|-------------|
| `format` | yes | `xlsx`, `csv`, or `pdf` |
| `search` | no | SKU, barcode, or product name (same rules as grid) |
| `storeId` | no | When set, **Store qty** is on-hand at that store only; store name/code appears in PDF title and filename |

### Response

File download with `Content-Disposition: attachment`.

| Format | Content-Type | Extension |
|--------|--------------|-----------|
| Excel | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | `.xlsx` |
| CSV | `text/csv; charset=utf-8` | `.csv` |
| PDF | `application/pdf` | `.pdf` |

Filename pattern: `inventory-{storeCode|all-stores}-{YYYY-MM-DD}.{ext}`

Example:

```
GET /api/inventory/export?format=xlsx&storeId=store-001&search=lehenga
```

### Row limit

Exports include **all** matching products (not limited to grid page size). Maximum **10,000** rows; returns `413 Payload Too Large` if the filter matches more. Narrow `search` or contact ops to raise the cap.

## Export columns

| Column | Source |
|--------|--------|
| SKU | Product SKU |
| Barcode | `upcEanCode` |
| Product | `itemName` |
| Brand | Populated brand name |
| Category | Populated category name |
| Warehouse qty | Ledger warehouse bucket |
| In transit | Ledger in-transit bucket |
| Store qty | Per `storeId` filter or sum of all stores |
| Cost price | `product.costPrice` (4 dp — [money-precision.md](./money-precision.md)) |
| MRP | `product.mrp` |
| Selling price | `product.sellingPrice` |
| Store price | `product.storePrice` (falls back to selling price in grid logic) |
| GST % | `product.gstPercent` |

## Admin SPA integration

The admin inventory page (`/inventory`) is **not in this repo**. Wire the existing Export modal and filters as follows.

### 1. Pass current filters

When the user clicks Export, read the same state used for the grid:

| UI state | Query param |
|----------|-------------|
| Search box | `search` (omit when empty) |
| Store filter — **All stores** | omit `storeId` |
| Store filter — specific store | `storeId={store.code}` |

### 2. Export modal actions

| Modal option | Request |
|--------------|---------|
| Export as PDF | `GET /api/inventory/export?format=pdf&...` |
| Export as Excel | `GET /api/inventory/export?format=xlsx&...` |
| Export as CSV | `GET /api/inventory/export?format=csv&...` |

Append current `search` and `storeId` query params when set.

**Export settings** (columns, page size): deferred — export always uses the full column set above and all matching rows (subject to the 10k cap).

### 3. Blob download (browser)

```typescript
async function downloadInventoryExport(format: 'xlsx' | 'csv' | 'pdf', filters: { search?: string; storeId?: string }) {
  const params = new URLSearchParams({ format });
  if (filters.search?.trim()) params.set('search', filters.search.trim());
  if (filters.storeId?.trim()) params.set('storeId', filters.storeId.trim());

  const res = await fetch(`/api/inventory/export?${params}`);
  if (!res.ok) throw new Error(await res.text());

  const blob = await res.blob();
  const disposition = res.headers.get('Content-Disposition') ?? '';
  const match = /filename="([^"]+)"/.exec(disposition);
  const filename = match?.[1] ?? `inventory-export.${format === 'xlsx' ? 'xlsx' : format}`;

  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
```

If JWT is added to inventory routes later, include `Authorization: Bearer …` on the fetch.

### 4. Keyboard shortcut

Wire **`[F2] EXPORT`** to open the Export modal (or trigger default Excel export if the modal is already focused). Match the bottom shortcut bar on the inventory page.

### 5. Error handling

| Status | Meaning | UI action |
|--------|---------|-----------|
| `413` | More than 10,000 rows match | Show message: narrow search or store filter |
| `400` | Invalid `format` | Should not happen if modal passes valid enum |
| `404` | — | N/A for export (unknown store still exports with code in filename) |

## Related

- Grid API: [inventory.md](inventory.md) — `GET /api/inventory/grid`
- Stock movements: [stock-transfers.md](stock-transfers.md)
