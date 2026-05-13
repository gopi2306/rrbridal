using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Inventory;

public sealed class InventoryGridClient
{
    private readonly IMongoCollection<BsonDocument> _products;

    public InventoryGridClient(IMongoDatabase localDb)
    {
        _products = localDb.GetCollection<BsonDocument>("local_products_cache");
    }

    public async Task<IReadOnlyList<InventoryGridRow>> SearchAsync(
        string search,
        string storeId,
        int limit = 100,
        CancellationToken ct = default)
    {
        var q = search?.Trim() ?? "";
        limit = Math.Clamp(limit, 1, 500);

        var filters = new List<FilterDefinition<BsonDocument>>
        {
            Builders<BsonDocument>.Filter.Exists("sku"),
        };

        if (!string.IsNullOrWhiteSpace(q))
        {
            var regex = new BsonRegularExpression(Regex.Escape(q), "i");
            filters.Add(Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Regex("sku", regex),
                Builders<BsonDocument>.Filter.Regex("upcEanCode", regex),
                Builders<BsonDocument>.Filter.Regex("itemName", regex),
                Builders<BsonDocument>.Filter.Regex("shortName", regex)));
        }

        var filter = Builders<BsonDocument>.Filter.And(filters);
        var docs = await _products
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("stockQty").Ascending("sku"))
            .Limit(limit)
            .ToListAsync(ct);

        return docs
            .Select(MapFromBson)
            .Where(static r => r != null)
            .Cast<InventoryGridRow>()
            .ToList();
    }

    private static InventoryGridRow? MapFromBson(BsonDocument d)
    {
        var sku = ReadString(d, "sku");
        if (string.IsNullOrWhiteSpace(sku)) return null;

        return new InventoryGridRow
        {
            Sku = sku,
            UpcEanCode = ReadString(d, "upcEanCode"),
            Product = ReadString(d, "itemName") ?? ReadString(d, "shortName") ?? sku,
            StoreQty = ReadDecimalBson(d, "stockQty") ?? 0m,
            Mrp = ReadDecimalBson(d, "mrp"),
            StorePrice = ReadDecimalBson(d, "storePrice") ?? ReadDecimalBson(d, "sellingPrice"),
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
}
