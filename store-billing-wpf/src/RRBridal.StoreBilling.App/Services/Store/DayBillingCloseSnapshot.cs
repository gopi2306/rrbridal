using System;
using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class DayBillingCloseSnapshot
{
    public required DateTime LocalDate { get; init; }

    public int BillCount { get; init; }

    public decimal TotalQty { get; init; }

    public decimal TotalAmount { get; init; }

    public decimal CashTotal { get; init; }

    public decimal CardTotal { get; init; }

    public decimal UpiTotal { get; init; }

    public decimal CreditNoteTotal { get; init; }

    public IReadOnlyList<DayCloseInvoiceRow> Invoices { get; init; } = Array.Empty<DayCloseInvoiceRow>();

    public IReadOnlyList<DayCloseStockExceptionRow> StockExceptions { get; init; } = Array.Empty<DayCloseStockExceptionRow>();
}

public sealed class DayCloseInvoiceRow
{
    public required string BillNo { get; init; }

    public string CounterDisplay { get; init; } = "";

    public required string PostedAtLocal { get; init; }

    public decimal TotalQty { get; init; }

    public decimal Payable { get; init; }

    public string PaymentMode { get; init; } = "";

    public required string SyncStatus { get; init; }

    public DateTime SortUtc { get; init; }
}

public sealed class DayCloseStockExceptionRow
{
    public required string BillNo { get; init; }

    public string CounterDisplay { get; init; } = "";

    public required string PostedAtLocal { get; init; }

    public required string Sku { get; init; }

    public string Description { get; init; } = "";

    public decimal RequestedQty { get; init; }

    public decimal AvailableQty { get; init; }

    public bool CanApprove { get; init; }
}

public sealed record PaymentDayTotals(decimal Cash, decimal Card, decimal Upi, decimal CreditNote);
