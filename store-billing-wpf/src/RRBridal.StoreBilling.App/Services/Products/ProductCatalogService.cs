using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Audit;

namespace RRBridal.StoreBilling.App.Services.Products;

public sealed class ProductCatalogService
{
    private readonly IMongoCollection<BsonDocument> _cache;
    private readonly IMongoDatabase _localDb;
    private readonly StoreAuditLogService? _auditLog;

    public ProductCatalogService(IMongoDatabase localDb, HttpClient centralApi, StoreAuditLogService? auditLog = null)
    {
        _localDb = localDb;
        _cache = localDb.GetCollection<BsonDocument>("local_products_cache");
        _auditLog = auditLog;
    }

    public async Task<IReadOnlyList<CatalogProduct>> SearchAsync(string query, CancellationToken ct = default)
    {
        var q = query?.Trim() ?? "";
        if (q.Length < 1)
            return Array.Empty<CatalogProduct>();

        var hsnLookup = await HsnSacResolver.LoadLookupAsync(_localDb, ct);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<CatalogProduct>();
        var stockFilter = Builders<BsonDocument>.Filter.Gt("stockQty", 0);

        if (IsAllDigits(q))
        {
            try
            {
                var skuExact = new BsonRegularExpression("^" + Regex.Escape(q) + "$", "i");
                var matchFilter = Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Exists("sku"),
                    stockFilter,
                    Builders<BsonDocument>.Filter.Or(
                        Builders<BsonDocument>.Filter.Eq("upcEanCode", q),
                        Builders<BsonDocument>.Filter.Regex("sku", skuExact)));

                var docs = await _cache.Find(matchFilter).Limit(40).ToListAsync(ct);
                foreach (var d in docs)
                {
                    var p = MapFromBson(d, hsnLookup);
                    if (p != null && seen.Add(p.Sku))
                        list.Add(p);
                }
            }
            catch { }
        }

