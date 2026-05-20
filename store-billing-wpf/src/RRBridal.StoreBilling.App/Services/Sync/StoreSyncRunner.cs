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
    private readonly ShellBrandingService? _shellBranding;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public StoreSyncRunner(
        ISyncEngine syncEngine,
        CentralAuthSession authSession,
        HttpClient centralApi,
        ReceiptConfigSyncService receiptConfigSync,
        ShellBrandingService? shellBranding = null)
    {
        _syncEngine = syncEngine;
        _authSession = authSession;
        _centralApi = centralApi;
        _receiptConfigSync = receiptConfigSync;
        _shellBranding = shellBranding;
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

            if (string.IsNullOrEmpty(_authSession.AccessToken))
                return SyncRunResult.Ok("Sync complete. Log in to Central (Settings) for company master and printer.");

            var receiptNote = _receiptConfigSync.IsProfileReadyForPrint()
                ? "Company master and printer saved."
                : "Company master not saved — check Central login and backend seed.";

            if (_shellBranding != null)
            {
                try
                {
                    await _shellBranding.RefreshAsync(ct).ConfigureAwait(false);
                }
                catch { /* best-effort */ }
            }

            return SyncRunResult.Ok($"Sync complete. {receiptNote}");
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
