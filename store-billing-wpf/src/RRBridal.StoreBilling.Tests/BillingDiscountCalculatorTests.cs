using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services.Billing;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public class BillingDiscountCalculatorTests
{
    [Fact]
    public void ReverseSplitFromInclusive_extracts_gst_from_selling_rate()
    {
        var breakdown = BillingDiscountCalculator.ReverseSplitFromInclusive(10_500m, 5m, isIgst: false);

        Assert.Equal(10_000m, breakdown.Taxable);
        Assert.Equal(250m, breakdown.Cgst);
        Assert.Equal(250m, breakdown.Sgst);
        Assert.Equal(500m, breakdown.TotalTax);
        Assert.Equal(10_500m, breakdown.Inclusive);
    }

    [Fact]
    public void BillingLineItem_treats_rate_as_gst_inclusive()
    {
        var line = new BillingLineItem
        {
            Qty = 1,
            Rate = 12_749m,
            TaxPercent = 5m,
            SchemeDiscountAmount = 1_274.90m,
        };

        Assert.Equal(12_749m, line.Amount);
        Assert.Equal(12_749m, line.OriginalInclusiveAmount);
        Assert.Equal(11_474.10m, line.RevisedInclusiveAmount);
        Assert.Equal(10_927.7143m, line.RevisedAmount);
        Assert.Equal(546.3857m, line.RevisedTaxAmount);
    }

    [Fact]
    public void Inclusive_selling_total_does_not_double_count_gst()
    {
        var rates = new[] { 12_749m, 6_599m, 8_849m, 2_999m, 3_749m };
        decimal payable = 0;
        decimal taxable = 0;
        decimal tax = 0;

        foreach (var rate in rates)
        {
            var line = new BillingLineItem
            {
                Qty = 1,
                Rate = rate,
                TaxPercent = 5m,
                SchemeDiscountAmount = MoneyMath.RoundAmount(rate * 0.10m),
            };
            payable += line.RevisedInclusiveAmount;
            taxable += line.RevisedAmount;
            tax += line.RevisedTaxAmount;
        }

        Assert.Equal(31_450.50m, payable);
        Assert.Equal(29_952.8571m, taxable);
        Assert.Equal(1_497.6429m, tax);
        Assert.Equal(payable, taxable + tax);
    }
}
