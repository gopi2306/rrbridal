using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Payments;

/// <summary>HTTP client for Ezetap / Razorpay POS Bridge (pay, status, cancel).</summary>
public sealed class RazorpayPosBridgeClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _http;

    public RazorpayPosBridgeClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
    }

    public async Task<PaymentResult> PayAndWaitAsync(
        RazorpayPosSettingsDocument settings,
        PaymentRequest request,
        CancellationToken ct)
    {
        ValidateSettings(settings);

        var mode = MapMode(request.PosMode);
        var payBody = new
        {
            username = settings.Username.Trim(),
            appKey = settings.AppKey.Trim(),
            amount = request.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            externalRefNumber = request.InvoiceNo,
            pushTo = new { deviceId = RazorpayPosSettingsDocument.NormalizeDeviceId(settings.DeviceId) },
            mode,
        };

        var payJson = await PostJsonAsync(CombineUrl(settings.ApiBaseUrl, "pay"), payBody, ct);
        var requestId = ReadString(payJson, "p2pRequestId")
            ?? ReadString(payJson, "origP2pRequestId")
            ?? ReadString(payJson, "requestId");

        if (string.IsNullOrWhiteSpace(requestId))
        {
            var err = ReadString(payJson, "errorMessage")
                ?? ReadString(payJson, "message")
                ?? "POS pay did not return p2pRequestId";
            throw new InvalidOperationException(err);
        }

        if (IsImmediateFailure(payJson))
        {
            var failMsg = ReadString(payJson, "errorMessage") ?? ReadString(payJson, "message") ?? "POS pay failed";
            return BuildResult(request, requestId, "Failed", payJson, failMsg);
        }

        var deadline = DateTime.UtcNow.AddSeconds(Math.Clamp(settings.StatusTimeoutSeconds, 30, 600));
        var pollMs = Math.Clamp(settings.StatusPollIntervalMs, 500, 10000);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(pollMs, ct);

            var statusBody = new
            {
                username = settings.Username.Trim(),
                appKey = settings.AppKey.Trim(),
                origP2pRequestId = requestId,
            };

            var statusJson = await PostJsonAsync(CombineUrl(settings.ApiBaseUrl, "status"), statusBody, ct);
            var status = ReadStatus(statusJson);

            if (status is "SUCCESS" or "APPROVED" or "COMPLETED")
                return BuildResult(request, requestId, "Success", statusJson);

            if (status is "FAILED" or "DECLINED" or "CANCELLED" or "CANCELED" or "VOID")
            {
                var msg = ReadString(statusJson, "errorMessage") ?? ReadString(statusJson, "message") ?? status;
                return BuildResult(request, requestId, "Failed", statusJson, msg);
            }
        }

        try
        {
            var cancelBody = new
            {
                username = settings.Username.Trim(),
                appKey = settings.AppKey.Trim(),
                origP2pRequestId = requestId,
                pushTo = new { deviceId = RazorpayPosSettingsDocument.NormalizeDeviceId(settings.DeviceId) },
            };
            await PostJsonAsync(CombineUrl(settings.ApiBaseUrl, "cancel"), cancelBody, CancellationToken.None);
        }
        catch
        {
            // ignore cancel errors on timeout
        }

        throw new TimeoutException($"POS payment timed out after {settings.StatusTimeoutSeconds}s (request {requestId}).");
    }

    private static void ValidateSettings(RazorpayPosSettingsDocument settings)
    {
        if (!settings.Enabled)
            throw new InvalidOperationException("Razorpay POS is disabled in Settings.");
        if (string.IsNullOrWhiteSpace(settings.Username))
            throw new InvalidOperationException("Razorpay POS username is not configured (Settings → Other → Razorpay POS).");
        if (string.IsNullOrWhiteSpace(settings.AppKey))
            throw new InvalidOperationException("Razorpay POS app key is not configured.");
        if (string.IsNullOrWhiteSpace(settings.DeviceId))
            throw new InvalidOperationException("Razorpay POS device ID is not configured.");
        if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
            throw new InvalidOperationException("Razorpay POS API base URL is not configured.");
    }

    private static string MapMode(RazorpayPosPayMode mode) => mode switch
    {
        RazorpayPosPayMode.Card => "CARD",
        RazorpayPosPayMode.Upi => "UPI",
        _ => "ALL",
    };

    private async Task<JsonDocument> PostJsonAsync(string url, object body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body, JsonOpts);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(url, content, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"POS API HTTP {(int)response.StatusCode}: {responseText}");

        try
        {
            return JsonDocument.Parse(responseText);
        }
        catch
        {
            throw new InvalidOperationException($"POS API returned invalid JSON: {responseText}");
        }
    }

    private static string CombineUrl(string baseUrl, string action)
    {
        var b = baseUrl.Trim();
        if (!b.EndsWith('/'))
            b += "/";
        return b + action.TrimStart('/');
    }

    private static string? ReadString(JsonDocument doc, string name)
    {
        if (!doc.RootElement.TryGetProperty(name, out var el))
        {
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                    return prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
            }
            return null;
        }

        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    private static string ReadStatus(JsonDocument doc)
    {
        var status = ReadString(doc, "status")
            ?? ReadString(doc, "txnStatus")
            ?? ReadString(doc, "paymentStatus")
            ?? "";
        return status.Trim().ToUpperInvariant();
    }

    private static bool IsImmediateFailure(JsonDocument doc)
    {
        var success = ReadString(doc, "success");
        if (string.Equals(success, "false", StringComparison.OrdinalIgnoreCase))
            return true;
        var status = ReadStatus(doc);
        return status is "FAILED" or "DECLINED";
    }

    private static PaymentResult BuildResult(
        PaymentRequest request,
        string requestId,
        string status,
        JsonDocument raw,
        string? errorNote = null)
    {
        var rawJson = raw.RootElement.GetRawText();
        if (!string.IsNullOrWhiteSpace(errorNote))
        {
            rawJson = JsonSerializer.Serialize(new
            {
                note = errorNote,
                response = JsonSerializer.Deserialize<object>(rawJson),
            });
        }

        return new PaymentResult(
            Provider: PaymentProviderKind.Razorpay,
            ProviderReference: $"RZP-POS-{requestId}",
            Status: status,
            RawResponseJson: rawJson);
    }
}
