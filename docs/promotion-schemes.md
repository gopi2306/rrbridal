# Promotion schemes & offers (POS engine)

Central backend stores configurable **promotion schemes** (`promotion_schemes` collection). Store clients sync active schemes and apply them at runtime, separate from manual item % and cash discounts.

## Concepts

| Field | Meaning |
| ----- | ------- |
| `kind` | `scheme` or `offer` (marketing label; same engine) |
| `type` | `item`, `bill`, `combo`, `slab` |
| `priority` | Lower number = higher priority |
| `stacking` | `best_benefit` (default), `highest_priority`, `allow_stack` |
| `storeIds` | Empty = all stores; otherwise only listed store codes |
| `conditions` | Optional filters (AND within scheme) |
| `benefit` | Type-specific discount payload |

Manual cashier discounts (`itemDiscountPercent`, `cashDiscAmount`) remain. Scheme savings are tracked in `schemeDiscountAmount` (line) and `schemeBillDiscount` (bill) for audit.

## API

Base path: `/api/promotion-schemes`

| Method | Path | Description |
| ------ | ---- | ----------- |
| `POST` | `/` | Create scheme |
| `GET` | `/` | List all (non-deleted) |
| `POST` | `/filter` | Paginated filter |
| `GET` | `/:id` | Get by Mongo id |
| `GET` | `/code/:code` | Get by code |
| `PATCH` | `/:id` | Update |
| `PATCH` | `/:id/deactivate` | Set `isActive: false` |
| `DELETE` | `/:id` | Soft-delete (syncs as delete delta) |

## Examples

### Buy 2 Get 1 free (item, cheapest free)

```json
POST /api/promotion-schemes
{
  "code": "bxgy-shirt",
  "name": "Buy 2 Get 1 Shirt",
  "type": "item",
  "priority": 10,
  "isActive": true,
  "stacking": "best_benefit",
  "storeIds": [],
  "conditions": { "skus": ["SHIRT-001"] },
  "benefit": {
    "mode": "buy_x_get_y",
    "buyQty": 2,
    "getQty": 1,
    "freeOn": "cheapest"
  }
}
```

**Expected:** 3 × ₹1000 shirts → ₹1000 scheme discount on cheapest line.

### Bill 10% when total ≥ ₹5000

```json
{
  "code": "bill-10-5k",
  "name": "10% off bills over 5000",
  "type": "bill",
  "priority": 50,
  "conditions": { "minBillAmount": 5000 },
  "benefit": { "discountPercent": 10, "minBillAmount": 5000 }
}
```

**Expected:** ₹6000 inclusive → ₹600 bill scheme discount.

### Slab discount

```json
{
  "code": "slab-tier",
  "name": "Slab 5–10%",
  "type": "slab",
  "priority": 40,
  "benefit": {
    "slabs": [
      { "fromAmount": 1000, "toAmount": 3000, "discountPercent": 5 },
      { "fromAmount": 3000, "toAmount": 5000, "discountPercent": 10 },
      { "fromAmount": 5000, "discountPercent": 15 }
    ]
  }
}
```

**Expected:** ₹3200 bill → 10% slab → ₹320 discount.

### Combo fixed price

```json
{
  "code": "combo-shirt-pant",
  "name": "Shirt + Pant combo",
  "type": "combo",
  "priority": 20,
  "benefit": {
    "comboSkus": ["SHIRT-001", "PANT-001"],
    "fixedPrice": 1999
  }
}
```

### Happy hour (time window)

```json
{
  "code": "happy-hour",
  "name": "Evening 5% off",
  "type": "bill",
  "timeWindows": [{ "dayOfWeek": 5, "fromHour": 18, "toHour": 21 }],
  "benefit": { "discountPercent": 5 }
}
```

Outside 18:00–21:00 on Friday the scheme does not apply.

## Sync

Store pull includes promotion deltas — see [sync-protocol.md](./sync-protocol.md#promotion-schemes).

## Invoice payload

Posted bills include:

```json
"appliedSchemes": [
  { "schemeCode": "bxgy-shirt", "schemeName": "Buy 2 Get 1 Shirt", "savedAmount": 1000.0000 }
],
"schemeBillDiscount": 0,
"schemeLineDiscount": 1000
```

## Related

- [sync-protocol.md](./sync-protocol.md)
- [money-precision.md](./money-precision.md) — amounts use 4 decimal places
