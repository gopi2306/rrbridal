using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Api;

internal static class CentralApiJson
{
    public static async Task<(JsonElement? Data, string? Error)> ReadClonedRootAsync(
        HttpResponseMessage res,
        string unauthorizedMessage,
        CancellationToken ct)
    {
        if (res.IsSuccessStatusCode)
        {
            var json = await res.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(json))
                return (null, "Central returned an empty response.");

            using var doc = JsonDocument.Parse(json);
            return (doc.RootElement.Clone(), null);
        }

        var body = await res.Content.ReadAsStringAsync(ct);
        var detail = string.IsNullOrWhiteSpace(body) ? res.ReasonPhrase : body.Trim();
        if (res.StatusCode == HttpStatusCode.Unauthorized)
            return (null, unauthorizedMessage);
        return (null, $"Request failed ({(int)res.StatusCode}): {detail}");
    }
}
