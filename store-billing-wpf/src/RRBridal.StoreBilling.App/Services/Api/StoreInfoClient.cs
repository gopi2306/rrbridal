using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Api;

public sealed class StoreInfoClient
{
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

    private static bool TryGetString(JsonElement el, string name, out string? value)
    {
        value = null;
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String)
            return false;
        value = p.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }
}
