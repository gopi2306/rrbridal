using System;
using System.Globalization;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class BillMarginRow
{
    public required string BillNo { get; init; }
    public string BillDate { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string SalesmanCode { get; init; } = "";
    public string SalesmanName { get; init; } = "";
    public string CounterDisplay { get; init; } = "";
    public required string PostedAtLocal { get; init; }
    public decimal TotalQty { get; init; }
    public decimal CostPrice { get; init; }
    public decimal SellingPrice { get; init; }
    public decimal Discount { get; init; }
    public decimal MarginAmount { get; init; }
    public decimal MarginPercent { get; init; }
    public bool HasReturn { get; init; }
    public string ReturnNo { get; init; } = "";
    public bool HasAdjustment { get; init; }
    public string AdjustmentNo { get; init; } = "";
    public DateTime SortUtc { get; init; }

    public string SalesmanDisplay => !string.IsNullOrWhiteSpace(SalesmanCode)
        ? $"{SalesmanCode} — {SalesmanName}"
        : SalesmanName;

    public string CostPriceFormatted => MoneyMath.FormatRupee(CostPrice);
    public string SellingPriceFormatted => MoneyMath.FormatRupee(SellingPrice);
    public string DiscountFormatted => MoneyMath.FormatRupee(Discount);
    public string MarginAmountFormatted => MoneyMath.FormatRupee(MarginAmount);
    public string MarginPercentFormatted =>
        MarginPercent.ToString("N2", CultureInfo.GetCultureInfo("en-IN")) + "%";
    public string ReturnedDisplay => HasReturn ? "Yes" : "—";
    public string AdjustmentDisplay => HasAdjustment ? "Yes" : "—";
}

public sealed class BillMarginSnapshot
{
    public IReadOnlyList<BillMarginRow> Rows { get; init; } = Array.Empty<BillMarginRow>();
    public int TotalMatched { get; init; }
    public bool WasTruncated { get; init; }
    public decimal TotalCost { get; init; }
    public decimal TotalSelling { get; init; }
    public decimal TotalDiscount { get; init; }
    public decimal TotalMargin { get; init; }
    public decimal TotalMarginPercent { get; init; }
}

public sealed class BillMarginQuery
{
    public string? InvoiceNo { get; init; }
    public DateTime? BusinessDate { get; init; }
    public bool UseDateRange { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public string? PosCounterFilter { get; init; }
    public string? SalesmanGroupKey { get; init; }
    public string? SalesmanCode { get; init; }
    public int Limit { get; init; } = 500;
}
