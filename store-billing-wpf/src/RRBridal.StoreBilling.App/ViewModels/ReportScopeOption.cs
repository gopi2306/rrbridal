using RRBridal.StoreBilling.App.Services.Store;

namespace RRBridal.StoreBilling.App.ViewModels;

public sealed class ReportScopeOption(ReportScope scope, string label)
{
    public ReportScope Scope { get; } = scope;

    public string Label { get; } = label;

    public override string ToString() => Label;
}
