using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Payments;

public sealed class RazorpayPosSettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private RazorpayPosSettingsDocument _current = new();

    public RazorpayPosSettingsStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RRBridal", "StoreBilling");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "razorpay_pos_settings.json");
        Load();
    }

    public RazorpayPosSettingsDocument Current
    {
        get
        {
            _lock.Wait();
            try { return _current; }
            finally { _lock.Release(); }
        }
    }

    public void Load()
    {
        _lock.Wait();
        try
        {
            if (!File.Exists(_filePath))
            {
                _current = new RazorpayPosSettingsDocument();
                return;
            }

            var json = File.ReadAllText(_filePath);
            _current = string.IsNullOrWhiteSpace(json)
                ? new RazorpayPosSettingsDocument()
                : JsonSerializer.Deserialize<RazorpayPosSettingsDocument>(json, JsonOpts) ?? new RazorpayPosSettingsDocument();
        }
        catch
        {
            _current = new RazorpayPosSettingsDocument();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(_current, JsonOpts);
            await File.WriteAllTextAsync(_filePath, json);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Update(Action<RazorpayPosSettingsDocument> mutate)
    {
        _lock.Wait();
        try
        {
            mutate(_current);
        }
        finally
        {
            _lock.Release();
        }
    }
}
