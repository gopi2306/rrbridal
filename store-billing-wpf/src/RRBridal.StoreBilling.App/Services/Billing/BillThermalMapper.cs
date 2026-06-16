using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Payments;

namespace RRBridal.StoreBilling.App.Services.Billing;

public static class BillThermalMapper
{
    public static ThermalInvoiceInput MapFromBillDocument(
        BsonDocument doc,
        StoreProfile store,
        int charWidth,
        bool isDuplicate,
        string? duplicatePrintedBy = null,
        DateTime? duplicatePrintedAtUtc = null)
    {
        var lines = new List<InvoiceLineSnap>();
        if (doc.TryGetValue("lines", out var linesVal) && linesVal.IsBsonArray)
        {
            foreach (BsonDocument line in linesVal.AsBsonArray.OfType<BsonDocument>())
            {
                var amount = ReadDecimal(line, "amount");
                var disc = ReadDecimal(line, "discountAmount") + ReadDecimal(line, "cashDiscountAmount");
                lines.Add(new InvoiceLineSnap
                {
                    LineNo = line.GetValue("lineNo", 0).ToInt32(),
                    Description = ReadString(line, "description") ?? "",
                    Hsn = ReadString(line, "hsn") ?? "",
                    TaxPercent = ReadDecimal(line, "taxPercent"),
                    Qty = ReadDecimal(line, "qty"),
                    Rate = ReadDecimal(line, "rate"),
                    Mrp = ReadDecimal(line, "mrp"),
                    Amount = amount,
                    LineDiscount = disc,
                    TaxableAmount = ReadDecimal(line, "revisedAmount"),
                    TaxAmount = ReadDecimal(line, "revisedTaxAmount"),
                    LineInclusiveAmount = ReadDecimal(line, "revisedInclusiveAmount"),
                });
            }
        }

        var active = lines.Where(l => l.Amount > 0 || l.TaxableAmount > 0).ToList();
        PaymentReceiptSnap? paySnap = null;
        if (doc.TryGetValue("payments", out var payVal) && payVal.IsBsonArray)
        {
            var legs = new List<(PaymentProviderKind Provider, decimal Amount)>();
            foreach (BsonDocument p in payVal.AsBsonArray.OfType<BsonDocument>())
            {
                var providerName = ReadString(p, "provider") ?? "";
                if (!Enum.TryParse<PaymentProviderKind>(providerName, true, out var kind))
                    continue;
                legs.Add((kind, ReadDecimal(p, "amount")));
            }

            var mode = ReadString(doc, "paymentMode") ?? "Cash";
            paySnap = PaymentReceiptSnap.FromLegs(legs, mode);
        }

        var billDate = ReadString(doc, "billDate") ?? "";
        var createdUtc = ReadCreatedUtc(doc);
        var time = createdUtc == DateTime.MinValue
            ? ""
            : createdUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        var itemDiscount = ReadDecimal(doc, "itemDiscount");
        var cashDiscAmount = ReadDecimal(doc, "cashDiscAmount");
        var itemDiscountPercent = ReadDecimal(doc, "itemDiscountPercent");
        if (itemDiscountPercent <= 0 && itemDiscount > 0)
        {
            var discountBase = ReadDecimal(doc, "originalInclusiveTotal");
            if (discountBase <= 0)
                discountBase = ReadDecimal(doc, "subTotal");
            if (discountBase > 0)
                itemDiscountPercent = Math.Round(itemDiscount / discountBase * 100m, 2, MidpointRounding.AwayFromZero);
        }

        return new ThermalInvoiceInput
        {
            Store = store,
            CharWidth = charWidth,
            BillNo = ReadString(doc, "billNo") ?? "",
            BillDate = billDate,
            UserName = ReadString(doc, "salesman") ?? "",
            Time = time,
            Counter = ReadString(doc, "posCounter") ?? "",
            CustomerName = ReadString(doc, "customerName") ?? "",
            CustomerPhone = ReadString(doc, "customerPhone") ?? "",
            Lines = active,
            SubTotal = ReadDecimal(doc, "subTotal"),
            OriginalTaxTotal = ReadDecimal(doc, "originalTaxTotal"),
            RevisedSubTotal = ReadDecimal(doc, "revisedSubTotal"),
            TaxTotal = ReadDecimal(doc, "taxTotal"),
            IsInterState = doc.Contains("isInterState") && doc["isInterState"].AsBoolean,
            CgstTotal = ReadDecimal(doc, "cgstTotal"),
            SgstTotal = ReadDecimal(doc, "sgstTotal"),
            IgstTotal = ReadDecimal(doc, "igstTotal"),
            ItemDiscountPercent = itemDiscountPercent,
            ItemDiscount = itemDiscount,
            CashDiscAmount = cashDiscAmount,
            RoundOff = ReadDecimal(doc, "roundOff"),
            Payable = ReadDecimal(doc, "payable"),
            TotalQty = active.Sum(l => l.Qty),
            ItemCount = active.Count,
            TotalMrp = active.Sum(l => l.Mrp * l.Qty),
            TotalLineAmount = active.Sum(l => l.Amount),
            TotalTaxableAmount = active.Sum(l => l.TaxableAmount),
            Savings = active.Sum(l => Math.Max(0m, l.Mrp * l.Qty - l.Amount)),
            Payments = paySnap,
            IsDuplicateCopy = isDuplicate,
            DuplicatePrintedBy = duplicatePrintedBy ?? "",
            DuplicatePrintedAtUtc = duplicatePrintedAtUtc,
            Stitching = doc.GetValue("stitching", false).AsBoolean,
            DoorDelivery = doc.GetValue("doorDelivery", false).AsBoolean,
            DeliveryDate = ReadString(doc, "deliveryDate") ?? "",
        };
    }

    private static DateTime ReadCreatedUtc(BsonDocument doc)
    {
        if (!doc.TryGetValue("createdAtUtc", out var v) || !v.IsString)
            return DateTime.MinValue;
        return DateTime.TryParse(v.AsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
            ? dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime()
            : DateTime.MinValue;
    }

    private static string? ReadString(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull)
            return null;
        return v.IsString ? v.AsString : v.ToString();
    }

    private static decimal ReadDecimal(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull)
            return 0m;
        return v switch
        {
            { IsDouble: true } => (decimal)v.AsDouble,
            { IsInt32: true } => v.AsInt32,
            { IsInt64: true } => v.AsInt64,
            { IsDecimal128: true } => (decimal)v.AsDecimal128,
            _ => 0m,
        };
    }
}
