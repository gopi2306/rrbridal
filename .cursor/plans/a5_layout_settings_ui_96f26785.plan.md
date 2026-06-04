---
name: A5 layout settings UI
overview: Move A5 pre-printed mm positions into persisted receipt settings with a full Settings editor; truncate BILL TO to 15 characters plus "..." when longer; print all pre-printed values in Arial.
todos:
  - id: layout-settings-model
    content: Add A5PrePrintedLayoutSettings POCO + attach to ReceiptPrintSettings; default factory + load migration
    status: pending
  - id: layout-runtime-resolver
    content: Refactor A5PrePrintedInvoiceLayout to instance FromSettings; wire builder + InvoicePrintFlow
    status: pending
  - id: a5-billto-and-font
    content: BILL TO truncate 15 chars + "..."; Arial font in A5PrePrintedInvoiceDocumentBuilder (settings-backed)
    status: pending
  - id: settings-ui-alignment
    content: Full mm field editor in SettingsDialog + SettingsViewModel load/save/reset/preview
    status: pending
  - id: docs-a5-layout-settings
    content: Update store-invoice-printing.md for Settings-based A5 alignment, BILL TO limit, Arial font
    status: pending
isProject: false
---

# A5 pre-printed alignment — configurable in WPF Settings

## Goal

Today all positions live as `const` in [`A5PrePrintedInvoiceLayout.cs`](store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A5PrePrintedInvoiceLayout.cs). You want **one Settings screen** where every field position (mm) can be changed; **Save receipt settings** writes to local `receipt_config.json`; the next **F10 / print** uses those values on the physical printer—no code edits.

```mermaid
flowchart LR
    SettingsUI["SettingsDialog A5 alignment"]
    ReceiptJson["receipt_config.json"]
    ReceiptConfig["ReceiptConfigStore"]
    Builder["A5PrePrintedInvoiceDocumentBuilder"]
    Printer["Windows print queue"]
    SettingsUI -->|Save| ReceiptJson
    ReceiptJson --> ReceiptConfig
    ReceiptConfig --> Builder
    Builder --> Printer
```

## 1. Persisted layout model

**New class** [`A5PrePrintedLayoutSettings.cs`](store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A5PrePrintedLayoutSettings.cs)

- POCO with the same properties as current layout (offsets, row1, row2, table columns, total, fonts, `MaxLineRows`).
- Text/display defaults:
  - `PrintFontFamily = "Arial"` (used for all pre-printed value fields; replaces `RetailInvoiceVisuals.BodyFont` / Georgia on this path only).
  - `BillToMaxChars = 15` (customer name on BILL TO line).
- Static `CreateDefault()` copies today’s tuned values from current [`A5PrePrintedInvoiceLayout.cs`](store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A5PrePrintedInvoiceLayout.cs) (lines 9–68) so existing behavior is unchanged on first run.

**Attach to receipt config** — [`ReceiptConfig.cs`](store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/ReceiptConfig.cs)

```csharp
public A5PrePrintedLayoutSettings A5PrePrintedLayout { get; set; } = A5PrePrintedLayoutSettings.CreateDefault();
```

Stored under `Print.A5PrePrintedLayout` in `receipt_config.json` (camelCase JSON, same as other print settings).

**Migration on load** — in [`ReceiptConfigStore.Load`](store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/ReceiptConfigStore.cs): if `A5PrePrintedLayout` is null after deserialize, assign `CreateDefault()`.

## 2. Runtime layout resolver (replace static const usage)

**Refactor** [`A5PrePrintedInvoiceLayout.cs`](store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A5PrePrintedInvoiceLayout.cs)

- Keep `PageWidthMm` / `PageHeightMm` as fixed constants (148×210).
- Replace `const` fields with an instance class `A5PrePrintedInvoiceLayout` holding a snapshot of `A5PrePrintedLayoutSettings`.
- Factory: `public static A5PrePrintedInvoiceLayout FromSettings(A5PrePrintedLayoutSettings s)` with `X()`, `Y()`, `TableY()` methods unchanged.

**Wire print path** — [`A5PrePrintedInvoiceDocumentBuilder.cs`](store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A5PrePrintedInvoiceDocumentBuilder.cs) + [`InvoicePrintFlow.cs`](store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/InvoicePrintFlow.cs)

- `Create(ThermalInvoiceInput input, A5PrePrintedLayoutSettings layout)` (or pass resolved layout instance).
- Read layout from `services.ReceiptConfig.Current.Print.A5PrePrintedLayout` when building pre-printed A5 documents.

