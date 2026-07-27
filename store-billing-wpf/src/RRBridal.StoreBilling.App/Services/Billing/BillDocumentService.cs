using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Api;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Store;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Billing;

public sealed class BillSearchRow
{
    public required string BillNo { get; init; }
    public string BillDate { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public decimal Payable { get; init; }
    public string Status { get; init; } = "posted";
    public string PostedAtUtc { get; init; } = "";
    public string CounterDisplay { get; init; } = "";
    public DateTime SortUtc { get; init; }
}

public sealed class BillDocumentService
{
    private readonly IMongoCollection<BsonDocument> _bills;
    private readonly StoreContext _store;
    private readonly ReceiptConfigStore _receiptConfig;
    private CentralOnlineModeService? _centralMode;
    private CentralStorePosClient? _storePos;

    public BillDocumentService(IMongoDatabase localDb, StoreContext store, ReceiptConfigStore receiptConfig)
    {
        _bills = localDb.GetCollection<BsonDocument>("store_bills");
        _store = store;
        _receiptConfig = receiptConfig;
    }

    public void ConfigureOnline(CentralOnlineModeService centralMode, CentralStorePosClient storePos)
    {
        _centralMode = centralMode;
        _storePos = storePos;
    }

    private bool IsCentralOnline => _centralMode?.IsOnlineMode == true && _storePos != null;

