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

    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly string _tempFilePath;
    private ReceiptConfigDocument _current = new();
    private bool _hasLoadedFromDisk;

    public ReceiptConfigStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RRBridal", "StoreBilling");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "receipt_config.json");
        _tempFilePath = _filePath + ".tmp";
        Load();
    }

    public ReceiptConfigDocument Current
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
            LoadCore();
        }
    }

    public void Reload() => Load();

    private void LoadCore()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _current = new ReceiptConfigDocument();
                _hasLoadedFromDisk = false;
                return;
            }

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                if (!_hasLoadedFromDisk)
                    _current = new ReceiptConfigDocument();
                return;
            }

            var doc = JsonSerializer.Deserialize<ReceiptConfigDocument>(json, JsonOpts);
            if (doc == null)
            {
                if (!_hasLoadedFromDisk)
                    _current = new ReceiptConfigDocument();
                return;
            }

            if (doc.Store.PolicyLines == null)
                doc.Store.PolicyLines = new List<string>();

            if (doc.Print.A5PrePrintedLayout == null)
                doc.Print.A5PrePrintedLayout = A5PrePrintedLayoutSettings.CreateDefault();
            else
                doc.Print.A5PrePrintedLayout.EnsureAlignmentDefaults();

            _current = doc;
            _hasLoadedFromDisk = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ReceiptConfigStore.Load failed ({_filePath}): {ex.Message}");
            if (!_hasLoadedFromDisk)
                _current = new ReceiptConfigDocument();
        }
    }

    public string FilePath => _filePath;

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

            _hasLoadedFromDisk = true;
        }
    }
}
