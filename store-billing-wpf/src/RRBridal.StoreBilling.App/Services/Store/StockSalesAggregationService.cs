using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Store;

public enum StockSalesViewMode
{
    BrandWise,
    ProductWise,
}

public enum StockAvailabilityFilter
{
    All,
    InStock,
    OutOfStock,
    NegativeStock,
}

public sealed class StockAvailabilityFilterOption
{
    public StockAvailabilityFilterOption(StockAvailabilityFilter filter, string label)
    {
        Filter = filter;
        Label = label;
    }

    public StockAvailabilityFilter Filter { get; }
    public string Label { get; }
}

public sealed class BrandSalesSummaryRow
{
    public required string BrandId { get; init; }
    public required string BrandCode { get; init; }
    public required string BrandName { get; init; }
    public int ProductCount { get; init; }
    public int BillCount { get; init; }
    public decimal TotalQty { get; init; }
    public decimal AvailableQty { get; init; }
    public decimal TotalAmount { get; init; }
    public string TotalQtyFormatted => TotalQty.ToString("N2", CultureInfo.InvariantCulture);
    public string AvailableQtyFormatted => AvailableQty.ToString("N2", CultureInfo.InvariantCulture);
    public string TotalAmountFormatted => MoneyMath.FormatRupee(TotalAmount);
}

public sealed class ProductSalesSummaryRow
{
    public required string Sku { get; init; }
    public required string ProductName { get; init; }
    public required string BrandName { get; init; }
    public int BillCount { get; init; }
    public decimal TotalQty { get; init; }
    public decimal AvailableQty { get; init; }
    public decimal TotalAmount { get; init; }
    public int? AgeDays { get; init; }
    public string TotalQtyFormatted => TotalQty.ToString("N2", CultureInfo.InvariantCulture);
    public string AvailableQtyFormatted => AvailableQty.ToString("N2", CultureInfo.InvariantCulture);
    public string TotalAmountFormatted => MoneyMath.FormatRupee(TotalAmount);
    public string AgeDisplay => AgeDays.HasValue ? $"{AgeDays.Value} days" : "—";
}

public sealed class StockSalesAggregationResult
{
    public IReadOnlyList<BrandSalesSummaryRow> BrandRows { get; init; } = Array.Empty<BrandSalesSummaryRow>();
    public IReadOnlyList<ProductSalesSummaryRow> ProductRows { get; init; } = Array.Empty<ProductSalesSummaryRow>();
    public decimal TotalQty { get; init; }
    public decimal TotalAvailableQty { get; init; }
    public decimal TotalAmount { get; init; }
    public int BillCount { get; init; }
}

public sealed class StockSalesAggregationService
{
    private const string UnknownBrandId = "__unknown__";

    private readonly IMongoDatabase _db;

    public StockSalesAggregationService(IMongoDatabase localDb)
    {
        _db = localDb;
    }

