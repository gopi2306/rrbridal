using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.Services.Invoicing;

namespace RRBridal.StoreBilling.App.Services.Sync;

public sealed class StoreSyncRunner
{
    private readonly ISyncEngine _syncEngine;
    private readonly CentralAuthSession _authSession;
    private readonly HttpClient _centralApi;
    private readonly ReceiptConfigSyncService _receiptConfigSync;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public StoreSyncRunner(
        ISyncEngine syncEngine,
        CentralAuthSession authSession,
        HttpClient centralApi,
        ReceiptConfigSyncService receiptConfigSync)
    {
        _syncEngine = syncEngine;
        _authSession = authSession;
        _centralApi = centralApi;
        _receiptConfigSync = receiptConfigSync;
    }

    public SemaphoreSlim SyncLock => _syncLock;

    public async Task<SyncRunResult> RunFullStoreSyncAsync(CancellationToken ct, bool skipIfBusy = false)
    {
        if (skipIfBusy)
        {
            if (!await _syncLock.WaitAsync(0, ct).ConfigureAwait(false))
                return SyncRunResult.Skipped();
        }
        else
        {
            await _syncLock.WaitAsync(ct).ConfigureAwait(false);
        }

        try
        {
            _authSession.ApplyTo(_centralApi);
            await _syncEngine.RunOnceAsync(ct).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(_authSession.AccessToken))
            {
                var (ok, msg) = await _receiptConfigSync.EnsureProfileReadyForPrintAsync(ct).ConfigureAwait(false);
                return ok
                    ? SyncRunResult.Ok($"Sync complete. {msg}")
                    : SyncRunResult.Ok($"Sync finished; receipt pull failed: {msg}");
            }

            return SyncRunResult.Ok("Sync complete. Log in to Central to pull receipt header.");
        }
        catch (Exception ex)
        {
            return SyncRunResult.Failed($"Sync failed: {ex.Message}");
        }
        finally
        {
            _syncLock.Release();
        }
    }
}
