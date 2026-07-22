# TruBilling — User Manual

**Application:** TruBilling  
**Version:** 2.0  
**Last updated:** 16 July 2026

---

## About this manual

This guide is for **cashiers**, **store managers**, and **support staff** who use the **TruBilling** desktop application at the retail counter. It explains how to perform everyday tasks: billing, quotations, returns, day close, printing, and sync.

For a technical feature reference (configuration, MongoDB collections, API sync details), see [RR-Bridal-Store-Billing-WPF-Feature-Guide.html](./RR-Bridal-Store-Billing-WPF-Feature-Guide.html).

**PDF / Word copies:** This Markdown file is the source of truth. Printable companions: [`RR-Bridal-Store-Billing-User-Manual.html`](./RR-Bridal-Store-Billing-User-Manual.html) / `.pdf` (and matching `TruBilling-User-Manual.*` copies). Re-save `.docx` from the HTML in Microsoft Word if you need an updated Word file.

---

## Table of contents

1. [Introduction](#1-introduction)
2. [Logging in](#2-logging-in)
3. [Main screen layout](#3-main-screen-layout)
4. [Starting your day](#4-starting-your-day)
5. [Billing (POS)](#5-billing-pos)
6. [Quotations](#6-quotations)
7. [Customer registration](#7-customer-registration)
8. [Salesman registration](#8-salesman-registration)
9. [Credit bills](#9-credit-bills)
10. [Bill lookup](#10-bill-lookup)
11. [Sale returns and exchanges](#11-sale-returns-and-exchanges)
12. [Bill adjustments](#12-bill-adjustments)
13. [Duplicate print and WhatsApp](#13-duplicate-print-and-whatsapp)
14. [Day close and cash management](#14-day-close-and-cash-management)
15. [Daily expenses](#15-daily-expenses)
16. [Dashboard and reports](#16-dashboard-and-reports)
17. [Online COD orders](#17-online-cod-orders)
18. [Barcode label printing](#18-barcode-label-printing)
19. [Settings and sync](#19-settings-and-sync)
20. [Keyboard shortcuts](#20-keyboard-shortcuts)
21. [Primary vs secondary till](#21-primary-vs-secondary-till)
22. [Troubleshooting](#22-troubleshooting)
23. [Quick reference — daily checklist](#23-quick-reference--daily-checklist)

---

## 1. Introduction

**TruBilling** is a **point-of-sale (POS)** application for retail counters. It works **offline-first**: bills are saved on the local computer and synced to the central office system when the internet is available.

### What you can do

| Task | Where |
|------|-------|
| Create and post sales bills | Billing |
| Hold bills and resume later | Billing |
| Create quotations and convert to bills | Quotations |
| Register customers | Customers |
| Register salesmen | Salesman |
| Find a posted bill (view / return / adjust) | Bill Lookup |
| Collect credit (pay-later) balances | Credit Bills (primary till) |
| Process returns and exchanges | Returns |
| Correct posted bills | Adjustments |
| Reprint invoices | Duplicate |
| Open/close business day | Day Close |
| Record expenses | Expenses (primary till) |
| View sales and inventory | Dashboard (primary till) |
| Track online COD payments | Online Sales (primary till) |
| Print barcode labels | Barcodes |
| Configure printers and sync | Settings (primary till) |

---

## 2. Logging in

1. Open **TruBilling** on your till computer.
2. The login screen shows the store name and till number.
3. Enter your **email** and **password** (provided by your manager).
4. Click **Login** or press **Enter**.

**Notes:**

- Credentials are validated against users synced from the central system.
- Only one person can be logged in on the same till at a time.
- If you see a sync warning banner, ask the manager to run sync from Settings before billing.
- After login, the app opens on the **Billing** screen.

To sign out, click the **logout icon** in the top-right header or choose **Logout** from the navigation menu.

---

## 3. Main screen layout

```
┌─────────────────────────────────────────────────────────────────┐
│  ☰  Logo   Store name          Page title        🔍 Search …   │
│              Till line           Day status  User  🔔  ⚙       │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│                     Current page content                        │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│  F1 Help  F2 New  F3 Search  F8 Hold  F9 Post  F10 Print …     │
└─────────────────────────────────────────────────────────────────┘
```

| Area | Purpose |
|------|---------|
| **☰ Menu** | Opens the navigation drawer with all pages |
| **Global search** | Scan or type a product code; on Billing, adds items to the bill |
| **Day status chip** | Shows whether the business day is open or closed; click to go to Day Close |
| **Notification bell** | Shows pending items waiting to sync; click to view and sync |
| **Settings gear** | Opens Settings (primary till only) |
| **Footer shortcuts** | Quick access to F1–F12 keys |

Press **Escape** to close the navigation drawer. **F2 New** clears or resets the **current** page (not only Billing).

---

## 4. Starting your day

Complete these steps **before** posting any bills.

### Step 1 — Sync product data (primary till / manager)

1. Open **Settings** (gear icon or navigation menu).
2. Go to **Connection & sync**.
3. Log in with your central office credentials if not already logged in.
4. Click **Run sync**.

Sync downloads products, stock, promotion schemes, salesmen, users, and related masters. Without sync, product lookup and barcode scanning will not work.

After a large catalog or price update in central, also click **Re-sync products** (see [Settings and sync](#19-settings-and-sync)).

### Step 2 — Open the business day

1. Go to **Day Close** (navigation menu or click the day status chip).
2. Confirm today's date is selected.
3. Enter the **opening cash** amount in the drawer.
4. Click **Open day**.

The day status chip in the header should now show **Day: Open**. Bills, returns, and expenses cannot be posted until the day is open.

---

## 5. Billing (POS)

Billing is the main screen for creating sales invoices.

### 5.1 Customer details

| Field | Description |
|-------|-------------|
| **Phone** | 10-digit mobile number (required to post). The field blinks until a valid number is entered. |
| **Customer name** | Required to post the bill |
| **Customer code** | Filled automatically when a customer is found |
| **Salesman** | Pick from registered salesmen (code + name). Required when at least one active salesman exists in the store master. |

**Finding a customer:**

1. Type the phone number and press **Tab** or **Enter** — the app searches automatically.
2. Or click **Find Customer** to open the search dialog.
3. If the phone is unknown, a quick-capture dialog asks for the customer name.

**Bill options (checkboxes):**

| Option | When to use |
|--------|-------------|
| **Hold bills** | Mark as a hold-type bill |
| **Door delivery** | Customer wants home delivery |
| **Online COD** | Online order — payment collected later (no payment at post) |
| **Stitching** | Shows a delivery date picker |
| **Print invoice** | Print automatically after posting |

### 5.2 Adding products

Use the **last row** of the line-items grid:

1. Type the **SKU or barcode** in the Product code column and press **Enter** — the item is added.
2. Or type a **product name** — a pick list opens; select the correct item.
3. Or click **Add manual** to enter a product code not in the catalog.
4. Use **F3** to jump focus to the product entry row.

You can also use the **global search box** in the header: enter a code and press **Enter**.

**Product pick list columns:**

- Always: SKU, Name, Rate, **Image note** (short description from central for the product image, when set)
- With Standard/Full detail: GST %, MRP, Stock, and (Full) HSN

If **Image note** is blank, central has no image description yet — billing still works normally.

**Editing lines:**

- Change quantity, rate, description, HSN, MRP, or tax % directly in the grid.
- Click the trash icon to remove a line.
- Use **Tab** to move between cells.

**Column display:** Managers can set Minimal, Standard, or Full columns in Settings → Other.

**Store rate:** The rate used at the counter comes from the local catalog. If central has no store price (or it is zero), the app uses the central selling price after sync.

### 5.3 Discounts and totals

The **invoice preview panel** on the right shows live totals:

- Subtotal, GST, item discount %, cash discount
- Applied promotion schemes (click to remove)
- Round off
- **Inter-state (IGST)** toggle for out-of-state customers
- Customer credit notes (toggle to apply)
- Credit / pay-later advance when credit billing is enabled by the manager

The **Payable** amount is what the customer owes now.

### 5.4 Hold a bill (F8)

Use when the customer is not ready to pay yet:

1. Add at least one line item.
2. Press **F8** or use the footer shortcut.

The bill is saved as a draft with a hold number. Stock is **not** deducted. To resume later, click **Resume held** on the Billing screen.

### 5.5 Post a bill (F9)

**Before posting, confirm:**

- Customer name is filled in
- Phone is a valid 10-digit number
- Salesman is selected if required
- At least one line has quantity × rate greater than zero
- Business day is **open**
- Your discount does not exceed your allowed limit

**To post:**

1. Press **F9**.
2. If stock is short, a warning appears — choose **Post anyway** (creates an indent request) or **Cancel**.
3. The **Payment** dialog opens (unless Online COD is checked):
   - **Cash**
   - **Card** (Pine Labs)
   - **UPI** (Razorpay)
   - **Credit note**
   - **Split** (multiple modes)
   - **Credit / advance** when pay-later billing is enabled
4. Confirm payment.

After a successful post:

- Bill number is assigned
- Stock is reduced
- Invoice may print and/or send via WhatsApp (if configured)
- Screen clears for the next bill

Press **F2** anytime to start a **new bill** (clears the current draft).

### 5.6 Print without posting (F10)

Press **F10** to preview or print the current draft **without** saving it as a posted bill. Useful for customer review before post.

Press **F1** on the Billing screen for an in-app quick reference.

---

## 6. Quotations

Use quotations when the customer wants a formal quote before buying.

1. Go to **Quotations** in the menu.
2. Click **Create** (or use **F2** on this page) to open the quotation editor.
3. Enter customer and products similar to Billing.
4. **Save** the quotation (it is not a posted bill and does not deduct stock).
5. Later, from the quotations list: **Open** to edit, **Cancel** unused quotes, or **Convert to bill** for an open quotation — this loads lines into Billing for payment and post.

---

## 7. Customer registration

Use when a new customer needs a full profile before billing.

1. Go to **Customers** (navigation menu).
2. Enter **Customer name** and **Mobile no.** (required).
3. Optionally mark **Credit customer** if the store allows pay-later for that customer.
4. Click **Save customer** or press **F4**.
5. A success dialog appears — choose to apply the customer to the current bill or register another.

Press **Escape** to cancel and return to Billing.

Customer codes are generated automatically. Data syncs to the central system.

---

## 8. Salesman registration

Salesmen appear in the Billing salesman dropdown.

1. Go to **Salesman** in the menu.
2. Enter salesman details and save.
3. Return to Billing and select the salesman on the bill.

Salesmen are also updated when the manager runs sync from central.

---

## 9. Credit bills

**Primary till only.**

Use when a pay-later (credit) sale still has a balance.

1. Go to **Credit Bills**.
2. Search by customer, bill number, status (pending / partial / settled), or date.
3. Select a bill and **Record payment** (cash, card, UPI, credit note, or split, as allowed by Settings).
4. Print a credit / balance receipt if needed.

Managers configure credit rules in Settings → Other (minimum advance, whether zero advance is allowed, etc.).

---

## 10. Bill lookup

Find any posted bill from one screen:

1. Go to **Bill Lookup**.
2. Search by invoice number, customer name, or mobile.
3. Open the bill to **view** details, start a **return**, start an **adjustment**, **duplicate print**, or print a **credit note**.

Available on all tills.

---

## 11. Sale returns and exchanges

1. Go to **Returns** (or start a return from **Bill Lookup**).
2. Enter the original **bill number** (full number or last 3–4 digits) and click **Load Bill**.
3. If multiple bills match, pick the correct one from the list.
4. Select return lines and enter **return quantity** for each.
5. Optionally add **exchange items** using the header search box.
6. Choose **Return mode:**
   - **Credit note** — customer gets store credit
   - **Cash refund** — cash returned (confirmation required)
7. Enter a **reason**.
8. Click **Post Return / Exchange**.

**Settings (Settings → Other):**

- **Allow multiple returns per bill (partial returns)** — when **off** (default), only one return transaction is allowed per posted bill.
- When **on**, you may return remaining quantity in separate transactions. Lines already fully returned in a prior transaction show a disabled **Select** checkbox and a **Prev Ret** column with the quantity already returned.

**Rules:**

- With multiple returns **disabled**, only one return is allowed per posted bill.
- With multiple returns **enabled**, each line tracks cumulative returned quantity; you cannot return more than the original bill quantity.
- Business day must be open.
- If exchange items cost more than the return, a payment dialog collects the difference.
- If exchange items cost less, credit balance is issued per the return mode.

---

## 12. Bill adjustments

Use to correct quantity or rate on an already-posted bill.

1. Go to **Adjustments** (or start from **Bill Lookup**).
2. Enter the bill number and click **Lookup**.
3. Edit **adjusted quantity** and **adjusted rate** per line.
4. Enter a **reason**.
5. Click **Post Adjustment**.

The screen shows the difference in payable amount and tax. Business day must be open.

---

## 13. Duplicate print and WhatsApp

Reprint a posted invoice or credit note with a **DUPLICATE** watermark.

1. Go to **Duplicate** (or press **F11** from any screen).
2. **Bill tab:** Search by invoice number, customer, or date range → select a bill → **Print duplicate copy** or **Send WhatsApp bill**.
3. **Credit Note tab:** Search and reprint a credit note.

Duplicate printing can be disabled by the manager in Settings → Other.

---

## 14. Day close and cash management

### During the day

On the **Day Close** page you can:

- View live summary: bills, sales, cash/card/UPI totals, expenses, deposits, withdrawals
- **Deposit cash** — record cash put into the safe
- **Withdraw cash** — record cash taken from the drawer
- **Download full report** (CSV or Excel)

Cash deposit and withdrawal are only available while the day is open and for today's date.

### End of day — Cash hand-over

1. Go to **Day Close**.
2. Click **Cash Hand Over…**
3. Count cash by denomination in the hand-over window.
4. Press **F3** to print a hand-over slip (optional).
5. Enter **actual cash in hand** and click **Close day**.
6. Confirm if the amount differs from expected.

After closing, the day status shows **Day: Closed**. No new bills can be posted until the next day is opened.

---

## 15. Daily expenses

**Primary till only.**

1. Go to **Expenses**.
2. Select the date.
3. Enter **description** and **amount**.
4. Click **Post expense**.

Description is required; amount must be greater than zero. Business day must be open.

---

## 16. Dashboard and reports

**Primary till only.**

### Overview tab

- Today's and this week's bill counts
- Pending sync / credit / COD summaries with links
- Product cache status
- Filter by POS counter
- **Inventory search** — find products by SKU, barcode, or name; filter by in-stock / out-of-stock; store price column
- Stock exception shortcuts
- Recent bills list

### Day close tab

- Tender breakdown: cash, card, UPI, credit notes, returns, expenses
- Store-wide rollup across all counters
- Returns, invoices, and stock exceptions (with approve) for the selected day
- Export CSV / Excel

### Salesman tab

- Salesman-wise totals for a date range, drill-down to bills, export

### Stock sales tab

- Brand-wise or product-wise sales compared with available quantity

### All bills tab

- Search bills by invoice number, customer name/mobile, or date range
- Download report
- **Open** a bill to view details, payments, and linked return/adjustment

### Bill margin tab

- Margin analysis (cost vs selling) per bill; Excel export

### Analytics

Shows a **14-day daily sales trend** with total bills and revenue (separate **Analytics** menu page).

### Ledger

Recent bills and payments with quick duplicate reprint (**Ledger** menu page).

---

## 17. Online COD orders

**Primary till only.**

Tracks online Cash-on-Delivery orders where payment was not collected at billing time.

1. Go to **Online Sales**.
2. Summary chips show balance till, pending count, and received today.
3. Search by bill number, customer, status, or date range.
4. For pending orders, click **Record payment** → enter payment mode and transaction number.

Only pending orders can receive payment. Duplicate payments are blocked.

---

## 18. Barcode label printing

1. Go to **Barcodes**.
2. Type a **SKU** in the blank row and press **Enter**, or press **F6** to open the product pick list.
3. Set **print quantity** per line (entering the same SKU again increases quantity).
4. Press **F5** to print → preview window → select label printer.
5. Press **F7** to clear the screen.

Products must be synced from central before labels can be printed. Label layout comes from the design synced from central.

---

## 19. Settings and sync

**Primary till only.** Open via the gear icon or navigation menu.

### Connection & sync

| Action | Purpose |
|--------|---------|
| **Login / Logout** | Authenticate with the central office API |
| **Run sync** | Push local bills/events; pull products, stock, schemes, users, salesmen |
| **Re-sync products** | Full product catalog refresh (resets product cursor and downloads all pages). Use after major central price or catalog changes when normal sync still looks stale |
| **Refresh status** | View outbox count, sync cursors, last error |

Run sync at the start of each day. Prefer **Re-sync products** when many item prices or image notes look wrong after a central update.

When store price is unset or zero in central, the local catalog uses **selling price** as the store rate after sync.

### Receipt & printing

Configure store header (name, address, GSTIN, etc.), printer queues, and print format:

- **Thermal 80 mm** — counter receipt
- **A4 / A5 / A4 Commercial** — tax / office invoice
- **Pre-printed A4 / A5** — values only on branded stationery (alignment editors included)
- **Credit receipt format** — Thermal or A4 (separate from normal invoice format)

Click **Save receipt settings** after changes.

### WhatsApp

- Enable **Auto-send bill on post** when customer phone is present
- Use **Test send** to verify WhatsApp delivery

### Other (manager options cashiers may notice)

| Setting | Description |
|---------|-------------|
| Allow duplicate bill print | Enable/disable F11 and Duplicate page |
| Confirm when adding a product already on the bill | Ask before increasing quantity |
| Allow CN remaining cash payout | Allow cash payout of credit note balance at payment |
| Allow multiple returns per bill | Partial returns across transactions |
| Credit billing options | Enable pay-later, min advance, credit-customer required, max balance |
| Line item column detail level | Minimal / Standard / Full grid columns |
| Razorpay POS | Device credentials for UPI payment (manager configures) |

---

## 20. Keyboard shortcuts

### Global shortcuts

| Key | Action | Notes |
|-----|--------|-------|
| **F1** | Help | Billing screen only |
| **F2** | New / clear current page | Page-aware (Billing, Quotation, Customers, Returns, Bill Lookup, etc.) |
| **F3** | Focus search | Product entry on Billing; SKU on Barcodes; also Returns / Bill Lookup return |
| **F4** | Save customer | Customers page only |
| **F8** | Hold bill | Billing only |
| **F9** | Post bill | Billing only |
| **F10** | Print stub | Preview/print without posting |
| **F11** | Duplicate page | From any screen |
| **F12** | Close application | Exits the app |
| **Escape** | Cancel / close menu | Context-dependent |

### Barcodes page

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

## 21. Primary vs secondary till

Each computer is configured as a **till** with a unique number (`POS_COUNTER` in configuration).

| Feature | Primary till (Counter 1) | Secondary till (Counter 2+) |
|---------|--------------------------|-------------------------------|
| Billing | ✓ | ✓ |
| Quotations | ✓ | ✓ |
| Bill Lookup | ✓ | ✓ |
| Salesman | ✓ | ✓ |
| Customers | ✓ | ✓ |
| Returns & Adjustments | ✓ | ✓ |
| Day Close | ✓ | ✓ |
| Duplicate print | ✓ | ✓ |
| Barcodes | ✓ | ✓ |
| Dashboard | ✓ | ✗ |
| Analytics | ✓ | ✗ |
| Online Sales | ✓ | ✗ |
| Credit Bills | ✓ | ✗ |
| Ledger | ✓ | ✗ |
| Expenses | ✓ | ✗ |
| Settings & sync | ✓ | ✗ |
| Auto-sync | ✓ | ✗ |

Secondary tills share the same store database on the local network but have their own device identity.

---

## 22. Troubleshooting

### Product not found when scanning or typing code

- Ask the manager to **Run sync** from Settings.
- If many items are missing or prices look old after a central update, use **Re-sync products**.
- Confirm the product exists in the central catalog with the correct SKU/barcode.
- For new barcode labels, print from Barcodes page, assign the code in central, then sync again.

### Store price looks wrong (zero or outdated)

- Run **Re-sync products** on the primary till.
- Central often stores the real rate in **selling price**; after sync, the store rate should follow when store price is unset or zero.

### Image note column is empty in product search

- Central has not set an image description for that product, or products have not been synced since it was added.
- Ask the manager to sync / re-sync products. Billing works without Image note.

### Cannot post bill — "Day not open"

- Go to **Day Close** and **Open day** with opening cash.
- Only today's date can be opened.

### Cannot post bill — discount limit

- Your combined item and cash discount exceeds your user limit. Reduce the discount or ask a manager.

### Stock shortfall warning at post

- Choose **Post anyway** to complete the sale (creates a stock indent request), or **Cancel** to adjust the bill.

### Pending notifications (bell icon)

- Click the bell → view pending sync items → **Sync now**.
- Ensure Settings → central login is active and internet is available.

### Printer not working

- Settings → Receipt & printing → **Refresh printers** → select correct queue → Save.
- For thermal receipts, confirm the 80 mm printer is set as the thermal printer.

### WhatsApp bill not sent

- Check Settings → WhatsApp connection status.
- Confirm customer phone is a valid 10-digit number.
- Verify auto-send is enabled if expecting automatic delivery.

### Login failed

- Check email and password with your manager.
- Confirm users were synced (manager runs sync from Settings).

### Secondary till missing pages

- Dashboard, Settings, Expenses, Credit Bills, Online Sales, Ledger, and Analytics are **primary till only**. Use Counter 1 for those tasks.

---

## 23. Quick reference — daily checklist

### Opening (manager / primary till)

- [ ] Log in
- [ ] Settings → Login to central → **Run sync**
- [ ] After major central catalog/price updates: **Re-sync products**
- [ ] Day Close → **Open day** with opening cash
- [ ] Verify day status shows **Open**

### During the day (all tills)

- [ ] Billing: phone → salesman → items → discounts → **F9 Post** → payment
- [ ] Use Quotations when quoting before sale; convert open quotes to Billing when ready
- [ ] Process returns, adjustments, and Bill Lookup as needed
- [ ] Record cash deposits/withdrawals on Day Close page

### Closing (manager / primary till)

- [ ] Collect pending **Credit Bills** / Online COD payments as needed
- [ ] Record any remaining **Expenses**
- [ ] Review Dashboard day-close summary
- [ ] Day Close → **Cash Hand Over** → count cash → **Close day**
- [ ] **Download full report**
- [ ] Confirm notifications bell shows no pending sync (or sync before logout)
- [ ] **Logout**

---

*TruBilling — User Manual v2.0*
