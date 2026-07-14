using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Models;

namespace RRBridal.StoreBilling.App.Services.Billing;

/// <summary>Shared BSON serialize/deserialize for bill and quotation line payloads.</summary>
public static class BillingPayloadBuilder
{
    public static BsonArray BuildLinesBsonArray(IEnumerable<BillingLineItem> lines)
    {
        var linesArr = new BsonArray();
        foreach (var line in lines.Where(l => l.Amount > 0))
        {
            linesArr.Add(new BsonDocument
            {
                { "lineNo", line.LineNo },
                { "centralProductId", line.CentralProductId ?? "" },
                { "sku", line.ProductCode },
                { "description", line.Description },
                { "hsn", line.HsnCode ?? "" },
                { "qty", (double)line.Qty },
                { "rate", (double)line.Rate },
                { "amount", (double)line.Amount },
                { "alterationAmount", (double)line.AlterationAmount },
                { "discountAmount", (double)line.DiscountAmount },
                { "cashDiscountAmount", (double)line.CashDiscountAmount },
                { "schemeDiscountAmount", (double)line.SchemeDiscountAmount },
                { "originalTaxAmount", (double)line.OriginalTaxAmount },
                { "revisedAmount", (double)line.RevisedAmount },
                { "revisedInclusiveAmount", (double)line.RevisedInclusiveAmount },
                { "revisedTaxAmount", (double)line.RevisedTaxAmount },
                { "mrp", (double)line.Mrp },
                { "costPrice", (double)line.CostPrice },
                { "marginPercent", (double)line.MarginPercent },
                { "taxPercent", (double)line.TaxPercent },
                { "cgstPercent", (double)line.CgstPercent },
                { "sgstPercent", (double)line.SgstPercent },
                { "igstPercent", (double)line.IgstPercent },
                { "cgstAmount", (double)line.CgstAmount },
                { "sgstAmount", (double)line.SgstAmount },
                { "igstAmount", (double)line.IgstAmount },
                { "taxAmount", (double)line.TaxAmount },
            });
        }

        return linesArr;
    }

    public static IReadOnlyList<BillingLineItem> LoadLinesFromDocument(BsonDocument doc, bool isInterState)
    {
        var result = new List<BillingLineItem>();
        if (!doc.TryGetValue("lines", out var linesVal) || !linesVal.IsBsonArray)
            return result;

        foreach (var lineBson in linesVal.AsBsonArray.OfType<BsonDocument>())
        {
            result.Add(new BillingLineItem
            {
                LineNo = lineBson.GetValue("lineNo", 0).ToInt32(),
                CentralProductId = lineBson.GetValue("centralProductId", "").AsString,
                ProductCode = lineBson.GetValue("sku", "").AsString,
                Description = lineBson.GetValue("description", "").AsString,
                HsnCode = lineBson.GetValue("hsn", "").AsString,
                Qty = (decimal)lineBson.GetValue("qty", 0).ToDouble(),
                Rate = (decimal)lineBson.GetValue("rate", 0).ToDouble(),
                Mrp = (decimal)lineBson.GetValue("mrp", 0).ToDouble(),
                TaxPercent = (decimal)lineBson.GetValue("taxPercent", 0).ToDouble(),
                IsIgst = isInterState,
                AlterationAmount = (decimal)lineBson.GetValue("alterationAmount", 0).ToDouble(),
            });
        }

        return result;
    }

    public static BillingHeaderSnap ReadHeader(BsonDocument doc)
    {
        return new BillingHeaderSnap
        {
            BillDate = doc.GetValue("billDate", "").AsString,
            CustomerCode = doc.GetValue("customerCode", "").AsString,
            CustomerName = doc.GetValue("customerName", "").AsString,
            CustomerPhone = doc.GetValue("customerPhone", "").AsString,
            Salesman = doc.GetValue("salesman", "").AsString,
            SalesmanCode = doc.GetValue("salesmanCode", "").AsString,
            SalesmanId = doc.GetValue("salesmanId", "").AsString,
            HoldBills = doc.GetValue("holdBills", false).AsBoolean,
            DoorDelivery = doc.GetValue("doorDelivery", false).AsBoolean,
            Stitching = doc.GetValue("stitching", false).AsBoolean,
            DeliveryDateRaw = doc.GetValue("deliveryDate", "").AsString,
            PrintInvoice = doc.GetValue("printInvoice", true).AsBoolean,
            IsInterState = doc.Contains("isInterState") && doc["isInterState"].AsBoolean,
            ItemDiscountPercent = (decimal)doc.GetValue("itemDiscountPercent", 0).ToDouble(),
            CashDiscAmount = (decimal)doc.GetValue("cashDiscAmount", 0).ToDouble(),
            AlterationGstIncluded = doc.GetValue("alterationGstIncluded", false).AsBoolean,
            Payable = (decimal)doc.GetValue("payable", 0).ToDouble(),
        };
    }

    public static string FormatDeliveryDate(System.DateTime? deliveryDate) =>
        deliveryDate?.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture).ToUpperInvariant() ?? "";

    public static System.DateTime? ParseDeliveryDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return System.DateTime.TryParseExact(
            value.Trim(),
            "dd-MMM-yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var dt)
            ? dt
            : null;
    }
}

public sealed class BillingHeaderSnap
{
    public string BillDate { get; init; } = "";
    public string CustomerCode { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public string Salesman { get; init; } = "";
    public string SalesmanCode { get; init; } = "";
    public string SalesmanId { get; init; } = "";
    public bool HoldBills { get; init; }
    public bool DoorDelivery { get; init; }
    public bool Stitching { get; init; }
    public string DeliveryDateRaw { get; init; } = "";
    public bool PrintInvoice { get; init; } = true;
    public bool IsInterState { get; init; }
    public decimal ItemDiscountPercent { get; init; }
    public decimal CashDiscAmount { get; init; }
    public bool AlterationGstIncluded { get; init; }
    public decimal Payable { get; init; }
}
