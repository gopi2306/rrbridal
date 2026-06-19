using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.WhatsApp;

public sealed class WhatsAppSettingsSnapshot
{
    public bool Enabled { get; init; }
    public bool Configured { get; init; }
    public string TemplateName { get; init; } = "";
    public string TemplateLanguage { get; init; } = "en";
    public string DefaultCountryCode { get; init; } = "91";
    public string AttachmentType { get; init; } = "image";
    public string? AccessTokenMasked { get; init; }

    public static WhatsAppSettingsSnapshot FromJson(JsonElement root)
    {
        return new WhatsAppSettingsSnapshot
        {
            Enabled = root.TryGetProperty("enabled", out var en) && en.ValueKind == JsonValueKind.True,
            Configured = root.TryGetProperty("configured", out var cfg) && cfg.ValueKind == JsonValueKind.True,
            TemplateName = ReadString(root, "templateName"),
            TemplateLanguage = ReadString(root, "templateLanguage", "en"),
            DefaultCountryCode = ReadString(root, "defaultCountryCode", "91"),
            AttachmentType = ReadString(root, "attachmentType", "image"),
            AccessTokenMasked = ReadOptionalString(root, "accessTokenMasked"),
        };
    }

    private static string ReadString(JsonElement root, string name, string fallback = "")
    {
        if (!root.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String)
            return fallback;
        return v.GetString()?.Trim() ?? fallback;
    }

    private static string? ReadOptionalString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String)
            return null;
        var s = v.GetString()?.Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }
}

public sealed class WhatsAppSendResult
{
    public required string MessageId { get; init; }
    public required string PhoneE164 { get; init; }
}

public sealed class WhatsAppSettingsClient
{
    private readonly HttpClient _http;

    public WhatsAppSettingsClient(HttpClient http) => _http = http;

    public async Task<(WhatsAppSettingsSnapshot? Data, string? Error)> GetSettingsAsync(
        string storeId,
        CancellationToken ct = default)
    {
        try
        {
            var code = Uri.EscapeDataString(storeId.Trim().ToLowerInvariant());
            using var res = await _http.GetAsync($"/api/whatsapp/settings?storeId={code}", ct);
            var (root, err) = await Api.CentralApiJson.ReadClonedRootAsync(res, "Central login required.", ct);
            if (err != null)
                return (null, err);
            return root.HasValue ? (WhatsAppSettingsSnapshot.FromJson(root.Value), null) : (null, "Empty settings response.");
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

    public async Task<(WhatsAppSendResult? Data, string? Error)> SendInvoiceAsync(
        string storeId,
        string billNo,
        string customerName,
        string customerPhone,
        decimal payable,
        byte[] attachment,
        string attachmentFileName,
        CancellationToken ct = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(storeId.Trim()), "storeId");
            form.Add(new StringContent(billNo.Trim()), "billNo");
            form.Add(new StringContent(customerName ?? ""), "customerName");
            form.Add(new StringContent(customerPhone.Trim()), "customerPhone");
            form.Add(new StringContent(payable.ToString(CultureInfo.InvariantCulture)), "payable");

            var fileContent = new ByteArrayContent(attachment);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(fileContent, "attachment", attachmentFileName);

            using var res = await _http.PostAsync("/api/whatsapp/send-invoice", form, ct);
            var (root, err) = await Api.CentralApiJson.ReadClonedRootAsync(res, "Central login required.", ct);
            if (err != null)
                return (null, err);

            if (!root.HasValue)
                return (null, "Empty send response.");

            var messageId = root.Value.TryGetProperty("messageId", out var mid) && mid.ValueKind == JsonValueKind.String
                ? mid.GetString() ?? ""
                : "";
            var phone = root.Value.TryGetProperty("phoneE164", out var ph) && ph.ValueKind == JsonValueKind.String
                ? ph.GetString() ?? ""
                : "";
            if (string.IsNullOrEmpty(messageId))
                return (null, "WhatsApp send returned no message id.");

            return (new WhatsAppSendResult { MessageId = messageId, PhoneE164 = phone }, null);
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

    public async Task<(WhatsAppSendResult? Data, string? Error)> SendTestAsync(
        string storeId,
        string customerPhone,
        string customerName,
        byte[] attachment,
        string attachmentFileName,
        CancellationToken ct = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(storeId.Trim()), "storeId");
            form.Add(new StringContent(customerPhone.Trim()), "customerPhone");
            form.Add(new StringContent(customerName ?? "Test Customer"), "customerName");

            var fileContent = new ByteArrayContent(attachment);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(fileContent, "attachment", attachmentFileName);

            using var res = await _http.PostAsync("/api/whatsapp/test", form, ct);
            var (root, err) = await Api.CentralApiJson.ReadClonedRootAsync(res, "Central login required.", ct);
            if (err != null)
                return (null, err);

            if (!root.HasValue)
                return (null, "Empty test send response.");

            var messageId = root.Value.TryGetProperty("messageId", out var mid) && mid.ValueKind == JsonValueKind.String
                ? mid.GetString() ?? ""
                : "";
            var phone = root.Value.TryGetProperty("phoneE164", out var ph) && ph.ValueKind == JsonValueKind.String
                ? ph.GetString() ?? ""
                : "";
            return (new WhatsAppSendResult { MessageId = messageId, PhoneE164 = phone }, null);
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
}
