using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RRBridal.StoreBilling.App.Services.Customers;

namespace RRBridal.StoreBilling.App.Services.Api;

public sealed class GstinLookupClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;

    public GstinLookupClient(HttpClient http) => _http = http;

    public async Task<(GstinLookupDetails? Data, string? Error)> LookupAsync(
        string gstin,
        CancellationToken ct = default)
    {
        var normalized = GstinLookupMapper.Normalize(gstin);
        try
        {
            using var res = await _http.GetAsync(
                $"/api/gst/lookup?gstin={Uri.EscapeDataString(normalized)}",
                ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            if (res.IsSuccessStatusCode)
            {
                if (string.IsNullOrWhiteSpace(json))
                    return (null, "Central returned an empty GST lookup response.");

                var details = JsonSerializer.Deserialize<GstinLookupDetails>(json, JsonOpts);
                if (details == null
                    || (string.IsNullOrWhiteSpace(details.Gstin) && string.IsNullOrWhiteSpace(details.Name)))
                    return (null, "GSTIN not found.");
                return (details, null);
            }

            return (null, FormatError((int)res.StatusCode, json, res.ReasonPhrase));
        }
        catch (OperationCanceledException)
        {
            return (null, "GST lookup was cancelled.");
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

    private static string FormatError(int status, string body, string? reason)
    {
        var detail = ExtractMessage(body);
        if (string.IsNullOrWhiteSpace(detail))
            detail = string.IsNullOrWhiteSpace(body) ? reason : body.Trim();
        if (status == 503)
            return string.IsNullOrWhiteSpace(detail)
                ? "GST lookup is not configured on the server."
                : detail;
        if (status == 404)
            return string.IsNullOrWhiteSpace(detail) ? "GSTIN not found." : detail;
        return $"GST lookup failed ({status}): {detail}";
    }

    private static string ExtractMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "";
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("message", out var msg))
                return "";
            if (msg.ValueKind == JsonValueKind.String)
                return msg.GetString()?.Trim() ?? "";
            if (msg.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in msg.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var text = item.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                            return text;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Use raw body in FormatError.
        }

        return "";
    }
}
