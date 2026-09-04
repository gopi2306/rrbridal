using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;

namespace RRBridal.StoreBilling.App.Services.Store;

public static class SkuSalesReportAggregator
{
    public const string UnmappedVendorId = "__unmapped__";
    public const string UnmappedVendorName = "No vendor mapped";
    public const string UnknownSupplierName = "Unknown supplier";

    public static (IReadOnlyList<SkuSalesRow> Rows, SkuSalesTotals Totals) Collect(
        IEnumerable<BsonDocument> invoices,
        IEnumerable<BsonDocument> returns,
        IReadOnlyDictionary<string, SkuCatalogEntry> catalog)
    {
        var map = new Dictionary<string, Acc>(StringComparer.OrdinalIgnoreCase);
        foreach (var invoice in invoices)
        {
            if (!IsPosted(invoice)) continue;
            foreach (var line in ReadLines(invoice, "lines"))
            {
                var qty = ReadDecimal(line, "qty");
                if (qty <= 0) continue;
                var sku = FirstNonEmpty(ReadString(line, "sku"), ReadString(line, "productCode"), "UNKNOWN");
                var current = Get(map, sku, FirstNonEmpty(ReadString(line, "description"), sku));
                current.SoldQty = Round(current.SoldQty + qty);
                current.SoldAmount = Round(current.SoldAmount + LineAmount(line, qty));
            }
        }

        foreach (var doc in returns)
        {
            if (!IsPosted(doc)) continue;
            var lines = ReadLines(doc, "returnLines");
            if (lines.Count == 0) lines = ReadLines(doc, "lines");
            foreach (var line in lines)
            {
                var qty = ReadDecimal(line, "returnQty");
                if (qty <= 0) qty = ReadDecimal(line, "qty");
                if (qty <= 0) continue;
                var sku = FirstNonEmpty(ReadString(line, "sku"), ReadString(line, "productCode"), "UNKNOWN");
                var current = Get(map, sku, FirstNonEmpty(ReadString(line, "description"), sku));
                current.ReturnQty = Round(current.ReturnQty + qty);
                current.ReturnAmount = Round(current.ReturnAmount + ReturnLineAmount(line, qty));
            }
        }

        var rows = new List<SkuSalesRow>(map.Count);
        foreach (var item in map.Values)
        {
            var found = catalog.TryGetValue(item.Sku, out var product);
            var supplierId = !found || string.IsNullOrWhiteSpace(product.SupplierId)
                ? UnmappedVendorId
                : product.SupplierId;
            var supplierName = supplierId == UnmappedVendorId
                ? UnmappedVendorName
                : FirstNonEmpty(found ? product.SupplierName : "", UnknownSupplierName);
            var description = item.Description;
            if (string.IsNullOrWhiteSpace(description) || description == item.Sku)
                description = FirstNonEmpty(found ? product.ItemName : "", item.Sku);
            rows.Add(new SkuSalesRow
            {
                Sku = item.Sku,
                Description = description,
                SupplierId = supplierId,
                SupplierName = supplierName,
                SoldQty = item.SoldQty,
                ReturnQty = item.ReturnQty,
                NetQty = Round(item.SoldQty - item.ReturnQty),
                SoldAmount = item.SoldAmount,
                ReturnAmount = item.ReturnAmount,
                NetAmount = Round(item.SoldAmount - item.ReturnAmount),
            });
        }

        return (rows, TotalsFrom(rows));
    }

