using System;
using System.Collections.Generic;
using System.Linq;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Pure GST row math for A4 Tax Invoice (HSN split + CGST/SGST/IGST totals).</summary>
public static class TaxInvoiceA4GstBreakdown
{
    public sealed record HsnGstRow(
        string Hsn,
        decimal TaxableValue,
        decimal GstPercent,
        decimal CgstRate,
        decimal CgstAmount,
        decimal SgstRate,
        decimal SgstAmount,
        decimal IgstRate,
        decimal IgstAmount,
        decimal TotalTax);

    public sealed record BillTaxTotals(
        decimal Taxable,
        decimal Cgst,
        decimal Sgst,
        decimal Igst,
        decimal TotalTax);

    public static IReadOnlyList<HsnGstRow> BuildHsnRows(ThermalInvoiceInput input)
    {
        var lines = InvoiceLinePagination.ActiveLines(input)
            .Where(l => l.TaxableAmount > 0 || l.TaxAmount > 0)
            .ToList();

        var groups = lines
            .GroupBy(l => (
                Hsn: string.IsNullOrWhiteSpace(l.Hsn) ? "—" : l.Hsn.Trim(),
                Percent: Math.Round(l.TaxPercent, 2)))
            .OrderBy(g => g.Key.Hsn)
            .ThenBy(g => g.Key.Percent);

        var rows = new List<HsnGstRow>();
        foreach (var g in groups)
        {
            var taxable = g.Sum(x => LineTaxableAmount(x));
            var tax = g.Sum(x => x.TaxAmount);
            var percent = g.Key.Percent;
            if (tax <= 0 && percent > 0 && taxable > 0)
                tax = MoneyMath.RoundAmount(taxable * percent / 100m);

            if (input.IsInterState)
            {
                rows.Add(new HsnGstRow(
                    g.Key.Hsn,
                    taxable,
                    percent,
                    0, 0, 0, 0,
                    percent,
                    tax,
                    tax));
            }
            else
            {
                var halfRate = MoneyMath.RoundAmount(percent / 2m);
                var half = MoneyMath.RoundAmount(tax / 2m);
                var cgst = half;
                var sgst = MoneyMath.RoundAmount(tax - half);
                rows.Add(new HsnGstRow(
                    g.Key.Hsn,
                    taxable,
                    percent,
                    halfRate,
                    cgst,
                    halfRate,
                    sgst,
                    0, 0,
                    tax));
            }
        }

        return rows;
    }

    public static BillTaxTotals ComputeTotals(ThermalInvoiceInput input)
    {
        var rows = BuildHsnRows(input);
        var taxable = rows.Sum(r => r.TaxableValue);
        if (taxable <= 0 && input.TotalTaxableAmount > 0)
            taxable = input.TotalTaxableAmount;

        if (input.IsInterState)
        {
            var igst = input.IgstTotal > 0 ? input.IgstTotal : rows.Sum(r => r.IgstAmount);
            return new BillTaxTotals(taxable, 0, 0, igst, igst);
        }

        var cgst = input.CgstTotal > 0 ? input.CgstTotal : rows.Sum(r => r.CgstAmount);
        var sgst = input.SgstTotal > 0 ? input.SgstTotal : rows.Sum(r => r.SgstAmount);
        return new BillTaxTotals(taxable, cgst, sgst, 0, cgst + sgst);
    }

    public static decimal LineTaxableAmount(InvoiceLineSnap line)
    {
        if (line.TaxableAmount > 0)
            return line.TaxableAmount;
        var inclusive = line.PrePrintedLineAmount();
        if (inclusive > 0 && line.TaxAmount > 0)
            return MoneyMath.RoundAmount(inclusive - line.TaxAmount);
        return inclusive;
    }

    public static decimal LineExclusiveRate(InvoiceLineSnap line)
    {
        var taxable = LineTaxableAmount(line);
        if (line.Qty > 0 && taxable > 0)
            return MoneyMath.RoundAmount(taxable / line.Qty);
        return line.Rate;
    }
}
