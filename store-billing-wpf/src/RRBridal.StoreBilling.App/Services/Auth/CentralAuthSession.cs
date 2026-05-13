using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Auth;

public sealed class CentralAuthSession
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly string _filePath;

    public CentralAuthSession()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RRBridal", "StoreBilling");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "central_auth.json");
    }

    public string? AccessToken { get; private set; }

    public void SetToken(string? token)
    {
        AccessToken = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }

    public void ApplyTo(HttpClient http)
    {
        http.DefaultRequestHeaders.Remove("Authorization");
        if (!string.IsNullOrEmpty(AccessToken))
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        }
    }

    public void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                AccessToken = null;
                return;
            }

            using var stream = File.OpenRead(_filePath);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("accessToken", out var t))
            {
                AccessToken = t.GetString();
            }
        }
        catch
        {
            AccessToken = null;
        }
    }

    public async Task SaveToDiskAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(AccessToken))
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    File.Delete(_filePath);
                }
                catch
                {
                    // ignore
                }
            }

            return;
        }

        var json = JsonSerializer.Serialize(new { accessToken = AccessToken }, JsonOpts);
        await File.WriteAllTextAsync(_filePath, json, ct);
    }

    public void ClearDiskAndToken()
    {
        AccessToken = null;
        if (!File.Exists(_filePath)) return;
        try
        {
            File.Delete(_filePath);
        }
        catch
        {
            // ignore
        }
    }
}