    public static IReadOnlyList<SkuSalesRow> FilterForSupplierSearch(
        IEnumerable<SkuSalesRow> rows,
        string? search)
    {
        var q = search?.Trim() ?? "";
        if (q.Length == 0) return rows.ToList();
        var matchingSuppliers = rows
            .Where(row => row.SupplierName.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Select(row => row.SupplierId)
            .ToHashSet(StringComparer.Ordinal);
        return rows.Where(row =>
            matchingSuppliers.Contains(row.SupplierId)
            || row.Sku.Contains(q, StringComparison.OrdinalIgnoreCase)
            || row.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static IReadOnlyList<SkuSalesRow> FilterForFastSearch(
        IEnumerable<SkuSalesRow> rows,
        string? search)
    {
        var q = search?.Trim() ?? "";
        if (q.Length == 0) return rows.ToList();
        return rows.Where(row =>
                row.Sku.Contains(q, StringComparison.OrdinalIgnoreCase)
                || row.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static IReadOnlyList<SkuSalesRow> RankFastSellers(IEnumerable<SkuSalesRow> rows) =>
        rows.OrderByDescending(row => row.NetQty)
            .ThenByDescending(row => row.NetAmount)
            .ThenBy(row => row.Sku, StringComparer.Ordinal)
            .Select((row, index) => Clone(row, index + 1))
            .ToList();

    /// <summary>
    /// Attaches store available qty and low-stock flag (qty ≤ matchQty when matchQty &gt; 0).
    /// Missing SKUs in the qty map are treated as 0 available.
    /// </summary>
    public static IReadOnlyList<SkuSalesRow> AttachStockLevels(
        IEnumerable<SkuSalesRow> rows,
        IReadOnlyDictionary<string, decimal> qtyBySku,
        decimal matchQty)
    {
        var threshold = Math.Max(0m, matchQty);
        return rows.Select(row =>
            {
                var available = 0m;
                if (qtyBySku.TryGetValue(row.Sku, out var qty))
                    available = Round(qty);
                return Clone(row, row.Rank, available, threshold > 0 && available <= threshold);
            })
            .ToList();
    }

    public static IReadOnlyList<SupplierWiseSupplierRow> GroupSupplierWise(IEnumerable<SkuSalesRow> rows)
    {
        var groups = new Dictionary<string, List<SkuSalesRow>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var key = string.IsNullOrWhiteSpace(row.SupplierId) ? UnmappedVendorId : row.SupplierId;
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }
            list.Add(row);
        }

        return groups.Select(pair =>
            {
                var products = pair.Value
                    .OrderByDescending(row => row.NetQty)
                    .ThenByDescending(row => row.NetAmount)
                    .ThenBy(row => row.Sku, StringComparer.Ordinal)
                    .ToList();
                return new SupplierWiseSupplierRow
                {
                    SupplierId = pair.Key,
                    SupplierName = products[0].SupplierName,
                    ProductCount = products.Count,
                    SoldQty = Round(products.Sum(row => row.SoldQty)),
                    ReturnQty = Round(products.Sum(row => row.ReturnQty)),
                    NetQty = Round(products.Sum(row => row.NetQty)),
                    SoldAmount = Round(products.Sum(row => row.SoldAmount)),
                    ReturnAmount = Round(products.Sum(row => row.ReturnAmount)),
                    NetAmount = Round(products.Sum(row => row.NetAmount)),
                    Products = products,
                };
            })
            .OrderByDescending(row => row.NetQty)
            .ThenByDescending(row => row.NetAmount)
            .ThenBy(row => row.SupplierName, StringComparer.Ordinal)
            .ToList();
    }

    public static SkuSalesTotals TotalsFrom(IEnumerable<SkuSalesRow> rows, int? supplierCount = null)
    {
        var list = rows.ToList();
        return new SkuSalesTotals
        {
            SkuCount = list.Count,
            SupplierCount = supplierCount ?? 0,
            SoldQty = Round(list.Sum(row => row.SoldQty)),
            ReturnQty = Round(list.Sum(row => row.ReturnQty)),
            NetQty = Round(list.Sum(row => row.NetQty)),
            SoldAmount = Round(list.Sum(row => row.SoldAmount)),
            ReturnAmount = Round(list.Sum(row => row.ReturnAmount)),
            NetAmount = Round(list.Sum(row => row.NetAmount)),
        };
    }

    public static Dictionary<string, SkuCatalogEntry> ReadCatalog(IEnumerable<BsonDocument> products)
    {
        var catalog = new Dictionary<string, SkuCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in products)
        {
            var sku = FirstNonEmpty(ReadString(doc, "sku"), ReadString(doc, "productCode"));
            if (string.IsNullOrWhiteSpace(sku)) continue;
            catalog[sku] = new SkuCatalogEntry(
                sku,
                FirstNonEmpty(ReadString(doc, "itemName"), ReadString(doc, "description")),
                ReadSupplierId(doc),
                ReadSupplierName(doc));
        }
        return catalog;
    }

    public readonly record struct SkuCatalogEntry(
        string Sku,
        string ItemName,
        string SupplierId,
        string SupplierName);

    private static Acc Get(Dictionary<string, Acc> map, string sku, string description)
    {
        if (!map.TryGetValue(sku, out var current))
        {
            current = new Acc { Sku = sku, Description = description };
            map[sku] = current;
        }
        else if (string.IsNullOrWhiteSpace(current.Description) && !string.IsNullOrWhiteSpace(description))
        {
            current.Description = description;
        }
        return current;
    }

    private static SkuSalesRow Clone(
        SkuSalesRow row,
        int rank,
        decimal? availableQty = null,
        bool? isLowStock = null) => new()
    {
        Rank = rank,
        Sku = row.Sku,
        Description = row.Description,
        SupplierId = row.SupplierId,
        SupplierName = row.SupplierName,
        SoldQty = row.SoldQty,
        ReturnQty = row.ReturnQty,
        NetQty = row.NetQty,
        SoldAmount = row.SoldAmount,
        ReturnAmount = row.ReturnAmount,
        NetAmount = row.NetAmount,
        AvailableQty = availableQty ?? row.AvailableQty,
        IsLowStock = isLowStock ?? row.IsLowStock,
    };

    private static string ReadSupplierId(BsonDocument doc)
    {
        if (!doc.TryGetValue("supplierNameId", out var value) || value.IsBsonNull) return "";
        if (value.IsObjectId) return value.AsObjectId.ToString();
        if (value.IsString) return value.AsString.Trim();
        if (value.IsBsonDocument)
        {
            var nested = value.AsBsonDocument;
            if (nested.TryGetValue("_id", out var id) && !id.IsBsonNull)
                return id.IsObjectId ? id.AsObjectId.ToString() : id.ToString() ?? "";
        }
        return value.ToString() ?? "";
    }

    private static string ReadSupplierName(BsonDocument doc)
    {
        var direct = ReadString(doc, "supplierName");
        if (!string.IsNullOrWhiteSpace(direct)) return direct;
        if (doc.TryGetValue("supplierNameId", out var value) && value.IsBsonDocument)
            return ReadString(value.AsBsonDocument, "name");
        return "";
    }

    private static decimal LineAmount(BsonDocument line, decimal qty)
    {
        var rate = ReadDecimal(line, "rate");
        if (rate > 0 && qty > 0) return Round(rate * qty);
        return FirstPositive(ReadDecimal(line, "amount"), ReadDecimal(line, "revisedAmount"));
    }

    private static decimal ReturnLineAmount(BsonDocument line, decimal qty)
    {
        var inclusive = FirstPositive(
            ReadDecimal(line, "revisedInclusiveAmount"),
            ReadDecimal(line, "lineTotal"),
            ReadDecimal(line, "amount"));
        if (inclusive > 0) return inclusive;
        var taxable = FirstPositive(ReadDecimal(line, "revisedAmount"), ReadDecimal(line, "amount"));
        var tax = FirstPositive(
            ReadDecimal(line, "revisedTaxAmount"),
            ReadDecimal(line, "taxAmount"),
            ReadDecimal(line, "taxAmt"),
            ReadDecimal(line, "cgstAmount") + ReadDecimal(line, "sgstAmount") + ReadDecimal(line, "igstAmount"));
        if (taxable > 0) return Round(taxable + tax);
        var rate = ReadDecimal(line, "rate");
        return rate > 0 && qty > 0 ? Round(rate * qty) : 0;
    }

    private static List<BsonDocument> ReadLines(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && value.IsBsonArray
            ? value.AsBsonArray.OfType<BsonDocument>().ToList()
            : [];

    private static bool IsPosted(BsonDocument doc) =>
        FirstNonEmpty(ReadString(doc, "status"), "posted")
            .Equals("posted", StringComparison.OrdinalIgnoreCase);

    private static string ReadString(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && !value.IsBsonNull
            ? (value.IsString ? value.AsString : value.ToString() ?? "").Trim()
            : "";

    private static decimal ReadDecimal(BsonDocument doc, string field)
    {
        if (!doc.TryGetValue(field, out var value) || value.IsBsonNull) return 0;
        if (value.IsNumeric) return (decimal)value.ToDouble();
        return decimal.TryParse((value.ToString() ?? "").Replace(",", ""), NumberStyles.Any,
            CultureInfo.InvariantCulture, out var parsed)
            ? Round(parsed)
            : 0;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";

    private static decimal FirstPositive(params decimal[] values) =>
        values.FirstOrDefault(value => value > 0);

    private static decimal Round(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private sealed class Acc
    {
        public string Sku { get; init; } = "";
        public string Description { get; set; } = "";
        public decimal SoldQty { get; set; }
        public decimal SoldAmount { get; set; }
        public decimal ReturnQty { get; set; }
        public decimal ReturnAmount { get; set; }
    }
}
