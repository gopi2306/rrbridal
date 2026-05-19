using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Sync;

public sealed class PeriodicSyncService : IDisposable
{
    private readonly StoreContext _storeContext;
    private readonly SyncScheduleOptions _schedule;
    private readonly StoreSyncRunner _syncRunner;
    private readonly ShellBrandingService? _shellBranding;
    private readonly IMongoCollection<BsonDocument> _syncState;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private bool _disposed;

    public PeriodicSyncService(
        StoreContext storeContext,
        SyncScheduleOptions schedule,
        StoreSyncRunner syncRunner,
        IMongoDatabase localDb,
        ShellBrandingService? shellBranding = null)
    {
        _storeContext = storeContext;
        _schedule = schedule;
        _syncRunner = syncRunner;
        _shellBranding = shellBranding;
        _syncState = localDb.GetCollection<BsonDocument>("sync_state");
    }

    public event Action? StatusChanged;

    public int IntervalMinutes => _schedule.IntervalMinutes;

    public bool IsScheduleEnabled => _schedule.Enabled;

    public bool IsActive => _storeContext.IsPrimaryCounter && IsScheduleEnabled;

    public DateTime? LastRunUtc { get; private set; }

    public string? LastRunMessage { get; private set; }

    public bool? LastRunSucceeded { get; private set; }

    public string StatusDescription
    {
        get
        {
            if (!_storeContext.IsPrimaryCounter)
                return "Auto-sync: not available on this till (runs on counter 1 / POS 1)";

            if (!IsScheduleEnabled)
                return "Auto-sync: off (SYNC_INTERVAL_MINUTES=0)";

            var interval = IntervalMinutes == 1 ? "1 min" : $"{IntervalMinutes} min";
            var baseText = $"Auto-sync: every {interval} (counter 1 only)";
            if (LastRunUtc is null)
                return baseText + " — starting…";

            var local = LastRunUtc.Value.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
            var outcome = LastRunSucceeded == true ? "OK" : "failed";
            return $"{baseText} · last: {local} ({outcome})";
        }
    }

    public void Start()
    {
        if (!IsActive || _loopTask is { IsCompleted: false })
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

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await RunTickAsync().ConfigureAwait(false);

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(IntervalMinutes), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunTickAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(Math.Max(IntervalMinutes * 2, 5)));
            var result = await _syncRunner.RunFullStoreSyncAsync(cts.Token, skipIfBusy: true).ConfigureAwait(false);
            if (result.SkippedBecauseBusy)
                return;

            LastRunUtc = DateTime.UtcNow;
            LastRunMessage = result.Message;
            LastRunSucceeded = result.Succeeded;

            await SaveStatusToMongoAsync().ConfigureAwait(false);

            if (result.Succeeded && _shellBranding != null)
            {
                try
                {
                    await _shellBranding.RefreshAsync(cts.Token).ConfigureAwait(false);
                }
                catch { /* best-effort */ }
            }
        }
        catch (Exception ex)
        {
            LastRunUtc = DateTime.UtcNow;
            LastRunMessage = ex.Message;
            LastRunSucceeded = false;
            await SaveStatusToMongoAsync().ConfigureAwait(false);
        }
        finally
        {
            RaiseStatusChanged();
        }
    }

    private async Task SaveStatusToMongoAsync()
    {
        try
        {
            var update = Builders<BsonDocument>.Update
                .Set("lastAutoSyncAt", LastRunUtc?.ToString("o") ?? "")
                .Set("lastAutoSyncMessage", LastRunMessage ?? "")
                .Set("lastAutoSyncOk", LastRunSucceeded == true);

            await _syncState.UpdateOneAsync(
                FilterDefinition<BsonDocument>.Empty,
                update,
                new UpdateOptions { IsUpsert = true }).ConfigureAwait(false);
        }
        catch { /* best-effort */ }
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
