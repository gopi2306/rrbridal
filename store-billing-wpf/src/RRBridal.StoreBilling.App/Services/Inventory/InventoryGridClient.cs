using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Api;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Inventory;

public sealed class InventoryGridClient
{
    private readonly IMongoCollection<BsonDocument> _products;
    private CentralOnlineModeService? _centralMode;
    private CentralDashboardClient? _dashboardApi;

    public InventoryGridClient(IMongoDatabase localDb)
    {
        _products = localDb.GetCollection<BsonDocument>("local_products_cache");
    }

    public void ConfigureOnline(CentralOnlineModeService centralMode, CentralDashboardClient dashboardApi)
    {
        _centralMode = centralMode;
        _dashboardApi = dashboardApi;
    }

    private bool IsCentralOnline => _centralMode?.IsOnlineMode == true && _dashboardApi != null;

    public Task<InventoryGridPageResult> SearchAsync(
        string search,
        string storeId,
        int page = 1,
        int limit = 100,
        InventoryStockFilter stockFilter = InventoryStockFilter.All,
        CancellationToken ct = default)
    {
        return IsCentralOnline
            ? SearchOnlineAsync(search, page, limit, stockFilter, ct)
            : SearchOfflineAsync(search, storeId, page, limit, stockFilter, ct);
    }

    public async Task<IReadOnlyList<InventoryGridRow>> GetAllAsync(
        string storeId,
        CancellationToken ct = default)
    {
        const int pageSize = 500;
        const int maxRows = 10_000;
        var rows = new List<InventoryGridRow>();
        var page = 1;
        while (rows.Count < maxRows)
        {
            var result = await SearchAsync(
                "",
                storeId,
                page,
                pageSize,
                InventoryStockFilter.All,
                ct).ConfigureAwait(false);
            rows.AddRange(result.Data);
            if (result.TotalPages == 0 || page >= result.TotalPages || result.Data.Count == 0)
                break;
            page++;
        }
        return rows
            .GroupBy(row => row.Sku, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(row => row.Sku, StringComparer.OrdinalIgnoreCase)
            .Take(maxRows)
            .ToList();
    }

    /// <summary>Central inventory grid is fail-closed: any API failure propagates so the caller shows an error.</summary>
    private async Task<InventoryGridPageResult> SearchOnlineAsync(
        string search,
        int page,
        int limit,
        InventoryStockFilter stockFilter,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 500);

        using var doc = await _dashboardApi!.GetInventoryGridAsync(search, page, limit, ct);
        var root = doc.RootElement;

        var data = new List<InventoryGridRow>();
        if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var rowEl in dataEl.EnumerateArray())
            {
                var row = MapFromJson(rowEl);
                if (row != null)
                    data.Add(row);
            }
        }

        data = ApplyStockFilter(data, stockFilter);

        var total = CentralDashboardClient.ReadInt(root, "total", data.Count);
        var totalPages = CentralDashboardClient.ReadInt(
            root, "totalPages", total == 0 ? 0 : (int)Math.Ceiling(total / (double)limit));

        return new InventoryGridPageResult
        {
            Data = data,
            Total = total,
            Page = CentralDashboardClient.ReadInt(root, "page", page),
            Limit = CentralDashboardClient.ReadInt(root, "limit", limit),
            TotalPages = totalPages,
        };
    }

    private static List<InventoryGridRow> ApplyStockFilter(List<InventoryGridRow> rows, InventoryStockFilter stockFilter) =>
        stockFilter switch
        {
            InventoryStockFilter.InStock => rows.Where(r => r.StoreQty > 0).ToList(),
            InventoryStockFilter.OutOfStock => rows.Where(r => r.StoreQty <= 0).ToList(),
            _ => rows,
        };

    private static InventoryGridRow? MapFromJson(JsonElement el)
    {
        var sku = CentralDashboardClient.ReadString(el, "sku");
        if (string.IsNullOrWhiteSpace(sku))
            return null;

        string? productName = null;
        if (el.TryGetProperty("product", out var productEl) && productEl.ValueKind == JsonValueKind.Object)
        {
            productName = ReadOptionalString(productEl, "itemName") ?? ReadOptionalString(productEl, "shortName");
        }

        return new InventoryGridRow
        {
            Sku = sku,
            UpcEanCode = ReadOptionalString(el, "upcEanCode"),
            Product = productName ?? sku,
            StoreQty = CentralDashboardClient.ReadDecimal(el, "storeQty"),
            Mrp = ReadOptionalDecimal(el, "mrp"),
            StorePrice = ReadOptionalDecimal(el, "storePrice"),
        };
    }

    private static string? ReadOptionalString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        var s = CentralDashboardClient.ReadString(el, name);
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static decimal? ReadOptionalDecimal(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return CentralDashboardClient.ReadDecimal(el, name);
    }

    private async Task<InventoryGridPageResult> SearchOfflineAsync(
        string search,
        string storeId,
        int page,
        int limit,
        InventoryStockFilter stockFilter,
        CancellationToken ct)
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
