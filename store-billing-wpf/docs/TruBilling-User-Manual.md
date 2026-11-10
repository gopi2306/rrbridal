# TruBilling — User Manual

**Application:** TruBilling (Store Billing desktop)  
**Version:** 3.0  
**Last updated:** 17 September 2026  
**Audience:** cashiers, store managers, and support staff

This document replaces the July 2026 v2.0 manual. It matches the current TruBilling WPF screens.

For a technical feature reference (configuration, MongoDB collections, API sync), see [RR-Bridal-Store-Billing-WPF-Feature-Guide.html](./RR-Bridal-Store-Billing-WPF-Feature-Guide.html).

**Printable copy:** open [`TruBilling-User-Manual.html`](./TruBilling-User-Manual.html) in a browser and print to PDF, or re-save as Word from the HTML.

---

## What’s new in version 3.0

| Area | What cashiers and managers will notice |
|------|----------------------------------------|
| GSTIN lookup | Enter a 15-character GSTIN and click **Search GST** to fill name and address from the GST taxpayer API (Central Online required). |
| Held bills | Resume or delete holds from **Resume held**. The invoice date on a resumed bill is the **resume / post day**, not the original hold day. |
| Vouchers | Tally-style **Vouchers** page (`Ctrl+V`) routes to Sales, Receipt, Payment, Credit Note, Journal, and Daily Expense. |
| Outbound dispatch | Create courier/post parcels against posted bills, track status, batch parcels, and print labels. |
| Reports | New **Reports** hub: Customer billing, Supplier-wise sales, Fast sellers, Going out of stock. |
| Central Online | Store can run against the central API (no local Mongo / ZeroTier). Header chips show **Central** and **Mongo** status. |
| Screen access | Managers on POS 1 decide which counters may open each screen. |
| WhatsApp broadcast | Select customers and send a promotion template from **Customers**. |
| Direct credit note | Issue store credit without a bill (from Vouchers → Credit Note). |
| Physical inventory | Download a count template and import physical quantities from Dashboard. |
| Expenses | Supplier, GSTIN, GST treatment, supply type, and split payments. |
| Card charges | Optional % and/or flat card fee on Card or Split-with-card payments. |
| Price GST mode | Selling rate can be GST-inclusive (default) or exclusive. |
| Navigation | Optional left sidebar (Tally-style) or header chips. `Ctrl+G` opens Go To. |

---

## Table of contents

