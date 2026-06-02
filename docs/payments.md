# Payments (store client)

The store billing client supports multiple payment providers. Cashier selects provider per transaction.

## Provider strategy
- **Pine Labs**: terminal-based in-store payments (card/UPI). Integration is typically via vendor SDK (DLL/COM/HTTP).
- **Razorpay**: gateway-based online payments. Recommended: create Razorpay orders from **central backend** so API keys stay server-side.

## Store-side abstraction
- `IPaymentProvider`: uniform interface for payment initiation
- `PaymentRouter`: invokes chosen provider, records local payment doc, emits outbox event `PaymentRecorded`

## Outbox event: PaymentRecorded
- `payload.invoiceNo`
- `payload.provider`
- `payload.amount`
- `payload.currency`
- `payload.status`
- `payload.providerReference`

## Central-side (next step)
- Create a `payments` collection with unique indexes on:\n  - `providerPaymentId` (if available)\n  - `idempotencyKey = invoiceNo + provider + attemptNo`\n- Provide endpoints:\n  - `POST /payments/razorpay/orders` (create order)\n  - `POST /payments/razorpay/webhook` (verify signature + update status)\n  - `POST /payments/reconcile` (optional admin/retry)\n+
