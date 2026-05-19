namespace RRBridal.StoreBilling.App.ViewModels;

/// <summary>Dashboard/Ledger filter: null PosCounter = all tills.</summary>
public sealed class PosCounterFilterOption(string? posCounter, string label)
{
    public string? PosCounter { get; } = posCounter;

    public string Label { get; } = label;
}
