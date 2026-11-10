using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Models;

namespace RRBridal.StoreBilling.App.Services.Billing;

/// <summary>Applies posted adjustment lines onto a bill document so payable and line rates reflect corrections.</summary>
public static class BillAdjustmentApplier
{
    public sealed record AdjustmentLineSnap(
        int LineNo,
        decimal AdjustedQty,
        decimal AdjustedRate,
        decimal AdjustedAmount,
        decimal TaxPercent);

    public static void ApplyToBill(
        BsonDocument billDoc,
        IReadOnlyList<AdjustmentLineSnap> adjustmentLines,
        bool isInterState,
        string adjustmentNo,
        decimal adjustedPayable,
        DateTime adjustedAtUtc)
    {
        if (adjustmentLines.Count == 0)
            return;

        var byLineNo = adjustmentLines.ToDictionary(l => l.LineNo);
        if (!billDoc.TryGetValue("lines", out var linesVal) || !linesVal.IsBsonArray)
            return;

        decimal subTotal = 0m;
        decimal cgstTotal = 0m;
        decimal sgstTotal = 0m;
        decimal igstTotal = 0m;

        foreach (var lineVal in linesVal.AsBsonArray)
        {
            if (lineVal is not BsonDocument line)
                continue;

            var lineNo = line.GetValue("lineNo", 0).ToInt32();
            if (byLineNo.TryGetValue(lineNo, out var adj))
            {
                var tax = ComputeLineTax(adj.AdjustedAmount, adj.TaxPercent, isInterState);
                line["qty"] = (double)adj.AdjustedQty;
                line["rate"] = (double)adj.AdjustedRate;
                line["amount"] = (double)adj.AdjustedAmount;
                line["taxPercent"] = (double)adj.TaxPercent;
                if (isInterState)
                {
                    line["cgstPercent"] = 0.0;
                    line["sgstPercent"] = 0.0;
                    line["igstPercent"] = (double)adj.TaxPercent;
                }
                else
                {
                    var half = adj.TaxPercent / 2m;
                    line["cgstPercent"] = (double)half;
                    line["sgstPercent"] = (double)half;
                    line["igstPercent"] = 0.0;
                }

                line["cgstAmount"] = (double)tax.Cgst;
                line["sgstAmount"] = (double)tax.Sgst;
                line["igstAmount"] = (double)tax.Igst;
                line["taxAmount"] = (double)(tax.Cgst + tax.Sgst + tax.Igst);
            }

            subTotal += (decimal)line.GetValue("amount", 0).ToDouble();
            cgstTotal += (decimal)line.GetValue("cgstAmount", 0).ToDouble();
            sgstTotal += (decimal)line.GetValue("sgstAmount", 0).ToDouble();
            igstTotal += (decimal)line.GetValue("igstAmount", 0).ToDouble();
        }

        var taxTotal = cgstTotal + sgstTotal + igstTotal;
        var roundOff = MoneyMath.RoundAmount(adjustedPayable - (subTotal + taxTotal));

        billDoc["lines"] = linesVal;
        billDoc["subTotal"] = (double)subTotal;
        billDoc["cgstTotal"] = (double)cgstTotal;
        billDoc["sgstTotal"] = (double)sgstTotal;
        billDoc["igstTotal"] = (double)igstTotal;
        billDoc["taxTotal"] = (double)taxTotal;
        billDoc["roundOff"] = (double)roundOff;
        billDoc["payable"] = (double)adjustedPayable;
        billDoc["hasAdjustment"] = true;
        billDoc["lastAdjustmentNo"] = adjustmentNo;
        billDoc["adjustedAtUtc"] = adjustedAtUtc.ToString("O");
    }

    public static IReadOnlyList<AdjustmentLineSnap> FromAdjustmentLineItems(IEnumerable<AdjustmentLineItem> lines) =>
        lines.Select(l => new AdjustmentLineSnap(
            l.LineNo,
            l.AdjustedQty,
            l.AdjustedRate,
            l.AdjustedAmount,
            l.TaxPercent)).ToList();

    public static IReadOnlyList<AdjustmentLineSnap> FromAdjustmentLinesArray(BsonArray linesArr)
    {
        var result = new List<AdjustmentLineSnap>();
        foreach (var lineVal in linesArr)
        {
            if (lineVal is not BsonDocument line)
                continue;
            result.Add(new AdjustmentLineSnap(
                line.GetValue("lineNo", 0).ToInt32(),
                (decimal)line.GetValue("adjustedQty", 0).ToDouble(),
                (decimal)line.GetValue("adjustedRate", 0).ToDouble(),
                (decimal)line.GetValue("adjustedAmount", 0).ToDouble(),
                (decimal)line.GetValue("taxPercent", 0).ToDouble()));
        }

        return result;
    }

    private static (decimal Cgst, decimal Sgst, decimal Igst) ComputeLineTax(
        decimal amount,
        decimal taxPercent,
        bool isInterState)
    {
        if (isInterState)
            return (0m, 0m, MoneyMath.RoundAmount(amount * taxPercent / 100m));

        var half = MoneyMath.RoundAmount(amount * (taxPercent / 2m) / 100m);
        return (half, half, 0m);
    }
}
