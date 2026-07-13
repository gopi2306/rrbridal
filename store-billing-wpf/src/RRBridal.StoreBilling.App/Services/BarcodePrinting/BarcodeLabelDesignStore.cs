using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

public sealed class BarcodeLabelDesignStore
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
    private BarcodeLabelDesignDocument _current = new();
    private bool _hasLoadedFromDisk;

    public BarcodeLabelDesignStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RRBridal", "StoreBilling");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "barcode_label_design.json");
        _tempFilePath = _filePath + ".tmp";
        Load();
    }

    public string FilePath => _filePath;

    public BarcodeLabelDesignDocument Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    public BarcodeLabelDesignConfig ResolveDesign()
    {
        var design = Current.Design;
        if (design != null && !string.IsNullOrWhiteSpace(design.LayoutStyle))
            return design;
        return BarcodeLabelDesignDefaults.LegacyBrandPrice();
    }

    public void Load()
    {
        lock (_lock)
        {
            LoadCore();
        }
    }

    public void Reload() => Load();

    public async Task SaveAsync(BarcodeLabelDesignDocument document, CancellationToken ct = default)
    {
        document.LastSyncedAt = DateTime.UtcNow;
        string json;
        lock (_lock)
        {
            _current = document;
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

    private void LoadCore()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _current = new BarcodeLabelDesignDocument();
                _hasLoadedFromDisk = false;
                return;
            }

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                if (!_hasLoadedFromDisk)
                    _current = new BarcodeLabelDesignDocument();
                return;
            }

            var doc = JsonSerializer.Deserialize<BarcodeLabelDesignDocument>(json, JsonOpts);
            _current = doc ?? new BarcodeLabelDesignDocument();
            _hasLoadedFromDisk = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BarcodeLabelDesignStore.Load failed ({_filePath}): {ex.Message}");
            if (!_hasLoadedFromDisk)
                _current = new BarcodeLabelDesignDocument();
        }
    }
}
