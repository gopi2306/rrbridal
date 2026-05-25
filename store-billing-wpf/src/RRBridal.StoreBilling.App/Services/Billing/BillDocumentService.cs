using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Store;

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

    public BillDocumentService(IMongoDatabase localDb, StoreContext store, ReceiptConfigStore receiptConfig)
    {
        _bills = localDb.GetCollection<BsonDocument>("store_bills");
        _store = store;
        _receiptConfig = receiptConfig;
    }

    public async Task<BsonDocument?> GetByBillNoAsync(string billNo, CancellationToken ct = default)
    {
        var trimmed = billNo.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        return await _bills.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
            Builders<BsonDocument>.Filter.Eq("billNo", trimmed))).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<BillSearchRow>> SearchBillsAsync(
        string? invoiceNo,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? customerName,
        string? status = "posted",
        int limit = 100,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 500);
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

        var docs = await _bills
            .Find(Builders<BsonDocument>.Filter.And(filters))
            .Sort(Builders<BsonDocument>.Sort.Descending("createdAtUtc"))
            .Limit(limit * 3)
            .ToListAsync(ct);

        var rows = docs
            .Select(MapSearchRow)
            .Where(r => r != null)
            .Cast<BillSearchRow>()
            .Where(r => InDateRange(r.SortUtc, dateFrom, dateTo))
            .Take(limit)
            .ToList();

        return rows;
    }

    public async Task<IReadOnlyList<BillSearchRow>> ListDraftsAsync(int limit = 50, CancellationToken ct = default)
    {
        return await SearchBillsAsync(null, null, null, null, "draft", limit, ct);
    }

    public async Task<bool> BillNoExistsAsync(string billNo, string? excludeBillNo = null, CancellationToken ct = default)
    {
        var trimmed = billNo.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return false;
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

        await _bills.UpdateOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
                Builders<BsonDocument>.Filter.Eq("billNo", billNo.Trim())),
            Builders<BsonDocument>.Update.Push("printAudit", entry),
            cancellationToken: ct);
    }

    public async Task DeleteDraftAsync(string billNo, CancellationToken ct = default)
    {
        await _bills.DeleteOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
                Builders<BsonDocument>.Filter.Eq("billNo", billNo.Trim()),
                Builders<BsonDocument>.Filter.Eq("status", "draft")),
            ct);
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
