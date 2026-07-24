using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Ui;

public sealed class ShellUiSettingsStore
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
    private ShellUiSettingsDocument _current = new();

    public event Action? Changed;

    public ShellUiSettingsStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RRBridal", "StoreBilling");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "shell_ui_settings.json");
        _tempFilePath = _filePath + ".tmp";
        Load();
    }

    public ShellUiSettingsDocument Current
    {
        get
        {
            lock (_lock)
                return _current;
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
                    _current = new ShellUiSettingsDocument();
                    return;
                }

                var json = File.ReadAllText(_filePath);
                _current = string.IsNullOrWhiteSpace(json)
                    ? new ShellUiSettingsDocument()
                    : JsonSerializer.Deserialize<ShellUiSettingsDocument>(json, JsonOpts) ?? new ShellUiSettingsDocument();
            }
            catch
            {
                _current = new ShellUiSettingsDocument();
            }
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        string json;
        lock (_lock)
            json = JsonSerializer.Serialize(_current, JsonOpts);

        await File.WriteAllTextAsync(_tempFilePath, json, ct).ConfigureAwait(false);
        lock (_lock)
        {
            if (File.Exists(_filePath))
                File.Replace(_tempFilePath, _filePath, null);
            else
                File.Move(_tempFilePath, _filePath);
        }

        Changed?.Invoke();
    }

    public void Update(Action<ShellUiSettingsDocument> mutate)
    {
        lock (_lock)
            mutate(_current);
    }
}
