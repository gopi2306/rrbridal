using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Customers;
using RRBridal.StoreBilling.App.Services.Invoicing;

namespace RRBridal.StoreBilling.App.Services.WhatsApp;

public enum WhatsAppDeliveryStatus
{
    Sent,
    Failed,
    Skipped,
}

public sealed class WhatsAppSendOutcome
{
    public required WhatsAppDeliveryStatus Status { get; init; }
    public string? MessageId { get; init; }
    public string? PhoneE164 { get; init; }
    public string? Error { get; init; }
}

public sealed class WhatsAppBillService
{
    private readonly IMongoCollection<BsonDocument> _bills;
    private readonly StoreContext _store;
    private readonly WhatsAppSettingsClient _client;
    private readonly WhatsAppLocalPreferencesStore _prefs;
    private readonly Func<AppServices> _getServices;

    public WhatsAppBillService(
        IMongoDatabase localDb,
        StoreContext store,
        WhatsAppSettingsClient client,
        WhatsAppLocalPreferencesStore prefs,
        Func<AppServices> getServices)
    {
        _bills = localDb.GetCollection<BsonDocument>("store_bills");
        _store = store;
        _client = client;
        _prefs = prefs;
        _getServices = getServices;
    }

    public bool ShouldAutoSendAfterPost => _prefs.Current.AutoSendAfterPost;

    public async Task<(WhatsAppSettingsSnapshot? Data, string? Error)> LoadCentralSettingsAsync(CancellationToken ct = default)
    {
        var services = _getServices();
        services.CentralAuthSession.ApplyTo(services.CentralApi);
        return await _client.GetSettingsAsync(_store.StoreId, ct);
    }

    public async Task<WhatsAppSendOutcome> TrySendBillAsync(
        string billNo,
        ThermalInvoiceInput input,
        string? customerPhone = null,
        bool force = false,
        CancellationToken ct = default)
    {
        var phone = customerPhone ?? input.CustomerPhone;
        var services = _getServices();
        services.CentralAuthSession.ApplyTo(services.CentralApi);

        var (settings, settingsErr) = await _client.GetSettingsAsync(_store.StoreId, ct);
        if (settings == null)
            return await PersistOutcomeAsync(billNo, WhatsAppDeliveryStatus.Failed, phone, settings, null, settingsErr ?? "Could not load WhatsApp settings.", ct);

        if (!settings.Enabled || !settings.Configured)
        {
            if (!force)
                return await PersistOutcomeAsync(billNo, WhatsAppDeliveryStatus.Skipped, phone, settings, null, "WhatsApp not enabled or configured.", ct);
            return new WhatsAppSendOutcome
            {
                Status = WhatsAppDeliveryStatus.Skipped,
                Error = "WhatsApp not enabled or configured.",
            };
        }

        if (!PhoneE164Helper.CanSendWhatsApp(phone, settings.DefaultCountryCode))
            return await PersistOutcomeAsync(billNo, WhatsAppDeliveryStatus.Skipped, phone, settings, null, "Customer phone missing or invalid.", ct);

        try
        {
            var (png, _, fileName) = await InvoiceAttachmentExporter.ExportThermalPngAsync(services, input, ct);
            var (result, sendErr) = await _client.SendInvoiceAsync(
                _store.StoreId,
                billNo,
                input.CustomerName,
                phone,
                input.Payable,
                png,
                fileName,
                ct);

            if (result == null)
                return await PersistOutcomeAsync(billNo, WhatsAppDeliveryStatus.Failed, phone, settings, null, sendErr ?? "WhatsApp send failed.", ct);

            return await PersistOutcomeAsync(billNo, WhatsAppDeliveryStatus.Sent, phone, settings, result, null, ct);
        }
        catch (Exception ex)
        {
            return await PersistOutcomeAsync(billNo, WhatsAppDeliveryStatus.Failed, phone, settings, null, ex.Message, ct);
        }
    }

    private async Task<WhatsAppSendOutcome> PersistOutcomeAsync(
        string billNo,
        WhatsAppDeliveryStatus status,
        string? phone,
        WhatsAppSettingsSnapshot? settings,
        WhatsAppSendResult? result,
        string? error,
        CancellationToken ct)
    {
        var phoneE164 = result?.PhoneE164
            ?? (settings != null ? PhoneE164Helper.ToWhatsAppE164(phone, settings.DefaultCountryCode) : PhoneE164Helper.ToWhatsAppE164(phone));

        var whatsapp = new BsonDocument
        {
            { "status", status.ToString().ToLowerInvariant() },
            { "sentAtUtc", DateTime.UtcNow.ToString("O") },
            { "phone", phoneE164 },
        };
        if (!string.IsNullOrWhiteSpace(result?.MessageId))
            whatsapp["messageId"] = result.MessageId;
        if (!string.IsNullOrWhiteSpace(error))
            whatsapp["error"] = error.Trim();

        if (!string.IsNullOrWhiteSpace(billNo))
        {
            await _bills.UpdateOneAsync(
                Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
                    Builders<BsonDocument>.Filter.Eq("billNo", billNo.Trim())),
                Builders<BsonDocument>.Update.Set("whatsapp", whatsapp),
                cancellationToken: ct);
        }

        return new WhatsAppSendOutcome
        {
            Status = status,
            MessageId = result?.MessageId,
            PhoneE164 = phoneE164,
            Error = error,
        };
    }
}
