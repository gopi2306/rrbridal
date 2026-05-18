using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public sealed class ReceiptLogoCache
{
    private readonly HttpClient _http;
    private readonly string _cachePath;

    public ReceiptLogoCache(HttpClient http)
    {
        _http = http;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RRBridal", "StoreBilling");
        Directory.CreateDirectory(dir);
        _cachePath = Path.Combine(dir, "receipt_logo.png");
    }

    public static ImageSource? TryLoadLocal(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;
        return LoadImageFromFile(filePath);
    }

    public async Task<ImageSource?> TryLoadFromUrlAsync(string? logoUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return File.Exists(_cachePath) ? LoadImageFromFile(_cachePath) : null;

        try
        {
            using var response = await _http.GetAsync(logoUrl, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var fs = File.Create(_cachePath);
            await stream.CopyToAsync(fs, ct);
        }
        catch
        {
            if (File.Exists(_cachePath))
                return LoadImageFromFile(_cachePath);
            return null;
        }

        return LoadImageFromFile(_cachePath);
    }

    private static ImageSource? LoadImageFromFile(string path)
    {
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.UriSource = new Uri(path, UriKind.Absolute);
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return null;
        }
    }
}