1. [Introduction](#1-introduction)
2. [Logging in](#2-logging-in)
3. [Main screen layout](#3-main-screen-layout)
4. [Starting your day](#4-starting-your-day)
5. [Billing (POS)](#5-billing-pos)
6. [GSTIN lookup](#6-gstin-lookup)
7. [Quotations](#7-quotations)
8. [Vouchers](#8-vouchers)
9. [Customers](#9-customers)
10. [Salesman](#10-salesman)
11. [Credit bills](#11-credit-bills)
12. [Bill lookup](#12-bill-lookup)
13. [Sale returns and exchanges](#13-sale-returns-and-exchanges)
14. [Outbound dispatch](#14-outbound-dispatch)
15. [Bill adjustments](#15-bill-adjustments)
16. [Duplicate print and WhatsApp](#16-duplicate-print-and-whatsapp)
17. [Day close and cash management](#17-day-close-and-cash-management)
18. [Daily expenses](#18-daily-expenses)
19. [Dashboard](#19-dashboard)
20. [Reports](#20-reports)
21. [Analytics and ledger](#21-analytics-and-ledger)
22. [Online COD orders](#22-online-cod-orders)
23. [Barcode label printing](#23-barcode-label-printing)
24. [Settings and sync](#24-settings-and-sync)
25. [Keyboard shortcuts](#25-keyboard-shortcuts)
26. [Which screens a counter can open](#26-which-screens-a-counter-can-open)
27. [Troubleshooting](#27-troubleshooting)
28. [Quick reference — daily checklist](#28-quick-reference--daily-checklist)

---

## 1. Introduction

**TruBilling** is the point-of-sale application for retail counters. It can run in two ways:

| Mode | How it works |
|------|----------------|
| **Central Online** | This store talks to the central office API. Header chip: **Central: Online**. Local MongoDB is not required. |
| **Local / sync** | Bills are saved on the till (or parent Mongo) and synced to central when the network is available. Header chip shows Mongo health. |

The manager chooses the mode in **Settings → 1. Sync**. All counters in the store share the same mode.

### What you can do

| Task | Menu |
|------|------|
| Create and post sales bills | Billing (`Ctrl+A`) |
| Hold a bill and resume later | Billing → **Resume held** / **F8** |
| Open voucher types (Tally-style) | Vouchers (`Ctrl+V`) |
| Create quotations and convert to bills | Quotations (`Ctrl+Q`) |
| Register and search customers; GSTIN fill; WhatsApp broadcast | Customers (`Ctrl+U`) |
| Register salesmen | Salesman (`Ctrl+M`) |
| Find a posted bill (view / return / adjust / dispatch / reprint) | Bill Lookup (`Ctrl+K`) |
| Collect pay-later balances | Credit Bills (`Ctrl+I`) |
| Process returns and exchanges | Returns (`Ctrl+R`) |
| Create courier / post dispatches | Outbound Dispatch |
| Correct posted bills | Adjustments (`Ctrl+T`) |
| Reprint invoices | Duplicate (`Ctrl+J` or **F11**) |
| Open / close the business day | Day Close (`Ctrl+W`) |
| Record expenses | Expenses (`Ctrl+E`) |
| View sales, stock, physical count | Dashboard (`Ctrl+D`) |
| Customer / supplier / fast-seller / low-stock reports | Reports |
| 14-day sales trend | Analytics (`Ctrl+Y`) |
| Recent bills and payments | Ledger (`Ctrl+L`) |
| Collect online COD | Online Sales (`Ctrl+O`) |
| Print barcode labels | Barcodes (`Ctrl+B`) |
| Printers, WhatsApp, billing rules, sync | Settings (`Ctrl+,`) — POS 1 |

Which of these appear in **your** menu depends on **Settings → Counter screen access**. If a page is missing, it is not an error — that counter is not allowed to open it.

---

## 2. Logging in

1. Open **TruBilling** on the till computer.
2. The login screen shows the **company name**, **store**, and **till / POS counter**.
3. Enter your **email** and **password** (from your manager).
4. Click **Login** or press **Enter**.

**Notes:**

- Credentials come from users synced (or loaded) from central.
- Only one person can be logged in on the same till at a time.
- A yellow **sync warning** banner means master data may be stale. Ask the manager to run sync from Settings before billing.
- After login the app opens on **Billing** (or the first screen this counter is allowed to open).

To sign out, use **Sign out** in the sidebar, the logout control in the header, or **Logout** in the Go To menu.

---

## 3. Main screen layout

```
┌────────────┬──────────────────────────────────────────────────┐
│ Sidebar    │  Logo  Store name     Page title    🔍 Search    │
│ (optional) │  Till line            Day  Central  Mongo  🔔 ⚙ │
│ Billing    ├──────────────────────────────────────────────────┤
│ Vouchers   │                                                  │
│ …          │              Current page                        │
│            │                                                  │
│            ├──────────────────────────────────────────────────┤
│ Shortcuts  │  F1 Help  F2 New  F8 Hold  F9 Post  F10 Print …  │
│ Sign out   │                                                  │
└────────────┴──────────────────────────────────────────────────┘
```

| Area | Purpose |
|------|---------|
| **Sidebar** | Persistent left menu (when enabled in Settings). `Ctrl+G` hides it. |
| **Header chips** | Quick links when sidebar is off. |
| **Go To (`Ctrl+G`)** | Overlay / drawer of all allowed pages. **Escape** closes it. |
| **Global search** | On Billing, scan or type a product code to add a line. |
| **Day status chip** | Day open or closed. Click to open Day Close. |
| **Central / Mongo chips** | Connection health. Online mode shows Mongo as not required. |
| **Notification bell** | Pending items waiting to sync. Click to view and sync. |
| **Settings gear** | Opens Settings (when this counter is allowed). |
| **Footer shortcuts** | F1–F12 chips for the current page. |

**Appearance:** Settings → **4. App & billing** → **Show left sidebar menu**. Off = header chips (wider workspace). On = Tally-style left menu.

**F2 / Ctrl+N** clears or refreshes the **current** page, not only Billing.

---

## 4. Starting your day

Complete these steps **before** posting bills.

### Step 1 — Connect and sync (manager / POS 1)

1. Open **Settings** (`Ctrl+,` or the gear icon).
2. Open tab **1. Sync**.
3. Confirm **Central Online mode** is set the way this store should run, then **Save mode**.
4. Under **Central connection**, log in with office credentials if the status is not already signed in.
5. Click **Run sync**.

Sync pushes local bills and pulls products, stock, schemes, salesmen, users, and receipt header (store name, GSTIN, bank details) from the company profile.

After a large catalog or price update, also click **Re-sync products**.

Without a recent sync (in local mode), barcode scan and product search will miss items.

### Step 2 — Open the business day

1. Go to **Day Close** (`Ctrl+W` or the day status chip).
2. Confirm today’s date is selected.
3. Enter **opening cash** in the drawer.
4. Click **Open day**.

The header chip should show **Day: Open**. Bills, returns, and expenses cannot be posted until the day is open.

---

## 5. Billing (POS)

Billing is the main screen for sales invoices. Open with **Ctrl+A**.

### 5.1 Customer details

| Field | Description |
|-------|-------------|
| **Phone** | 10-digit mobile (required to post). The field highlights until a valid number is entered. |
| **Customer name** | Required to post. |
| **Customer code** | Filled when a customer is found or saved. |
| **GSTIN** | Optional 15-character GSTIN. See [GSTIN lookup](#6-gstin-lookup). |
| **Salesman** | Pick from registered salesmen. Required when the store has at least one active salesman. |

**Finding a customer:**

1. Type the phone and press **Tab** or **Enter** — the app searches automatically.
2. Or click **Find Customer**.
3. If the phone is unknown, the **Customer** dialog asks for name (and GSTIN / address when you expand details).

**Bill option checkboxes:**

| Option | When to use |
|--------|-------------|
| **Hold bills** | Mark this draft as a hold-type bill. |
| **Door delivery** | Home delivery. |
| **Online COD** | Online order — no payment at post; collect later from Online Sales. |
| **Stitching** | Shows a delivery date picker. |
| **Print invoice** | Print automatically after posting. |

### 5.2 Adding products

Use the **last row** of the line-items grid:

1. Type **SKU or barcode** in Product code and press **Enter**.
2. Or type a **product name** — a pick list opens.
3. Or click **Add manual** for a code not in the catalog.
4. Press **F3** / **Ctrl+F** to jump to the product entry row.

You can also use the **header search** box: enter a code and press **Enter**.

**Product pick list columns** depend on Settings → Line item columns (Minimal / Standard / Full): SKU, Name, Rate, Image note, and (when Standard/Full) GST %, MRP, Stock, HSN.

If **Image note** is blank, central has no image description yet — billing still works.

**Editing lines:**

- Change quantity, rate, description, HSN, MRP, tax %, or **Alteration amt** in the grid.
- Click the trash icon to remove a line.
- **Tab** moves between cells.
- If **Confirm when the same product is added again** is on, scanning an SKU already on the bill asks before increasing quantity.

**Store rate:** comes from the local catalog. If central has no store price (or it is zero), sync uses the central selling price.

**Selling price GST mode** (Settings):

- **With GST** (default): rate includes GST; tax is reverse-split.
- **Without GST**: rate excludes GST; GST is added on top.

### 5.3 Discounts, alteration, and totals

The **invoice preview** on the right shows live totals:

- Subtotal, GST, item discount %, cash discount
- **Alteration amount** (stitching / alteration). Settings can treat it as GST-inclusive or a flat charge after merchandise tax.
- Applied promotion schemes (click to remove)
- Round off
- **Inter-state (IGST)** for out-of-state customers
- Customer credit notes (toggle to apply)
- Credit / pay-later advance when credit billing is enabled

**Payable** is what the customer owes now.

### 5.4 Hold a bill (F8 / Ctrl+H)

Use when the customer is not ready to pay:

1. Add at least one line.
2. Press **F8** or **Ctrl+H**, or use the footer shortcut.

The bill is saved as a draft with a **hold number**. Stock is **not** deducted.

**Resume or delete:**

1. Click **Resume held** on Billing.
2. Select a row (Hold no, customer, payable, counter, held time).
3. Click **Resume** to load it back into Billing, or **Delete hold** to discard it.

When you resume, the **invoice date becomes today** (the resume / post day), not the original hold date.

### 5.5 Post a bill (F9 / Ctrl+Enter)

**Before posting, confirm:**

- Customer name is filled
- Phone is a valid 10-digit number
- Salesman is selected if required
- At least one line has quantity × rate greater than zero
- Business day is **open**
- Combined discount does not exceed your user limit

**To post:**

1. Press **F9** or **Ctrl+Enter**.
2. If stock is short, choose **Post anyway** (creates an indent request) or **Cancel**.
3. The **Payment** dialog opens (unless **Online COD** is checked):
   - **Cash**
   - **Card** (Pine Labs / Razorpay POS, as configured)
   - **UPI** (Razorpay)
   - **Credit note**
   - **Split** (multiple modes)
   - **Credit / advance** when pay-later is enabled
4. If **card charges** are enabled, Card or Split-with-card may add a suggested % and/or flat fee. You can override the ₹ amount in Payment.
5. Confirm payment.

After a successful post:

- Bill number is assigned
- Stock is reduced
- Invoice may print and/or send on WhatsApp (if configured)
- Screen clears for the next bill

**F2 / Ctrl+N** starts a new bill (clears the current draft).

### 5.6 Print without posting (F10 / Ctrl+P)

Press **F10** or **Ctrl+P** to preview or print the current draft **without** posting. Use this for customer review.

Press **F1** on Billing for an in-app shortcut reminder.

---

## 6. GSTIN lookup

GSTIN lookup fills **customer name and address** from the GST taxpayer API so B2B bills have the correct tax identity.

### Requirements

1. Manager turns on **Enable GSTIN lookup** in Settings → **4. App & billing**.
2. The store is in **Central Online** mode.
3. GSTIN is a valid **15-character** number (example pattern: `22AAAAA0000A1Z5`).

If lookup is off, or the store is not online, the **Search GST** button is hidden or shows an error: *GSTIN lookup needs Central Online mode.*

### Where you can search

| Screen | How |
|--------|-----|
| **Billing** | GSTIN field + **Search GST** |
| **Customers** | Address & tax section + **Search GST** |
| **Customer** quick-capture dialog | After expanding address fields |

### How it behaves

- Click **Search GST** to look up the GSTIN. Address fields are **overwritten** from the GST record. Name is filled only if it is still blank.
- Leaving the GSTIN box (lost focus) also looks up, but only **fills empty** fields so you do not wipe a name already typed.
- Status text under the field shows trade name / GST status when found, or an error such as *GSTIN not found* / *Enter a valid 15-character GSTIN*.
- Always check the filled address, city, state, and pincode before posting. You can still edit every field by hand.

GSTIN lookup does **not** replace the required mobile number or salesman.

---

## 7. Quotations

Use quotations when the customer wants a formal quote before buying.

1. Go to **Quotations** (`Ctrl+Q`).
2. Click **Create** (or **F2** / **Ctrl+N** on this page).
3. Enter customer and products similar to Billing (including GSTIN if needed).
4. **Save** — this is not a posted bill and does not deduct stock.
5. From the list: **Open** to edit, **Cancel** unused quotes, or **Convert to bill** for an **open** quotation. Convert loads lines into Billing for payment and post.

---

## 8. Vouchers

**Vouchers** (`Ctrl+V`) is a Tally-style picker. Select a type and press **Enter**, double-click, or press **1–6**.

| Key | Type | Opens |
|-----|------|--------|
| **1** | Sales | Billing |
| **2** | Receipt | Credit Bills (collect customer balance) |
| **3** | Payment | Cash withdrawal on Day Close |
| **4** | Credit Note | Choose **bill return** or **direct customer credit** (no bill) |
| **5** | Journal | Adjustments |
| **6** | Daily Expense | Expenses |

**COD Receipt…** opens Online Sales to collect COD.

**Direct customer credit note:** search a customer, enter amount (and optional reason), then **Create credit note**. The customer can apply that credit on a later bill. No original invoice is required.

---

## 9. Customers

Open **Customers** (`Ctrl+U`) to search, register, and maintain customer masters.

### Search and list

Filter by **code**, **name**, or **mobile**, then **Search** or **Refresh**. The grid shows Code, Name, Mobile, Credit flag, and Sync status.

### Register or edit

1. Click **New customer** (**F2** / **Esc** also starts a new form on this page).
2. Enter **Customer name** and **Mobile** (required).
3. Optionally enter telephone, email, **GSTIN** (with **Search GST**), door, street, full address, city, pincode, state.
4. Tick **Credit customer — eligible for Bill on credit** if pay-later is allowed for this person.
5. Click **Save (F4)** or press **F4** / **Ctrl+S**.
6. **Use in billing** applies the selected customer to the current bill.

Customer codes are generated automatically. Records sync to central.

### WhatsApp broadcast

1. Tick customers in the list (or **Select all**).
2. Click **Broadcast WhatsApp**.
3. Choose **Marketing template** (recommended: offer %, scope, valid-until date, optional URL) or **Session free-text**.
4. Click **Send broadcast**.

Use this only for customers who opted in and when WhatsApp is connected (Settings → **3. WhatsApp**).

---

## 10. Salesman

1. Go to **Salesman** (`Ctrl+M`).
2. Enter salesman details and save (**F2** starts a new row).
3. Return to Billing and select the salesman on the bill.

Salesmen are also updated when the manager runs sync from central.

---

## 11. Credit bills

Collect remaining balances on pay-later (credit) sales.

1. Go to **Credit Bills** (`Ctrl+I`), or Vouchers → **2 Receipt**.
2. Search by customer, bill number, status (pending / partial / settled), or date.
3. Select a bill and **Record payment** (cash, card, UPI, credit note, or split, as allowed).
4. Print a credit / balance receipt if needed.

Managers set credit rules in Settings → **4. App & billing** (enable credit, require credit-customer flag, min advance % / ₹, allow zero advance, allow partial collection, max balance per bill).

---

## 12. Bill lookup

Find any posted bill from one screen (`Ctrl+K`):

1. Search by invoice number, customer name, or mobile.
2. Select a bill, then:
   - **View Bill** — details, payments, linked return / adjustment
   - **Add Return** / **Add Adjustment**
   - **Create / Open Dispatch** — outbound parcel for that bill
   - **Duplicate Print** / **Print Credit Note**
   - **Delete Bill** — only when your role and store rules allow it (ask a manager)

Available on any counter that has Bill Lookup access.

---

## 13. Sale returns and exchanges

1. Go to **Returns** (`Ctrl+R`), or start from **Bill Lookup**.
2. Enter the original **bill number** (full number or last 3–4 digits) and **Load Bill**.
3. If several bills match, pick the correct one.
4. Select return lines and enter **return quantity**.
5. Optionally add **exchange items** with **F3** / header search.
6. Choose **Return mode:**
   - **Credit note** — store credit
   - **Cash refund** — cash returned (confirmation required)
7. Enter a **reason**.
8. Click **Post Return / Exchange**.

**Settings → Allow multiple returns per bill (partial returns):**

- **Off** (default): only one return transaction per posted bill.
- **On:** you may return remaining quantity later. Fully returned lines show a disabled **Select** checkbox and a **Prev Ret** quantity.

**Rules:**

- You cannot return more than the original line quantity.
- Business day must be open.
- If exchange items cost more than the return, Payment collects the difference.
- If exchange items cost less, the remainder follows the return mode (credit note or cash).

From Vouchers → **4 Credit Note** you can also choose a **direct** credit (no bill) — see [Vouchers](#8-vouchers).

---

## 14. Outbound dispatch

Use **Outbound Dispatch** when a posted bill will be sent by **courier** or **post**.

You can also open it from Bill Lookup → **Create / Open Dispatch**.

### Create a dispatch

1. Search posted bills by bill no, customer, or mobile → **Search posted bills**.
2. Select the bill → **Load selected bill** (or double-click).
3. Confirm **Ship to** name, phone, address, city, state, pincode, landmark. Address is copied from the customer / bill when available.
4. Enter parcel details: carrier type (Courier / Post), carrier name, tracking no, package count.
5. **Fee payer:** Customer or Store. Enter fee ₹, payment mode, and optional reference.
6. Click **Save Draft**.

### Status flow

Move the dispatch with the footer buttons:

**Draft → Ready → Handed Over → Delivered**

**Returned** or **Cancelled** require a reason in the footer text box.

### Batches and print

- Tick **Batch** on several rows → **Assign common batch** to group parcels.
- Print **Parcel A4 / Thermal**, **Batch A4 / Thermal**, and **Charge A4 / Thermal** (when a charge receipt exists).

Filter the list by business date, status, batch no, or search text, then **Refresh** or **Open selected**.

---

## 15. Bill adjustments

Correct quantity or rate on an already-posted bill.

1. Go to **Adjustments** (`Ctrl+T`), Bill Lookup, or Vouchers → **5 Journal**.
2. Enter the bill number and **Lookup**.
3. Edit **adjusted quantity** and **adjusted rate** per line.
4. Enter a **reason**.
5. Click **Post Adjustment**.

The screen shows the difference in payable and tax. Business day must be open.

After you post, the bill’s line rates/amounts and payable are updated everywhere (Bill Lookup, duplicate print, reports). The adjustment voucher remains as the audit record showing original vs adjusted amounts. Only one adjustment is allowed per bill.

---

## 16. Duplicate print and WhatsApp

Reprint a posted invoice or credit note with a **DUPLICATE** watermark.

1. Go to **Duplicate** (`F11` or `Ctrl+J`), or start from Bill Lookup / Ledger.
2. **Bill tab:** search by invoice number, customer, or date range → **Print duplicate copy** or **Send WhatsApp bill**.
3. **Credit Note tab:** search and reprint a credit note.

Managers can disable duplicate print in Settings. Auto-send of the **original** bill after post is configured under Settings → **3. WhatsApp**.

---

## 17. Day close and cash management

### During the day

On **Day Close** (`Ctrl+W`) you can:

- View live summary: bills, sales, cash / card / UPI totals, expenses, deposits, withdrawals
- **Deposit cash** — cash put into the safe
- **Withdraw cash** — cash taken from the drawer (also Vouchers → **3 Payment**)
- **Download full report** (CSV or Excel)

Cash deposit and withdrawal are only available while the day is open and for **today’s** date.

### End of day — Cash hand-over

1. Go to **Day Close**.
2. Click **Cash Hand Over…**
3. Count cash by denomination.
4. Press **F3** in that window to print a hand-over slip (optional).
5. Enter **actual cash in hand** and click **Close day**.
6. Confirm if the amount differs from expected.

After closing, the header shows **Day: Closed**. No new bills until the next day is opened.

---

## 18. Daily expenses

Open **Expenses** (`Ctrl+E`) or Vouchers → **6 Daily Expense**.

1. Select **Expense date** (business day must be open).
2. Optionally enter **Supplier**, **Supplier GSTIN** (state code is derived), category, supplier invoice no / date.
3. Enter **Amount before/including GST**.
4. Choose **GST treatment:** No GST, Inclusive, or Exclusive; GST rate %; **Supply type** (intra / inter) when GST applies.
5. Enter a **description**.
6. Tick **payment legs** (cash, card, UPI, etc.) so amounts **balance to the total**.
7. Click **Save**.

The tax summary line shows taxable, CGST/SGST or IGST, and total. **Clear** / **F2** resets the form.

---

## 19. Dashboard

Dashboard (`Ctrl+D`) is the store operations hub. Filter by **POS counter** at the top.

### Overview

- Today and last-7-days bill counts
- COD balance, pending credit, pending sync — with links to Online Sales / Credit Bills
- Product cache status
- **Inventory search** — SKU, barcode, or name; in-stock / out-of-stock filter; store price
- **Download count template** / **Import physical counts** — Excel physical inventory (preview, then apply). Use **Adjust stock** on a row for a single SKU correction with a reason.
- Stock exception shortcuts
- Recent bills

### Day close tab

- Tender breakdown: cash, card, UPI, credit notes, returns, expenses
- Store-wide rollup across counters
- Returns, invoices, and stock exceptions (with approve) for the selected day
- Export CSV / Excel

### Salesman tab

Salesman-wise totals for a date range, drill-down to bills, export.

### Stock sales tab

Brand-wise or product-wise sales compared with available quantity.

### All bills tab

Search by invoice number, customer, or date range; download; **Open** a bill for details, payments, and linked return / adjustment.

### Bill margin tab

Margin analysis (cost vs selling) per bill; Excel export.

---

## 20. Reports

Open **Reports** in the menu (same access as Customer billing report). Four tabs:

| Tab | What it shows |
|-----|----------------|
| **Customer Billing** | Customers in a date range: bills, gross, returns, net, payment mix. Filter by counter or customer search. Download Excel. |
| **Supplier-wise** | Posted sales grouped by supplier; expand a supplier for SKU lines with returns netted. Date range and counter filter. Download Excel. |
| **Fast sellers** | SKUs ranked by net qty / amount for the dates. **Low stock** uses each product’s MOQ / min / reorder, or the store **Low-stock match qty** (Settings, default 5). |
| **Going out of stock** | SKUs at or below the threshold. Filter Critical / Low. Same match-qty setting. |

Choose dates (and counter where offered) → **Search** / **Refresh**.

---

## 21. Analytics and ledger

### Analytics (`Ctrl+Y`)

14-day daily sales trend: bill count and revenue.

### Ledger (`Ctrl+L`)

Recent bills and payments with quick duplicate reprint.

---

## 22. Online COD orders

Tracks Cash-on-Delivery orders where payment was **not** taken at billing.

1. Go to **Online Sales** (`Ctrl+O`), Dashboard COD chip, or Vouchers → **COD Receipt…**
2. Summary chips: balance till, pending count, received today.
3. Search by bill number, customer, status, or date range.
4. For a pending order: **Record payment** → mode and transaction number.

Only pending orders can receive payment. Duplicate payments are blocked.

---

## 23. Barcode label printing

1. Go to **Barcodes** (`Ctrl+B`).
2. Type a **SKU** in the blank row and press **Enter**, or **F6** for the product pick list.
3. Set **print quantity** per line (entering the same SKU again increases quantity).
4. **F5** to print → preview → choose the label printer.
5. **F7** to clear the screen.

Products must be synced from central. Label layout comes from the design synced from central.

---

## 24. Settings and sync

**Settings** (`Ctrl+,`) is available on **POS 1**. Organised in four tabs.

### 1. Sync

| Action | Purpose |
|--------|---------|
| **Central Online mode** | Store-wide: all counters use the central API (no ZeroTier / parent Mongo). **Save mode** after changing. |
| **Login / Logout** | Authenticate this counter with the central API. |
| **Run sync** | Push local bills/events; pull products, stock, schemes, users, salesmen; reload company header/footer (name, GSTIN, bank). |
| **Re-sync products** | Full catalog refresh. Use after major price or catalog changes. |
| **Refresh** | Outbox count, cursors, last error, Central / Mongo health. |

Run sync at the start of each day. Prefer **Re-sync products** when many prices or image notes look wrong.

When store price is unset or zero in central, the local catalog uses **selling price** after sync.

### 2. Printing

Configure store header (name, address, GSTIN, FSSAI, phone, website, terms, policy lines) and **bank details** for A4 Tax Invoice (account holder, number, IFSC, branch).

Print formats for normal invoices:

- **Thermal receipt (80 mm)**
- **A4 retail invoice** (optional pre-printed Bilal stationery + alignment editor)
- **A4 commercial invoice**
- **A4 Tax Invoice**
- **A5 tax invoice** (optional pre-printed stationery + alignment editor)

**Credit receipt format** (Bill on credit / Credit Bills): Thermal or A4 — separate from the normal invoice format.

Optional: **Always use Windows print dialog**; **Also print thermal receipt first (80 mm)** with A4/A5.

Printer **queues are per PC**. Store-wide formats, layouts, and Tax Invoice bank details sync to central on **Save**. Company header/footer also reload from the company profile on **Run sync**.

Click **Save receipt settings** after changes. **Refresh printers** if the queue list is empty.

### 3. WhatsApp

- Connection status
- **Auto-send bill on post** when customer phone is present
- **Send test WhatsApp bill** to a test mobile (PDF or thermal image, matching the selected format)

Customer promotion broadcasts are sent from the **Customers** page, not this tab.

### 4. App & billing

| Setting | Description |
|---------|-------------|
| Show left sidebar menu | Persistent left nav vs header chips |
| Allow duplicate bill print | F11 / Duplicate / Ledger |
| Confirm when the same product is added again | Ask before increasing quantity |
| Allow cash payout of credit note remaining balance | Payment dialog CN cash-out |
| Allow multiple returns per bill | Partial returns across transactions |
| Alteration amount is GST inclusive | Off = flat charge after merchandise tax |
| Enable GSTIN lookup | Search GST fills customer from GST API (Central Online) |
| Selling price GST mode | With GST (inclusive) or Without GST (exclusive + GST added) |
| Line item columns | Minimal / Standard / Full |
| Low-stock match qty | Shared by Fast sellers and Going out of stock (default 5; 0 disables fallback) |
| Enable card charges | % and/or flat ₹ on Card / Split-with-card |
| Credit billing options | Enable, require credit customer, min advance, zero advance, partial collection, max balance |
| Razorpay POS | Device username, app key, API URL, device ID |
| **Counter screen access** | Tick which POS counters may open each screen. POS 1 always keeps Settings. Save billing settings so all counters inherit the matrix. |

---

## 25. Keyboard shortcuts

Press **F1** on any screen for this list in-app.

### Go To (navigation)

| Key | Opens |
|-----|--------|
| **Ctrl+G** | Show / hide Go To menu (or sidebar) |
| **Ctrl+A** | Billing |
| **Ctrl+V** | Vouchers |
| **Ctrl+Q** | Quotations |
| **Ctrl+B** | Barcodes |
| **Ctrl+R** | Returns |
| **Ctrl+T** | Adjustments |
| **Ctrl+J** | Duplicate |
| **Ctrl+O** | Online Sales |
| **Ctrl+I** | Credit Bills |
| **Ctrl+U** | Customers |
| **Ctrl+M** | Salesman |
| **Ctrl+D** | Dashboard |
| **Ctrl+Y** | Analytics |
| **Ctrl+L** | Ledger |
| **Ctrl+K** | Bill Lookup |
| **Ctrl+W** | Day Close |
| **Ctrl+E** | Expenses |
| **Ctrl+,** | Settings |

### Vouchers page

| Key | Action |
|-----|--------|
| **1–6** | Select voucher type |
| **Enter** | Open selected |

### Actions

| Key | Action | Notes |
|-----|--------|-------|
| **F1** | Shortcuts help | Any screen |
| **F2 / Ctrl+N** | New / clear / refresh | Page-aware |
| **F3 / Ctrl+F** | Focus search | Product on Billing; SKU on Barcodes; exchange on Returns |
| **F4 / Ctrl+S** | Save customer | Customers page |
| **F8 / Ctrl+H** | Hold bill | Billing |
| **F9 / Ctrl+Enter** | Post bill | Billing |
| **F10 / Ctrl+P** | Print preview | Without posting |
| **F11** | Duplicate page | Any screen |
| **F12** | Close application | |
| **Escape** | Close Go To / reset customer form | Context-dependent |

### Barcodes

| Key | Action |
|-----|--------|
| **F5** | Print labels |
| **F6** | Product pick list |
| **F7** | Clear screen |

### Cash hand-over window

| Key | Action |
|-----|--------|
| **F3** | Print hand-over slip |
| **F12** | Exit window |

### Login

| Key | Action |
|-----|--------|
| **Enter** | Submit login |

---

## 26. Which screens a counter can open

Each computer is a **till** with a POS counter number (`POS_COUNTER` in configuration). **POS 1** is the manager till.

Default access (until the manager changes the matrix):

| Feature | Default counters |
|---------|------------------|
| Billing, Vouchers, Quotations, Barcodes, Customers, Salesman, Returns, Bill Lookup, Outbound Dispatch, Day Close, Duplicate, Adjustments | 1, 2, 3 |
| Dashboard, Analytics, Reports, Online Sales, Credit Bills, Ledger, Expenses, Settings | **1 only** |

The manager edits this on POS 1 → Settings → **4. App & billing** → **Counter screen access**. After **Save**, every till uses the same matrix.

**Settings** cannot be removed from POS 1.

Secondary tills share store data (local network Mongo, or Central Online) but keep their own device identity and printer queues.

If a page is missing from the menu, it is blocked for this counter — not a failed install.

---

## 27. Troubleshooting

### Product not found when scanning or typing a code

- Ask the manager to **Run sync**.
- If many items are missing or prices look old, use **Re-sync products**.
- Confirm the SKU/barcode exists in the central catalog.
- For new labels, print from Barcodes, assign the code in central, then sync again.

### Store price looks wrong (zero or outdated)

- **Re-sync products** on POS 1.
- After sync, store rate follows **selling price** when store price is unset or zero.

### Image note column is empty

- Central has no image description yet, or products have not been synced since it was added. Billing still works.

### GSTIN Search GST fails or the button is missing

- Settings → enable **GSTIN lookup**.
- Store must be in **Central Online** mode.
- Enter a full 15-character GSTIN.
- Check the **Central** header chip is online.

### Cannot post bill — “Day not open”

- Day Close → **Open day** with opening cash. Only today’s date can be opened.

### Cannot post bill — discount limit

- Combined item and cash discount exceeds your user limit. Reduce the discount or ask a manager.

### Stock shortfall warning at post

- **Post anyway** completes the sale and creates a stock indent, or **Cancel** to change the bill.

### Held bill resumed with “today’s” date

- This is expected. Invoice date is the **resume / post** day, not the original hold day.

### Pending notifications (bell)

- Click the bell → **Sync now**.
- Confirm Settings central login and that the network is up (or Central Online is healthy).

### Printer not working

- Settings → **2. Printing** → **Refresh printers** → select the queue → Save.
- For thermal receipts, set the 80 mm printer as the thermal queue.

### WhatsApp bill not sent

- Settings → **3. WhatsApp** connection status.
- Customer phone must be a valid 10-digit number.
- Enable auto-send if you expect automatic delivery after post.

### WhatsApp broadcast failed

- Confirm WhatsApp is connected and the marketing template is approved.
- Select at least one customer with a valid mobile.

### Login failed

- Check email and password with your manager.
- Confirm users were synced (manager runs sync from Settings).

### A screen is missing from the menu

- That counter is not ticked in **Counter screen access**. Use POS 1 (or a till that has that screen) for Dashboard, Settings, Expenses, Credit Bills, Online Sales, Ledger, Analytics, and Reports unless access was granted.

### Central chip shows Offline / Mongo error

- In Central Online mode, restore internet and central login.
- In local mode, check the Mongo service / ZeroTier parent connection. Ask support if the Mongo chip stays unhealthy.

---

## 28. Quick reference — daily checklist

### Opening (manager / POS 1)

- [ ] Log in
- [ ] Settings → Central login → confirm mode → **Run sync**
- [ ] After major catalog/price updates: **Re-sync products**
- [ ] Day Close → **Open day** with opening cash
- [ ] Header shows **Day: Open** and Central/Mongo healthy

### During the day (allowed tills)

- [ ] Billing: phone → GSTIN if B2B (**Search GST**) → salesman → items → discounts → **F9 Post** → payment
- [ ] **F8 Hold** / **Resume held** when the customer is not ready
- [ ] Quotations when quoting; convert open quotes to Billing when ready
- [ ] Returns, adjustments, Bill Lookup, outbound dispatch as needed
- [ ] Record cash deposits / withdrawals on Day Close
- [ ] Collect Credit Bills and Online COD when customers pay

### Closing (manager / POS 1)

- [ ] Pending Credit Bills / Online COD as needed
- [ ] Remaining **Expenses**
- [ ] Dashboard day-close summary / Reports if required
- [ ] Day Close → **Cash Hand Over** → count cash → **Close day**
- [ ] **Download full report**
- [ ] Notification bell clear (or sync before logout)
- [ ] **Sign out**

---

*TruBilling — User Manual v3.0 · 17 September 2026*
