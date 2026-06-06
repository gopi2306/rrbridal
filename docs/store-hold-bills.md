# Store billing — hold bills (F8)

Park in-progress carts without allocating an invoice number or posting to central.

## Collections

| Collection | When written | Contents |
|------------|--------------|----------|
| **`held_bills`** | F8 Hold (create/update) | In-progress cart; identity = `holdNo` (`HOLD-...`) |
| **`store_bills`** | F9 Post only | Completed invoices; `status: posted`; real `billNo` |

Holds are **never** saved to `store_bills`. Holds are local-only (not synced to central).

## Workflow

1. **New bill** — UI shows **DRAFT**; bill number is **—** (not allocated).
2. **F8 Hold** — saves to `held_bills` with `HOLD-{date}-{store}-{counter}-{seq}`; screen clears for next customer.
3. **Resume held** — pick from list (any counter, same store); add lines; **F8 again** updates the **same** hold.
4. **F9 Post** — allocates real invoice number, saves to `store_bills`, sync outbox, deletes hold from `held_bills`.

## vs “Hold bills” checkbox

The **Hold bills** checkbox on the billing screen is **metadata** on a **posted** invoice (`holdBills` field). It is unrelated to F8 hold/resume.

## Migration

On startup, legacy `store_bills` documents with `status: "draft"` are moved into `held_bills` with a new `holdNo` and removed from `store_bills`.

## Related

- [store-invoice-printing.md](./store-invoice-printing.md)
- [sync-protocol.md](./sync-protocol.md)
