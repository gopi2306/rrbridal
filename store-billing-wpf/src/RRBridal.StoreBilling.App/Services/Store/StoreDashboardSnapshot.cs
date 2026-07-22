using System;
using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class StoreDashboardSnapshot
{
    public required string StoreId { get; init; }

    public ReportScope Scope { get; init; } = ReportScope.ThisCounter;

    public int? StoreWideBillsTodayCount { get; init; }

    public decimal? StoreWideBillsTodayRevenue { get; init; }

    public int BillsTodayCount { get; init; }

    public decimal BillsTodayRevenue { get; init; }

    public int BillsLast7DaysCount { get; init; }

    public decimal BillsLast7DaysRevenue { get; init; }

    public long ProductCacheCount { get; init; }

    public decimal TotalAvailableQty { get; init; }

    public IReadOnlyList<DashboardRecentBill> RecentBills { get; init; } = Array.Empty<DashboardRecentBill>();
}

public sealed class DashboardRecentBill
{
    public required string BillNo { get; init; }

    public required string CreatedAtDisplay { get; init; }

    public decimal Payable { get; init; }

    public string CounterDisplay { get; init; } = "";
}
