using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;

namespace RRBridal.StoreBilling.App.Services.Store;

public static class GoingOutOfStockReportEvaluator
{
    public const string UnmappedVendorId = "__unmapped__";
    public const string UnmappedVendorName = "No vendor mapped";
    public const string UnknownSupplierName = "Unknown supplier";

    public readonly record struct ProductInput(
        string Sku,
        string ItemName,
        decimal? MinimumShelfFit,
        decimal? MinStock,
        decimal? ReorderLevel,
        string SupplierId,
        string SupplierName);

    public static decimal? GetShelfThreshold(ProductInput product)
    {
        if (product.MinimumShelfFit.HasValue) return product.MinimumShelfFit;
        if (product.MinStock.HasValue) return product.MinStock;
        if (product.ReorderLevel.HasValue) return product.ReorderLevel;
        return null;
    }

    public static GoingOutOfStockRow? Evaluate(ProductInput product, decimal storeQty)
    {
        var threshold = GetShelfThreshold(product);
        if (!threshold.HasValue) return null;
        if (storeQty > threshold.Value) return null;

        var criticalLevel = product.MinStock ?? threshold.Value;
        var status = storeQty <= criticalLevel ? "critical" : "low";
        var supplierId = string.IsNullOrWhiteSpace(product.SupplierId)
            ? UnmappedVendorId
            : product.SupplierId;
        var supplierName = supplierId == UnmappedVendorId
            ? UnmappedVendorName
            : FirstNonEmpty(product.SupplierName, UnknownSupplierName);

        return new GoingOutOfStockRow
        {
            Sku = product.Sku,
            ProductName = FirstNonEmpty(product.ItemName, product.Sku),
            StoreQty = Round(storeQty),
            Threshold = Round(threshold.Value),
            Status = status,
            SupplierId = supplierId,
            SupplierName = supplierName,
        };
    }

    public static IReadOnlyList<GoingOutOfStockRow> Collect(
        IEnumerable<ProductInput> products,
        IReadOnlyDictionary<string, decimal> storeQtyBySku)
    {
        var rows = new List<GoingOutOfStockRow>();
        foreach (var product in products)
        {
            storeQtyBySku.TryGetValue(product.Sku, out var qty);
            var row = Evaluate(product, qty);
            if (row != null) rows.Add(row);
        }

        return rows
            .OrderBy(row => row.StoreQty)
            .ThenBy(row => row.Sku, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<GoingOutOfStockRow> Filter(
        IEnumerable<GoingOutOfStockRow> rows,
        string? search,
        string? status)
    {
        var q = search?.Trim() ?? "";
        var statusFilter = status?.Trim().ToLowerInvariant();
        return rows.Where(row =>
            {
                if (!string.IsNullOrWhiteSpace(statusFilter)
                    && !string.Equals(row.Status, statusFilter, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (q.Length == 0) return true;
                return row.Sku.Contains(q, StringComparison.OrdinalIgnoreCase)
                       || row.ProductName.Contains(q, StringComparison.OrdinalIgnoreCase)
                       || row.SupplierName.Contains(q, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
    }

    public static GoingOutOfStockTotals Totals(IEnumerable<GoingOutOfStockRow> rows)
    {
        var list = rows.ToList();
        return new GoingOutOfStockTotals
        {
            SkuCount = list.Count,
            CriticalCount = list.Count(row =>
                row.Status.Equals("critical", StringComparison.OrdinalIgnoreCase)),
            LowCount = list.Count(row =>
                row.Status.Equals("low", StringComparison.OrdinalIgnoreCase)),
            ZeroQtyCount = list.Count(row => row.StoreQty <= 0),
        };
    }

    public static ProductInput? ReadProduct(BsonDocument doc)
    {
        var sku = FirstNonEmpty(ReadString(doc, "sku"), ReadString(doc, "productCode"));
        if (string.IsNullOrWhiteSpace(sku)) return null;

        var minShelf = ReadNullableDecimal(doc, "minimumShelfFit");
        var minStock = ReadNullableDecimal(doc, "minStock");
        var reorder = ReadNullableDecimal(doc, "reorderLevel");
        if (!minShelf.HasValue && !minStock.HasValue && !reorder.HasValue)
            return null;

        return new ProductInput(
            sku,
            FirstNonEmpty(ReadString(doc, "itemName"), ReadString(doc, "description"), sku),
            minShelf,
            minStock,
            reorder,
            ReadSupplierId(doc),
            ReadSupplierName(doc));
    }

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

    private static string ReadString(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && !value.IsBsonNull
            ? (value.IsString ? value.AsString : value.ToString() ?? "").Trim()
            : "";

    private static decimal? ReadNullableDecimal(BsonDocument doc, string field)
    {
        if (!doc.TryGetValue(field, out var value) || value.IsBsonNull) return null;
        if (value.IsNumeric) return Round((decimal)value.ToDouble());
        return decimal.TryParse((value.ToString() ?? "").Replace(",", ""), NumberStyles.Any,
            CultureInfo.InvariantCulture, out var parsed)
            ? Round(parsed)
            : null;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";

    private static decimal Round(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
