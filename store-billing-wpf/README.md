# Store Billing (WPF) - RR Bridal

This folder will contain the Windows **WPF** billing application, using a **local MongoDB** instance for offline-first billing and a **sync engine** to push/pull data to the central NestJS API.

## Planned components
- WPF UI: billing, barcode scan, printing, sync status
- Local MongoDB: invoices, payments, caches, outbox
- Sync engine: push outbox events, pull deltas
- Payment providers: Pine Labs + Razorpay (cashier chooses)

