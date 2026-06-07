# Store billing — post exceptions (non-blocking)

When local stock or follow-up steps fail, the sale can still complete. Hard rules (customer, payment, discount cap) still block post.

## Add line (scan / search)

- Products with **zero or low local stock** can be added to the bill **without a popup**.
- Stock is checked again at **F9 Post**.

## Post with stock shortage

If any billed qty exceeds local available stock:

1. **Stock shortage** dialog lists SKU, description, billed qty, available qty.
2. **Post anyway** — bill saves; **stock is not decremented** for those lines.
3. **Cancel** — cart unchanged; use **F8 Hold** if needed.

A best-effort **reference indent** is created for short lines at post (does not block the sale).

## Fields on posted bill

```json
"stockExceptions": [
  {
    "sku": "100080",
    "description": "Sample item",
    "requestedQty": 2,
    "availableQty": 0,
    "stockDecremented": false
  }
],
"postWarnings": [
  "Outbox enqueue failed: ..."
]
```

| Field | Meaning |
|-------|---------|
| `stockExceptions` | Lines where local stock was insufficient; no decrement |
| `postWarnings` | Non-fatal errors after save (outbox, stock decrement on other lines, hold delete) |

These fields sync to central inside the normal `InvoiceCreated` payload.

## Day close approval (Dashboard)

**Dashboard → Day close** tab shows posted bills for a selected **local business date** (default today).

### Stock exceptions list

Bills posted on that day with `stockExceptions` where `stockDecremented` is `false` appear in **Stock exceptions (selected day)**.

Click **Approve** on a row to:

1. Decrement local cache stock by `requestedQty` for each pending exception SKU (stock **may go negative**).
2. Set `stockDecremented: true` on each exception line.
3. Record `approvedAtUtc` and `approvedBy` on each approved line.

Approval is **local only**; central is not updated unless the bill is re-synced separately.

### Invoice sync column

Each invoice row shows sync status from `outbox_events` (`InvoiceCreated`):

| Status | Meaning |
|--------|---------|
| Synced | Outbox event status is `synced` |
| Pending sync | Outbox event status is `pending` |
| Not queued | No outbox row and `postWarnings` mentions outbox/enqueue failure |
| Unknown | No matching outbox event |

## What still blocks post

- Missing customer name or 10-digit mobile
- No line items
- Manual discount over user max %
- Payment dialog cancelled or payment validation failed (cash short, split mismatch, credit note)

## Related

- [store-hold-bills.md](./store-hold-bills.md)
- [store-invoice-printing.md](./store-invoice-printing.md)
