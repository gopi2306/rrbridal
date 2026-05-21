using System;

namespace RRBridal.StoreBilling.App.Services.Billing;

public readonly record struct GstTaxBreakdown(
    decimal Taxable,
    decimal Cgst,
    decimal Sgst,
    decimal Igst,
    decimal TotalTax)
{
    public decimal Inclusive => Taxable + TotalTax;
}

public static class BillingDiscountCalculator
{
    public static decimal ComputeOriginalInclusive(decimal amount, decimal taxPercent, bool isIgst) =>
        ComputeOriginalTax(amount, taxPercent, isIgst).Inclusive;

    public static GstTaxBreakdown ComputeOriginalTax(decimal amount, decimal taxPercent, bool isIgst) =>
        ComputeForwardTax(amount, taxPercent, isIgst);

    public static GstTaxBreakdown ComputeForwardTax(decimal taxable, decimal taxPercent, bool isIgst)
    {
        if (taxable <= 0 || taxPercent <= 0)
            return new GstTaxBreakdown(0, 0, 0, 0, 0);

        if (isIgst)
        {
            var igst = Math.Round(taxable * taxPercent / 100m, 2, MidpointRounding.AwayFromZero);
            return new GstTaxBreakdown(taxable, 0, 0, igst, igst);
        }

        var halfRate = Math.Round(taxPercent / 2m, 2);
        var cgst = Math.Round(taxable * halfRate / 100m, 2, MidpointRounding.AwayFromZero);
        var sgst = Math.Round(taxable * halfRate / 100m, 2, MidpointRounding.AwayFromZero);
        return new GstTaxBreakdown(taxable, cgst, sgst, 0, cgst + sgst);
    }

    public static GstTaxBreakdown ReverseSplitFromInclusive(decimal inclusive, decimal taxPercent, bool isIgst)
    {
        if (inclusive <= 0)
            return new GstTaxBreakdown(0, 0, 0, 0, 0);

        if (taxPercent <= 0)
            return new GstTaxBreakdown(Math.Round(inclusive, 2, MidpointRounding.AwayFromZero), 0, 0, 0, 0);

        var divisor = 1m + taxPercent / 100m;
        var taxable = Math.Round(inclusive / divisor, 2, MidpointRounding.AwayFromZero);
        var totalTax = Math.Round(inclusive - taxable, 2, MidpointRounding.AwayFromZero);

        if (isIgst)
            return new GstTaxBreakdown(taxable, 0, 0, totalTax, totalTax);

        var cgst = Math.Round(totalTax / 2m, 2, MidpointRounding.AwayFromZero);
        var sgst = totalTax - cgst;
        return new GstTaxBreakdown(taxable, cgst, sgst, 0, totalTax);
    }

    /// <summary>Discounts (₹) reduce tax-inclusive total; revised taxable and GST come from reverse split.</summary>
    public static GstTaxBreakdown ComputeRevisedFromInclusiveDiscounts(
        decimal originalInclusive,
        decimal itemDisc,
        decimal cashDisc,
        decimal taxPercent,
        bool isIgst)
    {
        var revisedInclusive = Math.Max(0m, originalInclusive - itemDisc - cashDisc);
        if (revisedInclusive <= 0)
            return new GstTaxBreakdown(0, 0, 0, 0, 0);

        return ReverseSplitFromInclusive(revisedInclusive, taxPercent, isIgst);
    }
}
