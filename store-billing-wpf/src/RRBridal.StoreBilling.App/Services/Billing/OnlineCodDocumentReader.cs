using System;
using System.Globalization;
using MongoDB.Bson;

namespace RRBridal.StoreBilling.App.Services.Billing;

public static class OnlineCodDocumentReader
{
    public const string SalesChannelOnline = "online";
    public const string StatusPending = "pending";
    public const string StatusReceived = "received";

    public static bool IsOnlineCodBill(BsonDocument doc) =>
        string.Equals(ReadString(doc, "salesChannel"), SalesChannelOnline, StringComparison.OrdinalIgnoreCase);

    public static bool IsOnlineCodPending(BsonDocument doc)
    {
        if (!IsOnlineCodBill(doc))
            return false;
        var status = ReadOnlineCodStatus(doc);
        return string.Equals(status, StatusPending, StringComparison.OrdinalIgnoreCase);
    }

    public static string ReadOnlineCodStatus(BsonDocument doc)
    {
        if (!doc.TryGetValue("onlineCod", out var oc) || !oc.IsBsonDocument)
            return "";
        return ReadString(oc.AsBsonDocument, "status") ?? "";
    }

    public static decimal ReadOnlineCodAmount(BsonDocument doc)
    {
        if (doc.TryGetValue("onlineCod", out var oc) && oc.IsBsonDocument)
        {
            var amt = ReadDecimal(oc.AsBsonDocument, "amount");
            if (amt > 0)
                return amt;
        }

        return ReadDecimal(doc, "payable");
    }

    public static string? ReadTransactionNo(BsonDocument doc)
    {
        if (!doc.TryGetValue("onlineCod", out var oc) || !oc.IsBsonDocument)
            return null;
        return ReadString(oc.AsBsonDocument, "transactionNo");
    }

    public static string? ReadReceivedPaymentMode(BsonDocument doc)
    {
        if (!doc.TryGetValue("onlineCod", out var oc) || !oc.IsBsonDocument)
            return null;
        return ReadString(oc.AsBsonDocument, "receivedPaymentMode");
    }

    public static bool IsOnlineCodReceived(BsonDocument doc) =>
        IsOnlineCodBill(doc)
        && string.Equals(ReadOnlineCodStatus(doc), StatusReceived, StringComparison.OrdinalIgnoreCase);

    public static bool TryReadReceivedAtUtc(BsonDocument doc, out DateTime receivedUtc)
    {
        receivedUtc = default;
        if (!doc.TryGetValue("onlineCod", out var oc) || !oc.IsBsonDocument)
            return false;
        var receivedAt = ReadString(oc.AsBsonDocument, "receivedAtUtc");
        if (string.IsNullOrWhiteSpace(receivedAt))
            return false;
        if (!DateTime.TryParse(receivedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return false;
        receivedUtc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        return true;
    }

    public static bool MatchesReceivedLocalDay(BsonDocument doc, DateTime localDate) =>
        TryReadReceivedAtUtc(doc, out var receivedUtc)
        && receivedUtc.ToLocalTime().Date == localDate.Date;

    public static DateTime ReadSortUtc(BsonDocument doc)
    {
        if (!doc.TryGetValue("createdAtUtc", out var cu) || !cu.IsString)
            return DateTime.MinValue;
        return DateTime.TryParse(cu.AsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
            ? dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime()
            : DateTime.MinValue;
    }

    public static bool InCreatedDateRange(DateTime utc, DateTime? from, DateTime? to)
    {
        if (utc == DateTime.MinValue)
            return from == null && to == null;
        if (from.HasValue && utc.Date < from.Value.Date)
            return false;
        if (to.HasValue && utc.Date > to.Value.Date)
            return false;
        return true;
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
