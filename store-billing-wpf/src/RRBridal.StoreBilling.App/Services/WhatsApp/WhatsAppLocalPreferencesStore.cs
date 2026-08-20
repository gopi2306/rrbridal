using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Invoicing;

namespace RRBridal.StoreBilling.App.Services.WhatsApp;

public sealed class WhatsAppLocalPreferencesDocument
{
    public bool AutoSendAfterPost { get; set; } = true;

    /// <summary>Central attachment channel mirrored for display: image | document.</summary>
    public string AttachmentFormat { get; set; } = "image";

    /// <summary>Local invoice layout for WhatsApp PDF: Thermal | A4 | A5 | A4Commercial | A4TaxInvoice.</summary>
    public string InvoiceFormat { get; set; } = "";
}

public sealed class WhatsAppLocalPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly string _tempFilePath;
    private WhatsAppLocalPreferencesDocument _current = new();

    public WhatsAppLocalPreferencesStore()
    {
        var dir = StoreBillingDataPath.Get();
        _filePath = Path.Combine(dir, "whatsapp_settings.json");
        _tempFilePath = _filePath + ".tmp";
        Load();
    }

    public WhatsAppLocalPreferencesDocument Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    public void Load()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    _current = new WhatsAppLocalPreferencesDocument();
                    return;
                }

                var json = File.ReadAllText(_filePath);
                _current = string.IsNullOrWhiteSpace(json)
                    ? new WhatsAppLocalPreferencesDocument()
                    : JsonSerializer.Deserialize<WhatsAppLocalPreferencesDocument>(json, JsonOpts) ?? new WhatsAppLocalPreferencesDocument();
            }
            catch
            {
                _current = new WhatsAppLocalPreferencesDocument();
            }
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        string json;
        lock (_lock)
        {
            json = JsonSerializer.Serialize(_current, JsonOpts);
        }

        await File.WriteAllTextAsync(_tempFilePath, json, ct).ConfigureAwait(false);
        lock (_lock)
        {
            if (File.Exists(_filePath))
                File.Replace(_tempFilePath, _filePath, null);
            else
                File.Move(_tempFilePath, _filePath);
        }
    }

    public void Update(Action<WhatsAppLocalPreferencesDocument> mutate)
    {
        lock (_lock)
        {
            mutate(_current);
        }
    }

    /// <summary>
    /// Resolves local WhatsApp invoice layout. Empty/invalid values fall back to <paramref name="seedFromPrint"/>.
    /// </summary>
    public InvoicePrintFormat ResolveInvoiceFormat(InvoicePrintFormat seedFromPrint = InvoicePrintFormat.Thermal)
    {
        lock (_lock)
        {
            if (TryParseInvoiceFormat(_current.InvoiceFormat, out var parsed))
                return parsed;

            _current.InvoiceFormat = seedFromPrint.ToString();
            return seedFromPrint;
        }
    }

    public static bool TryParseInvoiceFormat(string? value, out InvoicePrintFormat format)
    {
        format = InvoicePrintFormat.Thermal;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return Enum.TryParse(value.Trim(), ignoreCase: true, out format)
               && Enum.IsDefined(typeof(InvoicePrintFormat), format);
    }
}
