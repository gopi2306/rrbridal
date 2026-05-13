using System;
using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class StoreDashboardSnapshot
{
    public required string StoreId { get; init; }

    public int BillsTodayCount { get; init; }

    public decimal BillsTodayRevenue { get; init; }

    public int BillsLast7DaysCount { get; init; }

    public decimal BillsLast7DaysRevenue { get; init; }

    public int PendingOutboxCount { get; init; }

    public string SyncCursor { get; init; } = "—";

    public string? SyncUpdatedAt { get; init; }

    public long ProductCacheCount { get; init; }

    public IReadOnlyList<DashboardRecentBill> RecentBills { get; init; } = Array.Empty<DashboardRecentBill>();
}

public sealed class DashboardRecentBill
{
    public required string BillNo { get; init; }

    public required string CreatedAtDisplay { get; init; }

    public decimal Payable { get; init; }
}
