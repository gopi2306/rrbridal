using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Api;

public sealed class StoreReceiptSettingsClient
{
    private readonly HttpClient _http;

    public StoreReceiptSettingsClient(HttpClient http) => _http = http;

    public async Task<(JsonElement? Data, string? Error)> GetReceiptSettingsAsync(string storeCode, CancellationToken ct = default)
    {
        try
        {
            var code = Uri.EscapeDataString(storeCode.Trim().ToLowerInvariant());
            using var res = await _http.GetAsync($"/api/stores/{code}/receipt-settings", ct);
            if (res.StatusCode == HttpStatusCode.NotFound)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                var detail = string.IsNullOrWhiteSpace(body) ? res.ReasonPhrase : body.Trim();
                return (null, $"Store '{storeCode}' not found on central. {detail}");
            }

            return await CentralApiJson.ReadClonedRootAsync(res, "Central login required.", ct);
        }
        catch (HttpRequestException ex)
        {
            return (null, $"Cannot reach central API: {ex.Message}");
        }
    }
}
