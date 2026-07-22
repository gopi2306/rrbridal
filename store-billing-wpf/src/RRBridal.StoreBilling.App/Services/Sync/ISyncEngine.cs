using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Sync;

public interface ISyncEngine
{
    Task<SyncStatus> GetStatusAsync(CancellationToken ct);
    Task RunOnceAsync(CancellationToken ct);

    /// <summary>
    /// Sets the product pull cursor to <c>0</c> so the next sync re-pulls the full product catalog.
    /// Transfer / promotion / adjustment cursors are left unchanged.
    /// </summary>
    Task ResetProductCursorAsync(CancellationToken ct);

    /// <summary>
    /// Resets the product cursor and pulls all product pages until the central catalog is caught up.
    /// </summary>
    Task<int> ResyncAllProductsAsync(CancellationToken ct);
}

public sealed record SyncStatus(
    int PendingOutbox,
    string LastCursor,
    string? LastError,
    string LastTransferCursor,
    string? DiagnosticsSummary);

