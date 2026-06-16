using System;
using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class DayCloseReportMetadata
{
    public required string StoreId { get; init; }
    public string StoreName { get; init; } = "";
    public required string BusinessDate { get; init; }
    public required string CounterScope { get; init; }
    public string? SessionStatus { get; init; }
    public required string ExportedAtLocal { get; init; }
    public string TimeZone { get; init; } = TimeZoneInfo.Local.Id;
}

public sealed class DayCloseReportReturnRow
{
    public required string ReturnNo { get; init; }
    public required string OriginalBillNo { get; init; }
    public string CounterDisplay { get; init; } = "";
    public required string PostedAtLocal { get; init; }
    public decimal ReturnTotal { get; init; }
    public string ReturnMode { get; init; } = "";
    public decimal CashRefunded { get; init; }
    public decimal CreditBalance { get; init; }
    public decimal AmountCollected { get; init; }
    public string PaymentSummary { get; init; } = "";
    public string CreditNoteNo { get; init; } = "";
}

public sealed class DayCloseReportAdjustmentRow
{
    public required string AdjustmentNo { get; init; }
    public required string OriginalBillNo { get; init; }
    public string CounterDisplay { get; init; } = "";
    public required string PostedAtLocal { get; init; }
    public decimal OriginalPayable { get; init; }
    public decimal AdjustedPayable { get; init; }
    public decimal DiffPayable { get; init; }
    public string Reason { get; init; } = "";
}

public sealed class DayCloseReportExpenseRow
{
    public required string ExpenseNo { get; init; }
    public string CounterDisplay { get; init; } = "";
    public required string BusinessDate { get; init; }
    public string Description { get; init; } = "";
    public decimal Amount { get; init; }
}

public sealed class DayCloseReportCashMovementRow
{
    public required string MovementNo { get; init; }
    public required string MovementType { get; init; }
    public string CounterDisplay { get; init; } = "";
    public decimal Amount { get; init; }
    public string Note { get; init; } = "";
    public required string PostedAtLocal { get; init; }
}

public sealed class DayCloseReportCreditNoteCashoutRow
{
    public required string CashoutNo { get; init; }
    public required string CreditNoteNo { get; init; }
    public decimal Amount { get; init; }
    public string CounterDisplay { get; init; } = "";
    public required string PostedAtLocal { get; init; }
}

public sealed class DayCloseReportDenominationRow
{
    public required string CounterDisplay { get; init; }
    public int Denomination { get; init; }
    public int UnitCount { get; init; }
    public decimal Subtotal { get; init; }
}

public sealed class DayCloseReportData
{
    public required DayCloseReportMetadata Metadata { get; init; }
    public required DayBillingCloseSnapshot Snapshot { get; init; }
    public DaySessionRecord? Session { get; init; }
    public StoreDaySessionRollup? StoreRollup { get; init; }
    public IReadOnlyList<StoreBillListRow> Bills { get; init; } = Array.Empty<StoreBillListRow>();
    public IReadOnlyList<DayCloseReportReturnRow> Returns { get; init; } = Array.Empty<DayCloseReportReturnRow>();
    public IReadOnlyList<DayCloseReportAdjustmentRow> Adjustments { get; init; } = Array.Empty<DayCloseReportAdjustmentRow>();
    public IReadOnlyList<DayCloseReportExpenseRow> Expenses { get; init; } = Array.Empty<DayCloseReportExpenseRow>();
    public IReadOnlyList<DayCloseReportCashMovementRow> CashMovements { get; init; } = Array.Empty<DayCloseReportCashMovementRow>();
    public IReadOnlyList<DayCloseReportCreditNoteCashoutRow> CreditNoteCashouts { get; init; } = Array.Empty<DayCloseReportCreditNoteCashoutRow>();
    public IReadOnlyList<DayCloseReportDenominationRow> Denominations { get; init; } = Array.Empty<DayCloseReportDenominationRow>();
    public IReadOnlyList<DayCloseStockExceptionRow> StockExceptions { get; init; } = Array.Empty<DayCloseStockExceptionRow>();
}
