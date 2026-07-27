using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Api;

/// <summary>Central /api/dashboard and /api/inventory helpers for Pure Online POS screens.</summary>
public sealed class CentralDashboardClient
{
    private readonly HttpClient _http;
    private readonly StoreContext _storeContext;

    public CentralDashboardClient(HttpClient http, StoreContext storeContext)
    {
        _http = http;
        _storeContext = storeContext;
    }

    public Task<JsonDocument> GetStoreSalesAsync(
        string period = "today",
        string? from = null,
        string? to = null,
        int billPage = 1,
        int billLimit = 20,
        CancellationToken ct = default)
    {
        var q = new StringBuilder($"/api/dashboard/store/sales?storeId={Uri.EscapeDataString(_storeContext.StoreId)}");
        q.Append($"&period={Uri.EscapeDataString(period)}");
        if (!string.IsNullOrWhiteSpace(from))
            q.Append($"&from={Uri.EscapeDataString(from.Trim())}");
        if (!string.IsNullOrWhiteSpace(to))
            q.Append($"&to={Uri.EscapeDataString(to.Trim())}");
        q.Append($"&billPage={billPage}&billLimit={billLimit}");
        return GetJsonAsync(q.ToString(), "store sales dashboard", ct);
    }

    public Task<JsonDocument> GetStoreOpsAsync(CancellationToken ct = default)
    {
        var url = $"/api/dashboard/store?storeId={Uri.EscapeDataString(_storeContext.StoreId)}";
        return GetJsonAsync(url, "store dashboard", ct);
    }

    public Task<JsonDocument> GetStoreDayCloseAsync(
        string businessDate,
        string? posCounter = null,
        CancellationToken ct = default)
    {
        var q = new StringBuilder(
            $"/api/dashboard/store/day-close?storeId={Uri.EscapeDataString(_storeContext.StoreId)}" +
            $"&businessDate={Uri.EscapeDataString(businessDate.Trim())}");
        if (!string.IsNullOrWhiteSpace(posCounter))
            q.Append($"&posCounter={Uri.EscapeDataString(posCounter.Trim())}");
        return GetJsonAsync(q.ToString(), "store day-close", ct);
    }

    public Task<JsonDocument> GetStoreDayCloseReportAsync(
        string businessDate,
        string? posCounter = null,
        CancellationToken ct = default)
    {
        var q = new StringBuilder(
            $"/api/dashboard/store/day-close/report?storeId={Uri.EscapeDataString(_storeContext.StoreId)}" +
            $"&businessDate={Uri.EscapeDataString(businessDate.Trim())}");
        if (!string.IsNullOrWhiteSpace(posCounter))
            q.Append($"&posCounter={Uri.EscapeDataString(posCounter.Trim())}");
        return GetJsonAsync(q.ToString(), "store day-close report", ct);
    }

    public Task<JsonDocument> GetSalesmenAsync(
        string period = "today",
        string? from = null,
        string? to = null,
        CancellationToken ct = default)
    {
        var q = new StringBuilder(
            $"/api/dashboard/store/sales/salesmen?storeId={Uri.EscapeDataString(_storeContext.StoreId)}" +
            $"&period={Uri.EscapeDataString(period)}");
        if (!string.IsNullOrWhiteSpace(from))
            q.Append($"&from={Uri.EscapeDataString(from.Trim())}");
        if (!string.IsNullOrWhiteSpace(to))
            q.Append($"&to={Uri.EscapeDataString(to.Trim())}");
        return GetJsonAsync(q.ToString(), "salesmen dashboard", ct);
    }

    public Task<JsonDocument> GetBillMarginAsync(
        string period = "today",
        string? from = null,
        string? to = null,
        CancellationToken ct = default)
    {
        var q = new StringBuilder(
            $"/api/dashboard/store/sales/bill-margin?storeId={Uri.EscapeDataString(_storeContext.StoreId)}" +
            $"&period={Uri.EscapeDataString(period)}");
        if (!string.IsNullOrWhiteSpace(from))
            q.Append($"&from={Uri.EscapeDataString(from.Trim())}");
        if (!string.IsNullOrWhiteSpace(to))
            q.Append($"&to={Uri.EscapeDataString(to.Trim())}");
        return GetJsonAsync(q.ToString(), "bill-margin dashboard", ct);
    }

