using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Api;

public sealed class StoreInfoClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;

    public StoreInfoClient(HttpClient http) => _http = http;

    public async Task<(string? Name, string? Error)> GetStoreNameAsync(string storeCode, CancellationToken ct = default)
    {
        try
        {
            var code = Uri.EscapeDataString(storeCode.Trim().ToLowerInvariant());
            using var res = await _http.GetAsync($"/api/stores/{code}", ct);
            var (data, err) = await CentralApiJson.ReadClonedRootAsync(
                res,
                "Central login required to load store name.",
                ct);
            if (data == null)
                return (null, err);

            if (TryGetString(data.Value, "name", out var name) && !string.IsNullOrWhiteSpace(name))
                return (name.Trim(), null);

            if (TryGetString(data.Value, "tradeName", out var trade) && !string.IsNullOrWhiteSpace(trade))
                return (trade.Trim(), null);

            return (null, "Store record has no name field.");
        }
        catch (HttpRequestException ex)
        {
            return (null, $"Cannot reach central API: {ex.Message}");
        }
    }

    public sealed record StorePosMode(
        bool PreferCentralOnline,
        PosBillingSettingsDocument? BillingSettings,
        CounterScreenAccessSettings? ScreenAccess,
        string? ReceiptPrintSettingsJson);

    /// <summary>
    /// Public sync endpoint — no JWT. Counters inherit billing + print settings from central.
    /// </summary>
    public async Task<(StorePosMode? Mode, string? Error)> GetStorePosModeAsync(
        string storeCode,
        CancellationToken ct = default)
    {
        try
        {
            var code = Uri.EscapeDataString(storeCode.Trim().ToLowerInvariant());
            using var res = await _http.GetAsync($"/api/sync/store-pos-mode?storeId={code}", ct);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                return (null, string.IsNullOrWhiteSpace(body) ? res.ReasonPhrase : body);
            }

            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("preferCentralOnline", out var flag))
                return (null, "Missing preferCentralOnline in response");

            PosBillingSettingsDocument? billing = null;
            if (doc.RootElement.TryGetProperty("posBillingSettings", out var billingEl)
                && billingEl.ValueKind == JsonValueKind.Object)
            {
                billing = JsonSerializer.Deserialize<PosBillingSettingsDocument>(billingEl.GetRawText(), JsonOpts);
            }

            CounterScreenAccessSettings? access = billing?.ScreenAccess;
            if (access == null
                && doc.RootElement.TryGetProperty("posScreenAccess", out var accessEl)
                && accessEl.ValueKind == JsonValueKind.Object)
            {
                access = JsonSerializer.Deserialize<CounterScreenAccessSettings>(accessEl.GetRawText(), JsonOpts);
            }

            string? receiptJson = null;
            if (doc.RootElement.TryGetProperty("receiptPrintSettings", out var receiptEl)
                && receiptEl.ValueKind == JsonValueKind.Object)
            {
                receiptJson = receiptEl.GetRawText();
            }

            return (new StorePosMode(flag.ValueKind == JsonValueKind.True, billing, access, receiptJson), null);
        }
        catch (HttpRequestException ex)
        {
            return (null, $"Cannot reach central API: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    public async Task<(bool? PreferCentralOnline, string? Error)> GetPreferCentralOnlineAsync(
        string storeCode,
        CancellationToken ct = default)
    {
        var (mode, err) = await GetStorePosModeAsync(storeCode, ct).ConfigureAwait(false);
        return (mode?.PreferCentralOnline, err);
    }

    /// <summary>Requires central JWT (admin stores PATCH).</summary>
    public async Task<(bool Ok, string? Error)> SetPreferCentralOnlineAsync(
        string storeCode,
        bool preferCentralOnline,
        CancellationToken ct = default)
    {
        return await PatchStoreAsync(storeCode, new { preferCentralOnline }, ct).ConfigureAwait(false);
    }

    /// <summary>Requires central JWT — full billing settings for all counters.</summary>
    public async Task<(bool Ok, string? Error)> SetPosBillingSettingsAsync(
        string storeCode,
        PosBillingSettingsDocument settings,
        CancellationToken ct = default)
    {
        return await PatchStoreAsync(
            storeCode,
            new
            {
                preferCentralOnline = settings.PreferCentralOnline,
                posBillingSettings = settings,
                posScreenAccess = settings.ScreenAccess,
            },
            ct).ConfigureAwait(false);
    }

    /// <summary>Requires central JWT — store-wide receipt print settings (no PC queue names).</summary>
    public async Task<(bool Ok, string? Error)> SetReceiptPrintSettingsAsync(
        string storeCode,
        object receiptPrintSettings,
        CancellationToken ct = default)
    {
        return await PatchStoreAsync(
            storeCode,
            new { receiptPrintSettings },
            ct).ConfigureAwait(false);
    }

    private async Task<(bool Ok, string? Error)> PatchStoreAsync(
        string storeCode,
        object body,
        CancellationToken ct)
    {
        try
        {
            var code = Uri.EscapeDataString(storeCode.Trim().ToLowerInvariant());
            using var res = await _http.PatchAsJsonAsync(
                $"/api/admin/stores/{code}",
                body,
                JsonOpts,
                ct);
            if (res.IsSuccessStatusCode)
                return (true, null);

            var text = await res.Content.ReadAsStringAsync(ct);
            return (false, string.IsNullOrWhiteSpace(text) ? res.ReasonPhrase : text);
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Cannot reach central API: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static bool TryGetString(JsonElement el, string name, out string? value)
    {
        value = null;
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String)
            return false;
        value = p.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }
}
