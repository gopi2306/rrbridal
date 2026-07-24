using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using System.Windows.Threading;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services;

public enum MongoHealthState
{
    Unknown,
    Connected,
    Reconnecting,
    Offline,
}

public sealed class MongoHealthMonitor : IDisposable
{
    private readonly IMongoDatabase _db;
    private readonly StoreMongoOptions _options;
    private readonly object _gate = new();
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private bool _disposed;

    public MongoHealthMonitor(IMongoDatabase db, StoreMongoOptions options)
    {
        _db = db;
        _options = options;
        HostDisplay = options.HostDisplay;
    }

    public string HostDisplay { get; }

    public MongoHealthState State { get; private set; } = MongoHealthState.Unknown;

    public string? LastError { get; private set; }

    public DateTime? LastCheckedUtc { get; private set; }

    public bool IsOnline => State == MongoHealthState.Connected;

    public string StatusDescription
    {
        get
        {
            var host = HostDisplay;
            return State switch
            {
                MongoHealthState.Connected => $"Mongo: Connected ({host})",
                MongoHealthState.Reconnecting => $"Mongo: Reconnecting… ({host})",
                MongoHealthState.Offline => $"Mongo: Offline ({host})",
                _ => $"Mongo: Checking… ({host})",
            };
        }
    }

    public string OfflineUserMessage =>
        "Store MongoDB is unreachable (ZeroTier / parent Mongo).\n\n" +
        $"Host: {HostDisplay}\n\n" +
        "Check ZeroTier is connected, parent Mongo is running, and STORE_MONGO_URI is correct.";

    public event Action? StatusChanged;

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

        try
        {
            _loopCts.Cancel();
        }
        catch { /* ignore */ }

        _loopCts.Dispose();
        _loopCts = null;
        _loopTask = null;
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.RunCommandAsync((Command<BsonDocument>)"{ping:1}", cancellationToken: ct)
                .ConfigureAwait(false);
            SetState(MongoHealthState.Connected, null);
            return true;
        }
        catch (Exception ex)
        {
            SetState(MongoHealthState.Offline, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Blocks until Mongo responds or the user chooses Exit.
    /// Returns false if the user exits.
    /// </summary>
    public async Task<bool> WaitUntilReadyAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            SetState(MongoHealthState.Reconnecting, LastError);
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            attemptCts.CancelAfter(_options.ServerSelectionTimeout);

            if (await PingAsync(attemptCts.Token).ConfigureAwait(true))
                return true;

            var retry = await PromptRetryOrExitAsync().ConfigureAwait(true);
            if (!retry)
                return false;
        }

        return false;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (State != MongoHealthState.Connected)
                SetState(MongoHealthState.Reconnecting, LastError);

            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            attemptCts.CancelAfter(_options.ServerSelectionTimeout);
            await PingAsync(attemptCts.Token).ConfigureAwait(false);

            try
            {
                await Task.Delay(_options.HealthCheckInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void SetState(MongoHealthState state, string? error)
    {
        lock (_gate)
        {
            State = state;
            LastError = error;
            LastCheckedUtc = DateTime.UtcNow;
        }

        RaiseStatusChanged();
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

    private Task<bool> PromptRetryOrExitAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            tcs.SetResult(false);
            return tcs.Task;
        }

        dispatcher.BeginInvoke(() =>
        {
            var result = AppDialog.Show(
                OfflineUserMessage + "\n\nYes = Retry connection\nNo = Exit the app",
                "RR Bridal Billing — MongoDB unreachable",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            tcs.SetResult(result == MessageBoxResult.Yes);
        });

        return tcs.Task;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
    }
}
