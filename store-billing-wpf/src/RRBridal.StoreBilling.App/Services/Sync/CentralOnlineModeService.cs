using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Sync;

public sealed class CentralOnlineModeService : IDisposable
{
    private readonly PosBillingSettingsStore _settings;
    private readonly HttpClient _centralApi;
    private readonly StoreSyncRunner _syncRunner;
    private readonly object _gate = new();
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private bool _disposed;

    public CentralOnlineModeService(
        PosBillingSettingsStore settings,
        HttpClient centralApi,
        StoreSyncRunner syncRunner)
    {
        _settings = settings;
        _centralApi = centralApi;
        _syncRunner = syncRunner;
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

    public async Task SetOnlineModeAsync(bool enabled, CancellationToken ct = default)
    {
        var previous = IsOnlineMode;
        _settings.Update(s => s.PreferCentralOnline = enabled);
        await _settings.SaveAsync(ct).ConfigureAwait(false);

        if (!enabled)
        {
            IsCentralReachable = false;
            RaiseStatusChanged();
            return;
        }

        IsCentralReachable = await ProbeHealthAsync(ct).ConfigureAwait(false);
        RaiseStatusChanged();

        if (!previous && enabled)
            await _syncRunner.RunFullStoreSyncAsync(ct).ConfigureAwait(false);
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
