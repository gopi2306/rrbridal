using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Api;

public sealed class CompanyProfileClient
{
    private readonly HttpClient _http;

    public CompanyProfileClient(HttpClient http) => _http = http;

    public async Task<(JsonElement? Data, string? Error)> GetAsync(CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.GetAsync("/api/company-profile", ct);
            return await CentralApiJson.ReadClonedRootAsync(
                res,
                "Central login required — use Login above, then pull again.",
                ct);
        }
        catch (HttpRequestException ex)
        {
            return (null, $"Cannot reach central API: {ex.Message}");
        }
    }
}
