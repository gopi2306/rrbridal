# POS v2 API (additive)

Base path: `/api/pos-v2`

Online React TruBilling client surface. Does **not** replace `/api/sync` used by WPF.

## Auth

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| POST | `/auth/login` | no | `{ email, password, storeId, deviceId?, posCounter? }` → JWT |
| POST | `/auth/logout` | optional | client clears token |
| GET | `/session` | Bearer | current user + store |

## Catalog / inventory

| GET | `/health` |
| GET | `/catalog?storeId=` |
| GET | `/inventory?storeId=` |
| GET/PATCH | `/company-billing-settings?storeId=` |
| GET | `/masters?storeId=` |

## Writes (delegate to StoreSalesSyncService)

| POST | `/bills` |
| POST | `/returns` |
| POST | `/adjustments` |
| POST | `/quotations` (+ `/convert`, `/cancel`) |
| POST | `/credit-notes` (+ `/apply`, `/cashout`) |
| POST | `/payments/cod`, `/payments/credit` |
| POST | `/day-sessions/open`, `/day-sessions/close` |
| POST | `/cash-movements` |
| POST | `/expenses` |
| POST | `/inventory-adjustments` |

## Reads

| GET | `/bills`, `/bills/:billNo` |
| GET | `/history/bills` |
| GET | `/customers`, `/salesmen` |
| GET | `/dashboard`, `/analytics` |
| GET | `/online-sales`, `/credit-bills` |
| GET | `/quotations`, `/returns`, `/expenses`, `/day-sessions` |
| GET | `/barcode-label-design` |
| GET | `/whatsapp/settings` |
| POST | `/whatsapp/send-invoice` |
