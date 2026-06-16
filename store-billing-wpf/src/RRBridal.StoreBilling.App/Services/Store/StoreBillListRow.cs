using System;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class StoreBillListRow
{
    public required string BillNo { get; init; }
    public string BillDate { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public string CounterDisplay { get; init; } = "";
    public required string PostedAtLocal { get; init; }
    public decimal TotalQty { get; init; }
    public decimal Payable { get; init; }
    public decimal CashAmount { get; init; }
    public decimal CardAmount { get; init; }
    public decimal UpiAmount { get; init; }
    public decimal CreditNoteAmount { get; init; }
    public string CreditNoteRefs { get; init; } = "";
    public required string SyncStatus { get; init; }
    public bool HasReturn { get; init; }
    public string ReturnNo { get; init; } = "";
    public bool HasAdjustment { get; init; }
    public string AdjustmentNo { get; init; } = "";
    public DateTime SortUtc { get; init; }

    public string PayableFormatted => MoneyMath.FormatRupee(Payable);
    public string CashFormatted => MoneyMath.FormatRupee(CashAmount);
    public string CardFormatted => MoneyMath.FormatRupee(CardAmount);
    public string UpiFormatted => MoneyMath.FormatRupee(UpiAmount);
    public string CreditNoteFormatted => MoneyMath.FormatRupee(CreditNoteAmount);
    public string ReturnedDisplay => HasReturn ? "Yes" : "—";
    public string AdjustmentDisplay => HasAdjustment ? "Yes" : "—";
    public bool CanRedirect => HasReturn || HasAdjustment;
}

public sealed class StoreBillListSnapshot
{
    public IReadOnlyList<StoreBillListRow> Rows { get; init; } = Array.Empty<StoreBillListRow>();
    public int TotalMatched { get; init; }
    public bool WasTruncated { get; init; }
    public decimal TotalPayable { get; init; }
    public decimal TotalCash { get; init; }
    public decimal TotalCard { get; init; }
    public decimal TotalUpi { get; init; }
    public decimal TotalCreditNote { get; init; }
}

public sealed class StoreBillListQuery
{
    public string? InvoiceNo { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerMobile { get; init; }
    public DateTime? BusinessDate { get; init; }
    public bool UseDateRange { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public string? PosCounterFilter { get; init; }
    public int Limit { get; init; } = 500;
}
