using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Api;

public sealed class CompanyProfileClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

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

    /// <summary>Requires central JWT — updates company master fields used on Tax Invoice (including bank details).</summary>
    public async Task<(bool Ok, string? Error)> PatchAsync(object body, CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.PatchAsJsonAsync("/api/company-profile", body, JsonOpts, ct);
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
}
