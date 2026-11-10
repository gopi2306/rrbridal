using System.Threading;
using System.Threading.Tasks;
using RRBridal.StoreBilling.App.Services.Api;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Customers;

public sealed class GstinLookupService
{
    private readonly GstinLookupClient _client;
    private readonly PosBillingSettingsStore _settings;
    private readonly CentralOnlineModeService _centralMode;

    public GstinLookupService(
        GstinLookupClient client,
        PosBillingSettingsStore settings,
        CentralOnlineModeService centralMode)
    {
        _client = client;
        _settings = settings;
        _centralMode = centralMode;
    }

    public bool IsEnabled => _settings.Current.EnableGstinLookup;

    public static string? Guard(bool enabled, bool online, string? gstin)
    {
        if (!enabled)
            return "GSTIN lookup is turned off in Settings.";
        if (!online)
            return "GSTIN lookup needs Central Online mode.";
        return GstinLookupMapper.ValidateForLookup(gstin);
    }

    public async Task<(GstinLookupDetails? Data, string? Error)> LookupAsync(
        string? gstin,
        CancellationToken ct = default)
    {
        var blocked = Guard(IsEnabled, _centralMode.IsOnlineMode, gstin);
        if (blocked != null)
            return (null, blocked);

        return await _client.LookupAsync(GstinLookupMapper.Normalize(gstin), ct);
    }
}
