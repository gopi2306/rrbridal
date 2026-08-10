using System;
using System.IO;

namespace RRBridal.StoreBilling.App.Services;

/// <summary>
/// Resolves the per-user application data directory. Automated tests can set
/// RRBRIDAL_DATA_DIR to keep credentials and settings isolated from real tills.
/// </summary>
public static class StoreBillingDataPath
{
    public const string OverrideEnvironmentVariable = "RRBRIDAL_DATA_DIR";

    public static string Get()
    {
        var overridePath = Environment.GetEnvironmentVariable(OverrideEnvironmentVariable);
        var path = string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RRBridal",
                "StoreBilling")
            : Path.GetFullPath(overridePath.Trim());

        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetProductImages()
    {
        var overridePath = Environment.GetEnvironmentVariable(OverrideEnvironmentVariable);
        var path = string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RRBridal",
                "ProductImages")
            : Path.Combine(Path.GetFullPath(overridePath.Trim()), "ProductImages");

        Directory.CreateDirectory(path);
        return path;
    }
}
