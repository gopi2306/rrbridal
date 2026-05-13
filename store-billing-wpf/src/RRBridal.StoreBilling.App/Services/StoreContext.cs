using System;

namespace RRBridal.StoreBilling.App.Services;

/// <summary>
/// Centralised read-only holder for the current store identity.
/// Initialised once at app startup from the STORE_ID environment variable.
/// Every service and ViewModel should reference this instead of reading the env var directly.
/// </summary>
public sealed class StoreContext
{
    public string StoreId { get; }
    public string DeviceId { get; }

    public StoreContext()
    {
        StoreId = Environment.GetEnvironmentVariable("STORE_ID") ?? "store-001";
        DeviceId = Environment.GetEnvironmentVariable("DEVICE_ID") ?? "device-001";
    }
}
