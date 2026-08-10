using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.Services.Sync;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

public sealed class StoreSyncRunnerTests
{
    [Fact]
    public async Task RunFullStoreSyncAsync_runs_direct_operations_in_central_online_mode()
    {
        var engine = new RecordingSyncEngine();
        var online = new RecordingOnlineRunner();
        var runner = CreateRunner(engine, isOnlineMode: true, online);

        var result = await runner.RunFullStoreSyncAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("Online refresh", result.Message);
        Assert.Equal(0, engine.RunOnceCalls);
        Assert.Equal(1, online.RunCalls);
        Assert.True(online.LastIncludeStoreWideTransfers);
    }

    [Fact]
    public async Task RunFullStoreSyncAsync_can_skip_store_wide_online_operations()
    {
        var online = new RecordingOnlineRunner();
        var runner = CreateRunner(new RecordingSyncEngine(), isOnlineMode: true, online);

        await runner.RunFullStoreSyncAsync(
            CancellationToken.None,
            includeStoreWideOnlineOperations: false);

        Assert.False(online.LastIncludeStoreWideTransfers);
    }

    [Fact]
    public async Task RunFullStoreSyncAsync_runs_engine_in_local_mode()
    {
        var engine = new RecordingSyncEngine();
        var runner = CreateRunner(engine, isOnlineMode: false);

        var result = await runner.RunFullStoreSyncAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, engine.RunOnceCalls);
    }

    private static StoreSyncRunner CreateRunner(
        RecordingSyncEngine engine,
        bool isOnlineMode,
        IOnlineDirectOperationsRunner? online = null) =>
        new(
            engine,
            new CentralAuthSession(),
            new HttpClient(),
            receiptConfigSync: null!,
            isOnlineMode: () => isOnlineMode,
            onlineDirectOperations: online);

    private sealed class RecordingOnlineRunner : IOnlineDirectOperationsRunner
    {
        public int RunCalls { get; private set; }
        public bool LastIncludeStoreWideTransfers { get; private set; }

        public Task<OnlineDirectOperationsResult> RunAsync(
            bool includeStoreWideTransfers,
            CancellationToken ct = default)
        {
            RunCalls++;
            LastIncludeStoreWideTransfers = includeStoreWideTransfers;
            return Task.FromResult(new OnlineDirectOperationsResult(
                0,
                22,
                0,
                System.Array.Empty<string>()));
        }
    }

    private sealed class RecordingSyncEngine : ISyncEngine
    {
        public int RunOnceCalls { get; private set; }

        public Task<SyncStatus> GetStatusAsync(CancellationToken ct) =>
            Task.FromResult(new SyncStatus(0, "0", null, "0", null));

        public Task RunOnceAsync(CancellationToken ct)
        {
            RunOnceCalls++;
            return Task.CompletedTask;
        }

        public Task PushPendingAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ResetProductCursorAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<int> ResyncAllProductsAsync(CancellationToken ct) => Task.FromResult(0);
    }
}