    public async Task<StockSalesAggregationResult> AggregateAsync(
        string storeId,
        string? posCounterFilter,
        DateTime? businessDate,
        bool useDateRange,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? brandIdFilter,
        string? brandNameFilter,
        string? productSearch,
        StockAvailabilityFilter availabilityFilter = StockAvailabilityFilter.All,
        CancellationToken ct = default)
    {
        var query = new StoreBillListQuery
        {
            PosCounterFilter = posCounterFilter,
            BusinessDate = businessDate,
            UseDateRange = useDateRange,
            DateFrom = dateFrom,
            DateTo = dateTo,
            Limit = StoreBillListService.DefaultLimit,
        };

        var billsColl = _db.GetCollection<BsonDocument>("store_bills");
        var billDocs = await billsColl.Find(Builders<BsonDocument>.Filter.Eq("storeId", storeId)).ToListAsync(ct);

        var filteredBills = billDocs
            .Where(DayBillingCloseDocumentReader.IsPostedBill)
            .Where(d => DayBillingCloseDocumentReader.MatchesPosCounterFilter(d, posCounterFilter))
            .Where(d => StoreBillListService.MatchesDateFilter(d, query))
            .ToList();

        var productLookup = await BuildProductLookupAsync(ct);
        var brandNames = await BuildBrandNameLookupAsync(ct);
        var productSearchTerm = productSearch?.Trim() ?? "";
        var brandId = string.IsNullOrWhiteSpace(brandIdFilter) ? null : brandIdFilter.Trim();
        var brandNameTerm = string.IsNullOrWhiteSpace(brandNameFilter) ? null : brandNameFilter.Trim();
        var includeFullBrandInventory = brandId != null || brandNameTerm != null;

        var brandGroups = new Dictionary<string, (string Code, string Name, HashSet<string> Skus, HashSet<string> Bills, decimal Qty, decimal Amount)>(StringComparer.OrdinalIgnoreCase);
        var productGroups = new Dictionary<string, ProductAgg>(StringComparer.OrdinalIgnoreCase);
        var touchedBills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var bill in filteredBills)
        {
            var billNo = DayBillingCloseDocumentReader.ReadString(bill, "billNo") ?? "";
            if (!bill.TryGetValue("lines", out var linesVal) || !linesVal.IsBsonArray)
                continue;

            foreach (BsonDocument line in linesVal.AsBsonArray.OfType<BsonDocument>())
            {
                if (!IsSoldLine(line))
                    continue;

                var sku = DayBillingCloseDocumentReader.ReadString(line, "sku")?.Trim() ?? "";
                var centralProductId = DayBillingCloseDocumentReader.ReadString(line, "centralProductId")?.Trim() ?? "";
                var lineDescription = DayBillingCloseDocumentReader.ReadString(line, "description")?.Trim() ?? "";
                var qty = DayBillingCloseDocumentReader.ReadDecimal(line, "qty");
                var amount = ReadLineAmount(line);

                var productDoc = ResolveProductDoc(productLookup, sku, centralProductId);
                var meta = ResolveProductMeta(productDoc, sku, lineDescription, brandNames);
                if (!MatchesBrandFilter(brandId, brandNameTerm, meta.BrandId, meta.BrandName))
                    continue;

                if (!MatchesProductSearch(productSearchTerm, sku, meta.ProductName, lineDescription))
                    continue;

                if (!string.IsNullOrWhiteSpace(billNo))
                    touchedBills.Add(billNo);

                AccumulateBrandSale(brandGroups, meta, sku, billNo, qty, amount);
                AccumulateProductSale(productGroups, productDoc, meta, sku, centralProductId, lineDescription, billNo, qty, amount);
            }
        }

        if (includeFullBrandInventory)
            SeedInventoryProducts(productLookup, brandNames, brandId, brandNameTerm, productSearchTerm, productGroups);

        var inventoryByBrand = BuildInventoryTotalsByBrand(productLookup, brandNames);

        if (includeFullBrandInventory)
        {
            foreach (var kv in inventoryByBrand)
            {
                var meta = ResolveBrandMeta(kv.Key, brandNames);
                if (!MatchesBrandFilter(brandId, brandNameTerm, kv.Key, meta.Name))
                    continue;

                if (!brandGroups.ContainsKey(kv.Key))
                {
                    brandGroups[kv.Key] = (meta.Code, meta.Name, new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0m, 0m);
                }
            }
        }