    public Task<JsonDocument> GetVendorSalesAsync(
        string period = "today",
        string? from = null,
        string? to = null,
        CancellationToken ct = default)
    {
        var q = new StringBuilder(
            $"/api/dashboard/store/sales/vendors?storeId={Uri.EscapeDataString(_storeContext.StoreId)}" +
            $"&period={Uri.EscapeDataString(period)}");
        if (!string.IsNullOrWhiteSpace(from))
            q.Append($"&from={Uri.EscapeDataString(from.Trim())}");
        if (!string.IsNullOrWhiteSpace(to))
            q.Append($"&to={Uri.EscapeDataString(to.Trim())}");
        return GetJsonAsync(q.ToString(), "vendor sales dashboard", ct);
    }

    public async Task<JsonDocument> GetInventoryGridAsync(
        string? search = null,
        int page = 1,
        int limit = 100,
        CancellationToken ct = default)
    {
        var q = new StringBuilder(
            $"/api/inventory/grid?storeId={Uri.EscapeDataString(_storeContext.StoreId)}" +
            $"&page={Math.Max(1, page)}&limit={Math.Clamp(limit, 1, 500)}");
        if (!string.IsNullOrWhiteSpace(search))
            q.Append($"&search={Uri.EscapeDataString(search.Trim())}");
        return await GetJsonAsync(q.ToString(), "inventory grid", ct);
    }

    public async Task PostInventoryAdjustmentAsync(
        string sku,
        decimal qtyDelta,
        string reason,
        CancellationToken ct = default)
    {
        var body = new
        {
            locationKind = "store",
            storeCode = _storeContext.StoreId,
            reason,
            lines = new[]
            {
                new { sku, qtyDelta = (double)qtyDelta, note = reason },
            },
        };

        using var res = await _http.PostAsJsonAsync("/api/inventory-adjustments", body, ct);
        if (res.IsSuccessStatusCode)
            return;

        var raw = await res.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(
            $"Central inventory adjustment failed ({(int)res.StatusCode}): {Truncate(raw, 400)}");
    }

    private async Task<JsonDocument> GetJsonAsync(string url, string label, CancellationToken ct)
    {
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Central {label} failed ({(int)res.StatusCode}): {Truncate(raw, 400)}");
        return JsonDocument.Parse(raw);
    }

    public static decimal ReadDecimal(JsonElement el, string name, decimal fallback = 0m)
    {
        if (!el.TryGetProperty(name, out var p))
            return fallback;
        return p.ValueKind switch
        {
            JsonValueKind.Number when p.TryGetDecimal(out var d) => d,
            JsonValueKind.String when decimal.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
            _ => fallback,
        };
    }

    public static int ReadInt(JsonElement el, string name, int fallback = 0)
    {
        if (!el.TryGetProperty(name, out var p))
            return fallback;
        return p.ValueKind switch
        {
            JsonValueKind.Number when p.TryGetInt32(out var i) => i,
            JsonValueKind.String when int.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var i) => i,
            _ => fallback,
        };
    }

    public static long ReadLong(JsonElement el, string name, long fallback = 0)
    {
        if (!el.TryGetProperty(name, out var p))
            return fallback;
        return p.ValueKind switch
        {
            JsonValueKind.Number when p.TryGetInt64(out var i) => i,
            JsonValueKind.String when long.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var i) => i,
            _ => fallback,
        };
    }

    public static string ReadString(JsonElement el, string name, string fallback = "")
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return fallback;
        return p.ValueKind == JsonValueKind.String ? (p.GetString() ?? fallback) : (p.ToString() ?? fallback);
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";
}