        try
        {
            var escaped = Regex.Escape(q);
            var regex = new BsonRegularExpression(escaped, "i");
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Exists("sku"),
                stockFilter,
                Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Regex("sku", regex),
                    Builders<BsonDocument>.Filter.Regex("itemName", regex),
                    Builders<BsonDocument>.Filter.Regex("shortName", regex),
                    Builders<BsonDocument>.Filter.Regex("upcEanCode", regex)));

            var docs = await _cache.Find(filter).Limit(80).ToListAsync(ct);
            foreach (var d in docs)
            {
                var p = MapFromBson(d, hsnLookup);
                if (p != null && seen.Add(p.Sku))
                    list.Add(p);
            }
        }
        catch { }

        return list;
    }

    /// <summary>SKU, barcode (upcEanCode), or alias — exact match only.</summary>
    public async Task<CatalogProduct?> FindBySkuOrBarcodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        try
        {
            var hsnLookup = await HsnSacResolver.LoadLookupAsync(_localDb, ct);
            var q = code.Trim();
            var exact = new BsonRegularExpression("^" + Regex.Escape(q) + "$", "i");
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Exists("sku"),
                CodeExactMatchFilter(q, exact));

            var doc = await _cache.Find(filter).FirstOrDefaultAsync(ct);
            return doc != null ? MapFromBson(doc, hsnLookup) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Partial match on SKU, barcode, or alias only (not product name).</summary>
    public async Task<IReadOnlyList<CatalogProduct>> SearchByProductCodeAsync(string query, CancellationToken ct = default)
    {
        var q = query?.Trim() ?? "";
        if (q.Length < 1)
            return Array.Empty<CatalogProduct>();

        var hsnLookup = await HsnSacResolver.LoadLookupAsync(_localDb, ct);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<CatalogProduct>();
        var stockFilter = Builders<BsonDocument>.Filter.Gt("stockQty", 0);

        if (IsAllDigits(q))
        {
            try
            {
                var exact = new BsonRegularExpression("^" + Regex.Escape(q) + "$", "i");
                var matchFilter = Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Exists("sku"),
                    stockFilter,
                    CodeExactMatchFilter(q, exact));

                var docs = await _cache.Find(matchFilter).Limit(40).ToListAsync(ct);
                foreach (var d in docs)
                {
                    var p = MapFromBson(d, hsnLookup);
                    if (p != null && seen.Add(p.Sku))
                        list.Add(p);
                }
            }
            catch { }
        }

        try
        {
            var escaped = Regex.Escape(q);
            var regex = new BsonRegularExpression(escaped, "i");
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Exists("sku"),
                stockFilter,
                CodePartialMatchFilter(regex));

            var docs = await _cache.Find(filter).Limit(80).ToListAsync(ct);
            foreach (var d in docs)
            {
                var p = MapFromBson(d, hsnLookup);
                if (p != null && seen.Add(p.Sku))
                    list.Add(p);
            }
        }
        catch { }

        return list;
    }

    private static FilterDefinition<BsonDocument> CodeExactMatchFilter(string q, BsonRegularExpression exactRegex) =>
        Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("upcEanCode", q),
            Builders<BsonDocument>.Filter.Regex("sku", exactRegex),
            Builders<BsonDocument>.Filter.Regex("alias", exactRegex));

    private static FilterDefinition<BsonDocument> CodePartialMatchFilter(BsonRegularExpression regex) =>
        Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Regex("sku", regex),
            Builders<BsonDocument>.Filter.Regex("upcEanCode", regex),
            Builders<BsonDocument>.Filter.Regex("alias", regex));

    private static bool IsAllDigits(string s) =>
        s.Length > 0 && s.All(static c => c is >= '0' and <= '9');

    private static CatalogProduct? MapFromBson(BsonDocument d, IReadOnlyDictionary<string, string> hsnLookup)
    {
        if (!d.TryGetValue("sku", out var skuVal) || skuVal.IsBsonNull)
            return null;
        var sku = skuVal.AsString;
        if (string.IsNullOrWhiteSpace(sku))
            return null;

        var id = d.TryGetValue("centralProductId", out var cid) && !cid.IsBsonNull
            ? cid.ToString()!
            : d.TryGetValue("_id", out var oid) ? oid.ToString()! : "";

        var name = ReadString(d, "itemName")
            ?? ReadString(d, "shortName")
            ?? sku;

        var hsnSac = HsnSacResolver.Resolve(d, hsnLookup);

        return new CatalogProduct
        {
            CentralId = id,
            Sku = sku,
            UpcEanCode = ReadString(d, "upcEanCode"),
            Name = name,
            CostPrice = ReadDecimalBson(d, "costPrice"),
            MarginPercent = ReadDecimalBson(d, "marginPercent"),
            Mrp = ReadDecimalBson(d, "mrp"),
            SellingPrice = ReadDecimalBson(d, "sellingPrice"),
            StorePrice = ReadDecimalBson(d, "storePrice"),
            GstPercent = ReadDecimalBson(d, "gstPercent"),
            HsnSac = string.IsNullOrEmpty(hsnSac) ? null : hsnSac,
            StockQty = ReadDecimalBson(d, "stockQty") ?? 0m,
        };
    }

    private static string? ReadString(BsonDocument d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v.IsBsonNull)
            return null;
        return v.IsString ? v.AsString : v.ToString();
    }

    private static decimal? ReadDecimalBson(BsonDocument d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v.IsBsonNull)
            return null;
        return v switch
        {
            { IsDouble: true } => (decimal)v.AsDouble,
            { IsInt32: true } => v.AsInt32,
            { IsInt64: true } => v.AsInt64,
            { IsDecimal128: true } => (decimal)v.AsDecimal128,
            _ => null,
        };
    }

    public async Task DecrementStockAsync(
        string centralProductId,
        decimal qty,
        string? reason = null,
        string? billNo = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(centralProductId) || qty <= 0) return;
        try
        {
            var filter = Builders<BsonDocument>.Filter.Eq("centralProductId", centralProductId);
            var beforeDoc = await _cache.Find(filter).FirstOrDefaultAsync(ct);
            var stockBefore = beforeDoc == null ? 0m : ReadDecimalBson(beforeDoc, "stockQty") ?? 0m;
            var sku = beforeDoc == null ? "" : ReadString(beforeDoc, "sku") ?? "";

            var update = Builders<BsonDocument>.Update.Inc("stockQty", -(double)qty);
            await _cache.UpdateOneAsync(filter, update, cancellationToken: ct);

            if (_auditLog != null && !string.IsNullOrWhiteSpace(sku))
            {
                await _auditLog.LogProductStockChangeAsync(
                    sku,
                    centralProductId,
                    -qty,
                    stockBefore,
                    stockBefore - qty,
                    reason ?? "bill_post",
                    billNo,
                    ct: ct);
            }
        }
        catch { }
    }

    public async Task DecrementStockBySkuAsync(
        string sku,
        decimal qty,
        string? reason = null,
        string? billNo = null,
        string? actorName = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sku) || qty <= 0) return;
        try
        {
            var normalizedSku = sku.Trim();
            var filter = Builders<BsonDocument>.Filter.Eq("sku", normalizedSku);
            var beforeDoc = await _cache.Find(filter).FirstOrDefaultAsync(ct);
            var stockBefore = beforeDoc == null ? 0m : ReadDecimalBson(beforeDoc, "stockQty") ?? 0m;
            var centralId = beforeDoc == null
                ? ""
                : ReadString(beforeDoc, "centralProductId") ?? beforeDoc.GetValue("_id", "").ToString() ?? "";

            var update = Builders<BsonDocument>.Update
                .Inc("stockQty", -(double)qty)
                .Set("lastStockUpdatedAt", DateTime.UtcNow.ToString("O"));
            await _cache.UpdateOneAsync(filter, update, cancellationToken: ct);

            if (_auditLog != null)
            {
                await _auditLog.LogEventAsync(new StoreAuditEvent
                {
                    EntityType = "product",
                    EntityId = string.IsNullOrWhiteSpace(centralId) ? normalizedSku : centralId,
                    Action = "stock_decremented",
                    Sku = normalizedSku,
                    ActorName = actorName,
                    Metadata = new BsonDocument
                    {
                        { "qtyDelta", -(double)qty },
                        { "stockBefore", (double)stockBefore },
                        { "stockAfter", (double)(stockBefore - qty) },
                        { "reason", reason ?? "stock_exception_approve" },
                        { "billNo", billNo ?? "" },
                    },
                }, ct);
            }
        }
        catch { }
    }

    public async Task<decimal> GetAvailableStockAsync(string? centralProductId, string? sku, CancellationToken ct = default)
    {
        try
        {
            FilterDefinition<BsonDocument>? filter = null;
            if (!string.IsNullOrWhiteSpace(centralProductId))
                filter = Builders<BsonDocument>.Filter.Eq("centralProductId", centralProductId.Trim());
            if (!string.IsNullOrWhiteSpace(sku))
            {
                var skuFilter = Builders<BsonDocument>.Filter.Eq("sku", sku.Trim());
                filter = filter == null ? skuFilter : Builders<BsonDocument>.Filter.Or(filter, skuFilter);
            }

            if (filter == null) return 0m;
            var doc = await _cache.Find(filter).FirstOrDefaultAsync(ct);
            return doc == null ? 0m : ReadDecimalBson(doc, "stockQty") ?? 0m;
        }
        catch
        {
            return 0m;
        }
    }

    public async Task<bool> IncrementStockBySkuAsync(string sku, decimal qty, string? description = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sku) || qty <= 0) return true;
        try
        {
            var normalizedSku = sku.Trim();
            var filter = Builders<BsonDocument>.Filter.Eq("sku", normalizedSku);
            var update = Builders<BsonDocument>.Update
                .Inc("stockQty", (double)qty)
                .SetOnInsert("sku", normalizedSku)
                .SetOnInsert("itemName", string.IsNullOrWhiteSpace(description) ? normalizedSku : description.Trim())
                .Set("lastStockUpdatedAt", DateTime.UtcNow.ToString("O"));

            await _cache.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
