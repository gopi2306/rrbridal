using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using RRBridal.StoreBilling.App.Services.Api;

namespace RRBridal.StoreBilling.App.Services.Store;

/// <summary>Row shape returned by the central /api/dashboard/store/day-close endpoint.</summary>
public sealed class StoreDayCloseCounterFigures
{
    public string? PosCounter { get; init; }
    public string? Status { get; init; }
    public decimal OpeningCash { get; init; }
    public decimal ExpectedCash { get; init; }
    public decimal ActualCashCounted { get; init; }
    public decimal CashDifference { get; init; }
    public string? ClosedBy { get; init; }
    public string? ClosedAtUtc { get; init; }
}

/// <summary>Summary shape from GET /api/dashboard/store/day-close/report.</summary>
public sealed class StoreDayCloseReportSummaryFigures
{
    public decimal OpeningCash { get; init; }
    public decimal CashTotal { get; init; }
    public decimal ReturnCashRefundTotal { get; init; }
    public decimal CreditNoteCashoutTotal { get; init; }
    public decimal DailyExpensesTotal { get; init; }
    public decimal DailyExpenseCashTotal { get; init; }
    public decimal DepositsTotal { get; init; }
    public decimal WithdrawalsTotal { get; init; }
    public decimal ExpectedCash { get; init; }
    public decimal ActualCashCounted { get; init; }
    public decimal CashDifference { get; init; }
    public decimal NetCashInHand { get; init; }
    public decimal NetCardInHand { get; init; }
    public decimal NetUpiInHand { get; init; }
    public decimal ActualHandInTotal { get; init; }
    public int BillCount { get; init; }
    public int ReturnCount { get; init; }
    public decimal CardTotal { get; init; }
    public decimal UpiTotal { get; init; }
    public decimal CreditNoteTotal { get; init; }
    public decimal ReturnTotalAmount { get; init; }
    public decimal CreditNoteIssuedTotal { get; init; }
}

/// <summary>Pure helpers for parsing CentralDashboardClient.GetStoreDayCloseAsync responses.</summary>
public static class StoreDayCloseDashboardReader
{
    public static IReadOnlyList<StoreDayCloseCounterFigures> ReadCounters(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("counters", out var countersEl)
            || countersEl.ValueKind != JsonValueKind.Array)
            return System.Array.Empty<StoreDayCloseCounterFigures>();

        return countersEl.EnumerateArray().Select(ReadCounterRow).ToList();
    }

    public static StoreDayCloseCounterFigures? FindCounter(JsonElement root, string posCounter)
    {
        var target = posCounter.Trim();
        return ReadCounters(root).FirstOrDefault(c =>
            !string.IsNullOrWhiteSpace(c.PosCounter)
            && string.Equals(c.PosCounter!.Trim(), target, System.StringComparison.OrdinalIgnoreCase));
    }

    public static StoreDayCloseCounterFigures ReadTotals(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("totals", out var totalsEl)
            || totalsEl.ValueKind != JsonValueKind.Object)
            return new StoreDayCloseCounterFigures();

        return new StoreDayCloseCounterFigures
        {
            OpeningCash = CentralDashboardClient.ReadDecimal(totalsEl, "openingCash"),
            ExpectedCash = CentralDashboardClient.ReadDecimal(totalsEl, "expectedCash"),
            ActualCashCounted = CentralDashboardClient.ReadDecimal(totalsEl, "actualCashCounted"),
            CashDifference = CentralDashboardClient.ReadDecimal(totalsEl, "cashDifference"),
        };
    }

    /// <summary>Computed expected cash from GET /api/dashboard/store/day-close/report (summary.expectedCash).</summary>
    public static decimal ReadReportExpectedCash(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return 0m;
        if (root.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.Object)
            return CentralDashboardClient.ReadDecimal(summary, "expectedCash");
        return CentralDashboardClient.ReadDecimal(root, "expectedCash");
    }

    /// <summary>Full summary from GET /api/dashboard/store/day-close/report.</summary>
    public static StoreDayCloseReportSummaryFigures ReadReportSummary(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("summary", out var summary)
            || summary.ValueKind != JsonValueKind.Object)
            return new StoreDayCloseReportSummaryFigures();

        return new StoreDayCloseReportSummaryFigures
        {
            OpeningCash = CentralDashboardClient.ReadDecimal(summary, "openingCash"),
            CashTotal = CentralDashboardClient.ReadDecimal(summary, "cashTotal"),
            ReturnCashRefundTotal = CentralDashboardClient.ReadDecimal(summary, "returnCashRefundTotal"),
            CreditNoteCashoutTotal = CentralDashboardClient.ReadDecimal(summary, "creditNoteCashoutTotal"),
            DailyExpensesTotal = CentralDashboardClient.ReadDecimal(summary, "dailyExpensesTotal"),
            DailyExpenseCashTotal = CentralDashboardClient.ReadDecimal(summary, "dailyExpenseCashTotal"),
            DepositsTotal = CentralDashboardClient.ReadDecimal(summary, "depositsTotal"),
            WithdrawalsTotal = CentralDashboardClient.ReadDecimal(summary, "withdrawalsTotal"),
            ExpectedCash = CentralDashboardClient.ReadDecimal(summary, "expectedCash"),
            ActualCashCounted = CentralDashboardClient.ReadDecimal(summary, "actualCashCounted"),
            CashDifference = CentralDashboardClient.ReadDecimal(summary, "cashDifference"),
            NetCashInHand = CentralDashboardClient.ReadDecimal(summary, "netCashInHand"),
            NetCardInHand = CentralDashboardClient.ReadDecimal(summary, "netCardInHand"),
            NetUpiInHand = CentralDashboardClient.ReadDecimal(summary, "netUpiInHand"),
            ActualHandInTotal = CentralDashboardClient.ReadDecimal(summary, "actualHandInTotal"),
            BillCount = CentralDashboardClient.ReadInt(summary, "billCount"),
            ReturnCount = CentralDashboardClient.ReadInt(summary, "returnCount"),
            CardTotal = CentralDashboardClient.ReadDecimal(summary, "cardTotal"),
            UpiTotal = CentralDashboardClient.ReadDecimal(summary, "upiTotal"),
            CreditNoteTotal = CentralDashboardClient.ReadDecimal(summary, "creditNoteTotal"),
            ReturnTotalAmount = CentralDashboardClient.ReadDecimal(summary, "returnTotalAmount"),
            CreditNoteIssuedTotal = CentralDashboardClient.ReadDecimal(summary, "creditNoteIssuedTotal"),
        };
    }

    private static StoreDayCloseCounterFigures ReadCounterRow(JsonElement row) => new()
    {
        PosCounter = row.TryGetProperty("posCounter", out var pc) ? pc.GetString() : null,
        Status = row.TryGetProperty("status", out var st) ? st.GetString() : null,
        OpeningCash = CentralDashboardClient.ReadDecimal(row, "openingCash"),
        ExpectedCash = CentralDashboardClient.ReadDecimal(row, "expectedCash"),
        ActualCashCounted = CentralDashboardClient.ReadDecimal(row, "actualCashCounted"),
        CashDifference = CentralDashboardClient.ReadDecimal(row, "cashDifference"),
        ClosedBy = row.TryGetProperty("closedBy", out var cb) && cb.ValueKind == JsonValueKind.String ? cb.GetString() : null,
        ClosedAtUtc = row.TryGetProperty("closedAtUtc", out var ca) && ca.ValueKind == JsonValueKind.String ? ca.GetString() : null,
    };
}
