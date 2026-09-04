using System;
using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class SkuSalesReportQuery
{
    public required DateTime From { get; init; }
    public required DateTime To { get; init; }
    public string? StoreCode { get; init; }
    public string? StoreName { get; init; }
    public string? Search { get; init; }
    public string? PosCounter { get; init; }
    public int Limit { get; init; } = 10_000;
}

public sealed class SkuSalesReportPeriod
{
    public string From { get; init; } = "";
    public string To { get; init; } = "";
    public string Timezone { get; init; } = "";
    public string StoreCode { get; init; } = "";
    public string StoreName { get; init; } = "";
    public string? PosCounter { get; init; }
}

public sealed class SkuSalesReportFilters
{
    public string? Search { get; init; }
}

public sealed class SkuSalesRow
{
    public int Rank { get; init; }
    public string Sku { get; init; } = "";
    public string Description { get; init; } = "";
    public string SupplierId { get; init; } = "";
    public string SupplierName { get; init; } = "";
    public decimal SoldQty { get; init; }
    public decimal ReturnQty { get; init; }
    public decimal NetQty { get; init; }
    public decimal SoldAmount { get; init; }
    public decimal ReturnAmount { get; init; }
    public decimal NetAmount { get; init; }
    /// <summary>Current store available qty (Fast Sellers enrichment).</summary>
    public decimal AvailableQty { get; init; }
    /// <summary>True when AvailableQty is at or below Settings GoingOutOfStockMatchQty.</summary>
    public bool IsLowStock { get; init; }
    public string LowStockDisplay => IsLowStock ? "Yes" : "No";
}

public sealed class SkuSalesTotals
{
    public int SkuCount { get; set; }
    public int SupplierCount { get; set; }
    public decimal SoldQty { get; set; }
    public decimal ReturnQty { get; set; }
    public decimal NetQty { get; set; }
    public decimal SoldAmount { get; set; }
    public decimal ReturnAmount { get; set; }
    public decimal NetAmount { get; set; }
}

public sealed class SupplierWiseSupplierRow
{
    public string SupplierId { get; init; } = "";
    public string SupplierName { get; init; } = "";
    public int ProductCount { get; init; }
    public decimal SoldQty { get; init; }
    public decimal ReturnQty { get; init; }
    public decimal NetQty { get; init; }
    public decimal SoldAmount { get; init; }
    public decimal ReturnAmount { get; init; }
    public decimal NetAmount { get; init; }
    public IReadOnlyList<SkuSalesRow> Products { get; init; } = [];
}

public sealed class SupplierWiseReportResponse
{
    public SkuSalesReportPeriod Period { get; init; } = new();
    public SkuSalesReportFilters Filters { get; init; } = new();
    public int Limit { get; init; }
    public bool Truncated { get; init; }
    public int Total { get; init; }
    public SkuSalesTotals Totals { get; init; } = new();
    public IReadOnlyList<SupplierWiseSupplierRow> Data { get; init; } = [];
}

public sealed class FastSellersReportResponse
{
    public SkuSalesReportPeriod Period { get; init; } = new();
    public SkuSalesReportFilters Filters { get; init; } = new();
    public int Limit { get; init; }
    public bool Truncated { get; init; }
    public int Total { get; init; }
    public SkuSalesTotals Totals { get; init; } = new();
    public IReadOnlyList<SkuSalesRow> Data { get; init; } = [];
}
