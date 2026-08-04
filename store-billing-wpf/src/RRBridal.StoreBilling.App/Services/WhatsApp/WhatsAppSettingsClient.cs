using System;
using System.Globalization;
using System.Linq;
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
    public string PromoTemplateName { get; init; } = "";
    public string PromoTemplateLanguage { get; init; } = "en";
    public string PromoHeaderType { get; init; } = "none";
    public bool PromoHasUrlButton { get; init; }
    public bool PromoConfigured { get; init; }

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
            PromoTemplateName = ReadString(root, "promoTemplateName"),
            PromoTemplateLanguage = ReadString(root, "promoTemplateLanguage", "en"),
            PromoHeaderType = ReadString(root, "promoHeaderType", "none"),
            PromoHasUrlButton = root.TryGetProperty("promoHasUrlButton", out var pub) && pub.ValueKind == JsonValueKind.True,
            PromoConfigured = root.TryGetProperty("promoConfigured", out var pc) && pc.ValueKind == JsonValueKind.True,
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
        string attachmentMimeType = "image/png",
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
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(attachmentMimeType) ? "image/png" : attachmentMimeType.Trim());
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
        string attachmentMimeType = "image/png",
        CancellationToken ct = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(storeId.Trim()), "storeId");
            form.Add(new StringContent(customerPhone.Trim()), "customerPhone");
            form.Add(new StringContent(customerName ?? "Test Customer"), "customerName");

            var fileContent = new ByteArrayContent(attachment);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(attachmentMimeType) ? "image/png" : attachmentMimeType.Trim());
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

    public async Task<(string? JobId, string? Error)> StartBroadcastAsync(
        string storeId,
        string mode,
        string promoText,
        IReadOnlyList<(string Name, string Phone)> recipients,
        byte[]? attachment = null,
        string? attachmentFileName = null,
        string? attachmentMimeType = null,
        string? offerDate = null,
        string? urlButtonSuffix = null,
        string? offerScope = null,
        CancellationToken ct = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(storeId.Trim()), "storeId");
            form.Add(new StringContent(string.IsNullOrWhiteSpace(mode) ? "template" : mode.Trim()), "mode");
            form.Add(new StringContent(promoText ?? ""), "promoText");
            if (!string.IsNullOrWhiteSpace(offerScope))
                form.Add(new StringContent(offerScope.Trim()), "offerScope");
            if (!string.IsNullOrWhiteSpace(offerDate))
                form.Add(new StringContent(offerDate.Trim()), "offerDate");
            if (!string.IsNullOrWhiteSpace(urlButtonSuffix))
                form.Add(new StringContent(urlButtonSuffix.Trim()), "urlButtonSuffix");

            var recipientsJson = JsonSerializer.Serialize(
                recipients.Select(r => new { name = r.Name, phone = r.Phone }).ToList());
            form.Add(new StringContent(recipientsJson), "recipientsJson");

            if (attachment is { Length: > 0 })
            {
                var fileContent = new ByteArrayContent(attachment);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                    string.IsNullOrWhiteSpace(attachmentMimeType) ? "application/octet-stream" : attachmentMimeType.Trim());
                form.Add(fileContent, "attachment", string.IsNullOrWhiteSpace(attachmentFileName) ? "promo.bin" : attachmentFileName);
            }

            using var res = await _http.PostAsync("/api/whatsapp/broadcast", form, ct);
            var (root, err) = await Api.CentralApiJson.ReadClonedRootAsync(res, "Central login required.", ct);
            if (err != null)
                return (null, err);
            if (!root.HasValue)
                return (null, "Empty broadcast response.");

            var jobId = root.Value.TryGetProperty("jobId", out var jid) && jid.ValueKind == JsonValueKind.String
                ? jid.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(jobId))
                return (null, "Broadcast returned no job id.");
            return (jobId, null);
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

    public async Task<(WhatsAppBroadcastJobSnapshot? Data, string? Error)> GetBroadcastJobAsync(
        string jobId,
        CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.GetAsync($"/api/whatsapp/broadcast/{Uri.EscapeDataString(jobId.Trim())}", ct);
            var (root, err) = await Api.CentralApiJson.ReadClonedRootAsync(res, "Central login required.", ct);
            if (err != null)
                return (null, err);
            if (!root.HasValue)
                return (null, "Empty broadcast job response.");
            return (WhatsAppBroadcastJobSnapshot.FromJson(root.Value), null);
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

public sealed class WhatsAppBroadcastJobSnapshot
{
    public string JobId { get; init; } = "";
    public string Status { get; init; } = "";
    public int Total { get; init; }
    public int Sent { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
    public string? Error { get; init; }
    public string? FirstFailureReason { get; init; }
    public string? FirstRecipientSummary { get; init; }

    public static WhatsAppBroadcastJobSnapshot FromJson(JsonElement root)
    {
        string? firstFail = null;
        string? firstSummary = null;
        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in results.EnumerateArray())
            {
                var status = row.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String
                    ? st.GetString()
                    : null;
                var name = row.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                    ? n.GetString()
                    : "";
                var phone = row.TryGetProperty("phoneE164", out var pe) && pe.ValueKind == JsonValueKind.String
                    ? pe.GetString()
                    : (row.TryGetProperty("phone", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : "");
                var messageId = row.TryGetProperty("messageId", out var mid) && mid.ValueKind == JsonValueKind.String
                    ? mid.GetString()
                    : null;

                if (firstSummary == null && !string.IsNullOrWhiteSpace(phone))
                {
                    firstSummary = $"To: {name} +{phone}" +
                                   (string.IsNullOrWhiteSpace(messageId) ? "" : $"\nMeta messageId: {messageId}");
                }

                if (!string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (row.TryGetProperty("error", out var er) && er.ValueKind == JsonValueKind.String)
                {
                    firstFail = er.GetString();
                    if (!string.IsNullOrWhiteSpace(firstFail))
                        break;
                }
            }
        }

        return new WhatsAppBroadcastJobSnapshot
        {
            JobId = ReadString(root, "jobId"),
            Status = ReadString(root, "status"),
            Total = ReadInt(root, "total"),
            Sent = ReadInt(root, "sent"),
            Failed = ReadInt(root, "failed"),
            Skipped = ReadInt(root, "skipped"),
            Error = root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String
                ? e.GetString()
                : null,
            FirstFailureReason = firstFail,
            FirstRecipientSummary = firstSummary,
        };
    }

    private static string ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String)
            return "";
        return v.GetString()?.Trim() ?? "";
    }

    private static int ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var v))
            return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n))
            return n;
        return 0;
    }
}
