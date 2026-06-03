# Store billing — invoice printing (thermal vs A4 vs A5)

The WPF store billing app supports three bill print layouts, chosen in **Settings → Invoice / receipt → Print format**.

## Print formats

| Format | Use case | Paper |
|--------|----------|--------|
| **Thermal receipt (80mm)** | POS thermal printer (default) | ~80 mm roll |
| **A4 retail invoice** | Office laser/inkjet | A4 (210 × 297 mm) |
| **A5 retail invoice** | Office laser/inkjet (compact) | A5 (148 × 210 mm) |

Setting is stored locally in `%LocalAppData%\RRBridal\StoreBilling\receipt_config.json` under `Print.PrintFormat` (`Thermal`, `A4`, or `A5`). It is **not** synced from central in v1.

A4 and A5 share the same **branded retail invoice** template: dark green patterned background, cream arched panel, centered store header, customer meta fields, 4-column line table (Description, Qty, Rate, Amount), terms footer, and signature line. A5 scales margins, fonts, and image sizes proportionally (~70.5% of A4 width) so column alignment is preserved on smaller paper.

**Stitching** and **Door delivery** checkboxes on the billing screen appear on the printed A4/A5 invoice when checked.

## Settings

1. Open **Settings & sync** from the app.
2. Under **Print format**, choose:
   - **Thermal receipt (80mm)** — existing monospace receipt; **Receipt width (characters)** applies (typically 48).
   - **A4 retail invoice** — branded layout with logo header, bill to / inv no / date / contact, line table, totals, terms, and signature.
   - **A5 retail invoice** — same layout as A4 on A5 paper.
3. Set **Default bill printer** (load A4 or A5 paper to match the selected format).
4. Click **Save receipt settings**.

Store name, address, contact, logo, and terms come from receipt settings (synced from central company master).

## Printing bills

- **F10** on billing, duplicate bill reprint, and ledger reprint use [`InvoicePrintFlow`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/InvoicePrintFlow.cs).
- Preview opens in **Invoice print preview**; **Send to printer** uses the saved queue or Windows print dialog.
- **Sale return exchange** receipts always use thermal monospace (unchanged).

## Billing flags

On the billing screen (customer profiles card, top-right):

| Checkbox | BSON field | On A4/A5 print |
|----------|------------|----------------|
| Hold bills | `holdBills` | No |
| Door delivery | `doorDelivery` | Yes — D/D checkbox |
| Stitching | `stitching` | Yes — STITCHING checkbox |
| Print invoice | `printInvoice` | Gates print after post |

Flags reset when starting a new bill after post/print.

## Implementation

- Thermal: [`ThermalInvoiceTextBuilder`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/ThermalInvoiceTextBuilder.cs) + [`BillPrintService.CreateReceiptDocument`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/BillPrintService.cs)
- A4 / A5: [`A4InvoiceDocumentBuilder`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A4InvoiceDocumentBuilder.cs) with [`RetailInvoiceLayout`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/RetailInvoiceLayout.cs) and [`RetailInvoiceVisuals`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/RetailInvoiceVisuals.cs); page size 210×297 mm (A4) or 148×210 mm (A5)

## Test checklist

1. Billing: toggle **Stitching** and **Door delivery**; hold/resume draft — flags persist.
2. Settings → **Thermal** → save → print bill: narrow 80 mm preview (unchanged).
3. Settings → **A4** → save → F10 preview: green background, cream arch, centered header, 4-column table, checkboxes match billing flags.
4. Settings → **A5** → save → same bill: proportional layout, no horizontal clipping.
5. Compare A4 vs A5 on one bill: same sections and column alignment, smaller type on A5.
6. Duplicate reprint and ledger print with stitching/door delivery saved on posted bill.
7. New bill after post: Stitching/Door delivery reset to unchecked.

## Related

- [money-precision.md](./money-precision.md)
- [store-barcode-printing.md](./store-barcode-printing.md)
