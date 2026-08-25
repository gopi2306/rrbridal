using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Products;
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

    [Fact]
    public void WithGst_rate_118_at_18_percent_yields_taxable_100()
    {
        var line = new BillingLineItem
        {
            Qty = 1,
            Rate = 118m,
            TaxPercent = 18m,
            PricesExcludeGst = false,
        };

        Assert.Equal(100m, line.RevisedAmount);
        Assert.Equal(18m, line.RevisedTaxAmount);
        Assert.Equal(118m, line.RevisedInclusiveAmount);
    }

    [Fact]
    public void SuggestedTaxPercent_infers_from_inclusive_vs_without_gst_when_gst_percent_zero()
    {
        var product = new CatalogProduct
        {
            CentralId = "1",
            Sku = "SKU1",
            Name = "Test",
            SellingPrice = 895m,
            SellingPriceWithoutGst = 758.47m,
            GstPercent = 0m,
        };

        Assert.Equal(18m, product.SuggestedTaxPercent);
        Assert.Equal(758.47m, product.SuggestedExclusiveRate);
        // Without GST billing: selling price is taxable base → 895 + 18% GST.
        var payable = product.SuggestedPayableUnitRate(pricesExcludeGst: true);
        Assert.Equal(1056.1m, payable);
    }

    [Fact]
    public void WithoutGst_rate_650_at_18_percent_adds_gst_on_top()
    {
        var line = new BillingLineItem
        {
            Qty = 1,
            PricesExcludeGst = true,
            TaxPercent = 18m,
            Rate = 650m,
        };

        Assert.Equal(650m, line.Amount);
        Assert.Equal(650m, line.RevisedAmount);
        Assert.Equal(117m, line.RevisedTaxAmount);
        Assert.Equal(767m, line.RevisedInclusiveAmount);
        Assert.Equal(58.5m, line.CgstAmount);
        Assert.Equal(58.5m, line.SgstAmount);
    }

    [Fact]
    public void WithoutGst_rate_100_at_18_percent_adds_gst_on_top()
    {
        var line = new BillingLineItem
        {
            Qty = 1,
            PricesExcludeGst = true,
            TaxPercent = 18m,
            Rate = 100m,
        };

        Assert.Equal(100m, line.Amount);
        Assert.Equal(100m, line.RevisedAmount);
        Assert.Equal(18m, line.RevisedTaxAmount);
        Assert.Equal(118m, line.RevisedInclusiveAmount);
        Assert.Equal(9m, line.CgstAmount);
        Assert.Equal(9m, line.SgstAmount);
    }

    [Fact]
    public void WithoutGst_discount_reduces_taxable_then_adds_forward_tax()
    {
        var line = new BillingLineItem
        {
            Qty = 1,
            Rate = 100m,
            TaxPercent = 18m,
            PricesExcludeGst = true,
            DiscountAmount = 10m,
        };

        Assert.Equal(90m, line.RevisedAmount);
        Assert.Equal(16.20m, line.RevisedTaxAmount);
        Assert.Equal(106.20m, line.RevisedInclusiveAmount);
    }

    [Fact]
    public void ComputeForwardTax_zero_percent_keeps_taxable()
    {
        var breakdown = BillingDiscountCalculator.ComputeForwardTax(100m, 0m, isIgst: false);
        Assert.Equal(100m, breakdown.Taxable);
        Assert.Equal(0m, breakdown.TotalTax);
        Assert.Equal(100m, breakdown.Inclusive);
    }

    [Fact]
    public void SuggestedExclusiveRate_derives_from_inclusive_when_without_gst_fields_missing()
    {
        var product = new CatalogProduct
        {
            CentralId = "1",
            Sku = "SKU1",
            Name = "Test",
            SellingPrice = 118m,
            GstPercent = 18m,
        };

        Assert.Equal(100m, product.SuggestedExclusiveRate);
    }

    [Fact]
    public void SuggestedExclusiveRate_prefers_master_without_gst_fields()
    {
        var product = new CatalogProduct
        {
            CentralId = "1",
            Sku = "SKU1",
            Name = "Test",
            SellingPrice = 118m,
            SellingPriceWithoutGst = 99.5m,
            GstPercent = 18m,
        };

        Assert.Equal(99.5m, product.SuggestedExclusiveRate);
    }
}