        var brandRows = brandGroups
            .Select(kv =>
            {
                inventoryByBrand.TryGetValue(kv.Key, out var inv);
                var availableQty = inv.AvailableQty;
                var inventorySkuCount = inv.SkuCount;
                return new BrandSalesSummaryRow
                {
                    BrandId = kv.Key,
                    BrandCode = kv.Value.Code,
                    BrandName = kv.Value.Name,
                    ProductCount = Math.Max(kv.Value.Skus.Count, inventorySkuCount),
                    BillCount = kv.Value.Bills.Count,
                    TotalQty = kv.Value.Qty,
                    AvailableQty = availableQty,
                    TotalAmount = kv.Value.Amount,
                };
            })
            .Where(r => MatchesBrandFilter(brandId, brandNameTerm, r.BrandId, r.BrandName))
            .Where(r => !includeFullBrandInventory || r.TotalQty > 0 || r.AvailableQty > 0)
            .OrderByDescending(r => r.TotalQty)
            .ThenBy(r => r.BrandName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (includeFullBrandInventory && brandId != null && brandRows.Count == 0
            && inventoryByBrand.TryGetValue(brandId, out var onlyBrand))
        {
            var brandMeta = ResolveBrandMeta(brandId, brandNames);
            brandRows.Add(new BrandSalesSummaryRow
            {
                BrandId = brandId,
                BrandCode = brandMeta.Code,
                BrandName = brandMeta.Name,
                ProductCount = onlyBrand.SkuCount,
                BillCount = 0,
                TotalQty = 0m,
                AvailableQty = onlyBrand.AvailableQty,
                TotalAmount = 0m,
            });
        }

        var productRows = productGroups.Values
            .Select(p => new ProductSalesSummaryRow
            {
                Sku = p.Sku,
                ProductName = p.ProductName,
                BrandName = p.BrandName,
                BillCount = p.Bills.Count,
                TotalQty = p.Qty,
                AvailableQty = p.AvailableQty,
                TotalAmount = p.Amount,
                AgeDays = p.AgeDays,
            })
            .Where(r => MatchesAvailabilityFilter(availabilityFilter, r.AvailableQty))
            .Where(r => includeFullBrandInventory || r.TotalQty > 0 || r.AvailableQty != 0)
            .OrderByDescending(r => r.TotalQty)
            .ThenByDescending(r => r.AvailableQty)
            .ThenBy(r => r.ProductName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (includeFullBrandInventory || availabilityFilter != StockAvailabilityFilter.All)
        {
            brandRows = BuildBrandRowsFromProducts(productRows, brandNames);
        }
        else
        {
            brandRows = brandRows
                .Where(r => productRows.Any(p => string.Equals(p.BrandName, r.BrandName, StringComparison.OrdinalIgnoreCase))
                            || r.TotalQty > 0)
                .ToList();
        }

        var totalQty = productRows.Sum(r => r.TotalQty);
        var totalAvailable = productRows.Sum(r => r.AvailableQty);

        return new StockSalesAggregationResult
        {
            BrandRows = brandRows,
            ProductRows = productRows,
            TotalQty = totalQty,
            TotalAvailableQty = totalAvailable,
            TotalAmount = brandRows.Sum(r => r.TotalAmount) > 0
                ? brandRows.Sum(r => r.TotalAmount)
                : productRows.Sum(r => r.TotalAmount),
            BillCount = touchedBills.Count,
        };
    }

    private static void SeedInventoryProducts(
        ProductLookup productLookup,
        IReadOnlyDictionary<string, (string Code, string Name)> brandNames,
        string? brandId,
        string? brandNameTerm,
        string productSearchTerm,
        Dictionary<string, ProductAgg> productGroups)
    {
        foreach (var (sku, doc) in productLookup.BySku)
        {
            var meta = ResolveProductMeta(doc, sku, "", brandNames);
            if (!MatchesBrandFilter(brandId, brandNameTerm, meta.BrandId, meta.BrandName))
                continue;
            if (!MatchesProductSearch(productSearchTerm, sku, meta.ProductName, ""))
                continue;

            var key = sku;
            if (productGroups.ContainsKey(key))
                continue;

            productGroups[key] = new ProductAgg
            {
                Sku = sku,
                ProductName = meta.ProductName,
                BrandName = meta.BrandName,
                AvailableQty = ReadStockQty(doc),
                AgeDays = ReadProductAgeDays(doc),
            };
        }
    }

    private static Dictionary<string, (decimal AvailableQty, int SkuCount)> BuildInventoryTotalsByBrand(
        ProductLookup productLookup,
        IReadOnlyDictionary<string, (string Code, string Name)> brandNames)
    {
        var map = new Dictionary<string, (decimal AvailableQty, int SkuCount)>(StringComparer.OrdinalIgnoreCase);

        foreach (var (sku, doc) in productLookup.BySku)
        {
            var meta = ResolveProductMeta(doc, sku, "", brandNames);
            var stock = ReadStockQty(doc);
            if (!map.TryGetValue(meta.BrandId, out var agg))
                agg = (0m, 0);

            agg.AvailableQty += stock;
            agg.SkuCount += 1;
            map[meta.BrandId] = agg;
        }

        return map;
    }

    private static void AccumulateBrandSale(
        Dictionary<string, (string Code, string Name, HashSet<string> Skus, HashSet<string> Bills, decimal Qty, decimal Amount)> brandGroups,
        ResolvedLineProduct meta,
        string sku,
        string billNo,
        decimal qty,
        decimal amount)
    {
        if (!brandGroups.TryGetValue(meta.BrandId, out var brandAgg))
        {
            brandAgg = (meta.BrandCode, meta.BrandName, new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0m, 0m);
        }

        if (!string.IsNullOrWhiteSpace(sku))
            brandAgg.Skus.Add(sku);
        if (!string.IsNullOrWhiteSpace(billNo))
            brandAgg.Bills.Add(billNo);
        brandAgg.Qty += qty;
        brandAgg.Amount += amount;
        brandGroups[meta.BrandId] = brandAgg;
    }

    private static void AccumulateProductSale(
        Dictionary<string, ProductAgg> productGroups,
        BsonDocument? productDoc,
        ResolvedLineProduct meta,
        string sku,
        string centralProductId,
        string lineDescription,
        string billNo,
        decimal qty,
        decimal amount)
    {
        var productKey = !string.IsNullOrWhiteSpace(sku)
            ? sku
            : !string.IsNullOrWhiteSpace(centralProductId)
                ? $"id:{centralProductId}"
                : $"desc:{lineDescription}";

        if (!productGroups.TryGetValue(productKey, out var productAgg))
        {
            productAgg = new ProductAgg
            {
                Sku = string.IsNullOrWhiteSpace(sku) ? "—" : sku,
                ProductName = meta.ProductName,
                BrandName = meta.BrandName,
                AvailableQty = ReadStockQty(productDoc),
                AgeDays = ReadProductAgeDays(productDoc),
            };
        }

        if (!string.IsNullOrWhiteSpace(billNo))
            productAgg.Bills.Add(billNo);
        productAgg.Qty += qty;
        productAgg.Amount += amount;
        if (productDoc != null)
        {
            productAgg.AvailableQty = ReadStockQty(productDoc);
            productAgg.AgeDays = ReadProductAgeDays(productDoc);
        }
        productGroups[productKey] = productAgg;
    }

    private static List<BrandSalesSummaryRow> BuildBrandRowsFromProducts(
        IReadOnlyList<ProductSalesSummaryRow> productRows,
        IReadOnlyDictionary<string, (string Code, string Name)> brandNames)
    {
        return productRows
            .GroupBy(p => p.BrandName, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var brandName = g.Key;
                var match = brandNames
                    .FirstOrDefault(kv => string.Equals(kv.Value.Name, brandName, StringComparison.OrdinalIgnoreCase));
                return new BrandSalesSummaryRow
                {
                    BrandId = string.IsNullOrWhiteSpace(match.Key) ? UnknownBrandId : match.Key,
                    BrandCode = match.Value.Code,
                    BrandName = brandName,
                    ProductCount = g.Count(),
                    BillCount = g.Max(p => p.BillCount),
                    TotalQty = g.Sum(p => p.TotalQty),
                    AvailableQty = g.Sum(p => p.AvailableQty),
                    TotalAmount = g.Sum(p => p.TotalAmount),
                };
            })
            .OrderByDescending(r => r.TotalQty)
            .ThenBy(r => r.BrandName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool MatchesAvailabilityFilter(StockAvailabilityFilter filter, decimal availableQty) =>
        filter switch
        {
            StockAvailabilityFilter.InStock => availableQty > 0,
            StockAvailabilityFilter.OutOfStock => availableQty == 0,
            StockAvailabilityFilter.NegativeStock => availableQty < 0,
            _ => true,
        };

    private static bool MatchesBrandFilter(string? brandId, string? brandNameTerm, string brandIdValue, string brandName)
    {
        if (!string.IsNullOrWhiteSpace(brandId))
            return string.Equals(brandIdValue, brandId.Trim(), StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(brandNameTerm))
        {
            return brandName.Contains(brandNameTerm, StringComparison.OrdinalIgnoreCase)
                   || brandIdValue.Contains(brandNameTerm, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static (string Code, string Name) ResolveBrandMeta(
        string brandId,
        IReadOnlyDictionary<string, (string Code, string Name)> brandNames)
    {
        if (brandNames.TryGetValue(brandId, out var brand))
            return brand;
        return ("", string.Equals(brandId, UnknownBrandId, StringComparison.Ordinal) ? "(Unknown brand)" : brandId);
    }

    private static bool IsSoldLine(BsonDocument line)
    {
        var qty = DayBillingCloseDocumentReader.ReadDecimal(line, "qty");
        if (qty <= 0)
            return false;

        var amount = DayBillingCloseDocumentReader.ReadDecimal(line, "amount");
        var revised = DayBillingCloseDocumentReader.ReadDecimal(line, "revisedAmount");
        return amount > 0 || revised > 0;
    }

    private static decimal ReadLineAmount(BsonDocument line)
    {
        var amount = DayBillingCloseDocumentReader.ReadDecimal(line, "amount");
        if (amount > 0)
            return amount;
        return DayBillingCloseDocumentReader.ReadDecimal(line, "revisedAmount");
    }

    private static decimal ReadStockQty(BsonDocument? doc)
    {
        if (doc == null)
            return 0m;
        return DayBillingCloseDocumentReader.ReadDecimal(doc, "stockQty");
    }

    private static int? ReadProductAgeDays(BsonDocument? doc)
    {
        if (doc == null)
            return null;

        if (!TryReadUtcDate(doc, "createdAt", out var anchor)
            && !TryReadUtcDate(doc, "lastStockUpdatedAt", out anchor)
            && !TryReadUtcDate(doc, "lastSyncedAt", out anchor))
            return null;

        var days = (int)(DateTime.UtcNow.Date - anchor.Date).TotalDays;
        return Math.Max(0, days);
    }

    private static bool TryReadUtcDate(BsonDocument doc, string key, out DateTime utc)
    {
        utc = default;
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull)
            return false;

        if (v.IsValidDateTime)
        {
            utc = v.ToUniversalTime().Date;
            return true;
        }

        if (v.IsString
            && DateTime.TryParse(v.AsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            utc = (parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime()).Date;
            return true;
        }

        return false;
    }

    private static bool MatchesProductSearch(string term, string sku, string productName, string lineDescription)
    {
        if (string.IsNullOrWhiteSpace(term))
            return true;

        return (!string.IsNullOrWhiteSpace(sku) && sku.Contains(term, StringComparison.OrdinalIgnoreCase))
               || productName.Contains(term, StringComparison.OrdinalIgnoreCase)
               || lineDescription.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ProductLookup> BuildProductLookupAsync(CancellationToken ct)
    {
        var productsColl = _db.GetCollection<BsonDocument>("local_products_cache");
        var docs = await productsColl.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct);

        var bySku = new Dictionary<string, BsonDocument>(StringComparer.OrdinalIgnoreCase);
        var byCentralId = new Dictionary<string, BsonDocument>(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in docs)
        {
            var sku = DayBillingCloseDocumentReader.ReadString(doc, "sku")?.Trim();
            if (!string.IsNullOrWhiteSpace(sku) && !bySku.ContainsKey(sku))
                bySku[sku] = doc;

            var centralId = DayBillingCloseDocumentReader.ReadString(doc, "centralProductId")?.Trim();
            if (!string.IsNullOrWhiteSpace(centralId) && !byCentralId.ContainsKey(centralId))
                byCentralId[centralId] = doc;
        }

        return new ProductLookup(bySku, byCentralId);
    }

    private async Task<Dictionary<string, (string Code, string Name)>> BuildBrandNameLookupAsync(CancellationToken ct)
    {
        var brandsColl = _db.GetCollection<BsonDocument>("master_brands");
        var docs = await brandsColl.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct);
        var map = new Dictionary<string, (string Code, string Name)>(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in docs)
        {
            var id = DayBillingCloseDocumentReader.ReadString(doc, "centralId")?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var code = DayBillingCloseDocumentReader.ReadString(doc, "code")?.Trim() ?? "";
            var name = DayBillingCloseDocumentReader.ReadString(doc, "name")?.Trim() ?? "";
            map[id] = (code, string.IsNullOrWhiteSpace(name) ? code : name);
        }

        return map;
    }

    private static BsonDocument? ResolveProductDoc(ProductLookup lookup, string sku, string centralProductId)
    {
        if (!string.IsNullOrWhiteSpace(sku) && lookup.BySku.TryGetValue(sku, out var bySku))
            return bySku;
        if (!string.IsNullOrWhiteSpace(centralProductId) && lookup.ByCentralId.TryGetValue(centralProductId, out var byId))
            return byId;
        return null;
    }

    private static ResolvedLineProduct ResolveProductMeta(
        BsonDocument? product,
        string sku,
        string lineDescription,
        IReadOnlyDictionary<string, (string Code, string Name)> brandNames)
    {
        var brandId = ReadRefId(product, "brandId");
        if (string.IsNullOrWhiteSpace(brandId))
            brandId = UnknownBrandId;

        var brandCode = "";
        var brandName = "(Unknown brand)";
        if (brandNames.TryGetValue(brandId, out var brand))
        {
            brandCode = brand.Code;
            brandName = string.IsNullOrWhiteSpace(brand.Name) ? brand.Code : brand.Name;
        }

        var productName = product != null
            ? DayBillingCloseDocumentReader.ReadString(product, "itemName")
              ?? DayBillingCloseDocumentReader.ReadString(product, "shortName")
              ?? sku
            : lineDescription;

        if (string.IsNullOrWhiteSpace(productName))
            productName = string.IsNullOrWhiteSpace(sku) ? "(Unknown product)" : sku;

        return new ResolvedLineProduct(brandId, brandCode, brandName, productName.Trim());
    }

    private static string ReadRefId(BsonDocument? doc, string key)
    {
        if (doc == null || !doc.TryGetValue(key, out var v) || v.IsBsonNull)
            return "";

        return v.BsonType switch
        {
            BsonType.ObjectId => v.AsObjectId.ToString(),
            BsonType.String => v.AsString,
            _ => v.ToString() ?? "",
        };
    }

    private sealed class ProductAgg
    {
        public string Sku { get; init; } = "—";
        public string ProductName { get; init; } = "";
        public string BrandName { get; init; } = "";
        public HashSet<string> Bills { get; } = new(StringComparer.OrdinalIgnoreCase);
        public decimal Qty { get; set; }
        public decimal Amount { get; set; }
        public decimal AvailableQty { get; set; }
        public int? AgeDays { get; set; }
    }

    private sealed record ProductLookup(
        Dictionary<string, BsonDocument> BySku,
        Dictionary<string, BsonDocument> ByCentralId);

    private sealed record ResolvedLineProduct(string BrandId, string BrandCode, string BrandName, string ProductName);
}
