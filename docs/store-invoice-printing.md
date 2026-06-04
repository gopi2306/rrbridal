# Store billing — invoice printing (thermal vs A4 vs A5)

The WPF store billing app supports three bill print layouts, chosen in **Settings → Invoice / receipt → Print format**.

## Print formats

| Format | Use case | Paper |
|--------|----------|--------|
| **Thermal receipt (80mm)** | POS thermal printer (default) | ~80 mm roll |
| **A4 retail invoice** | Office laser/inkjet | A4 (210 × 297 mm) |
| **A5 retail invoice** | Office laser/inkjet (compact) | A5 (148 × 210 mm) |
| **A5 pre-printed (values only)** | Pre-printed PAKEEZA-style stationery | A5 (148 × 210 mm) |

Setting is stored locally in `%LocalAppData%\RRBridal\StoreBilling\receipt_config.json` under `Print.PrintFormat` (`Thermal`, `A4`, or `A5`), `Print.A5PrePrintedEnabled`, and `Print.AlsoPrintThermalFirst`. It is **not** synced from central in v1.

A4 and A5 (full template) share the same **branded retail invoice** layout: dark green patterned background, cream arched panel, centered store header, customer meta fields, 4-column line table (Description, Qty, Rate, Amount), terms footer, and signature line. A5 scales proportionally (~70.5% of A4 width).

**A5 pre-printed mode:** When **A5 tax invoice** is selected and **Use pre-printed A5 paper (values only)** is checked, the app prints **only bill data** (no background, labels, borders, or headers) at fixed positions aligned to pre-printed form lines. Tune alignment in [`A5PrePrintedInvoiceLayout.cs`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A5PrePrintedInvoiceLayout.cs) after a test print on physical paper.

**Stitching** checkbox and **D/D delivery date** appear on full A4/A5 and pre-printed A5 when stitching is selected and a date is entered.

## Settings

1. Open **Settings & sync** from the app.
2. Under **Print format**, choose:
   - **Thermal receipt (80mm)** — existing monospace receipt; **Receipt width (characters)** applies (typically 48).
   - **A4 retail invoice** — full branded layout on A4.
   - **A5 retail invoice** — full branded layout on A5, or enable **Use pre-printed A5 paper (values only)** for branded stationery.
3. When **A4** or **A5** is selected, optionally enable **Also print thermal receipt first (80mm)** to print the 80mm thermal receipt first, then the A4/A5 invoice on the same printer (two print jobs in order).
4. Set **Default bill printer** (load A4 or A5 paper to match the selected format).
5. Click **Save receipt settings**.

Store name, address, contact, logo, and terms come from receipt settings (synced from central company master) and apply to full A4/A5 only—not pre-printed overlay mode.

## Printing bills

- **F10** on billing, duplicate bill reprint, and ledger reprint use [`InvoicePrintFlow`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/InvoicePrintFlow.cs).
- Preview opens in **Invoice print preview**; **Print** uses the saved queue or Windows print dialog.
- When **Also print thermal receipt first** is enabled (A4 or A5 format only), **Print** sends two jobs in order: (1) 80mm thermal receipt, (2) A4/A5/A5 pre-printed invoice. Both use the same default bill printer. If **Always use Windows print dialog** is checked, the user sees two dialogs in sequence.
- **Sale return exchange** receipts always use thermal monospace (unchanged).

## Pre-printed A5 — fields printed

| Form line | Data printed |
|-----------|--------------|
| BILL TO | Customer name |
| INV. NO. | Bill number |
| DATE | Bill date |
| CONTACT | Customer phone |
| STITCHING box | ✓ when stitching checked |
| D/D | Delivery date |
| Table rows | Description, Qty, Rate, Amount per line |
| TOTAL amount cell | Payable total |

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
- A4 / A5 full: [`A4InvoiceDocumentBuilder`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A4InvoiceDocumentBuilder.cs)
- A5 pre-printed: [`A5PrePrintedInvoiceDocumentBuilder`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A5PrePrintedInvoiceDocumentBuilder.cs) + [`A5PrePrintedInvoiceLayout`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A5PrePrintedInvoiceLayout.cs)

## Test checklist

1. Settings → **A5** → enable **Use pre-printed A5 paper** → save → F10 shows values only (no template graphics).
2. Disable pre-printed checkbox → full branded A5 template returns.
3. Physical print on pre-printed A5 → if all fields shift together, tweak `OffsetXMm` (right) / `OffsetYMm` (up) in [`A5PrePrintedInvoiceLayout.cs`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A5PrePrintedInvoiceLayout.cs); otherwise adjust per-field `*LeftMm` / `*TopMm` constants.
4. Check **Stitching** + delivery date → tick and D/D appear on pre-printed and full A5.
5. Duplicate reprint and ledger print with pre-printed mode enabled.
6. Settings → **A4** or **A5** → enable **Also print thermal receipt first** → F10 → Print sends thermal then invoice (two jobs or two dialogs).
7. Thermal-only format → dual checkbox hidden; print behaves as before.
8. A4 and thermal-only unchanged when dual setting off.

## Related

- [money-precision.md](./money-precision.md)
- [store-barcode-printing.md](./store-barcode-printing.md)
