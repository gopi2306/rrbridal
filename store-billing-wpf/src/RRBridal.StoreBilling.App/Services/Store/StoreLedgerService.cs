using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Api;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class StoreLedgerService
{
    private readonly IMongoDatabase _db;
    private CentralOnlineModeService? _centralMode;
    private CentralStorePosClient? _storePos;

    public StoreLedgerService(IMongoDatabase localDb)
    {
        _db = localDb;
    }

    public void ConfigureOnline(CentralOnlineModeService centralMode, CentralStorePosClient storePos)
    {
        _centralMode = centralMode;
        _storePos = storePos;
    }

    private bool IsCentralOnline => _centralMode?.IsOnlineMode == true && _storePos != null;

    public async Task<IReadOnlyList<string>> GetDistinctPosCountersAsync(string storeId, CancellationToken ct = default)
    {
        var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i <= 3; i++)
            merged.Add(i.ToString(CultureInfo.InvariantCulture));

        if (IsCentralOnline)
        {
            using var onlineJson = await _storePos!.ListBillsAsync(null, 500, ct);
            if (onlineJson.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in onlineJson.RootElement.EnumerateArray())
                {
                    var doc = BillDocumentService.MapCentralBillToDoc(el);
                    var pos = ReadString(doc, "posCounter") ?? "";
                    if (!string.IsNullOrWhiteSpace(pos))
                        merged.Add(pos);
                }
            }

            return merged
                .OrderBy(p => int.TryParse(p, out var n) ? n : int.MaxValue)
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var storeFilter = Builders<BsonDocument>.Filter.Eq("storeId", storeId);
        var billDocs = await billsColl.Find(storeFilter).ToListAsync(ct);

        var fromDb = billDocs
            .Select(d => ReadString(d, "posCounter") ?? "")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
        foreach (var p in fromDb)
            merged.Add(p);

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

        if (IsCentralOnline)
        {
            return await LoadOnlineAsync(maxBills, scope, deviceId, posCounterFilter, dateFrom, dateTo, ct);
        }

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

    private async Task<StoreLedgerSnapshot> LoadOnlineAsync(
        int maxBills,
        ReportScope scope,
        string? deviceId,
        string? posCounterFilter,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken ct)
    {
        using var json = await _storePos!.ListBillsAsync(null, Math.Min(500, maxBills * 3), ct);
        var billDocs = new List<BsonDocument>();
        if (json.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in json.RootElement.EnumerateArray())
                billDocs.Add(BillDocumentService.MapCentralBillToDoc(el));
        }

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

        using var payJson = await _storePos.ListGatewayPaymentsAsync(500, posCounterFilter, ct);
        var payments = new List<LedgerPaymentRow>();
        if (payJson.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in payJson.RootElement.EnumerateArray())
            {
                var doc = MapCentralGatewayPaymentToDoc(el);
                if (!MatchesPaymentPosCounterFilter(doc, posCounterFilter))
                    continue;
                if (!MatchesPaymentDateFilter(doc, dateFrom, dateTo))
                    continue;
                payments.Add(MapPayment(doc));
            }
        }

        payments = payments
            .OrderByDescending(x => x.SortUtc)
            .Take(maxBills)
            .ToList();

        return new StoreLedgerSnapshot
        {
            Bills = bills,
            Payments = payments,
        };
    }

    private static BsonDocument MapCentralGatewayPaymentToDoc(JsonElement el)
    {
        BsonDocument doc;
        if (el.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
            doc = BsonDocument.Parse(payload.GetRawText());
        else
            doc = BsonDocument.Parse(el.GetRawText());

        if (el.TryGetProperty("invoiceNo", out var inv) && inv.ValueKind == JsonValueKind.String)
            doc["invoiceNo"] = inv.GetString() ?? "";
        if (el.TryGetProperty("posCounter", out var pos) && pos.ValueKind == JsonValueKind.String)
            doc["posCounter"] = pos.GetString() ?? "";
        if (el.TryGetProperty("deviceId", out var dev) && dev.ValueKind == JsonValueKind.String)
            doc["deviceId"] = dev.GetString() ?? "";
        if (el.TryGetProperty("createdAt", out var created))
        {
            if (created.ValueKind == JsonValueKind.String)
                doc["createdAt"] = created.GetString() ?? "";
            else if (created.ValueKind == JsonValueKind.Number || created.ValueKind != JsonValueKind.Null)
                doc["createdAt"] = created.ToString();
        }

        return doc;
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
