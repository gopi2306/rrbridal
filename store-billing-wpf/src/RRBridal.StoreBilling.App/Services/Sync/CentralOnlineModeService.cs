using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using RRBridal.StoreBilling.App.Services.Api;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Invoicing;

namespace RRBridal.StoreBilling.App.Services.Sync;

public sealed class CentralOnlineModeService : IDisposable
{
    private readonly PosBillingSettingsStore _settings;
    private readonly HttpClient _centralApi;
    private readonly StoreSyncRunner _syncRunner;
    private readonly StoreInfoClient _storeInfo;
    private readonly ReceiptConfigStore _receiptConfig;
    private readonly ReceiptConfigSyncService _receiptSync;
    private readonly string _storeId;
    private readonly object _gate = new();
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private bool _disposed;

    public CentralOnlineModeService(
        PosBillingSettingsStore settings,
        HttpClient centralApi,
        StoreSyncRunner syncRunner,
        StoreInfoClient storeInfo,
        ReceiptConfigStore receiptConfig,
        ReceiptConfigSyncService receiptSync,
        string storeId)
    {
        _settings = settings;
        _centralApi = centralApi;
        _syncRunner = syncRunner;
        _storeInfo = storeInfo;
        _receiptConfig = receiptConfig;
        _receiptSync = receiptSync;
        _storeId = storeId?.Trim() ?? "";
    }

    public event Action? StatusChanged;

    public bool IsOnlineMode => _settings.Current.PreferCentralOnline;

    public bool IsCentralReachable { get; private set; }

    public string StatusChipText =>
        !IsOnlineMode
            ? "Central: Offline"
            : IsCentralReachable
                ? "Central: Online"
                : "Central: Unreachable";

    public void Start()
    {
        if (_disposed || _loopTask is { IsCompleted: false })
            return;

        Stop();
        _loopCts = new CancellationTokenSource();
        _loopTask = RunLoopAsync(_loopCts.Token);
    }

    public void Stop()
    {
        if (_loopCts == null)
            return;

        try { _loopCts.Cancel(); } catch { /* ignore */ }
        _loopCts.Dispose();
        _loopCts = null;
        _loopTask = null;
    }

