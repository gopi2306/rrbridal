using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public sealed class ReceiptConfigStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;

    public ReceiptConfigStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RRBridal", "StoreBilling");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "receipt_config.json");
        Load();
    }

    public ReceiptConfigDocument Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                Current = new ReceiptConfigDocument();
                return;
            }

            var json = File.ReadAllText(_filePath);
            var doc = JsonSerializer.Deserialize<ReceiptConfigDocument>(json);
            Current = doc ?? new ReceiptConfigDocument();
            if (Current.Store.PolicyLines == null)
                Current.Store.PolicyLines = new List<string>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ReceiptConfigStore.Load failed ({_filePath}): {ex.Message}");
            // Keep previous Current if re-read fails — do not wipe synced data.
            if (Current.Store.StoreName == "RR Bridal" && string.IsNullOrWhiteSpace(Current.Store.Gstin))
                Current = new ReceiptConfigDocument();
        }
    }

    public string FilePath => _filePath;

    public async Task SaveAsync(CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(Current, JsonOpts);
        await File.WriteAllTextAsync(_filePath, json, ct);
    }

    public void Reload() => Load();
}
