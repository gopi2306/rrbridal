using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RRBridal.StoreBilling.App.Services.Api;
using RRBridal.StoreBilling.App.Services.Auth;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public sealed class ReceiptConfigSyncService
{
    private readonly CompanyProfileClient _companyProfile;
    private readonly StoreReceiptSettingsClient _storeReceipt;
    private readonly ReceiptConfigStore _config;
    private readonly ReceiptLogoCache _logoCache;
    private readonly StoreContext _storeContext;
    private readonly CentralAuthSession _authSession;
    private readonly HttpClient _centralHttp;

    public ReceiptConfigSyncService(
        CompanyProfileClient companyProfile,
        StoreReceiptSettingsClient storeReceipt,
        ReceiptConfigStore config,
        ReceiptLogoCache logoCache,
        StoreContext storeContext,
        CentralAuthSession authSession,
        HttpClient centralHttp)
    {
        _companyProfile = companyProfile;
        _storeReceipt = storeReceipt;
        _config = config;
        _logoCache = logoCache;
        _storeContext = storeContext;
        _authSession = authSession;
        _centralHttp = centralHttp;
    }

    public bool IsProfileReadyForPrint()
    {
        var c = _config.Current;
        if (!c.LastReceiptSettingsSyncUtc.HasValue)
            return false;
        return !LooksLikeUnsyncedDefault(c.Store);
    }

    public async Task<(bool Ok, string Message)> EnsureProfileReadyForPrintAsync(CancellationToken ct = default)
    {
        if (IsProfileReadyForPrint())
            return (true, $"Receipt profile ready: {_config.Current.Store.StoreName}");

        _config.Reload();
        if (IsProfileReadyForPrint())
            return (true, $"Receipt profile ready: {_config.Current.Store.StoreName}");

        return (false,
            "Receipt header not synced. Open Settings, log in to Central, then use Run sync once.");
    }

    /// <summary>
    /// Pull company master + printer from central and save to receipt_config.json.
    /// Only call from store sync (Run sync once, periodic sync, notifications Sync now).
    /// </summary>
    public async Task<(bool Ok, string Message)> SyncReceiptFromCentralOnStoreSyncAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_authSession.AccessToken))
            return (false, "Central login required for company master sync.");

        _authSession.ApplyTo(_centralHttp);
        try
        {
            return await SyncFromCentralAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static bool LooksLikeUnsyncedDefault(StoreProfile store) =>
        string.Equals(store.StoreName, "RR Bridal", StringComparison.OrdinalIgnoreCase)
        && string.IsNullOrWhiteSpace(store.Gstin);

    public async Task<(bool Ok, string Message)> SyncFromCentralAsync(CancellationToken ct = default)
    {
        var (profile, profileErr) = await _companyProfile.GetAsync(ct);
        if (profile == null)
            return (false, profileErr ?? "Could not load company profile from central.");

        var tradeName = GetString(profile.Value, "tradeName");
        var legalName = GetString(profile.Value, "legalName");
        if (string.IsNullOrWhiteSpace(tradeName) && string.IsNullOrWhiteSpace(legalName))
        {
            return (false,
                "Central company profile is empty — run 'npm run seed' on central-backend (database rr_bridal_central).");
        }

        ApplyCompanyProfile(profile.Value);

        var storeCode = _storeContext.StoreId;
        if (!string.IsNullOrWhiteSpace(storeCode))
        {
            var (printSettings, printErr) = await _storeReceipt.GetReceiptSettingsAsync(storeCode, ct);
            if (printSettings != null)
                ApplyStorePrintSettings(printSettings.Value);
            else if (!string.IsNullOrWhiteSpace(printErr))
                return (false, $"Company profile read OK, but printer settings failed: {printErr}");
        }

        _config.Current.LastReceiptSettingsSyncUtc = DateTime.UtcNow;
        await _config.SaveAsync(ct);

        if (!string.IsNullOrWhiteSpace(_config.Current.Store.LogoUrl))
            await _logoCache.TryLoadFromUrlAsync(_config.Current.Store.LogoUrl, ct);

        var name = _config.Current.Store.StoreName;
        return (true, $"Synced: {name} → receipt_config.json");
    }

    private void ApplyCompanyProfile(JsonElement profile)
    {
        var store = _config.Current.Store;
        var trade = GetString(profile, "tradeName");
        var legal = GetString(profile, "legalName");
        if (!string.IsNullOrWhiteSpace(trade))
            store.StoreName = trade;
        else if (!string.IsNullOrWhiteSpace(legal))
            store.StoreName = legal;

        var address = BuildAddress(profile);
        if (!string.IsNullOrWhiteSpace(address))
            store.Address = address;

        store.Gstin = GetString(profile, "gstin") ?? store.Gstin;
        store.StateName = GetString(profile, "state") ?? store.StateName;
        store.CustomerCarePhone = GetString(profile, "phone") ?? store.CustomerCarePhone;

        var logo = GetString(profile, "companyLogo");
        if (!string.IsNullOrWhiteSpace(logo))
            store.LogoUrl = ResolveCentralMediaUrl(logo);

        store.FssaiNo = GetString(profile, "fssaiNo") ?? GetExtraString(profile, "fssaiNo") ?? store.FssaiNo;
        store.BranchCode = GetExtraString(profile, "branchCode")
            ?? GetString(profile, "branchCode")
            ?? store.BranchCode;
        store.Website = GetString(profile, "website") ?? GetExtraString(profile, "website") ?? store.Website;
        store.TermsAndConditions = GetString(profile, "termsAndConditions")
            ?? GetExtraString(profile, "termsAndConditions")
            ?? store.TermsAndConditions;
        store.ThankYouLine = GetString(profile, "thankYouLine")
            ?? GetExtraString(profile, "thankYouLine")
            ?? store.ThankYouLine;

        var policyLines = ParsePolicyLines(profile);
        if (policyLines.Count > 0)
            store.PolicyLines = policyLines;

        var qrSlots = ParseQrSlots(profile);
        if (qrSlots.Count > 0)
            store.QrSlots = qrSlots;
        if (TryGetProperty(profile, "receiptBarcodeEnabled", out var bce)
            && (bce.ValueKind == JsonValueKind.True || bce.ValueKind == JsonValueKind.False))
            store.ShowBillBarcode = bce.GetBoolean();
        else if (TryGetProperty(profile, "extraFields", out var ex2)
            && TryGetProperty(ex2, "receiptBarcodeEnabled", out var ebc)
            && (ebc.ValueKind == JsonValueKind.True || ebc.ValueKind == JsonValueKind.False))
            store.ShowBillBarcode = ebc.GetBoolean();
    }

    private void ApplyStorePrintSettings(JsonElement settings)
    {
        var print = _config.Current.Print;
        if (TryGetProperty(settings, "receiptCharWidth", out var w) && w.TryGetInt32(out var cw) && cw is >= 32 and <= 56)
            print.ReceiptCharWidth = cw;
        if (TryGetProperty(settings, "alwaysUsePrintDialog", out var aud)
            && (aud.ValueKind == JsonValueKind.True || aud.ValueKind == JsonValueKind.False))
            print.AlwaysUsePrintDialog = aud.GetBoolean();

        var queueHint = GetString(settings, "billPrinterQueueName");
        var modelHint = GetString(settings, "printerModel");
        if (!string.IsNullOrWhiteSpace(queueHint))
            print.CentralPrinterHint = queueHint;
        if (!string.IsNullOrWhiteSpace(modelHint))
            print.CentralPrinterModel = modelHint;

        var resolved = PrinterQueueResolver.ResolveFullName(queueHint, modelHint);
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            print.BillPrinterFullName = resolved;
            if (string.IsNullOrWhiteSpace(print.ThermalPrinterFullName))
                print.ThermalPrinterFullName = resolved;
            if (string.IsNullOrWhiteSpace(print.OfficeInvoicePrinterFullName))
                print.OfficeInvoicePrinterFullName = resolved;
        }
    }

    private static List<string> ParsePolicyLines(JsonElement profile)
    {
        if (TryGetProperty(profile, "policyLines", out var pl) && pl.ValueKind == JsonValueKind.Array)
        {
            return pl.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => s.Length > 0)
                .ToList();
        }

        if (TryGetProperty(profile, "extraFields", out var extra)
            && TryGetProperty(extra, "policyLines", out var epl)
            && epl.ValueKind == JsonValueKind.Array)
        {
            return epl.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => s.Length > 0)
                .ToList();
        }

        return new List<string>();
    }

    private static List<ReceiptQrSlotConfig> ParseQrSlots(JsonElement profile)
    {
        JsonElement? arr = null;
        if (TryGetProperty(profile, "receiptQrSlots", out var slots) && slots.ValueKind == JsonValueKind.Array)
            arr = slots;
        else if (TryGetProperty(profile, "extraFields", out var extra)
            && TryGetProperty(extra, "receiptQrSlots", out var es)
            && es.ValueKind == JsonValueKind.Array)
            arr = es;

        if (arr == null)
            return new List<ReceiptQrSlotConfig>();

        return arr.Value.EnumerateArray()
            .Take(3)
            .Select(e => new ReceiptQrSlotConfig
            {
                Label = TryGetProperty(e, "label", out var l) ? l.GetString() ?? "" : "",
                Payload = TryGetProperty(e, "payload", out var p) ? p.GetString() ?? "" : "",
            })
            .Where(s => !string.IsNullOrWhiteSpace(s.Payload))
            .ToList();
    }

    private static string BuildAddress(JsonElement profile)
    {
        var parts = new List<string>();
        var address = GetString(profile, "address");
        if (!string.IsNullOrWhiteSpace(address)) parts.Add(address);
        var city = GetString(profile, "city");
        var state = GetString(profile, "state");
        var pin = GetString(profile, "pinCode");
        var cityLine = string.Join(", ", new[] { city, state }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(cityLine)) parts.Add(cityLine);
        if (!string.IsNullOrWhiteSpace(pin)) parts.Add(pin);
        return string.Join(Environment.NewLine, parts);
    }

    private static bool TryGetProperty(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value))
            return true;

        if (el.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var prop in el.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (!TryGetProperty(el, name, out var p) || p.ValueKind != JsonValueKind.String)
            return null;
        var s = p.GetString()?.Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static string? GetExtraString(JsonElement profile, string key)
    {
        if (!TryGetProperty(profile, "extraFields", out var extra))
            return null;
        return GetString(extra, key);
    }

    /// <summary>
    /// Turns upload paths like /media/files/logo/... into a full central API URL (includes /api).
    /// </summary>
    private string? ResolveCentralMediaUrl(string? logo)
    {
        if (string.IsNullOrWhiteSpace(logo))
            return null;

        var trimmed = logo.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        var baseUri = _centralHttp.BaseAddress;
        if (baseUri == null)
            return trimmed;

        if (trimmed.StartsWith("/media/files/", StringComparison.OrdinalIgnoreCase))
            trimmed = "/api" + trimmed;
        else if (trimmed.StartsWith("media/files/", StringComparison.OrdinalIgnoreCase))
            trimmed = "/api/" + trimmed;

        if (trimmed.StartsWith('/'))
            return new Uri(baseUri, trimmed).ToString();

        return new Uri(baseUri, trimmed).ToString();
    }
}
