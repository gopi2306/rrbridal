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

    public async Task<InventoryGridPageResult> SearchAsync(
        string search,
        string storeId,
        int page = 1,
        int limit = 100,
        InventoryStockFilter stockFilter = InventoryStockFilter.All,
        CancellationToken ct = default)
    {
        _ = storeId;
        var q = search?.Trim() ?? "";
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 500);
        var skip = (page - 1) * limit;

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

        switch (stockFilter)
        {
            case InventoryStockFilter.InStock:
                filters.Add(Builders<BsonDocument>.Filter.Gt("stockQty", 0));
                break;
            case InventoryStockFilter.OutOfStock:
                filters.Add(Builders<BsonDocument>.Filter.Lte("stockQty", 0));
                break;
        }

        var filter = Builders<BsonDocument>.Filter.And(filters);

        var totalLong = await _products.CountDocumentsAsync(filter, cancellationToken: ct);
        var total = totalLong > int.MaxValue ? int.MaxValue : (int)totalLong;

        var docs = await _products
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("stockQty").Ascending("sku"))
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(ct);

        var data = docs
            .Select(MapFromBson)
            .Where(static r => r != null)
            .Cast<InventoryGridRow>()
            .ToList();

        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)limit);

        return new InventoryGridPageResult
        {
            Data = data,
            Total = total,
            Page = page,
            Limit = limit,
            TotalPages = totalPages,
        };
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
            StorePrice = ReadPositiveDecimalBson(d, "storePrice") ?? ReadPositiveDecimalBson(d, "sellingPrice"),
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

    private static decimal? ReadPositiveDecimalBson(BsonDocument d, string key)
    {
        var value = ReadDecimalBson(d, key);
        return value is > 0 ? value : null;
    }
}