    public async Task<BsonDocument?> GetByBillNoAsync(string billNo, CancellationToken ct = default)
    {
        var trimmed = billNo.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;

        if (IsCentralOnline)
        {
            try
            {
                using var json = await _storePos!.GetBillAsync(trimmed, ct);
                return MapCentralBillToDoc(json.RootElement);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        return await _bills.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
            Builders<BsonDocument>.Filter.Eq("billNo", trimmed))).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<BillSearchRow>> SearchBillsAsync(
        string? invoiceNo,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? customerName,
        string? customerPhone = null,
        string? status = "posted",
        int limit = 100,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 500);

        if (IsCentralOnline)
        {
            var search = !string.IsNullOrWhiteSpace(invoiceNo)
                ? invoiceNo
                : !string.IsNullOrWhiteSpace(customerName)
                    ? customerName
                    : !string.IsNullOrWhiteSpace(customerPhone)
                        ? customerPhone
                        : null;

            using var json = await _storePos!.ListBillsAsync(search, Math.Min(200, limit * 3), ct);
            var docs = new List<BsonDocument>();
            if (json.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in json.RootElement.EnumerateArray())
                    docs.Add(MapCentralBillToDoc(el));
            }

            IEnumerable<BsonDocument> query = docs;
            if (!string.IsNullOrWhiteSpace(status))
            {
                var st = status.Trim();
                query = query.Where(d =>
                    string.Equals(ReadString(d, "status") ?? "posted", st, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(invoiceNo))
            {
                var q = invoiceNo.Trim();
                query = query.Where(d =>
                    (ReadString(d, "billNo") ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                var n = customerName.Trim();
                query = query.Where(d =>
                    (ReadString(d, "customerName") ?? "").Contains(n, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(customerPhone))
            {
                var digits = new string(customerPhone.Trim().Where(char.IsDigit).ToArray());
                if (digits.Length > 0)
                {
                    query = query.Where(d =>
                        (ReadString(d, "customerPhone") ?? "").Contains(digits, StringComparison.Ordinal));
                }
            }

            return query
                .Select(MapSearchRow)
                .Where(r => r != null)
                .Cast<BillSearchRow>()
                .Where(r => InDateRange(r.SortUtc, dateFrom, dateTo))
                .Take(limit)
                .ToList();
        }

        var filters = new List<FilterDefinition<BsonDocument>>
        {
            Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
        };

        if (!string.IsNullOrWhiteSpace(status))
            filters.Add(Builders<BsonDocument>.Filter.Eq("status", status));

        if (!string.IsNullOrWhiteSpace(invoiceNo))
        {
            var safe = Regex.Escape(invoiceNo.Trim());
            filters.Add(Builders<BsonDocument>.Filter.Regex("billNo", new BsonRegularExpression(safe, "i")));
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var safe = Regex.Escape(customerName.Trim());
            filters.Add(Builders<BsonDocument>.Filter.Regex("customerName", new BsonRegularExpression(safe, "i")));
        }

        if (!string.IsNullOrWhiteSpace(customerPhone))
        {
            var digits = new string(customerPhone.Trim().Where(char.IsDigit).ToArray());
            if (digits.Length > 0)
            {
                var safe = Regex.Escape(digits);
                filters.Add(Builders<BsonDocument>.Filter.Regex("customerPhone", new BsonRegularExpression(safe)));
            }
        }

        var localDocs = await _bills
            .Find(Builders<BsonDocument>.Filter.And(filters))
            .Sort(Builders<BsonDocument>.Sort.Descending("createdAtUtc"))
            .Limit(limit * 3)
            .ToListAsync(ct);

        var rows = localDocs
            .Select(MapSearchRow)
            .Where(r => r != null)
            .Cast<BillSearchRow>()
            .Where(r => InDateRange(r.SortUtc, dateFrom, dateTo))
            .Take(limit)
            .ToList();

        return rows;
    }

    public async Task<bool> BillNoExistsAsync(string billNo, string? excludeBillNo = null, CancellationToken ct = default)
    {
        var trimmed = billNo.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return false;

        if (IsCentralOnline)
        {
            if (!string.IsNullOrWhiteSpace(excludeBillNo)
                && string.Equals(trimmed, excludeBillNo.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;
            var doc = await GetByBillNoAsync(trimmed, ct);
            return doc != null;
        }

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
            Builders<BsonDocument>.Filter.Eq("billNo", trimmed));
        if (!string.IsNullOrWhiteSpace(excludeBillNo))
            filter = Builders<BsonDocument>.Filter.And(filter,
                Builders<BsonDocument>.Filter.Ne("billNo", excludeBillNo.Trim()));
        return await _bills.Find(filter).AnyAsync(ct);
    }

    public ThermalInvoiceInput MapToThermalInput(BsonDocument doc, bool isDuplicate, string? printedBy = null)
    {
        var print = _receiptConfig.Current.Print;
        var charWidth = print.ReceiptCharWidth is >= 32 and <= 56 ? print.ReceiptCharWidth : 48;
        return BillThermalMapper.MapFromBillDocument(
            doc,
            _receiptConfig.Current.Store,
            charWidth,
            isDuplicate,
            printedBy,
            isDuplicate ? DateTime.UtcNow : null);
    }

    public async Task AppendPrintAuditAsync(
        string billNo,
        string kind,
        string printedBy,
        CancellationToken ct = default)
    {
        var entry = new BsonDocument
        {
            { "kind", kind },
            { "printedBy", printedBy },
            { "printedAtUtc", DateTime.UtcNow.ToString("O") },
            { "deviceId", _store.DeviceId },
            { "posCounter", _store.PosCounter },
        };

        if (IsCentralOnline)
        {
            await _storePos!.AppendBillPrintAuditAsync(
                billNo.Trim(),
                new
                {
                    kind,
                    printedBy,
                    printedAtUtc = entry["printedAtUtc"].AsString,
                    posCounter = _store.PosCounter,
                },
                ct);
            return;
        }

        await _bills.UpdateOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
                Builders<BsonDocument>.Filter.Eq("billNo", billNo.Trim())),
            Builders<BsonDocument>.Update.Push("printAudit", entry),
            cancellationToken: ct);
    }

    internal static BsonDocument MapCentralBillToDoc(JsonElement el)
    {
        BsonDocument doc;
        if (el.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
            doc = BsonDocument.Parse(payload.GetRawText());
        else
            doc = BsonDocument.Parse(el.GetRawText());

        if (el.TryGetProperty("billNo", out var billNoEl) && billNoEl.ValueKind == JsonValueKind.String)
            doc["billNo"] = billNoEl.GetString() ?? "";
        if (el.TryGetProperty("storeId", out var storeIdEl) && storeIdEl.ValueKind == JsonValueKind.String)
            doc["storeId"] = storeIdEl.GetString() ?? "";
        if (el.TryGetProperty("posCounter", out var posEl) && posEl.ValueKind == JsonValueKind.String)
            doc["posCounter"] = posEl.GetString() ?? "";
        if (el.TryGetProperty("status", out var statusEl) && statusEl.ValueKind == JsonValueKind.String)
            doc["status"] = statusEl.GetString() ?? "posted";
        else if (!doc.Contains("status"))
            doc["status"] = "posted";

        if (el.TryGetProperty("deviceId", out var deviceEl) && deviceEl.ValueKind == JsonValueKind.String)
            doc["deviceId"] = deviceEl.GetString() ?? "";

        if (!doc.Contains("createdAtUtc")
            && el.TryGetProperty("createdAt", out var createdEl)
            && createdEl.ValueKind != JsonValueKind.Null
            && createdEl.ValueKind != JsonValueKind.Undefined)
        {
            if (createdEl.ValueKind == JsonValueKind.String)
                doc["createdAtUtc"] = createdEl.GetString() ?? "";
            else if (createdEl.TryGetDateTime(out var dt))
                doc["createdAtUtc"] = dt.ToUniversalTime().ToString("O");
        }

        return doc;
    }

    private static bool InDateRange(DateTime utc, DateTime? from, DateTime? to)
    {
        if (utc == DateTime.MinValue)
            return from == null && to == null;
        if (from.HasValue && utc.Date < from.Value.Date)
            return false;
        if (to.HasValue && utc.Date > to.Value.Date)
            return false;
        return true;
    }

    private BillSearchRow? MapSearchRow(BsonDocument doc)
    {
        var billNo = ReadString(doc, "billNo") ?? "";
        if (string.IsNullOrEmpty(billNo))
            return null;

        var sortUtc = DateTime.MinValue;
        if (doc.TryGetValue("createdAtUtc", out var cu) && cu.IsString
            && DateTime.TryParse(cu.AsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            sortUtc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();

        var posted = sortUtc == DateTime.MinValue
            ? "—"
            : sortUtc.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC";

        var pos = ReadString(doc, "posCounter") ?? "";
        var dev = ReadString(doc, "deviceId") ?? "";

        return new BillSearchRow
        {
            BillNo = billNo,
            BillDate = ReadString(doc, "billDate") ?? "",
            CustomerName = ReadString(doc, "customerName") ?? "",
            CustomerPhone = ReadString(doc, "customerPhone") ?? "",
            Payable = ReadDecimal(doc, "payable"),
            Status = ReadString(doc, "status") ?? "posted",
            PostedAtUtc = posted,
            CounterDisplay = CounterDisplayFormatter.Format(pos, dev),
            SortUtc = sortUtc,
        };
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
