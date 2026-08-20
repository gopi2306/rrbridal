using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class OutboundDispatchPrintMapper
{
    public static OutboundDispatchPrintInput FromDocument(
        BsonDocument dispatch,
        StoreProfile store,
        int charWidth = 48)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        ArgumentNullException.ThrowIfNull(store);

        var shipTo = ReadDocument(dispatch, "shipTo");
        var snapshot = ReadDocument(dispatch, "billSnapshot");
        var customer = ReadDocument(snapshot, "customer");
        var address = JoinNonEmpty(
            Read(shipTo, "addressLine1"),
            Read(shipTo, "addressLine2"),
            Read(shipTo, "landmark"),
            JoinNonEmpty(Read(shipTo, "city"), Read(shipTo, "state"), Read(shipTo, "pincode")));

        return new OutboundDispatchPrintInput
        {
            Store = store,
            CharWidth = NormalizeWidth(charWidth),
            DispatchNo = Read(dispatch, "dispatchNo"),
            BatchNo = Read(dispatch, "batchNo"),
            BillNo = First(Read(dispatch, "billNo"), Read(snapshot, "billNo")),
            RecipientName = First(Read(shipTo, "name"), Read(customer, "name")),
            RecipientAddress = address,
            RecipientPhone = First(Read(shipTo, "phone"), Read(customer, "phone")),
            CarrierType = Read(dispatch, "carrierType"),
            CarrierName = Read(dispatch, "carrierName"),
            TrackingNo = Read(dispatch, "trackingNo"),
            PackageCount = Math.Max(0, ReadInt(dispatch, "packageCount")),
            FeePayer = Read(dispatch, "feePayer").ToLowerInvariant(),
            DispatchFee = ReadDecimal(dispatch, "dispatchFee"),
            Status = Read(dispatch, "status"),
            Date = First(Read(dispatch, "businessDate"), FormatDate(Read(dispatch, "createdAtUtc"))),
            PreparedBy = ReadLatestActor(dispatch),
            Lines = ReadLines(dispatch, snapshot),
        };
    }

    public static OutboundDispatchBatchPrintInput ToBatch(
        IEnumerable<BsonDocument> dispatches,
        StoreProfile store,
        int charWidth = 48)
    {
        ArgumentNullException.ThrowIfNull(dispatches);
        var mapped = dispatches.Select(d => FromDocument(d, store, charWidth)).ToList();
        if (mapped.Count == 0)
            throw new ArgumentException("At least one outbound dispatch is required.", nameof(dispatches));

        var first = mapped[0];
        return new OutboundDispatchBatchPrintInput
        {
            Store = store,
            CharWidth = NormalizeWidth(charWidth),
            BatchNo = first.BatchNo,
            CarrierName = first.CarrierName,
            CarrierType = first.CarrierType,
            Date = first.Date,
            Rows = mapped.Select(d => new OutboundDispatchBatchRow
            {
                DispatchNo = d.DispatchNo,
                BillNo = d.BillNo,
                CustomerName = d.RecipientName,
                CustomerPhone = d.RecipientPhone,
                TrackingNo = d.TrackingNo,
                PackageCount = d.PackageCount,
                FeePayer = d.FeePayer,
                DispatchFee = d.DispatchFee,
            }).ToList(),
        };
    }

    public static OutboundDispatchChargeReceiptPrintInput ToChargeReceipt(
        BsonDocument dispatch,
        StoreProfile store,
        int charWidth = 48)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        var receipt = ReadDocument(dispatch, "chargeReceipt")
            ?? throw new InvalidOperationException("The dispatch does not contain a customer dispatch-charge receipt.");
        var snapshot = ReadDocument(dispatch, "billSnapshot");
        var customer = ReadDocument(snapshot, "customer");

        return new OutboundDispatchChargeReceiptPrintInput
        {
            Store = store,
            CharWidth = NormalizeWidth(charWidth),
            ReceiptNo = First(Read(receipt, "receiptNo"), Read(dispatch, "chargeReceiptNo")),
            BillNo = First(Read(receipt, "billNo"), Read(dispatch, "billNo")),
            DispatchNo = First(Read(receipt, "dispatchNo"), Read(dispatch, "dispatchNo")),
            CustomerName = First(Read(receipt, "customerName"), Read(customer, "name")),
            CustomerPhone = First(Read(receipt, "customerPhone"), Read(customer, "phone")),
            Amount = ReadDecimal(receipt, "amount"),
            PaymentMode = Read(receipt, "mode"),
            Reference = Read(receipt, "reference"),
            Date = FormatDate(Read(receipt, "createdAtUtc")),
            ReceivedBy = Read(receipt, "receivedBy"),
        };
    }

    private static IReadOnlyList<OutboundDispatchPrintLine> ReadLines(
        BsonDocument dispatch,
        BsonDocument? snapshot)
    {
        var array = ReadArray(dispatch, "lines") ?? ReadArray(snapshot, "lines");
        if (array == null)
            return Array.Empty<OutboundDispatchPrintLine>();

        return array.OfType<BsonDocument>().Select(line => new OutboundDispatchPrintLine
        {
            Sku = First(
                Read(line, "sku"),
                Read(line, "itemCode"),
                Read(line, "productCode"),
                Read(line, "barcode")),
            Description = First(
                Read(line, "description"),
                Read(line, "productName"),
                Read(line, "itemName")),
            Quantity = FirstDecimal(line, "qty", "quantity"),
        }).ToList();
    }

    private static string ReadLatestActor(BsonDocument dispatch)
    {
        var audit = ReadArray(dispatch, "audit") ?? ReadArray(dispatch, "auditHistory");
        var entry = audit?.OfType<BsonDocument>().LastOrDefault();
        return entry == null ? "" : First(Read(entry, "actorName"), Read(entry, "actorEmail"));
    }

    private static string FormatDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed.ToString("dd-MMM-yyyy HH:mm", CultureInfo.GetCultureInfo("en-IN"))
            : value;
    }

    private static int NormalizeWidth(int width) => width is >= 32 and <= 56 ? width : 48;

    private static string JoinNonEmpty(params string[] parts) =>
        string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string First(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";

    private static BsonDocument? ReadDocument(BsonDocument? document, string field) =>
        document != null
        && document.TryGetValue(field, out var value)
        && value.IsBsonDocument
            ? value.AsBsonDocument
            : null;

    private static BsonArray? ReadArray(BsonDocument? document, string field) =>
        document != null
        && document.TryGetValue(field, out var value)
        && value.IsBsonArray
            ? value.AsBsonArray
            : null;

    private static string Read(BsonDocument? document, string field)
    {
        if (document == null || !document.TryGetValue(field, out var value) || value.IsBsonNull)
            return "";
        return (value.IsString ? value.AsString : value.ToString() ?? "").Trim();
    }

    private static int ReadInt(BsonDocument document, string field) =>
        decimal.ToInt32(decimal.Truncate(ReadDecimal(document, field)));

    private static decimal FirstDecimal(BsonDocument document, params string[] fields)
    {
        foreach (var field in fields)
        {
            if (document.Contains(field))
                return ReadDecimal(document, field);
        }
        return 0m;
    }

    private static decimal ReadDecimal(BsonDocument document, string field)
    {
        if (!document.TryGetValue(field, out var value) || value.IsBsonNull)
            return 0m;
        return value.BsonType switch
        {
            BsonType.Decimal128 => (decimal)value.AsDecimal128,
            BsonType.Double => (decimal)value.AsDouble,
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            _ => decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
                ? number
                : 0m,
        };
    }
}
