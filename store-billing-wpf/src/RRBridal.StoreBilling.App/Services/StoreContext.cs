using System;

namespace RRBridal.StoreBilling.App.Services;

/// <summary>
/// Centralised read-only holder for the current store and till identity.
/// Initialised once at app startup from environment variables (.env).
/// </summary>
public sealed class StoreContext
{
    private const string DefaultStoreId = "store-001";
    private const string DefaultDeviceId = "device-001";

    public string StoreId { get; }
    public string DeviceId { get; }
    public string PosCounter { get; }

    /// <summary>Manager till (POS 1) — full nav and store-wide dashboard.</summary>
    public bool IsPrimaryCounter { get; }

    /// <summary>Human-readable till label for UI (store / POS / device).</summary>
    public string DisplayLabel { get; }

    public bool UsesDefaultDeviceId { get; }

    public StoreContext()
    {
        StoreId = Environment.GetEnvironmentVariable("STORE_ID")?.Trim() ?? DefaultStoreId;
        DeviceId = Environment.GetEnvironmentVariable("DEVICE_ID")?.Trim() ?? DefaultDeviceId;
        PosCounter = Environment.GetEnvironmentVariable("POS_COUNTER")?.Trim() ?? "1";
        IsPrimaryCounter = string.Equals(PosCounter, "1", StringComparison.Ordinal);
        UsesDefaultDeviceId = string.Equals(DeviceId, DefaultDeviceId, StringComparison.OrdinalIgnoreCase);
        DisplayLabel = $"{StoreId} / POS{PosCounter} / {DeviceId}";
    }
}
