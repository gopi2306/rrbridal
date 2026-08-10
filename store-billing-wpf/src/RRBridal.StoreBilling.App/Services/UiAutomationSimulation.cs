using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RRBridal.StoreBilling.App.Services;

/// <summary>
/// Explicitly gated seams for end-to-end UI automation. This must never activate
/// unless the test runner opts in through RRBRIDAL_UI_AUTOMATION.
/// </summary>
public static class UiAutomationSimulation
{
    public const string EnvironmentVariable = "RRBRIDAL_UI_AUTOMATION";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static bool IsEnabled
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(EnvironmentVariable)?.Trim();
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static string RecordJson(string category, string operation, string deterministicKey, object payload)
    {
        if (!IsEnabled)
            throw new InvalidOperationException("UI automation simulation is not enabled.");

        var directory = Path.Combine(StoreBillingDataPath.Get(), category);
        Directory.CreateDirectory(directory);

        var key = deterministicKey?.Trim() ?? "";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))
            .ToLowerInvariant()[..16];
        var fileName = $"{Slug(operation)}-{hash}.json";
        var path = Path.Combine(directory, fileName);
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            operation,
            deterministicKey = key,
            payload,
        }, JsonOptions);

        File.WriteAllText(path, json + Environment.NewLine, new UTF8Encoding(false));
        return path;
    }

    public static string StableId(string prefix, string deterministicKey)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(deterministicKey ?? "")))
            .ToLowerInvariant()[..16];
        return $"{prefix}-{hash}";
    }

    private static string Slug(string value)
    {
        var chars = (value ?? "").Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var slug = string.Join('-', new string(chars)
            .Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(slug) ? "record" : slug;
    }
}
