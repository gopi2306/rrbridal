# Configurable document numbers (central API)

Auto-generated business numbers (SKU, PO, purchase intent, purchase return, goods receipt RCV/GRN) use **optional prefix + zero-padded numeric suffix** (e.g. `PO-100001`, or `100001` when `prefix` is `""`). Settings live in MongoDB collection `document_number_configs`; atomic counters use `id_sequences` (internal, do not edit manually).

## Config keys

| `configKey` | Default prefix | Field |
|-------------|----------------|-------|
| `product_sku` | `SKU-` | `Product.sku` |
| `purchase_order` | `PO-` | `PurchaseOrder.poNo` |
| `purchase_intent` | `PINV-` | `PurchaseIntent.intentNo` |
| `purchase_return` | `PR-` | `PurchaseReturn.purchaseReturnNo` |
| `goods_receipt_rcv` | `RCV-` | `GoodsReceipt.receiptNo` |
| `goods_receipt_grn` | `GRN-` | `GoodsReceipt.grnNumber` |

Each row also has `padLength` (default 6) and `startFrom` (default 1).

## Example: start series at 100001

PATCH `admin/document-number-configs/purchase_order`:

```json
{
  "prefix": "PO-",
  "padLength": 6,
  "startFrom": 100001
}
```

Next purchase orders: `PO-100001`, `PO-100002`, …

To issue numbers only (no literal prefix), set `"prefix": ""` — next values are `100001`, `100002`, … (still zero-padded to `padLength`).

`startFrom` must fit in `padLength` (100001 needs pad ≥ 6). You may only **raise** `startFrom`, not lower it (v1).

## Admin API

Requires JWT with role **`admin`** or **`super_admin`**.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/admin/document-number-configs` | List all configs |
| `PATCH` | `/admin/document-number-configs/:configKey` | Update `prefix`, `padLength`, `startFrom`, `label`, `description` |

On create, services sync the counter from existing documents (e.g. seed `PO-1001`) so the next number is never duplicated.

## Manual override

- **Product SKU**: optional on create; otherwise auto.
- **Purchase return**: optional `purchaseReturnNo` on create; otherwise auto.
- PO and purchase intent numbers are always auto-generated.
