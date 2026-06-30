# TruBilling — User Manual

**Application:** TruBilling  
**Version:** 1.0  
**Last updated:** 19 June 2026

---

## About this manual

This guide is for **cashiers**, **store managers**, and **support staff** who use the **TruBilling** desktop application at the retail counter. It explains how to perform everyday tasks: billing, returns, day close, printing, and sync.

For a technical feature reference (configuration, MongoDB collections, API sync details), see [RR-Bridal-Store-Billing-WPF-Feature-Guide.html](./RR-Bridal-Store-Billing-WPF-Feature-Guide.html).

---

## Table of contents

1. [Introduction](#1-introduction)
2. [Logging in](#2-logging-in)
3. [Main screen layout](#3-main-screen-layout)
4. [Starting your day](#4-starting-your-day)
5. [Billing (POS)](#5-billing-pos)
6. [Customer registration](#6-customer-registration)
7. [Sale returns and exchanges](#7-sale-returns-and-exchanges)
8. [Bill adjustments](#8-bill-adjustments)
9. [Duplicate print and WhatsApp](#9-duplicate-print-and-whatsapp)
10. [Day close and cash management](#10-day-close-and-cash-management)
11. [Daily expenses](#11-daily-expenses)
12. [Dashboard and reports](#12-dashboard-and-reports)
13. [Online COD orders](#13-online-cod-orders)
14. [Barcode label printing](#14-barcode-label-printing)
15. [Settings and sync](#15-settings-and-sync)
16. [Keyboard shortcuts](#16-keyboard-shortcuts)
17. [Primary vs secondary till](#17-primary-vs-secondary-till)
18. [Troubleshooting](#18-troubleshooting)
19. [Quick reference — daily checklist](#19-quick-reference--daily-checklist)

---

## 1. Introduction

**TruBilling** is a **point-of-sale (POS)** application for retail counters. It works **offline-first**: bills are saved on the local computer and synced to the central office system when the internet is available.

### What you can do

| Task | Where |
|------|-------|
| Create and post sales bills | Billing |
| Hold bills and resume later | Billing |
| Register customers | Customers |
| Register salesmen | Salesmen |
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

Press **Escape** to close the navigation drawer.

---

## 4. Starting your day

Complete these steps **before** posting any bills.

### Step 1 — Sync product data (primary till / manager)

1. Open **Settings** (gear icon or navigation menu).
2. Go to **Connection & sync**.
3. Log in with your central office credentials if not already logged in.
4. Click **Run sync**.

Sync downloads products, stock, promotion schemes, and user accounts. Without sync, product lookup and barcode scanning will not work.

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

**Editing lines:**

- Change quantity, rate, description, HSN, MRP, or tax % directly in the grid.
- Click the trash icon to remove a line.
- Use **Tab** to move between cells.

**Column display:** Managers can set Minimal, Standard, or Full columns in Settings → Other.

### 5.3 Discounts and totals

The **invoice preview panel** on the right shows live totals:

- Subtotal, GST, item discount %, cash discount
- Applied promotion schemes (click to remove)
- Round off
- **Inter-state (IGST)** toggle for out-of-state customers
- Customer credit notes (toggle to apply)

The **Payable** amount is what the customer owes.

### 5.4 Hold a bill (F8)

Use when the customer is not ready to pay yet:

1. Add at least one line item.
2. Press **F8** or use the footer shortcut.

The bill is saved as a draft with a hold number. Stock is **not** deducted. To resume later, click **Resume held** on the Billing screen.

### 5.5 Post a bill (F9)

**Before posting, confirm:**

- Customer name is filled in
- Phone is a valid 10-digit number
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
4. Confirm payment.

After a successful post:

- Bill number is assigned
- Stock is reduced
- Invoice may print and/or send via WhatsApp (if configured)
- Screen clears for the next bill

Press **F2** anytime to start a **new bill** (clears the current draft).

### 5.6 Print without posting (F10)

Press **F10** to preview or print the current draft **without** saving it as a posted bill. Useful for quotations or customer review.

Press **F1** on the Billing screen for an in-app quick reference.

---

## 6. Customer registration

Use when a new customer needs a full profile before billing.

1. Go to **Customers** (navigation menu).
2. Enter **Customer name** and **Mobile no.** (required).
3. Click **Save customer** or press **F4**.
4. A success dialog appears — choose to apply the customer to the current bill or register another.

Press **Escape** to cancel and return to Billing.

Customer codes are generated automatically. Data syncs to the central system.

---

## 7. Sale returns and exchanges

1. Go to **Returns**.
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

## 8. Bill adjustments

Use to correct quantity or rate on an already-posted bill.

1. Go to **Adjustments**.
2. Enter the bill number and click **Lookup**.
3. Edit **adjusted quantity** and **adjusted rate** per line.
4. Enter a **reason**.
5. Click **Post Adjustment**.

The screen shows the difference in payable amount and tax. Business day must be open.

---

## 9. Duplicate print and WhatsApp

Reprint a posted invoice or credit note with a **DUPLICATE** watermark.

1. Go to **Duplicate** (or press **F11** from any screen).
2. **Bill tab:** Search by invoice number, customer, or date range → select a bill → **Print duplicate copy** or **Send WhatsApp bill**.
3. **Credit Note tab:** Search and reprint a credit note.

Duplicate printing can be disabled by the manager in Settings → Other.

---

## 10. Day close and cash management

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

## 11. Daily expenses

**Primary till only.**

1. Go to **Expenses**.
2. Select the date.
3. Enter **description** and **amount**.
4. Click **Post expense**.

Description is required; amount must be greater than zero. Business day must be open.

---

## 12. Dashboard and reports

**Primary till only.**

### Overview tab

- Today's and this week's bill counts
- Pending sync count
- Product cache status
- Online COD balance
- Filter by POS counter
- **Inventory search** — find products by SKU, barcode, or name; filter by in-stock / out-of-stock
- **Approve** stock exceptions (shortfalls at post)
- Recent bills list

### Day close tab

- Tender breakdown: cash, card, UPI, credit notes, returns, expenses
- Store-wide rollup across all counters
- Returns, invoices, and stock exceptions for the selected day

### All bills tab

- Search bills by invoice number, customer name/mobile, or date range
- Download report
- **Open** a bill to view details, payments, and linked return/adjustment

### Analytics

Shows a **14-day daily sales trend** with total bills and revenue.

### Ledger

Recent bills and payments with quick duplicate reprint.

---

## 13. Online COD orders

**Primary till only.**

Tracks online Cash-on-Delivery orders where payment was not collected at billing time.

1. Go to **Online Sales**.
2. Summary chips show balance till, pending count, and received today.
3. Search by bill number, customer, status, or date range.
4. For pending orders, click **Record payment** → enter payment mode and transaction number.

Only pending orders can receive payment. Duplicate payments are blocked.

---

## 14. Barcode label printing

1. Go to **Barcodes**.
2. Type a **SKU** in the blank row and press **Enter**, or press **F6** to open the product pick list.
3. Set **print quantity** per line (entering the same SKU again increases quantity).
4. Press **F5** to print → preview window → select label printer.
5. Press **F7** to clear the screen.

Products must be synced from central before labels can be printed.

---

## 15. Settings and sync

**Primary till only.** Open via the gear icon or navigation menu.

### Connection & sync

| Action | Purpose |
|--------|---------|
| **Login / Logout** | Authenticate with the central office API |
| **Run sync** | Push local bills/events; pull products, stock, schemes, users |
| **Refresh status** | View outbox count, sync cursors, last error |

Run sync at the start of each day and whenever products or stock seem outdated.

### Receipt & printing

Configure store header (name, address, GSTIN, etc.), printer queues, and print format:

- **Thermal 80 mm** — counter receipt
- **A4 / A5** — tax invoice
- **Pre-printed A5** — values only on branded stationery (alignment editor included)

Click **Save receipt settings** after changes.

### WhatsApp

- Enable **Auto-send bill on post** when customer phone is present
- Use **Test send** to verify WhatsApp delivery

### Other

| Setting | Description |
|---------|-------------|
| Allow duplicate bill print | Enable/disable F11 and Duplicate page |
| Allow CN remaining cash payout | Allow cash payout of credit note balance at payment |
| Line item column detail level | Minimal / Standard / Full grid columns |

---

## 16. Keyboard shortcuts

### Global shortcuts

| Key | Action | Notes |
|-----|--------|-------|
| **F1** | Help | Billing screen only |
| **F2** | New bill | Clears current draft |
| **F3** | Focus search | Product entry row on Billing; SKU row on Barcodes |
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

## 17. Primary vs secondary till

Each computer is configured as a **till** with a unique number (`POS_COUNTER` in configuration).

| Feature | Primary till (Counter 1) | Secondary till (Counter 2+) |
|---------|--------------------------|-------------------------------|
| Billing | ✓ | ✓ |
| Returns & Adjustments | ✓ | ✓ |
| Day Close | ✓ | ✓ |
| Duplicate print | ✓ | ✓ |
| Barcodes | ✓ | ✓ |
| Customers | ✓ | ✓ |
| Dashboard | ✓ | ✗ |
| Analytics | ✓ | ✗ |
| Online Sales | ✓ | ✗ |
| Ledger | ✓ | ✗ |
| Expenses | ✓ | ✗ |
| Settings & sync | ✓ | ✗ |
| Auto-sync | ✓ | ✗ |

Secondary tills share the same store database on the local network but have their own device identity.

---

## 18. Troubleshooting

### Product not found when scanning or typing code

- Ask the manager to **Run sync** from Settings.
- Confirm the product exists in the central catalog with the correct SKU/barcode.
- For new barcode labels, print from Barcodes page, assign the code in central, then sync again.

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

- Dashboard, Settings, Expenses, and similar pages are **primary till only**. Use Counter 1 for those tasks.

---

## 19. Quick reference — daily checklist

### Opening (manager / primary till)

- [ ] Log in
- [ ] Settings → Login to central → **Run sync**
- [ ] Day Close → **Open day** with opening cash
- [ ] Verify day status shows **Open**

### During the day (all tills)

- [ ] Billing: phone → items → discounts → **F9 Post** → payment
- [ ] Process returns and adjustments as needed
- [ ] Record cash deposits/withdrawals on Day Close page

### Closing (manager / primary till)

- [ ] Record any remaining **Expenses**
- [ ] Review Dashboard day-close summary
- [ ] Day Close → **Cash Hand Over** → count cash → **Close day**
- [ ] **Download full report**
- [ ] Confirm notifications bell shows no pending sync (or sync before logout)
- [ ] **Logout**

---

*TruBilling — User Manual v1.0*
