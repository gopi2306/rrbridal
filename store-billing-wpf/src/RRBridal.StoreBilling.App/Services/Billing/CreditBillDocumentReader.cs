using System;
using System.Globalization;
using MongoDB.Bson;

namespace RRBridal.StoreBilling.App.Services.Billing;

public static class CreditBillDocumentReader
{
    public const string StatusPending = "pending";
    public const string StatusPartial = "partial";
    public const string StatusSettled = "settled";

    public static bool HasCreditBilling(BsonDocument doc) =>
        doc.TryGetValue("creditBilling", out var cb) && cb.IsBsonDocument;

    public static BsonDocument? GetCreditBilling(BsonDocument doc) =>
        HasCreditBilling(doc) ? doc["creditBilling"].AsBsonDocument : null;

    public static string ReadStatus(BsonDocument doc)
    {
        var cb = GetCreditBilling(doc);
        return cb == null ? "" : ReadString(cb, "status") ?? "";
    }

    public static bool IsOpen(BsonDocument doc)
    {
        var status = ReadStatus(doc);
        return string.Equals(status, StatusPending, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, StatusPartial, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSettled(BsonDocument doc) =>
        string.Equals(ReadStatus(doc), StatusSettled, StringComparison.OrdinalIgnoreCase);

    public static decimal ReadTotalPayable(BsonDocument doc)
    {
        var cb = GetCreditBilling(doc);
        if (cb != null)
        {
            var total = ReadDecimal(cb, "totalPayable");
            if (total > 0)
                return total;
        }

        return ReadDecimal(doc, "payable");
    }

    public static decimal ReadAdvanceAtPost(BsonDocument doc)
    {
        var cb = GetCreditBilling(doc);
        return cb == null ? 0m : ReadDecimal(cb, "advanceAtPost");
    }

    public static decimal ReadAmountPaid(BsonDocument doc)
    {
        var cb = GetCreditBilling(doc);
        return cb == null ? 0m : ReadDecimal(cb, "amountPaid");
    }

    public static decimal ReadBalanceDue(BsonDocument doc)
    {
        var cb = GetCreditBilling(doc);
        if (cb != null)
        {
            if (cb.Contains("balanceDue"))
                return ReadDecimal(cb, "balanceDue");
            return Math.Max(0m, ReadTotalPayable(doc) - ReadAmountPaid(doc));
        }

        return 0m;
    }

    public static string ResolveStatus(decimal totalPayable, decimal amountPaid)
    {
        if (amountPaid <= 0m)
            return StatusPending;
        if (amountPaid + 0.009m < totalPayable)
            return StatusPartial;
        return StatusSettled;
    }

    public static BsonDocument BuildCreditBillingDocument(
        decimal totalPayable,
        decimal advanceAtPost,
        decimal amountPaid,
        bool creditCustomer,
        BsonArray? payments = null)
    {
        var balance = Math.Max(0m, MoneyMath.RoundDisplayAmount(totalPayable - amountPaid));
        return new BsonDocument
        {
            { "status", ResolveStatus(totalPayable, amountPaid) },
            { "totalPayable", (double)totalPayable },
            { "advanceAtPost", (double)advanceAtPost },
            { "amountPaid", (double)amountPaid },
            { "balanceDue", (double)balance },
            { "creditCustomer", creditCustomer },
            { "payments", payments ?? new BsonArray() },
        };
    }

    public static DateTime ReadSortUtc(BsonDocument doc)
    {
        if (!doc.TryGetValue("createdAtUtc", out var cu) || !cu.IsString)
            return DateTime.MinValue;
        return DateTime.TryParse(cu.AsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
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
            _ => 0m,
        };
    }
}
