# Store billing — invoice printing (thermal vs A4 vs A5)

The WPF store billing app supports four bill print layouts, chosen in **Settings → Invoice / receipt → Print format**.

## Print formats

| Format | Use case | Paper |
|--------|----------|--------|
| **Thermal receipt (80mm)** | POS thermal printer (default) | ~80 mm roll |
| **A4 retail invoice** | Office laser/inkjet — branded layout | A4 (210 × 297 mm) |
| **A4 commercial invoice** | Office laser/inkjet — plain GST/commercial layout | A4 (210 × 297 mm) |
| **A5 retail invoice** | Office laser/inkjet (compact) | A5 (148 × 210 mm) |
| **A5 pre-printed (values only)** | Pre-printed PAKEEZA-style stationery | A5 (148 × 210 mm) |

Setting is stored locally in `%LocalAppData%\RRBridal\StoreBilling\receipt_config.json` under `print.printFormat` (`Thermal`, `A4`, `A4Commercial`, or `A5`), `print.a5PrePrintedEnabled`, `print.a5PrePrintedLayout` (mm alignment + font), and `print.alsoPrintThermalFirst`. It is **not** synced from central in v1.

A4 and A5 (full template) share the same **branded retail invoice** layout: dark green patterned background, cream arched panel, centered store header, customer meta fields, 4-column line table (Description, Qty, Rate, Amount), optional **DISC %** / **DISCOUNT** footer rows (manual item + cash discounts only), terms footer, and signature line. A5 scales proportionally (~70.5% of A4 width).

**A5 multi-page pagination:** When a bill has more line items than **Lines per page** (default **10**, from `print.a5PrePrintedLayout.maxLineRows`), both **full A5** and **A5 pre-printed** print multiple sheets:

- **Every page:** same header/meta (BILL TO, INV NO, DATE, CONTACT, stitching tick, D/D).
- **Page 1** (when bill continues): items 1..N → **Total Qty** (whole-bill sum, e.g. `18.000`) → **Continued** in Description column on the next row.
- **Page 2+:** header repeated + remaining line items only (no Total Qty repeat).
- **Last page:** remaining lines + discount + payable total (+ terms/signature on full A5).
- **Single page** (≤ Lines per page): no Continued row; layout unchanged from before.

A4 retail format remains single-page regardless of line count.

**A4 commercial invoice:** Plain white A4 with bordered grid sections (seller, consignee/buyer, invoice meta, line table, amount in words, declaration, signatory). Line table columns: SI No, Description, HSN/SAC, Qty, Rate, per (NOS), Disc. %, Amount. The line table expands vertically to fill the page between the meta section and footer blocks. Bill-level discount appears as **Less : DISCOUNT**; totals show whole-bill qty and payable. Multi-page when more than **15 lines** (header/meta repeated; totals on last page only). No CGST/SGST/IGST columns in v1.

**A5 pre-printed mode:** When **A5 tax invoice** is selected and **Use pre-printed A5 paper (values only)** is checked, the app prints **only bill data** (no background, labels, borders, or headers) at mm positions aligned to pre-printed form lines. Font family, body/total point sizes, and **BILL TO** max length are configurable in Settings (default font **Arial**, default **15** chars + `...` if longer). Tune alignment in **Settings → A5 pre-printed alignment (mm)**; use **Preview test layout** then **Save receipt settings**, then verify on physical PAKEEZA paper via F10.

**Stitching** checkbox and **D/D delivery date** appear on full A4/A5 and pre-printed A5 when stitching is selected and a date is entered.

## Settings

1. Open **Settings & sync** from the app.
2. Under **Print format**, choose:
   - **Thermal receipt (80mm)** — existing monospace receipt; **Receipt width (characters)** applies (typically 48).
   - **A4 retail invoice** — full branded layout on A4.
   - **A4 commercial invoice** — plain bordered GST/commercial layout on A4.
   - **A5 retail invoice** — full branded layout on A5, or enable **Use pre-printed A5 paper (values only)** for branded stationery.
