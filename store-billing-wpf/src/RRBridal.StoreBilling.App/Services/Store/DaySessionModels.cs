using System;
using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Store;

public static class DaySessionStatus
{
    public const string Open = "open";
    public const string Closed = "closed";
}

public static class CashMovementType
{
    public const string DepositToBank = "deposit_to_bank";
    public const string CashWithdrawal = "cash_withdrawal";
}

public sealed class CashDenominationLine
{
    public int Denomination { get; init; }
    public int UnitCount { get; init; }
    public decimal Amount => Denomination * UnitCount;
}

public sealed class DaySessionRecord
{
    public required string SessionId { get; init; }
    public required string StoreId { get; init; }
    public required string PosCounter { get; init; }
    public required string DeviceId { get; init; }
    public required string BusinessDate { get; init; }
    public required string Status { get; init; }
    public decimal OpeningCash { get; init; }
    public decimal ExpectedCash { get; init; }
    public decimal ActualCashCounted { get; init; }
    public decimal CashDifference { get; init; }
    public IReadOnlyList<CashDenominationLine> CashDenominations { get; init; } = Array.Empty<CashDenominationLine>();
    public string? CashHandOverPrintedAtUtc { get; init; }
    public string? OpenedBy { get; init; }
    public string? OpenedAtUtc { get; init; }
    public string? ClosedBy { get; init; }
    public string? ClosedAtUtc { get; init; }
    public string? Notes { get; init; }
    public string? CashTaken { get; init; }
    public DayBillingCloseSnapshot? CloseSnapshot { get; init; }
}

public sealed class CashMovementRecord
{
    public required string MovementNo { get; init; }
    public required string MovementType { get; init; }
    public required string Description { get; init; }
    public required string BusinessDate { get; init; }
    public decimal Amount { get; init; }
    public required string Status { get; init; }
    public required string StoreId { get; init; }
    public required string PosCounter { get; init; }
    public required string DeviceId { get; init; }
    public required string CreatedAtUtc { get; init; }
}

public sealed class DaySessionRollupRow
{
    public required string PosCounter { get; init; }
    public required string Status { get; init; }
    public decimal OpeningCash { get; init; }
    public decimal ExpectedCash { get; init; }
    public decimal ActualCashCounted { get; init; }
    public decimal CashDifference { get; init; }
    public string? ClosedBy { get; init; }
    public string? ClosedAtUtc { get; init; }
}

public sealed class StoreDaySessionRollup
{
    public required string BusinessDate { get; init; }
    public IReadOnlyList<DaySessionRollupRow> Counters { get; init; } = Array.Empty<DaySessionRollupRow>();
    public decimal TotalOpeningCash { get; init; }
    public decimal TotalExpectedCash { get; init; }
    public decimal TotalActualCashCounted { get; init; }
    public decimal TotalCashDifference { get; init; }
}

public static class CashDenominationDefaults
{
    public static readonly int[] StandardDenominations = { 500, 200, 100, 50, 20, 10, 5, 2, 1 };

    public static IReadOnlyList<CashDenominationLine> EmptyGrid(bool include2000 = false)
    {
        var list = new List<CashDenominationLine>();
        if (include2000)
            list.Add(new CashDenominationLine { Denomination = 2000, UnitCount = 0 });
        foreach (var d in StandardDenominations)
            list.Add(new CashDenominationLine { Denomination = d, UnitCount = 0 });
        return list;
    }

    public static decimal SumDenominations(IEnumerable<CashDenominationLine> lines)
    {
        decimal total = 0m;
        foreach (var line in lines)
            total += line.Amount;
        return total;
    }

    public static bool ValidateDenominations(IReadOnlyList<CashDenominationLine> lines, decimal declaredTotal, out string? error)
    {
        error = null;
        foreach (var line in lines)
        {
            if (line.Amount != line.Denomination * line.UnitCount)
            {
                error = $"Invalid amount for ₹{line.Denomination} denomination.";
                return false;
            }
        }

        var sum = SumDenominations(lines);
        if (sum != declaredTotal)
        {
            error = "Denomination total does not match cash in hand.";
            return false;
        }

        return true;
    }
}

public static class DaySessionCashMath
{
    public static decimal ComputeExpectedCash(
        decimal openingCash,
        decimal netCashInHand,
        decimal depositsTotal,
        decimal withdrawalsTotal)
        => openingCash + netCashInHand - depositsTotal - withdrawalsTotal;
}
