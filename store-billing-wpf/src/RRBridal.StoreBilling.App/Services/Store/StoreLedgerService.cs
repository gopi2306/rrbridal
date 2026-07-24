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

    public async Task<IReadOnlyList<string>> GetDistinctPosCountersAsync(string storeId, CancellationToken ct = default)
    {
        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var storeFilter = Builders<BsonDocument>.Filter.Eq("storeId", storeId);
        var billDocs = await billsColl.Find(storeFilter).ToListAsync(ct);

        var fromDb = billDocs
            .Select(d => ReadString(d, "posCounter") ?? "")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => int.TryParse(p, out var n) ? n : int.MaxValue)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var merged = new HashSet<string>(fromDb, StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i <= 3; i++)
            merged.Add(i.ToString(CultureInfo.InvariantCulture));

        return merged
            .OrderBy(p => int.TryParse(p, out var n) ? n : int.MaxValue)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<StoreLedgerSnapshot> LoadAsync(
        string storeId,
        int maxBills,
        int maxPayments,
        ReportScope scope = ReportScope.ThisCounter,
        string? deviceId = null,
        string? posCounterFilter = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        maxBills = Math.Clamp(maxBills, 1, 500);
        maxPayments = Math.Clamp(maxPayments, 1, 500);

        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var payColl = _db.GetCollection<BsonDocument>("local_payments");

        var storeFilter = Builders<BsonDocument>.Filter.Eq("storeId", storeId);
        var billDocs = await billsColl.Find(storeFilter).ToListAsync(ct);

        var bills = billDocs
            .Where(d => MatchesScope(d, deviceId, scope))
            .Where(d => MatchesPosCounterFilter(d, posCounterFilter))
            .Where(d => MatchesBillDateFilter(d, dateFrom, dateTo))
            .Select(MapBill)
            .Where(x => x != null)
            .Cast<LedgerBillRow>()
            .OrderByDescending(x => x.SortUtc)
            .Take(maxBills)
            .ToList();

        var payDocs = await payColl.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct);
        var payments = payDocs
            .Where(d => MatchesPaymentScope(d, storeId, deviceId, scope))
            .Where(d => MatchesPaymentPosCounterFilter(d, posCounterFilter))
            .Where(d => MatchesPaymentDateFilter(d, dateFrom, dateTo))
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

        var pos = ReadString(doc, "posCounter") ?? "";
        var dev = ReadString(doc, "deviceId") ?? "";
        var counterDisplay = CounterDisplayFormatter.Format(pos, dev);

        return new LedgerBillRow
        {
            BillNo = billNo,
            BillDate = billDate,
            CustomerName = customer,
            Payable = payable,
            PostedAtUtc = posted,
            Status = status,
            CounterDisplay = counterDisplay,
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

        var pos = ReadString(doc, "PosCounter", "posCounter") ?? "";
        var dev = ReadString(doc, "DeviceId", "deviceId") ?? "";
        var counterDisplay = CounterDisplayFormatter.Format(pos, dev);

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
            CounterDisplay = counterDisplay,
        };
    }

    private static bool MatchesPosCounterFilter(BsonDocument doc, string? posCounterFilter)
    {
        if (string.IsNullOrWhiteSpace(posCounterFilter))
            return true;
        var pos = ReadString(doc, "posCounter") ?? "";
        return string.Equals(pos, posCounterFilter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesPaymentPosCounterFilter(BsonDocument doc, string? posCounterFilter)
    {
        if (string.IsNullOrWhiteSpace(posCounterFilter))
            return true;
        var pos = ReadString(doc, "PosCounter", "posCounter") ?? "";
        return string.Equals(pos, posCounterFilter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesBillDateFilter(BsonDocument doc, DateTime? dateFrom, DateTime? dateTo)
    {
        if (dateFrom == null && dateTo == null)
            return true;
        if (!TryGetUtcDate(doc, "createdAtUtc", out var utc))
            return false;
        return IsLocalDateInRange(utc, dateFrom, dateTo);
    }

    private static bool MatchesPaymentDateFilter(BsonDocument doc, DateTime? dateFrom, DateTime? dateTo)
    {
        if (dateFrom == null && dateTo == null)
            return true;
        var created = ReadDateTimeUtc(doc, "CreatedAt", "createdAt");
        if (created == DateTime.MinValue && doc.TryGetValue("_id", out var id) && id.IsObjectId)
            created = DateTime.SpecifyKind(id.AsObjectId.CreationTime, DateTimeKind.Utc);
        if (created == DateTime.MinValue)
            return false;
        return IsLocalDateInRange(created, dateFrom, dateTo);
    }

    private static bool IsLocalDateInRange(DateTime utc, DateTime? dateFrom, DateTime? dateTo)
    {
        var localDate = utc.ToLocalTime().Date;
        if (dateFrom.HasValue && localDate < dateFrom.Value.Date)
            return false;
        if (dateTo.HasValue && localDate > dateTo.Value.Date)
            return false;
        return true;
    }

    private static bool MatchesScope(BsonDocument doc, string? deviceId, ReportScope scope)
    {
        if (scope == ReportScope.StoreWide)
            return true;
        if (string.IsNullOrWhiteSpace(deviceId))
            return false;
        if (!doc.TryGetValue("deviceId", out var v) || !v.IsString)
            return false;
        return string.Equals(v.AsString, deviceId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesPaymentScope(BsonDocument doc, string storeId, string? deviceId, ReportScope scope)
    {
        if (scope == ReportScope.StoreWide)
        {
            var docStore = ReadString(doc, "StoreId", "storeId");
            return string.IsNullOrWhiteSpace(docStore)
                || string.Equals(docStore, storeId, StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrWhiteSpace(deviceId))
            return false;
        if (!doc.TryGetValue("deviceId", out var dev) && !doc.TryGetValue("DeviceId", out dev))
            return false;
        if (!dev.IsString)
            return false;
        return string.Equals(dev.AsString, deviceId, StringComparison.OrdinalIgnoreCase);
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
