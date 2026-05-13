using System;
using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class StoreAnalyticsSnapshot
{
    public required string PeriodLabel { get; init; }

    public int TotalBillsInPeriod { get; init; }

    public decimal TotalRevenueInPeriod { get; init; }

    public IReadOnlyList<DailySalesRow> DailyRows { get; init; } = Array.Empty<DailySalesRow>();
}

public sealed class DailySalesRow
{
    public DateTime DayUtc { get; init; }

    public required string DayLabel { get; init; }

    public int BillsCount { get; init; }

    public decimal Revenue { get; init; }
}
