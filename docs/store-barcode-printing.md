# Store billing — barcode label printing (WPF)

The store billing WPF app includes a **Barcodes** tab for printing shelf labels on **TVS LP 46 NEO** (203 DPI, TSPL) using RAW commands via the Windows printer queue.

## Workflow

1. Open **Barcodes** from the main navigation.
2. Type a **SKU or barcode** in the blank row at the bottom of the grid and press **Enter**, or press **F6** to open the product pick list.
3. Set **Qty** on each line (default 1 when a product is added). **Qty** is the **total sticker count** for that SKU. The printer puts **2 identical stickers per row** (left + right cup on 2-up media).
4. Press **F5** or **Print** — a **label preview** opens (one card per SKU with Code128, price, and copy count).
5. Select your **TVS LP 46 NEO** Windows queue (auto-selected when the name contains `LP 46`, `LP46`, `NEO`, or `TVS`).
6. Click **Send to printer** — **TSPL** is sent as RAW data; success message shows total labels sent.

## Multiple labels

- Several SKUs on the grid → one label design per SKU, printed in order.
- **Qty** on a row → that many stickers for the same SKU. Each `PRINT` row sends **2 cups** when qty allows (e.g. qty 10 → 5 print rows × 2 cups). Odd qty ends with one row that prints only the **left** cup (38 mm).
- Example: SKU-A qty 10 + SKU-B qty 30 → **40 stickers** total.

## Shortcuts (on Barcodes page)

| Key | Action |
|-----|--------|
| F3 | Focus SKU entry row |
| F5 | Print all lines with qty &gt; 0 |
| F6 | Product list dialog |
| F7 | Clear grid |

## Label content

- Company name from synced receipt config (`StoreName` / company profile).
- Product description, Code128 barcode (UPC/EAN when synced, else SKU), human-readable code.
- Store price (or selling price / MRP fallback), 2 decimal places, with “(include all tax)” footer.

Implementation: `TsplBarcodeLabelBuilder.cs` (TVS LP 46 NEO). Legacy EPL remains for queues named like `LP346` only.

## 2 cups per row (2-up media)

LP 46 NEO rolls often have **two stickers side-by-side** on each row (~38 mm + 38 mm). The app prints **both cups on one row** with the same content (`SIZE 76 mm, 33 mm` and duplicate layout at X offset 304 dots). Odd sticker counts use a final **38 mm** row for the leftover left cup only.

If your stickers are a different width, adjust `BarcodeLabelDimensions.LabelWidthMm` in code (measure one cup with a ruler).

## Requirements

- Product must exist in **local catalog** (`local_products_cache` after sync).
- **TVS LP 46 NEO** installed as a Windows printer; driver set to accept **RAW** / pass-through.
- Do not use **Microsoft Print to PDF** — it creates an invalid `.pdf` file containing TSPL text.

## Related

- [money-precision.md](./money-precision.md) — grid amounts display at 2 dp
- [sync-protocol.md](./sync-protocol.md) — product cache sync
