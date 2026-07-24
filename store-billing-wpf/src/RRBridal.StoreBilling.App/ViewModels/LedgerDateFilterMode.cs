namespace RRBridal.StoreBilling.App.ViewModels;

public enum LedgerDateFilterMode
{
    Today,
    Custom,
}

public sealed class LedgerDateFilterOption(LedgerDateFilterMode mode, string label)
{
    public LedgerDateFilterMode Mode { get; } = mode;

    public string Label { get; } = label;
}
