# Monetary precision (4 decimal places)

All **money amounts** use **4 digits after the decimal point** for calculations, storage, and API boundaries. **Display** differs by client (see WPF below).

## Rule

| Kind | Precision | Notes |
|------|-----------|--------|
| Prices, line amounts, tax amounts, discounts, credit notes (calc/storage) | **4 dp** | `50000.0000`, `1234.5678` |
| Final bill **payable** (value after round-off) | **Whole rupee** | Rounded in `ComputeBillTotalsCore`; see WPF display |
| Quantities | Unchanged | Weighable qty may show 3 dp on thermal |
| GST **percent** display | 0–2 dp | Percent splits, not rupee amounts |

## WPF display (store billing)

The store billing WPF app (`MoneyMath` in [`store-billing-wpf/.../MoneyMath.cs`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Billing/MoneyMath.cs)) shows **2 decimal places** (en-IN) for all rupee amounts in UI and thermal receipts:

- `FormatRupee`, `FormatPayable`, grids (`N2`), editable fields (`0.00`)
- Internal `RoundAmount` remains **4 dp** for GST splits, discounts, and sync payloads
- Payable is still computed with whole-rupee round-off; the UI shows it as e.g. `₹ 1,250.00`

## Helpers

### Central backend — [`central-backend/src/common/money.util.ts`](../central-backend/src/common/money.util.ts)

```typescript
MONEY_DECIMAL_PLACES = 4
roundMoney(value)   // numeric rounding
formatMoney(value)  // "1234.5678" string
formatMoneyOrEmpty(value)
```

Used in inventory export columns, store sales dashboard KPI parsing, and product `decimalPoint` default on create.

## Product master

- Field `decimalPoint` on products defaults to **4** on create and in the import template.
- Per-product `decimalPoint` is stored; clients should use the same 4dp rule unless explicitly overridden.

## Payable round-off

In `BillingViewModel.ComputeBillTotalsCore`, `grandBeforeRound` is still rounded to the nearest **whole rupee** for payable; round-off absorbs the difference. Line-level amounts retain 4dp until that step.

## Related docs

- [inventory-export.md](./inventory-export.md) — price columns formatted with `formatMoney`
- [product-import.md](./product-import.md) — `decimalPoint` column in template
