using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Sync;

public interface ISyncEngine
{
    Task<SyncStatus> GetStatusAsync(CancellationToken ct);
    Task RunOnceAsync(CancellationToken ct);
}

public sealed record SyncStatus(int PendingOutbox, string LastCursor, string? LastError);

