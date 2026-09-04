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

public sealed class GoingOutOfStockReportService
{
    public const int MaxRows = 10_000;
    private static readonly TimeSpan BusinessOffset = TimeSpan.FromHours(5.5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMongoDatabase _db;
    private readonly HttpClient _centralApi;
    private readonly StoreContext _storeContext;
    private readonly PosBillingSettingsStore _billingSettings;
    private CentralOnlineModeService? _centralMode;

    public GoingOutOfStockReportService(
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

    public Task<GoingOutOfStockReportResponse> LoadAsync(
        GoingOutOfStockReportQuery query,
        CancellationToken ct = default)
    {
        Validate(query);
        return _centralMode?.IsOnlineMode == true
            ? LoadOnlineAsync(query, ct)
            : LoadOfflineAsync(query, ct);
    }

    public async Task SaveExportAsync(
        string filePath,
        GoingOutOfStockReportQuery query,
        CancellationToken ct = default)
    {
        Validate(query);
        if (_centralMode?.IsOnlineMode == true)
        {
            using var response = await _centralApi.GetAsync(BuildUri(query, export: true), ct)
                .ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw BuildCentralError("download", response.StatusCode, System.Text.Encoding.UTF8.GetString(bytes));
            await System.IO.File.WriteAllBytesAsync(filePath, bytes, ct).ConfigureAwait(false);
            return;
        }

        var report = await LoadOfflineAsync(query, ct).ConfigureAwait(false);
        GoingOutOfStockReportExcelExporter.ExportToFile(filePath, report);
    }

    private async Task<GoingOutOfStockReportResponse> LoadOnlineAsync(
        GoingOutOfStockReportQuery query,
        CancellationToken ct)
    {
        using var response = await _centralApi.GetAsync(BuildUri(query, export: false), ct)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw BuildCentralError("load", response.StatusCode, body);
        return JsonSerializer.Deserialize<GoingOutOfStockReportResponse>(body, JsonOptions)
               ?? throw new InvalidOperationException("Central going-out-of-stock report returned an empty response.");
    }

    private async Task<GoingOutOfStockReportResponse> LoadOfflineAsync(
        GoingOutOfStockReportQuery query,
        CancellationToken ct)
    {
        var storeCode = EffectiveStoreCode(query);
        var defaultMatchQty = ResolveMatchQty(query);
        var products = await _db.GetCollection<BsonDocument>("local_products_cache")
            .Find(Builders<BsonDocument>.Filter.Exists("sku"))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var inputs = new List<GoingOutOfStockReportEvaluator.ProductInput>();
        var qtyBySku = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in products)
        {
            if (doc.TryGetValue("isActive", out var active) && active.IsBoolean && !active.AsBoolean)
                continue;

            var input = GoingOutOfStockReportEvaluator.ReadProduct(doc);
            if (input is null) continue;
            inputs.Add(input.Value);

            var sku = input.Value.Sku;
            if (!qtyBySku.ContainsKey(sku))
                qtyBySku[sku] = ReadDecimal(doc, "stockQty");
        }

        var allRows = GoingOutOfStockReportEvaluator.Collect(inputs, qtyBySku, defaultMatchQty);
        var filtered = GoingOutOfStockReportEvaluator.Filter(allRows, query.Search, query.Status);
        var limit = Math.Clamp(query.Limit, 1, MaxRows);
        var data = filtered.Take(limit).ToList();
        return new GoingOutOfStockReportResponse
        {
            Period = new GoingOutOfStockReportPeriod
            {
                AsOf = FormatBusinessTodayYmd(),
                Timezone = "Asia/Kolkata",
                StoreCode = storeCode,
                StoreName = string.IsNullOrWhiteSpace(query.StoreName) ? storeCode : query.StoreName.Trim(),
            },
            Filters = new GoingOutOfStockReportFilters
            {
                Search = TrimOrNull(query.Search),
                Status = TrimOrNull(query.Status),
            },
            Limit = limit,
            Truncated = filtered.Count > limit,
            Total = filtered.Count,
            Totals = GoingOutOfStockReportEvaluator.Totals(data),
            Data = data,
        };
    }

    private string BuildUri(GoingOutOfStockReportQuery query, bool export)
    {
        var matchQty = ResolveMatchQty(query);
        var values = new List<(string Key, string? Value)>
        {
            ("storeCode", EffectiveStoreCode(query)),
            ("search", TrimOrNull(query.Search)),
            ("status", TrimOrNull(query.Status)),
            ("limit", Math.Clamp(query.Limit, 1, MaxRows).ToString(CultureInfo.InvariantCulture)),
            ("matchQty", matchQty.ToString(CultureInfo.InvariantCulture)),
        };
        var qs = string.Join("&", values
            .Where(pair => pair.Value != null)
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
        return $"/api/reports/going-out-of-stock{(export ? "/export" : "")}?{qs}";
    }

    private decimal ResolveMatchQty(GoingOutOfStockReportQuery query) =>
        query.MatchQty ?? Math.Max(0m, _billingSettings.Current.GoingOutOfStockMatchQty);

    private string EffectiveStoreCode(GoingOutOfStockReportQuery query) =>
        string.IsNullOrWhiteSpace(query.StoreCode) ? _storeContext.StoreId : query.StoreCode.Trim();

    private static string FormatBusinessTodayYmd() =>
        DateTimeOffset.UtcNow.ToOffset(BusinessOffset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static decimal ReadDecimal(BsonDocument doc, string field)
    {
        if (!doc.TryGetValue(field, out var value) || value.IsBsonNull) return 0;
        if (value.IsNumeric) return (decimal)value.ToDouble();
        return decimal.TryParse((value.ToString() ?? "").Replace(",", ""), NumberStyles.Any,
            CultureInfo.InvariantCulture, out var parsed)
            ? Math.Round(parsed, 4, MidpointRounding.AwayFromZero)
            : 0;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static InvalidOperationException BuildCentralError(
        string operation,
        System.Net.HttpStatusCode statusCode,
        string responseBody)
    {
        var detail = string.IsNullOrWhiteSpace(responseBody) ? "No error details were returned." : responseBody.Trim();
        return new InvalidOperationException(
            $"Central going-out-of-stock report {operation} failed ({(int)statusCode}): {detail}");
    }

    private static void Validate(GoingOutOfStockReportQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Limit < 1 || query.Limit > MaxRows)
            throw new ArgumentOutOfRangeException(nameof(query), $"Limit must be between 1 and {MaxRows}.");
        if (TrimOrNull(query.Status) is { } status
            && !status.Equals("low", StringComparison.OrdinalIgnoreCase)
            && !status.Equals("critical", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Status must be low or critical.", nameof(query));
        if (query.MatchQty is < 0)
            throw new ArgumentOutOfRangeException(nameof(query), "MatchQty cannot be negative.");
    }
}
