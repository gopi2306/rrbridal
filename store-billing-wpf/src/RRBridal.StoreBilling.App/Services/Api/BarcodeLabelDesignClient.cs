using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Api;

public sealed class BarcodeLabelDesignClient
{
    private readonly HttpClient _http;

    public BarcodeLabelDesignClient(HttpClient http) => _http = http;

    public async Task<(JsonElement? Data, string? Error)> GetActiveDesignAsync(CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.GetAsync("/api/barcode-label-designs/active", ct);
            return await CentralApiJson.ReadClonedRootAsync(res, "Central login required.", ct);
        }
        catch (HttpRequestException ex)
        {
            return (null, $"Cannot reach central API: {ex.Message}");
        }
    }
}
