using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Invoicing;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class SaleReturnDocumentMapper
{
    public static bool HasExchangeLines(BsonDocument returnDoc)
    {
        if (!returnDoc.TryGetValue("exchangeLines", out var el) || !el.IsBsonArray)
            return false;
        return el.AsBsonArray.Count > 0;
    }

    public static ThermalSaleReturnInput MapFromReturnDocument(
        BsonDocument returnDoc,
        StoreProfile store,
        int charWidth,
        string userName,
        string counter,
        string? creditNoteNo,
        bool isDuplicate,
        DateTime? returnPostedUtc = null)
    {
        var returnNo = ReadString(returnDoc, "returnNo") ?? "";
        var originalBillNo = ReadString(returnDoc, "originalBillNo") ?? "";
        var returnMode = ReadString(returnDoc, "returnMode") ?? "";
        var returnModeLabel = string.Equals(returnMode, "credit_note", StringComparison.OrdinalIgnoreCase)
            ? "Credit Note"
            : "Cash";

        var postedUtc = returnPostedUtc ?? ReadCreatedAtUtc(returnDoc);
        var localPosted = postedUtc == DateTime.MinValue ? DateTime.Now : postedUtc.ToLocalTime();

        var lines = ReadReturnLines(returnDoc);
        var grossAmount = lines.Sum(l => l.Amount);
        var cgst = ReadDecimal(returnDoc, "cgstTotal");
        var sgst = ReadDecimal(returnDoc, "sgstTotal");
        var igst = ReadDecimal(returnDoc, "igstTotal");
        var returnTotal = ReadDecimal(returnDoc, "returnTotal");
        if (returnTotal <= 0)
            returnTotal = lines.Sum(l => l.Amount);

        return new ThermalSaleReturnInput
        {
            Store = store,
            CharWidth = charWidth,
            ReturnNo = returnNo,
            OriginalBillNo = originalBillNo,
            UserName = userName,
            Counter = counter,
            ReturnDate = localPosted.ToString("dd/MM/yy", CultureInfo.GetCultureInfo("en-IN")),
            ReturnTime = localPosted.ToString("h:mmtt", CultureInfo.GetCultureInfo("en-IN")).ToUpperInvariant(),
            ReturnModeLabel = returnModeLabel,
            CreditNoteNo = creditNoteNo,
            Lines = lines,
            TotalQty = lines.Sum(l => l.Qty),
            ItemCount = lines.Count,
            GrossAmount = grossAmount,
            TaxTotal = cgst + sgst + igst,
            ReturnAmount = returnTotal,
            IsInterState = returnDoc.GetValue("isInterState", false).ToBoolean(),
            CgstTotal = cgst,
            SgstTotal = sgst,
            IgstTotal = igst,
            CashRefunded = ReadDecimal(returnDoc, "cashRefunded"),
            IsDuplicateCopy = isDuplicate,
        };
    }

    private static IReadOnlyList<SaleReturnLineSnap> ReadReturnLines(BsonDocument returnDoc)
    {
        var linesArray = returnDoc.TryGetValue("returnLines", out var rl) && rl.IsBsonArray
            ? rl.AsBsonArray
            : returnDoc.TryGetValue("lines", out var l) && l.IsBsonArray
                ? l.AsBsonArray
                : new BsonArray();

        var result = new List<SaleReturnLineSnap>();
        foreach (var item in linesArray)
        {
            if (!item.IsBsonDocument)
                continue;
            var line = item.AsBsonDocument;
            var sku = ReadString(line, "sku") ?? "";
            var description = ReadString(line, "description") ?? sku;
            var qty = ReadDecimal(line, "returnQty");
            if (qty <= 0)
                qty = ReadDecimal(line, "qty");
            var rate = ReadDecimal(line, "rate");
            var amount = ReadDecimal(line, "lineTotal");
            if (amount <= 0)
                amount = ReadDecimal(line, "amount");
            if (amount <= 0 && qty > 0 && rate > 0)
                amount = qty * rate;

            result.Add(new SaleReturnLineSnap
            {
                Description = description,
                Qty = qty,
                Rate = rate,
                Amount = amount,
            });
        }

        return result;
    }

    private static DateTime ReadCreatedAtUtc(BsonDocument doc)
    {
        if (!doc.TryGetValue("createdAtUtc", out var v) || !v.IsString)
            return DateTime.MinValue;
        return DateTime.TryParse(v.AsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
            ? dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime()
            : DateTime.MinValue;
    }

    private static string? ReadString(BsonDocument d, string key) =>
        d.TryGetValue(key, out var v) && !v.IsBsonNull
            ? v.IsString ? v.AsString : v.ToString()
            : null;

    private static decimal ReadDecimal(BsonDocument d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v.IsBsonNull)
            return 0m;
        return v switch
        {
            { IsDouble: true } => (decimal)v.AsDouble,
            { IsInt32: true } => v.AsInt32,
            { IsInt64: true } => v.AsInt64,
            { IsDecimal128: true } => (decimal)v.AsDecimal128,
            { IsBoolean: true } => v.AsBoolean ? 1 : 0,
            _ => 0m,
        };
    }
}