    /// <summary>
    /// Pull store-wide Online flag from central (public endpoint) and apply locally.
    /// Only promotes to Online when central says true — never demotes a local Online till
    /// (turning Offline is Settings → Save or .env PREFER_CENTRAL_ONLINE=false).
    /// .env override always wins and skips central inherit.
    /// </summary>
    public async Task SyncFromCentralStoreAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_storeId))
            return;

        var (mode, _) = await _storeInfo.GetStorePosModeAsync(_storeId, ct).ConfigureAwait(false);
        if (mode is null)
            return;

        var changed = false;
        var wasOnline = IsOnlineMode;
        var centralWantsOnline =
            mode.BillingSettings?.PreferCentralOnline ?? mode.PreferCentralOnline;
        var allowPromotion = true;

        if (!_settings.IsEnvOnlineOverride && !wasOnline && centralWantsOnline)
        {
            var flush = await _syncRunner.RunFullStoreSyncAsync(ct).ConfigureAwait(false);
            allowPromotion = flush.Succeeded;
            if (!allowPromotion)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Central Online promotion deferred until offline sync succeeds: {flush.Message}");
            }
        }

        if (mode.BillingSettings != null)
        {
            _settings.ApplyFromCentral(mode.BillingSettings);
            changed = true;
        }
        else if (mode.ScreenAccess != null)
        {
            // Legacy: only screen-access mirror present.
            _settings.Update(s => s.ScreenAccess = mode.ScreenAccess);
            changed = true;
        }

        // .env wins. Central may promote only after a successful final offline sync;
        // it never silently demotes a till that is already Online.
        if (!_settings.IsEnvOnlineOverride)
        {
            var resolvedOnline = wasOnline || (centralWantsOnline && allowPromotion);
            if (IsOnlineMode != resolvedOnline)
                changed = true;
            _settings.Update(s => s.PreferCentralOnline = resolvedOnline);
        }

        if (!string.IsNullOrWhiteSpace(mode.ReceiptPrintSettingsJson))
        {
            try
            {
                using var receiptDoc = JsonDocument.Parse(mode.ReceiptPrintSettingsJson);
                _receiptSync.ApplyStorePrintSettings(receiptDoc.RootElement);
                _receiptConfig.Current.LastReceiptSettingsSyncUtc = DateTime.UtcNow;
                await _receiptConfig.SaveAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                /* best-effort print inherit */
            }
        }

        if (!changed)
        {
            if (IsOnlineMode)
                IsCentralReachable = await ProbeHealthAsync(ct).ConfigureAwait(false);
            RaiseStatusChanged();
            return;
        }

        await _settings.SaveAsync(ct).ConfigureAwait(false);
        _settings.ReapplyEnvOnlineModeOverride();

        if (IsOnlineMode)
            IsCentralReachable = await ProbeHealthAsync(ct).ConfigureAwait(false);

        RaiseStatusChanged();
    }

    public async Task SetOnlineModeAsync(bool enabled, CancellationToken ct = default)
    {
        if (_settings.IsEnvOnlineOverride)
        {
            // .env is source of truth — keep PreferCentralOnline aligned and skip UI flip.
            _settings.ReapplyEnvOnlineModeOverride();
            if (IsOnlineMode)
                IsCentralReachable = await ProbeHealthAsync(ct).ConfigureAwait(false);
            else
                IsCentralReachable = false;
            RaiseStatusChanged();
            return;
        }

        var previous = IsOnlineMode;
        if (!previous && enabled)
        {
            var flush = await _syncRunner.RunFullStoreSyncAsync(ct).ConfigureAwait(false);
            if (!flush.Succeeded)
                throw new InvalidOperationException(
                    $"Cannot enable Central Online until pending offline work is synced. {flush.Message}");
        }

        _settings.Update(s => s.PreferCentralOnline = enabled);
        await _settings.SaveAsync(ct).ConfigureAwait(false);

        // Propagate store-wide so other counters inherit (requires central JWT when available).
        if (!string.IsNullOrWhiteSpace(_storeId))
        {
            var (ok, err) = await _storeInfo
                .SetPreferCentralOnlineAsync(_storeId, enabled, ct)
                .ConfigureAwait(false);
            if (!ok && !string.IsNullOrWhiteSpace(err))
            {
                // Local mode still applied; counters pick up after central auth + next save/poll.
                System.Diagnostics.Debug.WriteLine($"SetPreferCentralOnline failed: {err}");
            }
        }

        if (!enabled)
        {
            IsCentralReachable = false;
            RaiseStatusChanged();
            return;
        }

        IsCentralReachable = await ProbeHealthAsync(ct).ConfigureAwait(false);
        RaiseStatusChanged();

        if (!previous && enabled)
        {
            var refresh = await _syncRunner.RunFullStoreSyncAsync(ct).ConfigureAwait(false);
            if (!refresh.Succeeded)
                System.Diagnostics.Debug.WriteLine($"Initial online refresh incomplete: {refresh.Message}");
        }
    }

    public async Task<bool> ProbeHealthAsync(CancellationToken ct = default)
    {
        if (!IsOnlineMode)
            return false;

        try
        {
            using var res = await _centralApi.GetAsync("/api/sync/health", ct).ConfigureAwait(false);
            IsCentralReachable = res.IsSuccessStatusCode;
            return IsCentralReachable;
        }
        catch
        {
            IsCentralReachable = false;
            return false;
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SyncFromCentralStoreAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                /* best-effort inherit */
            }

            if (IsOnlineMode)
                await ProbeHealthAsync(ct).ConfigureAwait(false);
            else
                IsCentralReachable = false;

            RaiseStatusChanged();

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void RaiseStatusChanged()
    {
        var handler = StatusChanged;
        if (handler == null)
            return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            handler();
            return;
        }

        dispatcher.BeginInvoke(DispatcherPriority.Background, handler);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
    }
}
