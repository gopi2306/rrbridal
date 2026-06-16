using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RRBridal.StoreBilling.App.Services.Store;

internal static class DayCloseReportSectionBuilder
{
    internal sealed record SheetSection(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

    public static IReadOnlyList<(string Label, string Value)> BuildMetadataRows(DayCloseReportData data)
    {
        var m = data.Metadata;
        return
        [
            ("Report", "Day Close Full Report"),
            ("Store", string.IsNullOrWhiteSpace(m.StoreName) ? m.StoreId : $"{m.StoreName} ({m.StoreId})"),
            ("Business date", m.BusinessDate),
            ("Counter scope", m.CounterScope),
            ("Session status", m.SessionStatus ?? "—"),
            ("Exported at", m.ExportedAtLocal),
            ("Time zone", m.TimeZone),
        ];
    }

    public static IReadOnlyList<(string Label, string Value)> BuildSummaryRows(DayCloseReportData data)
    {
        var s = data.Snapshot;
        return
        [
            ("Opening cash", F(s.OpeningCash)),
            ("Gross cash from bills", F(s.CashTotal)),
            ("Cash refunds (returns)", F(-s.ReturnCashRefundTotal)),
            ("Credit note cashouts", F(-s.CreditNoteCashoutTotal)),
            ("Daily expenses", F(-s.DailyExpensesTotal)),
            ("Deposits to bank", F(-s.DepositsTotal)),
            ("Cash withdrawals", F(s.WithdrawalsTotal)),
            ("Expected cash (drawer)", F(s.ExpectedCash)),
            ("Actual cash counted", F(s.ActualCashCounted)),
            ("Difference", F(s.CashDifference)),
            ("---", "---"),
            ("Net cash", F(s.NetCashInHand)),
            ("Net card", F(s.NetCardInHand)),
            ("Net UPI", F(s.NetUpiInHand)),
            ("Expected tender total", F(s.ActualHandInTotal)),
            ("Bill count", s.BillCount.ToString(InCulture)),
            ("Return count", s.ReturnCount.ToString(InCulture)),
            ("Gross card from bills", F(s.CardTotal)),
            ("Gross UPI from bills", F(s.UpiTotal)),
            ("Credit note applied (bills)", F(s.CreditNoteTotal)),
            ("Return total amount", F(s.ReturnTotalAmount)),
            ("Credit notes issued", F(s.CreditNoteIssuedTotal)),
        ];
    }

    public static SheetSection BuildCounterRollupSection(DayCloseReportData data)
    {
        const string h1 = "Counter";
        const string h2 = "Status";
        const string h3 = "Opening";
        const string h4 = "Expected";
        const string h5 = "Actual";
        const string h6 = "Diff";
        const string h7 = "Closed by";
        const string h8 = "Closed at (local)";
        var headers = new[] { h1, h2, h3, h4, h5, h6, h7, h8 };
        var rows = new List<IReadOnlyList<string>>();
        if (data.StoreRollup != null)
        {
            foreach (var row in data.StoreRollup.Counters)
            {
                rows.Add(
                [
                    $"POS{row.PosCounter}",
                    row.Status,
                    F(row.OpeningCash),
                    F(row.ExpectedCash),
                    F(row.ActualCashCounted),
                    row.Status == DaySessionStatus.Closed ? F(row.CashDifference) : "—",
                    row.ClosedBy ?? "—",
                    DayBillingCloseDocumentReader.FormatUtcLocal(row.ClosedAtUtc),
                ]);
            }
        }

        return new SheetSection("COUNTER_ROLLUP", headers, rows);
    }

    public static SheetSection BuildBillsSection(DayCloseReportData data)
    {
        var headers = new[]
        {
            "Bill no", "Posted (local)", "Bill date", "Counter", "Customer", "Mobile",
            "Qty", "Payable", "Cash", "Card", "UPI", "Credit note", "Credit note no(s)",
            "Returned", "Return no", "Adjustment", "Adjustment no", "Sync",
        };
        var rows = data.Bills.Select(b => (IReadOnlyList<string>)
        [
            b.BillNo, b.PostedAtLocal, b.BillDate, b.CounterDisplay, b.CustomerName, b.CustomerPhone,
            Q(b.TotalQty), F(b.Payable), F(b.CashAmount), F(b.CardAmount), F(b.UpiAmount),
            F(b.CreditNoteAmount), b.CreditNoteRefs, b.ReturnedDisplay, b.ReturnNo,
            b.AdjustmentDisplay, b.AdjustmentNo, b.SyncStatus,
        ]).ToList();
        return new SheetSection("BILLS", headers, rows);
    }

    public static SheetSection BuildReturnsSection(DayCloseReportData data)
    {
        var headers = new[]
        {
            "Return no", "Original bill", "Counter", "Posted (local)", "Return total", "Mode",
            "Cash refunded", "Credit balance", "Collected", "Payments", "Credit note no",
        };
        var rows = data.Returns.Select(r => (IReadOnlyList<string>)
        [
            r.ReturnNo, r.OriginalBillNo, r.CounterDisplay, r.PostedAtLocal, F(r.ReturnTotal),
            r.ReturnMode, F(r.CashRefunded), F(r.CreditBalance), F(r.AmountCollected),
            r.PaymentSummary, r.CreditNoteNo,
        ]).ToList();
        return new SheetSection("RETURNS", headers, rows);
    }

    public static SheetSection BuildAdjustmentsSection(DayCloseReportData data)
    {
        var headers = new[]
        {
            "Adjustment no", "Original bill", "Counter", "Posted (local)",
            "Original payable", "Adjusted payable", "Diff payable", "Reason",
        };
        var rows = data.Adjustments.Select(a => (IReadOnlyList<string>)
        [
            a.AdjustmentNo, a.OriginalBillNo, a.CounterDisplay, a.PostedAtLocal,
            F(a.OriginalPayable), F(a.AdjustedPayable), F(a.DiffPayable), a.Reason,
        ]).ToList();
        return new SheetSection("ADJUSTMENTS", headers, rows);
    }

    public static SheetSection BuildExpensesSection(DayCloseReportData data)
    {
        var headers = new[] { "Expense no", "Counter", "Business date", "Description", "Amount" };
        var rows = data.Expenses.Select(e => (IReadOnlyList<string>)
        [
            e.ExpenseNo, e.CounterDisplay, e.BusinessDate, e.Description, F(e.Amount),
        ]).ToList();
        return new SheetSection("EXPENSES", headers, rows);
    }

    public static SheetSection BuildCashMovementsSection(DayCloseReportData data)
    {
        var headers = new[] { "Movement no", "Type", "Counter", "Amount", "Note", "Posted (local)" };
        var rows = data.CashMovements.Select(m => (IReadOnlyList<string>)
        [
            m.MovementNo, m.MovementType, m.CounterDisplay, F(m.Amount), m.Note, m.PostedAtLocal,
        ]).ToList();
        return new SheetSection("CASH_MOVEMENTS", headers, rows);
    }

    public static SheetSection BuildCreditNoteCashoutsSection(DayCloseReportData data)
    {
        var headers = new[] { "Cashout no", "Credit note no", "Amount", "Counter", "Posted (local)" };
        var rows = data.CreditNoteCashouts.Select(c => (IReadOnlyList<string>)
        [
            c.CashoutNo, c.CreditNoteNo, F(c.Amount), c.CounterDisplay, c.PostedAtLocal,
        ]).ToList();
        return new SheetSection("CREDIT_NOTE_CASHOUTS", headers, rows);
    }

    public static SheetSection BuildDenominationsSection(DayCloseReportData data)
    {
        var headers = new[] { "Counter", "Denomination", "Count", "Subtotal" };
        var rows = data.Denominations.Select(d => (IReadOnlyList<string>)
        [
            d.CounterDisplay, d.Denomination.ToString(InCulture), d.UnitCount.ToString(InCulture), F(d.Subtotal),
        ]).ToList();
        return new SheetSection("DENOMINATIONS", headers, rows);
    }

    public static SheetSection BuildStockExceptionsSection(DayCloseReportData data)
    {
        var headers = new[]
        {
            "Bill no", "Counter", "Posted (local)", "SKU", "Description",
            "Requested qty", "Available qty",
        };
        var rows = data.StockExceptions.Select(x => (IReadOnlyList<string>)
        [
            x.BillNo, x.CounterDisplay, x.PostedAtLocal, x.Sku, x.Description,
            Q(x.RequestedQty), Q(x.AvailableQty),
        ]).ToList();
        return new SheetSection("STOCK_EXCEPTIONS", headers, rows);
    }

    public static IReadOnlyList<SheetSection> BuildAllDetailSections(DayCloseReportData data)
    {
        var sections = new List<SheetSection>
        {
            BuildCounterRollupSection(data),
            BuildBillsSection(data),
            BuildReturnsSection(data),
            BuildAdjustmentsSection(data),
            BuildExpensesSection(data),
            BuildCashMovementsSection(data),
        };
        if (data.CreditNoteCashouts.Count > 0)
            sections.Add(BuildCreditNoteCashoutsSection(data));
        if (data.Denominations.Count > 0)
            sections.Add(BuildDenominationsSection(data));
        if (data.StockExceptions.Count > 0)
            sections.Add(BuildStockExceptionsSection(data));
        return sections;
    }

    private static readonly CultureInfo InCulture = CultureInfo.InvariantCulture;

    private static string F(decimal value) => value.ToString("0.00", InCulture);

    private static string Q(decimal value) => value.ToString("0.##", InCulture);
}
