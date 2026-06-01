# Monetary precision (4 decimal places)

All **money amounts** in RR Bridal use **4 digits after the decimal point** for calculations, storage boundaries, and display (except final bill payable — see below).

## Rule

| Kind | Precision | Notes |
|------|-----------|--------|
| Prices, line amounts, tax amounts, discounts, credit notes | **4 dp** | `50000.0000`, `1234.5678` |
| Final bill **payable** (after round-off) | **Whole rupee** | Unchanged round-off to nearest ₹1 |
| Quantities | Unchanged | Weighable qty may show 3 dp on thermal |
| GST **percent** display | 0–2 dp | Percent splits, not rupee amounts |

## Helpers

### Central backend — [`central-backend/src/common/money.util.ts`](../central-backend/src/common/money.util.ts)

```typescript
MONEY_DECIMAL_PLACES = 4
roundMoney(value)   // numeric rounding
formatMoney(value)  // "1234.5678" string
formatMoneyOrEmpty(value)
```

Used in inventory export columns, store sales dashboard KPI parsing, and product `decimalPoint` default on create.

### Store billing WPF — [`MoneyMath.cs`](../store-billing-wpf/src/RRBridal.StoreBilling.App/Services/Billing/MoneyMath.cs)

```csharp
MoneyMath.RoundAmount(decimal)   // 4dp calculation
MoneyMath.FormatAmount(decimal)  // N4
MoneyMath.FormatRupee(decimal)   // "₹ 1,234.5678"
MoneyMath.FormatPayable(decimal) // "₹ 1,235" whole rupee
```

Billing models, GST calculator, returns/exchanges, and ViewModels use `RoundAmount` instead of `Math.Round(..., 2)`.

## Product master

- Field `decimalPoint` on products defaults to **4** on create and in the import template.
- Per-product `decimalPoint` is stored but WPF billing currently uses the global 4dp constant.

## Payable round-off

In `BillingViewModel.ComputeBillTotalsCore`, `grandBeforeRound` is still rounded to the nearest **whole rupee** for payable; round-off absorbs the difference. Line-level amounts retain 4dp until that step.

## Related docs

- [inventory-export.md](./inventory-export.md) — price columns formatted with `formatMoney`
- [product-import.md](./product-import.md) — `decimalPoint` column in template
