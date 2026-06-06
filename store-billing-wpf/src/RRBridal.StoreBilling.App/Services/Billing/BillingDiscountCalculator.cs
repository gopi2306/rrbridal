using System;
using System.Collections.Generic;
using System.Linq;

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
    /// <summary>Qty × Rate line total — GST-inclusive selling amount.</summary>
    public static decimal ComputeOriginalInclusive(decimal inclusiveLineAmount, decimal taxPercent, bool isIgst) =>
        inclusiveLineAmount <= 0 ? 0 : MoneyMath.RoundAmount(inclusiveLineAmount);

    /// <summary>Split GST out of a GST-inclusive line total (Qty × inclusive Rate).</summary>
    public static GstTaxBreakdown ComputeOriginalTax(decimal inclusiveLineAmount, decimal taxPercent, bool isIgst) =>
        ReverseSplitFromInclusive(inclusiveLineAmount, taxPercent, isIgst);

    public static GstTaxBreakdown ComputeForwardTax(decimal taxable, decimal taxPercent, bool isIgst)
    {
        if (taxable <= 0 || taxPercent <= 0)
            return new GstTaxBreakdown(0, 0, 0, 0, 0);

        if (isIgst)
        {
            var igst = MoneyMath.RoundAmount(taxable * taxPercent / 100m);
            return new GstTaxBreakdown(taxable, 0, 0, igst, igst);
        }

        var halfRate = Math.Round(taxPercent / 2m, 2);
        var cgst = MoneyMath.RoundAmount(taxable * halfRate / 100m);
        var sgst = MoneyMath.RoundAmount(taxable * halfRate / 100m);
        return new GstTaxBreakdown(taxable, cgst, sgst, 0, cgst + sgst);
    }

    public static GstTaxBreakdown ReverseSplitFromInclusive(decimal inclusive, decimal taxPercent, bool isIgst)
    {
        if (inclusive <= 0)
            return new GstTaxBreakdown(0, 0, 0, 0, 0);

        if (taxPercent <= 0)
            return new GstTaxBreakdown(MoneyMath.RoundAmount(inclusive), 0, 0, 0, 0);

        var divisor = 1m + taxPercent / 100m;
        var taxable = MoneyMath.RoundAmount(inclusive / divisor);
        var totalTax = MoneyMath.RoundAmount(inclusive - taxable);

        if (isIgst)
            return new GstTaxBreakdown(taxable, 0, 0, totalTax, totalTax);

        var cgst = MoneyMath.RoundAmount(totalTax / 2m);
        var sgst = totalTax - cgst;
        return new GstTaxBreakdown(taxable, cgst, sgst, 0, totalTax);
    }

    /// <summary>Discounts (₹) reduce tax-inclusive total; revised taxable and GST come from reverse split.</summary>
    public static GstTaxBreakdown ComputeRevisedFromInclusiveDiscounts(
        decimal originalInclusive,
        decimal schemeDisc,
        decimal itemDisc,
        decimal cashDisc,
        decimal taxPercent,
        bool isIgst)
    {
        var revisedInclusive = Math.Max(0m, originalInclusive - schemeDisc - itemDisc - cashDisc);
        if (revisedInclusive <= 0)
            return new GstTaxBreakdown(0, 0, 0, 0, 0);

        return ReverseSplitFromInclusive(revisedInclusive, taxPercent, isIgst);
    }

    /// <summary>Tax-inclusive total after scheme discounts, before manual item/cash discounts.</summary>
    public static decimal ComputeManualDiscountBase(
        IEnumerable<(decimal OriginalInclusive, decimal SchemeDiscount)> lines) =>
        lines.Sum(x => Math.Max(0m, x.OriginalInclusive - x.SchemeDiscount));

    public static decimal ComputeCombinedDiscountPercent(decimal manualDiscountBase, decimal itemDisc, decimal cashDisc)
    {
        if (manualDiscountBase <= 0)
            return 0;
        return (itemDisc + cashDisc) / manualDiscountBase * 100m;
    }

    public static bool IsWithinMaxManualDiscount(
        decimal manualDiscountBase,
        decimal itemDisc,
        decimal cashDisc,
        decimal maxPercent) =>
        ComputeCombinedDiscountPercent(manualDiscountBase, itemDisc, cashDisc) <= maxPercent + 0.001m;
}