3. When pre-printed A5 is enabled, expand **A5 pre-printed alignment (mm)** to adjust field positions, **Lines per page** (multi-page chunk size), Total Qty alignment (page 1 only), **Continued** label text and column position, font family, and bill-to truncation. Use **Reset to defaults** or **Preview test layout** as needed.
4. When **A4**, **A4 commercial**, or **A5** is selected, optionally enable **Also print thermal receipt first (80mm)** to print the 80mm thermal receipt first, then the office invoice (two print jobs to separate printers).
5. Set **Thermal receipt printer (80mm)** and **A4 / A5 invoice printer** (load matching paper in each tray). Pre-printed A5 uses the office invoice printer.
6. Click **Save receipt settings**.

Store name, address, contact, logo, and terms come from receipt settings (synced from central company master) and apply to full A4/A5 only—not pre-printed overlay mode.

## Printing bills

- **F10** on billing, duplicate bill reprint, and ledger reprint use [`InvoicePrintFlow`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/InvoicePrintFlow.cs).
- Preview opens in **Invoice print preview**; **Print** uses the saved queue or Windows print dialog.
- When **Also print thermal receipt first** is enabled (A4, A4 commercial, or A5 format), **Print** sends two jobs in order: (1) 80mm thermal receipt → thermal printer, (2) office invoice → office invoice printer. If **Always use Windows print dialog** is checked, the user sees two dialogs in sequence.
- **Sale return exchange** receipts always use thermal monospace (unchanged).

## A4 commercial — fields printed

| Section | Data printed |
|---------|--------------|
| Seller | Store name, address, state name/code (from company profile + GSTIN), GSTIN |
| Consignee / Buyer | Customer name + phone (same for both) |
| Invoice No. / Dated | Bill number, bill date |
| Salesman | Salesman from bill |
| Line table | SI No, description, HSN, qty, rate, NOS, amount |
| Subtotal | Sum of line amounts |
| Less : DISCOUNT | Manual discount (item + cash) when &gt; 0 |
| Total | Whole-bill qty (NOS) + payable |
| Amount in words | INR … Only |
| Declaration | Standard declaration + terms/policy lines |
| Signatory | for {store name} / Authorised Signatory |

## Pre-printed A5 — fields printed

| Form line | Data printed |
|-----------|--------------|
| BILL TO | Customer name (configurable max chars, default 15 + `...` if longer) |
| INV. NO. | Bill number |
| DATE | Bill date |
| CONTACT | Customer phone |
| STITCHING box | ✓ when stitching checked |
| D/D | Delivery date |
| Table rows | Description, Qty, Rate, Amount per line (chunked by **Lines per page**) |
| Total Qty (page 1, multi-page) | Whole-bill quantity sum in Qty column area (e.g. `18.000`) |
| Continued (page 1, multi-page) | Configurable label (default **Continued**) in Description column; Qty/Rate/Amount blank. Set under **Continued** in A5 pre-printed alignment. |
| Discount name cell | `Discount 10%` — label **Discount** is fixed; **10** is actual item discount % (hidden when no manual discount) |
| Discount amount cell | Actual manual discount as negative, e.g. `-249.90` (item + cash; scheme excluded) |
| TOTAL amount cell | Payable total |

Manual discount fields on the billing screen map to invoice print as follows:

| Billing input | Printed value |
|---------------|-----------------|
| **Item discount (%)** | Discount name column, e.g. `Discount 10%` (percent from bill; label fixed) |
| **Item discount (₹)** + **Cash disc amt** | Discount amount column as negative, e.g. `-249.90` |
| **Item discount (₹)** + **Cash disc amt** | Discount amount (hidden when zero) |
| Scheme / offer discount | Not shown on these invoice fields |

## Billing flags

On the billing screen (customer profiles card, top-right):

| Field | BSON field | On A4/A5 print |
|-------|------------|----------------|
| Hold bills | `holdBills` | No |
| Door delivery | `doorDelivery` | No (billing metadata only) |
| Stitching | `stitching` | Yes — STITCHING checkbox / tick |
| Delivery date | `deliveryDate` | Yes — **D/D** (when stitching checked) |
| Print invoice | `printInvoice` | Gates print after post |

