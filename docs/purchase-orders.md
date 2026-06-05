# Purchase orders (central API)

Purchase orders are created and managed under `POST/GET/PATCH /purchase-orders`. Document numbers use the `purchase_order` config key (see [document-numbers.md](./document-numbers.md)).

## Refresh from product master

When product **cost**, **GST %**, **MRP**, or **selling price** change after a PO was saved, refresh recalculates every line and header totals from the current product master — **without** updating products.

| Method | Path | Allowed status |
|--------|------|----------------|
| `POST` | `/purchase-orders/:id/refresh` | `open`, `awaiting_approval` only |

Returns the same enriched shape as `GET /purchase-orders/:id` (lines include populated `product`). If any line SKU has no product, that line is left unchanged and `refreshWarnings` lists `"SKU xxx: product not found"`.

### Product → line (on refresh)

| Product | PO line |
|---------|---------|
| `_id` | `productId` |
| `upcEanCode` | `barcode` |
| `itemName` / `shortName` | `description` |
| `costPrice` | `cost` |
| `sellingPrice` | `selling` |
| `mrp` | `mrp` |
| `gstPercent` | `taxPercent`, `cgstPercent` (= gst/2), `sgstPercent` (= gst/2) |

**Preserved on the line:** `recdQty`, `freeQty` (clamped ≥ 0), `discountPercent`, `surchargePercent`, `cashDiscPercent`, `rotPercent`, `grossPercent`.

### Line amount formulas

All steps use `roundMoney` (4 dp — see [money-precision.md](./money-precision.md)).

```
recdQty = max(0, recdQty)
freeQty = max(0, freeQty)
base = recdQty × cost
discountAmount = base × discountPercent ÷ 100
surchargeAmount = base × surchargePercent ÷ 100
taxAmount = base × taxPercent ÷ 100          // tax on base only
cgstAmount = base × (taxPercent ÷ 2) ÷ 100
sgstAmount = base × (taxPercent ÷ 2) ÷ 100
amount = base + surchargeAmount − discountAmount
gross = amount + taxAmount                   // internal
netCost = gross ÷ max(1, recdQty)
cashDiscAmount = gross × cashDiscPercent ÷ 100
netAmount = gross − cashDiscAmount
```

### Header rollup

```
itemDiscAmount  = Σ line.discountAmount
surchargeAmount = Σ line.surchargeAmount
taxAmount       = Σ line.taxAmount
cgstAmount      = Σ line.cgstAmount
sgstAmount      = Σ line.sgstAmount
linesNet        = Σ line.netAmount
cashDiscount    = linesNet × cashDiscPercent ÷ 100
netAmount       = linesNet − cashDiscount
```

`cashDiscPercent` on the PO document defaults from `supplier.cashDiscount` when unset.

## Permanent delete

Hard-deletes the purchase order document from MongoDB (not recoverable).

| Method | Path | Allowed status |
|--------|------|----------------|
| `DELETE` | `/purchase-orders/:id` | `open`, `awaiting_approval` only |

Returns `{ deleted: true, id, poNo }`.

- `404` — PO not found
- `400` — invalid id or status not deletable
- `409` — goods receipt exists with `poId` referencing this PO

## Related

- [inventory.md](./inventory.md) — stock movements after goods receipt
- [document-numbers.md](./document-numbers.md) — PO number allocation
