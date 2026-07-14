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
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Billing;

public sealed class QuotationSearchRow
{
    public required string QuotationNo { get; init; }
    public string QuotationDate { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public decimal Payable { get; init; }
    public string Status { get; init; } = "";
    public string CounterDisplay { get; init; } = "";
    public DateTime SortUtc { get; init; }
}

public sealed class QuotationService
{
    public const string StatusOpen = "open";
    public const string StatusConverted = "converted";
    public const string StatusCancelled = "cancelled";

    private const string CollectionName = "store_quotations";

    private readonly IMongoCollection<BsonDocument> _quotations;
    private readonly StoreContext _store;
    private readonly BillNumberGenerator _billNumbers;
    private readonly BillingOutboxPublisher _outbox;

    public QuotationService(
        IMongoDatabase localDb,
        StoreContext store,
        BillNumberGenerator billNumbers,
        BillingOutboxPublisher outbox)
    {
        _quotations = localDb.GetCollection<BsonDocument>(CollectionName);
        _store = store;
        _billNumbers = billNumbers;
        _outbox = outbox;
    }

    public async Task<string> UpsertAsync(BsonDocument payload, string? existingQuotationNo = null, CancellationToken ct = default)
    {
        var quotationNo = (existingQuotationNo ?? "").Trim();
        if (string.IsNullOrEmpty(quotationNo))
            quotationNo = await _billNumbers.NextQuotationAsync(ct);

        var now = DateTime.UtcNow.ToString("O");
        var doc = (BsonDocument)payload.DeepClone();
        doc["quotationNo"] = quotationNo;
        doc["storeId"] = _store.StoreId;
        doc["deviceId"] = _store.DeviceId;
        doc["posCounter"] = _store.PosCounter;
        doc["status"] = StatusOpen;
        doc["updatedAtUtc"] = now;
        if (!doc.Contains("createdAtUtc") || string.IsNullOrWhiteSpace(doc.GetValue("createdAtUtc", "").AsString))
            doc["createdAtUtc"] = now;
        if (!doc.Contains("quotationDate") || string.IsNullOrWhiteSpace(doc.GetValue("quotationDate", "").AsString))
            doc["quotationDate"] = doc.GetValue("billDate", DateTime.Today.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture)).AsString;

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
            Builders<BsonDocument>.Filter.Eq("quotationNo", quotationNo));

        await _quotations.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true }, ct);
        await _outbox.PublishQuotationUpsertedAsync(doc, ct);
        return quotationNo;
    }

    public async Task<BsonDocument?> GetByQuotationNoAsync(string quotationNo, CancellationToken ct = default)
    {
        var trimmed = quotationNo.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;

        return await _quotations.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
            Builders<BsonDocument>.Filter.Eq("quotationNo", trimmed))).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<QuotationSearchRow>> SearchAsync(
        string? quotationNo,
        string? customerName,
        string? customerPhone,
        int limit = 100,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var filter = Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId);
        var docs = await _quotations
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("updatedAtUtc"))
            .Limit(500)
            .ToListAsync(ct);

        IEnumerable<BsonDocument> query = docs;
        if (!string.IsNullOrWhiteSpace(quotationNo))
        {
            var q = quotationNo.Trim();
            query = query.Where(d =>
                (d.GetValue("quotationNo", "").AsString ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var n = customerName.Trim();
            query = query.Where(d =>
                (d.GetValue("customerName", "").AsString ?? "").Contains(n, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(customerPhone))
        {
            var digits = Regex.Replace(customerPhone, @"\D", "");
            query = query.Where(d =>
            {
                var phone = Regex.Replace(d.GetValue("customerPhone", "").AsString ?? "", @"\D", "");
                return phone.Contains(digits, StringComparison.Ordinal);
            });
        }

        return query
            .Select(MapRow)
            .Where(r => r != null)
            .Cast<QuotationSearchRow>()
            .Take(limit)
            .ToList();
    }

    public async Task<bool> MarkConvertedAsync(string quotationNo, string billNo, CancellationToken ct = default)
    {
        var trimmed = quotationNo.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return false;

        var update = Builders<BsonDocument>.Update
            .Set("status", StatusConverted)
            .Set("convertedBillNo", billNo.Trim())
            .Set("convertedAtUtc", DateTime.UtcNow.ToString("O"))
            .Set("updatedAtUtc", DateTime.UtcNow.ToString("O"));

        var result = await _quotations.UpdateOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
                Builders<BsonDocument>.Filter.Eq("quotationNo", trimmed),
                Builders<BsonDocument>.Filter.Eq("status", StatusOpen)),
            update,
            cancellationToken: ct);

        if (result.ModifiedCount > 0)
        {
            var updated = await GetByQuotationNoAsync(trimmed, ct);
            if (updated != null)
                await _outbox.PublishQuotationConvertedAsync(trimmed, billNo.Trim(), updated, ct);
        }

        return result.ModifiedCount > 0;
    }

    public async Task<bool> CancelAsync(string quotationNo, CancellationToken ct = default)
    {
        var trimmed = quotationNo.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return false;

        var update = Builders<BsonDocument>.Update
            .Set("status", StatusCancelled)
            .Set("updatedAtUtc", DateTime.UtcNow.ToString("O"));

        var result = await _quotations.UpdateOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
                Builders<BsonDocument>.Filter.Eq("quotationNo", trimmed),
                Builders<BsonDocument>.Filter.Eq("status", StatusOpen)),
            update,
            cancellationToken: ct);

        if (result.ModifiedCount > 0)
        {
            var updated = await GetByQuotationNoAsync(trimmed, ct);
            if (updated != null)
                await _outbox.PublishQuotationCancelledAsync(trimmed, updated, ct);
        }

        return result.ModifiedCount > 0;
    }

    private static QuotationSearchRow? MapRow(BsonDocument doc)
    {
        var quotationNo = doc.GetValue("quotationNo", "").AsString;
        if (string.IsNullOrWhiteSpace(quotationNo))
            return null;

        DateTime sortUtc = DateTime.MinValue;
        var updated = doc.GetValue("updatedAtUtc", "").AsString;
        if (!string.IsNullOrWhiteSpace(updated)
            && DateTime.TryParse(updated, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            sortUtc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();

        return new QuotationSearchRow
        {
            QuotationNo = quotationNo,
            QuotationDate = doc.GetValue("quotationDate", doc.GetValue("billDate", "").AsString).AsString,
            CustomerName = doc.GetValue("customerName", "").AsString,
            CustomerPhone = doc.GetValue("customerPhone", "").AsString,
            Payable = (decimal)doc.GetValue("payable", 0).ToDouble(),
            Status = doc.GetValue("status", "").AsString,
            CounterDisplay = doc.GetValue("posCounter", "").AsString,
            SortUtc = sortUtc,
        };
    }
}
