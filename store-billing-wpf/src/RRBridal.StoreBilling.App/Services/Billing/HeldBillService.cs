using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Store;

namespace RRBridal.StoreBilling.App.Services.Billing;

public sealed class HeldBillRow
{
    public required string HoldNo { get; init; }
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public decimal Payable { get; init; }
    public string CounterDisplay { get; init; } = "";
    public string HeldAtUtc { get; init; } = "";
    public DateTime SortUtc { get; init; }
}

public sealed class HeldBillService
{
    private const string CollectionName = "held_bills";

    private readonly IMongoCollection<BsonDocument> _holds;
    private readonly IMongoCollection<BsonDocument> _bills;
    private readonly StoreContext _store;
    private readonly BillNumberGenerator _billNumbers;

    public HeldBillService(IMongoDatabase localDb, StoreContext store, BillNumberGenerator billNumbers)
    {
        _holds = localDb.GetCollection<BsonDocument>(CollectionName);
        _bills = localDb.GetCollection<BsonDocument>("store_bills");
        _store = store;
        _billNumbers = billNumbers;
    }

    public async Task UpsertAsync(BsonDocument doc, CancellationToken ct = default)
    {
        var holdNo = doc.GetValue("holdNo", "").AsString.Trim();
        if (string.IsNullOrEmpty(holdNo))
            throw new InvalidOperationException("holdNo is required.");

        var now = DateTime.UtcNow.ToString("O");
        if (!doc.Contains("heldAtUtc"))
            doc["heldAtUtc"] = now;
        doc["updatedAtUtc"] = now;
        doc["storeId"] = _store.StoreId;

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
            Builders<BsonDocument>.Filter.Eq("holdNo", holdNo));

        await _holds.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true }, ct);
    }

    public async Task<BsonDocument?> GetByHoldNoAsync(string holdNo, CancellationToken ct = default)
    {
        var trimmed = holdNo.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;

        return await _holds.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
            Builders<BsonDocument>.Filter.Eq("holdNo", trimmed))).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<HeldBillRow>> ListAsync(int limit = 50, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var docs = await _holds
            .Find(Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId))
            .Sort(Builders<BsonDocument>.Sort.Descending("updatedAtUtc"))
            .Limit(limit)
            .ToListAsync(ct);

        return docs.Select(MapRow).Where(r => r != null).Cast<HeldBillRow>().ToList();
    }

    public async Task DeleteAsync(string holdNo, CancellationToken ct = default)
    {
        var trimmed = holdNo.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return;

        await _holds.DeleteOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
                Builders<BsonDocument>.Filter.Eq("holdNo", trimmed)),
            ct);
    }

    /// <summary>Moves legacy store_bills drafts into held_bills (one-time on startup).</summary>
    public async Task MigrateDraftsFromStoreBillsAsync(CancellationToken ct = default)
    {
        var drafts = await _bills
            .Find(Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
                Builders<BsonDocument>.Filter.Eq("status", "draft")))
            .ToListAsync(ct);

        foreach (var draft in drafts)
        {
            var holdNo = await _billNumbers.NextHoldAsync(ct);
            var hold = (BsonDocument)draft.DeepClone();
            hold.Remove("status");
            hold.Remove("billNo");
            hold["holdNo"] = holdNo;
            hold["storeId"] = _store.StoreId;
            var heldAt = draft.TryGetValue("createdAtUtc", out var ca) && ca.IsString
                ? ca.AsString
                : DateTime.UtcNow.ToString("O");
            hold["heldAtUtc"] = heldAt;
            hold["updatedAtUtc"] = heldAt;

            await _holds.InsertOneAsync(hold, cancellationToken: ct);
            await _bills.DeleteOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", draft["_id"]),
                ct);
        }
    }

    private HeldBillRow? MapRow(BsonDocument doc)
    {
        var holdNo = ReadString(doc, "holdNo") ?? "";
        if (string.IsNullOrEmpty(holdNo))
            return null;

        var sortUtc = DateTime.MinValue;
        if (doc.TryGetValue("updatedAtUtc", out var uu) && uu.IsString
            && DateTime.TryParse(uu.AsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var updated))
            sortUtc = updated.Kind == DateTimeKind.Utc ? updated : updated.ToUniversalTime();
        else if (doc.TryGetValue("heldAtUtc", out var hu) && hu.IsString
            && DateTime.TryParse(hu.AsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var held))
            sortUtc = held.Kind == DateTimeKind.Utc ? held : held.ToUniversalTime();

        var heldDisplay = sortUtc == DateTime.MinValue
            ? "—"
            : sortUtc.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC";

        var pos = ReadString(doc, "posCounter") ?? "";
        var dev = ReadString(doc, "deviceId") ?? "";

        return new HeldBillRow
        {
            HoldNo = holdNo,
            CustomerName = ReadString(doc, "customerName") ?? "",
            CustomerPhone = ReadString(doc, "customerPhone") ?? "",
            Payable = ReadDecimal(doc, "payable"),
            CounterDisplay = CounterDisplayFormatter.Format(pos, dev),
            HeldAtUtc = heldDisplay,
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
