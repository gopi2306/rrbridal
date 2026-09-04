using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class SkuSalesReportService
{
    public const int MaxRows = 10_000;
    public const int FastSellersDefaultLimit = 100;
    private static readonly TimeSpan BusinessOffset = TimeSpan.FromHours(5.5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMongoDatabase _db;
    private readonly HttpClient _centralApi;
    private readonly StoreContext _storeContext;
    private readonly PosBillingSettingsStore _billingSettings;
    private CentralOnlineModeService? _centralMode;

    public SkuSalesReportService(
        IMongoDatabase localDb,
        HttpClient centralApi,
        StoreContext storeContext,
        PosBillingSettingsStore billingSettings)
    {
        _db = localDb;
        _centralApi = centralApi;
        _storeContext = storeContext;
        _billingSettings = billingSettings;
    }

    public void ConfigureOnline(CentralOnlineModeService centralMode) => _centralMode = centralMode;

    public Task<SupplierWiseReportResponse> LoadSupplierWiseAsync(
        SkuSalesReportQuery query,
        CancellationToken ct = default)
    {
        Validate(query, MaxRows);
        return _centralMode?.IsOnlineMode == true
            ? LoadOnlineAsync<SupplierWiseReportResponse>(query, "supplier-wise", ct)
            : LoadSupplierWiseOfflineAsync(query, ct);
    }

    public Task<FastSellersReportResponse> LoadFastSellersAsync(
        SkuSalesReportQuery query,
        CancellationToken ct = default)
    {
        Validate(query, MaxRows);
        return _centralMode?.IsOnlineMode == true
            ? LoadOnlineAsync<FastSellersReportResponse>(query, "fast-sellers", ct)
            : LoadFastSellersOfflineAsync(query, ct);
    }

    public Task SaveSupplierWiseExportAsync(
        string filePath,
        SkuSalesReportQuery query,
        CancellationToken ct = default) =>
        SaveExportAsync(filePath, query, "supplier-wise", async token =>
        {
            var report = await LoadSupplierWiseOfflineAsync(query, token).ConfigureAwait(false);
            SupplierWiseReportExcelExporter.ExportToFile(filePath, report);
        }, ct);

    public Task SaveFastSellersExportAsync(
        string filePath,
        SkuSalesReportQuery query,
        CancellationToken ct = default) =>
        SaveExportAsync(filePath, query, "fast-sellers", async token =>
        {
            var report = await LoadFastSellersOfflineAsync(query, token).ConfigureAwait(false);
            FastSellersReportExcelExporter.ExportToFile(filePath, report);
        }, ct);

    private async Task SaveExportAsync(
        string filePath,
        SkuSalesReportQuery query,
        string path,
        Func<CancellationToken, Task> offlineExport,
        CancellationToken ct)
    {
        Validate(query, MaxRows);
        if (_centralMode?.IsOnlineMode == true)
        {
            using var response = await _centralApi.GetAsync(BuildUri(query, path, export: true), ct)
                .ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw BuildCentralError(path, "download", response.StatusCode, System.Text.Encoding.UTF8.GetString(bytes));
            await System.IO.File.WriteAllBytesAsync(filePath, bytes, ct).ConfigureAwait(false);
            return;
        }

        await offlineExport(ct).ConfigureAwait(false);
    }

    private async Task<T> LoadOnlineAsync<T>(SkuSalesReportQuery query, string path, CancellationToken ct)
    {
        using var response = await _centralApi.GetAsync(BuildUri(query, path, export: false), ct)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw BuildCentralError(path, "load", response.StatusCode, body);
        return JsonSerializer.Deserialize<T>(body, JsonOptions)
               ?? throw new InvalidOperationException($"Central {path} report returned an empty response.");
    }

    private async Task<SupplierWiseReportResponse> LoadSupplierWiseOfflineAsync(
        SkuSalesReportQuery query,
        CancellationToken ct)
    {
        var loaded = await LoadOfflineRowsAsync(query, Math.Clamp(query.Limit, 1, MaxRows), ct)
            .ConfigureAwait(false);
        var filtered = SkuSalesReportAggregator.FilterForSupplierSearch(loaded.Rows, query.Search);
        var data = SkuSalesReportAggregator.GroupSupplierWise(filtered);
        return new SupplierWiseReportResponse
        {
            Period = loaded.Period,
            Filters = new SkuSalesReportFilters { Search = TrimOrNull(query.Search) },
            Limit = loaded.InvoiceLimit,
            Truncated = loaded.InvoiceTruncated,
            Total = loaded.InvoiceTotal,
            Totals = SkuSalesReportAggregator.TotalsFrom(filtered, data.Count),
            Data = data,
        };
    }

    private async Task<FastSellersReportResponse> LoadFastSellersOfflineAsync(
        SkuSalesReportQuery query,
        CancellationToken ct)
    {
        var loaded = await LoadOfflineRowsAsync(query, MaxRows, ct).ConfigureAwait(false);
        var filtered = SkuSalesReportAggregator.FilterForFastSearch(loaded.Rows, query.Search);
        var ranked = SkuSalesReportAggregator.RankFastSellers(filtered);
        var limit = Math.Clamp(query.Limit, 1, MaxRows);
        var page = ranked.Take(limit).ToList();
        var data = SkuSalesReportAggregator.AttachStockLevels(page, loaded.QtyBySku, ResolveMatchQty());
        return new FastSellersReportResponse
        {
            Period = loaded.Period,
            Filters = new SkuSalesReportFilters { Search = TrimOrNull(query.Search) },
            Limit = limit,
            Truncated = loaded.InvoiceTruncated || ranked.Count > limit,
            Total = ranked.Count,
            Totals = SkuSalesReportAggregator.TotalsFrom(data),
            Data = data,
        };
    }

    private async Task<OfflineSkuSales> LoadOfflineRowsAsync(
        SkuSalesReportQuery query,
        int invoiceLimit,
        CancellationToken ct)
    {
        var storeCode = EffectiveStoreCode(query);
        var billDocs = await _db.GetCollection<BsonDocument>("store_bills")
            .Find(Builders<BsonDocument>.Filter.Eq("storeId", storeCode))
            .ToListAsync(ct).ConfigureAwait(false);

        var matching = billDocs
            .Where(IsPosted)
            .Select(doc => (Doc: doc, Occurred: ReadOccurredAt(doc)))
            .Where(row => row.Occurred.HasValue && IsInBusinessRange(row.Occurred.Value, query.From, query.To))
            .Where(row => MatchesCounter(row.Doc, query.PosCounter))
            .OrderByDescending(row => row.Occurred)
            .ThenByDescending(row => ReadString(row.Doc, "billNo"), StringComparer.Ordinal)
            .ToList();

        var invoiceTotal = matching.Count;
        var selected = matching.Take(invoiceLimit).Select(row => row.Doc).ToList();
        var billNos = selected
            .Select(doc => ReadString(doc, "billNo"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

        var returnDocs = billNos.Count == 0
            ? []
            : await _db.GetCollection<BsonDocument>("store_sale_returns")
                .Find(Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("storeId", storeCode),
                    Builders<BsonDocument>.Filter.In("originalBillNo", billNos)))
                .ToListAsync(ct).ConfigureAwait(false);

        var skus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in selected.Concat(returnDocs))
        {
            foreach (var field in new[] { "lines", "returnLines" })
            {
                if (!doc.TryGetValue(field, out var value) || !value.IsBsonArray) continue;
                foreach (var line in value.AsBsonArray.OfType<BsonDocument>())
                {
                    var sku = FirstNonEmpty(ReadString(line, "sku"), ReadString(line, "productCode"));
                    if (!string.IsNullOrWhiteSpace(sku)) skus.Add(sku);
                }
            }
        }

        var productDocs = skus.Count == 0
            ? []
            : await _db.GetCollection<BsonDocument>("local_products_cache")
                .Find(Builders<BsonDocument>.Filter.In("sku", skus))
                .ToListAsync(ct).ConfigureAwait(false);

        var catalog = SkuSalesReportAggregator.ReadCatalog(productDocs);
        var qtyBySku = ReadStockQtyBySku(productDocs);
        var (rows, _) = SkuSalesReportAggregator.Collect(selected, returnDocs.Where(IsPosted), catalog);
        return new OfflineSkuSales(
            new SkuSalesReportPeriod
            {
                From = FormatYmd(query.From),
                To = FormatYmd(query.To),
                Timezone = "Asia/Kolkata",
                StoreCode = storeCode,
                StoreName = string.IsNullOrWhiteSpace(query.StoreName) ? storeCode : query.StoreName.Trim(),
                PosCounter = TrimOrNull(query.PosCounter),
            },
            invoiceLimit,
            invoiceTotal,
            invoiceTotal > invoiceLimit,
            rows,
            qtyBySku);
    }

    private string BuildUri(SkuSalesReportQuery query, string path, bool export)
    {
        var values = new List<(string Key, string? Value)>
        {
            ("storeCode", EffectiveStoreCode(query)),
            ("from", FormatYmd(query.From)),
            ("to", FormatYmd(query.To)),
            ("search", TrimOrNull(query.Search)),
            ("posCounter", TrimOrNull(query.PosCounter)),
            ("limit", Math.Clamp(query.Limit, 1, MaxRows).ToString(CultureInfo.InvariantCulture)),
        };
        if (string.Equals(path, "fast-sellers", StringComparison.Ordinal))
            values.Add(("matchQty", ResolveMatchQty().ToString(CultureInfo.InvariantCulture)));
        var qs = string.Join("&", values
            .Where(pair => pair.Value != null)
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
        return $"/api/reports/{path}{(export ? "/export" : "")}?{qs}";
    }

    private decimal ResolveMatchQty() =>
        Math.Max(0m, _billingSettings.Current.GoingOutOfStockMatchQty);

    private string EffectiveStoreCode(SkuSalesReportQuery query) =>
        string.IsNullOrWhiteSpace(query.StoreCode) ? _storeContext.StoreId : query.StoreCode.Trim();

    private static Dictionary<string, decimal> ReadStockQtyBySku(IEnumerable<BsonDocument> products)
    {
        var qtyBySku = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in products)
        {
            var sku = FirstNonEmpty(ReadString(doc, "sku"), ReadString(doc, "productCode"));
            if (string.IsNullOrWhiteSpace(sku) || qtyBySku.ContainsKey(sku)) continue;
            qtyBySku[sku] = ReadDecimal(doc, "stockQty");
        }
        return qtyBySku;
    }

    private static bool MatchesCounter(BsonDocument doc, string? posCounter)
    {
        if (TrimOrNull(posCounter) is not { } counter) return true;
        return string.Equals(ReadString(doc, "posCounter"), counter, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset? ReadOccurredAt(BsonDocument doc)
    {
        foreach (var field in new[] { "createdAtUtc", "createdAt" })
        {
            if (!doc.TryGetValue(field, out var value) || value.IsBsonNull) continue;
            if (value.IsBsonDateTime)
                return new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero);
            if (DateTimeOffset.TryParse(value.ToString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var parsed))
                return parsed;
        }
        return null;
    }

    private static bool IsInBusinessRange(DateTimeOffset occurred, DateTime from, DateTime to)
    {
        var date = occurred.ToOffset(BusinessOffset).Date;
        return date >= from.Date && date <= to.Date;
    }

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
            ? Math.Round(parsed, 4, MidpointRounding.AwayFromZero)
            : 0;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatYmd(DateTime value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static InvalidOperationException BuildCentralError(
        string path,
        string operation,
        System.Net.HttpStatusCode statusCode,
        string responseBody)
    {
        var detail = string.IsNullOrWhiteSpace(responseBody) ? "No error details were returned." : responseBody.Trim();
        return new InvalidOperationException(
            $"Central {path} report {operation} failed ({(int)statusCode}): {detail}");
    }

    private static void Validate(SkuSalesReportQuery query, int maxLimit)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.From.Date > query.To.Date)
            throw new ArgumentException("From date must be on or before to date.", nameof(query));
        if (query.Limit < 1 || query.Limit > maxLimit)
            throw new ArgumentOutOfRangeException(nameof(query), $"Limit must be between 1 and {maxLimit}.");
    }

    private sealed record OfflineSkuSales(
        SkuSalesReportPeriod Period,
        int InvoiceLimit,
        int InvoiceTotal,
        bool InvoiceTruncated,
        IReadOnlyList<SkuSalesRow> Rows,
        IReadOnlyDictionary<string, decimal> QtyBySku);
}
