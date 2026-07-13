# Barcode label design master API

Company-wide barcode shelf-label designs for the store billing WPF app. One active design is synced to all stores on **Run sync once** (same pattern as receipt settings).

## Endpoints

### Store (JWT)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/barcode-label-designs/active` | Active design + printer profile |
| `GET` | `/api/barcode-label-designs/printer-profiles` | Seeded printer presets |

### Admin (JWT)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/admin/barcode-label-designs` | List designs |
| `GET` | `/api/admin/barcode-label-designs/printer-profiles` | Printer presets |
| `POST` | `/api/admin/barcode-label-designs` | Create design |
| `PATCH` | `/api/admin/barcode-label-designs/:id` | Update design |
| `DELETE` | `/api/admin/barcode-label-designs/:id` | Delete (blocked if active) |
| `POST` | `/api/admin/barcode-label-designs/:id/activate` | Set company active design |

## Active design response

```json
{
  "design": {
    "name": "Retail stacked (default)",
    "isActive": true,
    "layoutStyle": "retail_stacked",
    "printerProfileId": "tsc-ttp-244-pro",
    "labelWidthMm": 50,
    "labelHeightMm": 38,
    "labelsPerRow": 2,
    "dpi": 203,
    "fields": {
      "productName": true,
      "designSku": true,
      "sellingPrice": true,
      "sizeNote": true,
      "batchNumber": false,
      "expiryDate": false,
      "brandName": false
    },
    "text": {
      "productNameSource": "itemName",
      "designNoPrefix": "D.No:",
      "pricePrefix": "Price ₹:",
      "notePrefix": "Note:",
      "priceStyle": "whole",
      "barcodeHumanText": "sku_spaced",
      "alignment": "center"
    },
    "barcode": { "heightMm": 12, "widthMm": 42 },
    "styles": {
      "productName": { "sizePt": 6, "weight": "bold" },
      "barcodeNumber": { "sizePt": 7, "weight": "bold" }
    },
    "decoration": "price_underline",
    "printOffsetMm": { "vertical": 0, "horizontal": 0 }
  },
  "printerProfile": {
    "profileId": "tsc-ttp-244-pro",
    "name": "TSC TTP-244 Pro / TTP-345",
    "dpi": 203,
    "labelWidthMm": 50,
    "labelHeightMm": 38,
    "labelsPerRow": 2
  }
}
```

## Layout styles

| Style | Description |
|-------|-------------|
| `retail_stacked` | Centered stacked text (name, D.No, price, note) + barcode at bottom |
| `brand_price` | Legacy left barcode + right price block (38×33 mm style) |

## Field data sources (WPF)

| Design field | Product / line source |
|--------------|----------------------|
| Product name | `itemName`, `shortName`, or `alias` |
| Design / SKU | `sku` |
| Selling price | `storePrice` → `sellingPrice` → `MRP` |
| Size / note | `alias` from product cache |
| Batch / expiry | `BatchNo` / `ExpDate` on print line |
| Brand name | `customBrandText` on design |

## WPF sync

On store sync, WPF calls `GET /api/barcode-label-designs/active` and saves to:

```
%LocalAppData%/RRBridal/StoreBilling/barcode_label_design.json
```

Preview and TSPL/EPL print both use `BarcodeLabelLayoutEngine` from the synced design. If no synced file exists, WPF falls back to the legacy **brand + price** 38×33 layout.

## Seeded printer profiles

- `tsc-ttp-244-pro` — TSC TTP-244 Pro / TTP-345 (50×38 mm, 2-up). Admin UI may send alias `printer-tsc-ttp-244`; backend maps it to this id.
- `tvs-lp46-neo` — TVS LP 46 NEO (38×33 mm, 2-up)
- `zebra-gk420t`, `godex-g500`, `citizen-cl-s621` — 50×25 mm, 2-up
- `generic-thermal` — 50×25 mm, 1-up

A default **retail_stacked** design is seeded as active on first backend start.

## Admin UI payload

The TruStock admin form may POST `styles.__frontend` (preview field styles, labels, decoration flags) instead of flat `styles.productName` keys. The backend normalizes this on create/update:

- `text.productNameSource: "alias"` is supported.
- `styles.__frontend.fieldStyles` maps to print styles (`itemName` → `productName`, `code` → `barcodeNumber`, etc.).
- `styles.__frontend.decoration.priceUnderline: true` sets `decoration: "price_underline"` when top-level decoration is `none`.
- Raw `__frontend` is stored for round-trip when listing designs.

## Related docs

- [Store barcode printing](./store-barcode-printing.md)
- [Sync protocol](./sync-protocol.md)
