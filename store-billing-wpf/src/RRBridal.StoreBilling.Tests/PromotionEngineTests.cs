using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Billing.Promotions;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class PromotionEngineTests
{
    private static PromotionEngine CreateEngine() => new();

    private static BillLineContext Line(int lineNo, string sku, decimal qty, decimal rate, decimal taxPercent = 18m)
    {
        var amount = qty * rate;
        var inclusive = taxPercent > 0
            ? MoneyMath.RoundAmount(amount * (1m + taxPercent / 100m))
            : amount;
        return new BillLineContext
        {
            LineNo = lineNo,
            Sku = sku,
            Qty = qty,
            Rate = rate,
            Amount = amount,
            TaxPercent = taxPercent,
            IsIgst = false,
            OriginalInclusive = inclusive,
        };
    }

    [Fact]
    public void Buy2Get1_CheapestFree_SavesOneUnit()
    {
        var schemes = new[]
        {
            new PromotionSchemeDefinition
            {
                Code = "bxgy-shirt",
                Name = "Buy 2 Get 1",
                Type = "item",
                Priority = 10,
                Benefit = new PromotionBenefit { Mode = "buy_x_get_y", BuyQty = 2, GetQty = 1, FreeOn = "cheapest" },
                Conditions = new PromotionConditions { Skus = ["SHIRT"] },
            },
        };

        var context = new BillContext
        {
            StoreId = "store1",
            BillDateTime = DateTime.Now,
            Lines = [Line(1, "SHIRT", 3, 1000m, taxPercent: 0)],
            InclusiveTotal = 3000m,
            Subtotal = 3000m,
        };

        var result = CreateEngine().Evaluate(context, schemes);
        var saved = result.LineAdjustments.Sum(a => a.SchemeDiscountAmount);
        Assert.Equal(1000m, saved);
        Assert.Single(result.AppliedSchemes);
    }

    [Fact]
    public void BillTenPercent_WhenTotalAtLeast5000()
    {
        var schemes = new[]
        {
            new PromotionSchemeDefinition
            {
                Code = "bill-10",
                Name = "10% bill off",
                Type = "bill",
                Priority = 50,
                Benefit = new PromotionBenefit { DiscountPercent = 10, MinBillAmount = 5000 },
                Conditions = new PromotionConditions { MinBillAmount = 5000 },
            },
        };

        var inclusive = 6000m;
        var context = new BillContext
        {
            StoreId = "store1",
            BillDateTime = DateTime.Now,
            Lines = [Line(1, "ITEM", 1, inclusive / 1.18m)],
            InclusiveTotal = inclusive,
            Subtotal = inclusive / 1.18m,
        };

        var result = CreateEngine().Evaluate(context, schemes);
        Assert.Equal(600m, result.BillAdjustment);
    }

    [Fact]
    public void SlabDiscount_3200InTenPercentSlab()
    {
        var schemes = new[]
        {
            new PromotionSchemeDefinition
            {
                Code = "slab",
                Name = "Slab",
                Type = "slab",
                Priority = 40,
                Benefit = new PromotionBenefit
                {
                    Slabs =
                    [
                        new PromotionSlab { FromAmount = 1000, ToAmount = 3000, DiscountPercent = 5 },
                        new PromotionSlab { FromAmount = 3000, ToAmount = 5000, DiscountPercent = 10 },
                    ],
                },
            },
        };

        var inclusive = 3200m;
        var context = new BillContext
        {
            StoreId = "store1",
            BillDateTime = DateTime.Now,
            Lines = [Line(1, "ITEM", 1, inclusive / 1.18m)],
            InclusiveTotal = inclusive,
            Subtotal = inclusive / 1.18m,
        };

        var result = CreateEngine().Evaluate(context, schemes);
        Assert.Equal(320m, result.BillAdjustment);
    }

    [Fact]
    public void BestBenefit_PicksHigherSavingsWithinSameStage()
    {
        var schemes = new[]
        {
            new PromotionSchemeDefinition
            {
                Code = "bill-5",
                Name = "5% bill",
                Type = "bill",
                Priority = 10,
                Stacking = "best_benefit",
                Benefit = new PromotionBenefit { DiscountPercent = 5 },
            },
            new PromotionSchemeDefinition
            {
                Code = "bill-10",
                Name = "10% bill",
                Type = "bill",
                Priority = 20,
                Stacking = "best_benefit",
                Benefit = new PromotionBenefit { DiscountPercent = 10 },
            },
        };

        var inclusive = 6000m;
        var context = new BillContext
        {
            StoreId = "store1",
            BillDateTime = DateTime.Now,
            Lines = [Line(1, "ITEM", 1, inclusive / 1.18m)],
            InclusiveTotal = inclusive,
            Subtotal = inclusive / 1.18m,
        };

        var result = CreateEngine().Evaluate(context, schemes);
        var totalSaved = result.LineAdjustments.Sum(a => a.SchemeDiscountAmount) + result.BillAdjustment;
        Assert.Equal(600m, totalSaved);
        Assert.Contains(result.AppliedSchemes, s => s.SchemeCode == "bill-10");
    }

    [Fact]
    public void TimeWindow_OutsideHours_DoesNotApply()
    {
        var friday = new DateTime(2026, 5, 29, 14, 0, 0);
        var schemes = new[]
        {
            new PromotionSchemeDefinition
            {
                Code = "happy-hour",
                Name = "Evening off",
                Type = "bill",
                TimeWindows = [new PromotionTimeWindow { DayOfWeek = (int)friday.DayOfWeek, FromHour = 18, ToHour = 21 }],
                Benefit = new PromotionBenefit { DiscountPercent = 5 },
            },
        };

        var inclusive = 2000m;
        var context = new BillContext
        {
            StoreId = "store1",
            BillDateTime = friday,
            Lines = [Line(1, "ITEM", 1, inclusive / 1.18m)],
            InclusiveTotal = inclusive,
            Subtotal = inclusive / 1.18m,
        };

        var result = CreateEngine().Evaluate(context, schemes);
        Assert.Equal(0m, result.BillAdjustment);
        Assert.Empty(result.AppliedSchemes);
    }

    [Fact]
    public void ComboFixedPrice_AppliesBundleDiscount()
    {
        var schemes = new[]
        {
            new PromotionSchemeDefinition
            {
                Code = "combo",
                Name = "Shirt + Pant",
                Type = "combo",
                Priority = 15,
                Benefit = new PromotionBenefit { ComboSkus = ["SHIRT", "PANT"], FixedPrice = 1999m },
            },
        };

        var context = new BillContext
        {
            StoreId = "store1",
            BillDateTime = DateTime.Now,
            Lines =
            [
                Line(1, "SHIRT", 1, 1500m),
                Line(2, "PANT", 1, 1200m),
            ],
            InclusiveTotal = (1500m + 1200m) * 1.18m,
            Subtotal = 2700m,
        };

        var result = CreateEngine().Evaluate(context, schemes);
        var saved = result.LineAdjustments.Sum(a => a.SchemeDiscountAmount);
        var expected = MoneyMath.RoundAmount(context.InclusiveTotal - 1999m);
        Assert.Equal(expected, saved);
    }
}
