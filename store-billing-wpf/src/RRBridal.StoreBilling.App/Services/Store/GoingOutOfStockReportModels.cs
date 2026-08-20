using System;
using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class GoingOutOfStockReportQuery
{
    public string? StoreCode { get; init; }
    public string? StoreName { get; init; }
    public string? Search { get; init; }
    public string? Status { get; init; }
    public int Limit { get; init; } = 10_000;
}

public sealed class GoingOutOfStockReportResponse
{
    public GoingOutOfStockReportPeriod Period { get; init; } = new();
    public GoingOutOfStockReportFilters Filters { get; init; } = new();
    public int Limit { get; init; }
    public bool Truncated { get; init; }
    public int Total { get; init; }
    public GoingOutOfStockTotals Totals { get; init; } = new();
    public IReadOnlyList<GoingOutOfStockRow> Data { get; init; } = [];
}

public sealed class GoingOutOfStockReportPeriod
{
    public string AsOf { get; init; } = "";
    public string Timezone { get; init; } = "";
    public string StoreCode { get; init; } = "";
    public string StoreName { get; init; } = "";
}

public sealed class GoingOutOfStockReportFilters
{
    public string? Search { get; init; }
    public string? Status { get; init; }
}

public sealed class GoingOutOfStockRow
{
    public string Sku { get; init; } = "";
    public string ProductName { get; init; } = "";
    public decimal StoreQty { get; init; }
    public decimal Threshold { get; init; }
    public string Status { get; init; } = "";
    public string SupplierId { get; init; } = "";
    public string SupplierName { get; init; } = "";
}

public sealed class GoingOutOfStockTotals
{
    public int SkuCount { get; set; }
    public int CriticalCount { get; set; }
    public int LowCount { get; set; }
    public int ZeroQtyCount { get; set; }
}
