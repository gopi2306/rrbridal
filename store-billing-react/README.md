# TruBilling React (Online POS)

Online-only browser POS that mirrors the WPF store billing UI and calls the **same central APIs** WPF Online uses:

- `/api/auth/login`
- `/api/store-pos/*`
- `/api/dashboard/*`, `/api/inventory/*`
- `/api/customers`, `/api/salesmen`, stores, barcode designs

**No local Mongo. No `/api/pos-v2`. WPF app is unchanged.**

## Stack

- Vite + React 19 + TypeScript
- React Router
- Fetch client → `VITE_API_BASE` (same origin style as WPF `CENTRAL_API_BASE`)

## Setup

```bash
# central-backend must be running (store-pos module)
cd central-backend
npm run start:dev

# UI
cd store-billing-react/ui
cp .env.example .env   # VITE_API_BASE=http://localhost:3000
npm install
npm run dev
```

Open http://localhost:5173

## Env

| Variable | Default | Purpose |
|----------|---------|---------|
| `VITE_API_BASE` | _(empty)_ | If empty, Vite proxies `/api` → `http://localhost:3000`. Set absolute URL only if Nest CORS allows the UI origin. |

**Login “Failed to fetch”:** restart Vite after `.env` changes, and ensure `central-backend` is running on port 3000.

## Screens (WPF ShellPage parity)

| React route | WPF screen |
|-------------|------------|
| `/` | Billing |
| `/vouchers` | Vouchers |
| `/quotations` | Quotations |
| `/barcodes` | Barcodes |
| `/returns` | Sale return |
| `/adjustments` | Adjustments |
| `/duplicate` | Duplicate bill |
| `/online-sales` | Online Sales (POS 1) |
| `/credit-bills` | Credit Bills (POS 1) |
| `/customers` | Customers |
| `/salesmen` | Salesman |
| `/dashboard` | Dashboard (POS 1) |
| `/analytics` | Analytics (POS 1) |
| `/ledger` | Ledger (POS 1) |
| `/bills` | Bill Lookup |
| `/day-close` | Day Close |
| `/expenses` | Daily Expenses (POS 1) |
| `/settings` | Settings (POS 1) |

Shortcuts match WPF (`Ctrl+A` Billing, `Ctrl+G` toggle sidebar, `F9` post bill, `F8` hold, `F12` sign out, …).

## Smoke checklist vs WPF Online

1. Sign in → ONLINE chip / health probe
2. Day Close → Open day → Billing catalog search → post bill → appears in Bill Lookup
3. Hold / resume held bill
4. Vouchers → Sales / Receipt / Payment / Credit Note / Journal / Expense
5. Online Sales COD collect; Credit Bills payment (POS 1)
6. Returns, quotations convert/cancel, adjustments, duplicate print
7. Dashboard / Analytics / Ledger (POS 1)
8. Confirm `store-billing-wpf` sources were not modified
