using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Products;

public sealed class ProductImageCache
{
    private readonly HttpClient _centralApi;
    private readonly string _cacheDir;

    public ProductImageCache(HttpClient centralApi)
    {
        _centralApi = centralApi;
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RRBridal",
            "ProductImages");
        Directory.CreateDirectory(_cacheDir);
    }

    public string GetLocalPath(string productId) => Path.Combine(_cacheDir, $"{productId}.img");

    public async Task<string?> EnsureDownloadedAsync(string productId, CancellationToken ct)
    {
        var path = GetLocalPath(productId);
        if (File.Exists(path)) return path;

        var res = await _centralApi.GetAsync($"/api/media/products/{Uri.EscapeDataString(productId)}/image", ct);
        if (!res.IsSuccessStatusCode) return null;

        var bytes = await res.Content.ReadAsByteArrayAsync(ct);
        await File.WriteAllBytesAsync(path, bytes, ct);
        return path;
    }
}