When **Stitching** is checked, a **Delivery date** picker appears below the checkboxes. The date is saved as `dd-MMM-yyyy` (e.g. `15-JUN-2026`).

Flags and delivery date reset when starting a new bill after post/print.

## Implementation

- Thermal: [`ThermalInvoiceTextBuilder`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/ThermalInvoiceTextBuilder.cs) + [`BillPrintService.CreateReceiptDocument`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/BillPrintService.cs)
- A4 / A5 full: [`A4InvoiceDocumentBuilder`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A4InvoiceDocumentBuilder.cs) (A5-only pagination via [`InvoiceLinePagination`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/InvoiceLinePagination.cs))
- A4 commercial: [`CommercialA4InvoiceDocumentBuilder`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/CommercialA4InvoiceDocumentBuilder.cs) + [`IndianAmountInWords`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/IndianAmountInWords.cs) + [`GstStateCodeResolver`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/GstStateCodeResolver.cs)
- A5 pre-printed: [`A5PrePrintedInvoiceDocumentBuilder`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A5PrePrintedInvoiceDocumentBuilder.cs) + [`A5PrePrintedLayoutSettings`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A5PrePrintedLayoutSettings.cs) (persisted) + [`A5PrePrintedInvoiceLayout`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A5PrePrintedInvoiceLayout.cs) (runtime resolver)

## Test checklist

1. Settings → **A5** → enable **Use pre-printed A5 paper** → save → F10 shows values only (no template graphics).
2. Disable pre-printed checkbox → full branded A5 template returns.
3. Settings → change **Offset Y**, Save, F10 → vertical shift on paper; **Preview test layout** works without posting a bill.
4. Change font family (e.g. Times New Roman) → preview and print reflect font.
5. Physical print on pre-printed A5 → if all fields shift together, tweak **Offset X** (right) / **Offset Y** (up) in Settings; otherwise adjust per-field mm boxes.
6. Check **Stitching** + delivery date → tick and D/D appear on pre-printed and full A5.
7. Duplicate reprint and ledger print with pre-printed mode enabled.
8. Settings → **A4** or **A5** → enable **Also print thermal receipt first** → F10 → Print sends thermal then invoice (two jobs or two dialogs).
9. Thermal-only format → dual checkbox hidden; print behaves as before.
10. A4 and thermal-only unchanged when dual setting off.
11. Restart app → `print.a5PrePrintedLayout` persists from JSON.
12. Bill with **Item discount 10%** and **Cash disc amt** → full A5 shows DISC % / DISCOUNT rows above TOTAL; pre-printed A5 shows both values at configured mm positions.
13. Bill with scheme discount only → no discount % / amount on A5 invoices (scheme is separate on billing panel).
14. Duplicate reprint → discount fields read from stored `itemDiscountPercent`, `itemDiscount`, `cashDiscAmount`.
15. Bill with **15 items**, **Lines per page = 10** → page 1: items 1–10, **Total Qty** (whole bill, e.g. `18.000`), **Continued** row; page 2: items 11–15 + discount + payable. Applies to pre-printed and full A5.
16. Page 1 Total Qty uses whole-bill `totalQty`, not page-1 line sum only.
17. Single-page bill (≤10 items): no Continued, no extra Total Qty row.
18. A4 retail format with many lines → still single page.
19. **Preview test layout** with 15 sample lines → two-page preview when Lines per page is 10.
20. Settings → **A4 commercial invoice** → Preview test layout → plain bordered template (no green branding).
21. A4 commercial bill with discount → **Less : DISCOUNT** row and amount in words on last page.
22. A4 commercial bill with 20+ lines → multi-page; totals/words/signatory on last page only.
23. **Also print thermal first** with A4 commercial → two print jobs.
24. Restart app → `printFormat: "A4Commercial"` persists in JSON.

## Related

- [money-precision.md](./money-precision.md)
- [store-barcode-printing.md](./store-barcode-printing.md)
