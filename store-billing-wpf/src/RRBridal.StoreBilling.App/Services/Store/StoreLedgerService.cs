using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class StoreLedgerService
{
    private readonly IMongoDatabase _db;

    public StoreLedgerService(IMongoDatabase localDb)
    {
        _db = localDb;
    }

    public async Task<StoreLedgerSnapshot> LoadAsync(string storeId, int maxBills, int maxPayments, CancellationToken ct = default)
    {
        maxBills = Math.Clamp(maxBills, 1, 500);
        maxPayments = Math.Clamp(maxPayments, 1, 500);

        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var payColl = _db.GetCollection<BsonDocument>("local_payments");

        var storeFilter = Builders<BsonDocument>.Filter.Eq("storeId", storeId);
        var billDocs = await billsColl.Find(storeFilter).ToListAsync(ct);

        var bills = billDocs
            .Select(MapBill)
            .Where(x => x != null)
            .Cast<LedgerBillRow>()
            .OrderByDescending(x => x.SortUtc)
            .Take(maxBills)
            .ToList();

        var payDocs = await payColl.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct);
        var payments = payDocs
            .Select(MapPayment)
            .OrderByDescending(x => x.SortUtc)
            .Take(maxPayments)
            .ToList();

        return new StoreLedgerSnapshot
        {
            Bills = bills,
            Payments = payments,
        };
    }

    private static LedgerBillRow? MapBill(BsonDocument doc)
    {
        if (!TryGetUtcDate(doc, "createdAtUtc", out var sortUtc))
            sortUtc = DateTime.MinValue;

        var billNo = ReadString(doc, "billNo") ?? "";
        var billDate = ReadString(doc, "billDate") ?? "";
        var customer = ReadString(doc, "customerName") ?? "";
        var payable = ReadDecimal(doc, "payable");
        var posted = sortUtc == DateTime.MinValue
            ? "—"
            : sortUtc.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC";
        var status = ReadString(doc, "status") ?? "posted";

        return new LedgerBillRow
        {
            BillNo = billNo,
            BillDate = billDate,
            CustomerName = customer,
            Payable = payable,
            PostedAtUtc = posted,
            Status = status,
            SortUtc = sortUtc,
        };
    }

    private static LedgerPaymentRow MapPayment(BsonDocument doc)
    {
        var created = ReadDateTimeUtc(doc, "CreatedAt", "createdAt");
        if (created == DateTime.MinValue && doc.TryGetValue("_id", out var id) && id.IsObjectId)
            created = DateTime.SpecifyKind(id.AsObjectId.CreationTime, DateTimeKind.Utc);

        var createdDisplay = created == DateTime.MinValue
            ? "—"
            : created.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC";

        return new LedgerPaymentRow
        {
            SortUtc = created,
            CreatedAtUtc = created,
            CreatedAtDisplay = createdDisplay,
            InvoiceNo = ReadString(doc, "InvoiceNo", "invoiceNo") ?? "",
            Provider = ReadString(doc, "Provider", "provider") ?? "",
            Amount = ReadDecimal(doc, "Amount", "amount"),
            Currency = ReadString(doc, "Currency", "currency") ?? "INR",
            Status = ReadString(doc, "Status", "status") ?? "",
            ProviderReference = ReadString(doc, "ProviderReference", "providerReference") ?? "",
        };
    }

    private static string? ReadString(BsonDocument doc, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!doc.TryGetValue(key, out var v) || v.IsBsonNull)
                continue;
            if (v.IsString) return v.AsString;
            return v.ToString();
        }

        return null;
    }

    private static decimal ReadDecimal(BsonDocument doc, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!doc.TryGetValue(key, out var v) || v.IsBsonNull)
                continue;
            return v switch
            {
                { IsDouble: true } => (decimal)v.AsDouble,
                { IsInt32: true } => v.AsInt32,
                { IsInt64: true } => v.AsInt64,
                { IsDecimal128: true } => (decimal)v.AsDecimal128,
                { IsString: true } => decimal.TryParse(v.AsString, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m,
                _ => 0m,
            };
        }

        return 0m;
    }

    private static DateTime ReadDateTimeUtc(BsonDocument doc, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!doc.TryGetValue(key, out var v) || v.IsBsonNull)
                continue;
            if (v.IsBsonDateTime)
                return DateTime.SpecifyKind(v.ToUniversalTime(), DateTimeKind.Utc);
            if (v.IsString && DateTime.TryParse(v.AsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        }

        return DateTime.MinValue;
    }

    private static bool TryGetUtcDate(BsonDocument doc, string key, out DateTime utc)
    {
        utc = default;
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull || !v.IsString)
            return false;
        if (!DateTime.TryParse(v.AsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return false;
        utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        return true;
    }
}