## 2b. BILL TO truncation and Arial font (pre-printed only)

**BILL TO customer name** — [`A5PrePrintedInvoiceDocumentBuilder.cs`](store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/A5PrePrintedInvoiceDocumentBuilder.cs)

- Before placing `input.CustomerName` on the BILL TO zone, format with a small helper (e.g. `A5PrePrintedText.FormatBillTo(name, maxChars)`):
  - If length ≤ 15: print as-is.
  - If longer: print **first 15 characters** + **`...`** (18 characters on the form; fits the pre-printed line).
- Use `TextWrapping.NoWrap` and `TextTrimming.None` for BILL TO so WPF does not wrap or auto-ellipsis differently than the rule above.

**Font** — same builder, `PlaceText`:

- Set `FontFamily = new FontFamily(settings.PrintFontFamily)` defaulting to **Arial** (not [`RetailInvoiceVisuals.BodyFont`](store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Invoicing/RetailInvoiceVisuals.cs) which is Georgia/Times for full A4/A5 templates).
- Apply Arial to **all** pre-printed fields (header, table, total) for consistent laser output.
- Full branded A4/A5 invoice and thermal receipt paths stay unchanged.

Optional in Settings UI (same A5 block): editable **Bill to max characters** (default 15) and **Print font** (default Arial) — can be fixed constants in v1 if you prefer fewer fields.

## 3. Settings UI — full field editor

**ViewModel** — extend [`SettingsViewModel.cs`](store-billing-wpf/src/RRBridal.StoreBilling.App/ViewModels/SettingsViewModel.cs)

- Nested bindable object or duplicate properties mirroring `A5PrePrintedLayoutSettings` (simplest: expose `A5PrePrintedLayoutSettings Layout` as observable wrapper, or individual `[ObservableProperty]` fields grouped in partial class).
- `Load` from `ReceiptConfig.Current.Print.A5PrePrintedLayout` in `ApplyReceiptFieldsFromConfig`.
- `SaveReceiptSettingsAsync`: copy VM → `c.Print.A5PrePrintedLayout`, then existing `SaveAsync`.
- Commands:
  - `ResetA5PrePrintedLayoutToDefaultsCommand` → `CreateDefault()` + refresh bindings.
  - `PreviewA5PrePrintedAlignmentCommand` (optional but useful): open [`InvoicePrintPreviewWindow`](store-billing-wpf/src/RRBridal.StoreBilling.App/Views/InvoicePrintPreviewWindow.xaml) with a small **sample** `ThermalInvoiceInput` using current layout (no bill post required).

**View** — [`SettingsDialog.xaml`](store-billing-wpf/src/RRBridal.StoreBilling.App/Views/SettingsDialog.xaml)

- Show block when `IsA5ReceiptFormat && A5PrePrintedEnabled` (same visibility as pre-printed checkbox).
- Scrollable **“A5 pre-printed alignment (mm)”** section with labeled `TextBox`es in groups:
  - Global: Offset X, Offset Y, Table offset Y, Text baseline nudge
  - Row 1: Bill To (top/left/width), Inv No, Date
  - Row 2: Contact, Stitching tick, Delivery date
  - Table: Table top, row height, Description/Qty/Rate/Amount columns
  - Total row
  - Fonts: Body pt, Total pt; optional Bill to max chars (15), font family (Arial)
- Buttons: **Reset to defaults**, **Preview test print** (if implemented)
- Helper text: *Saved with receipt settings; print a test on PAKEEZA paper after changes.*

Use two-column grid (label + 80px mm box) to keep the screen usable.

## 4. Docs

Update [`docs/store-invoice-printing.md`](docs/store-invoice-printing.md): alignment is configured in **Settings → A5 pre-printed alignment**, stored in `receipt_config.json` under `print.a5PrePrintedLayout`; code file is only defaults fallback.

## 5. Testing checklist

1. Fresh install / delete layout section in JSON → defaults match current print.
2. Change `offsetYMm` in Settings, Save, F10 pre-printed bill → vertical shift on paper.
3. Reset to defaults → values and print revert.
4. Restart app → settings persist from JSON.
5. Full A5 branded template and thermal paths unchanged.
6. Long customer name on pre-printed bill → exactly 15 letters + `...` on BILL TO line.
7. Pre-printed print uses Arial; full A4/A5 template still uses existing serif font.

## Out of scope

- Per-printer profiles (one layout per machine file is enough for v1).
- Central sync of alignment (local till calibration only).
- Visual drag-and-drop designer on top of the form image.
