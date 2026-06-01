namespace RRBridal.StoreBilling.App.Models;

public sealed class AppliedSchemeDisplayItem
{
    public required string SchemeCode { get; init; }
    public required string SchemeName { get; init; }
    public required decimal SavedAmount { get; init; }
    public string SavedAmountFormatted => Services.Billing.MoneyMath.FormatRupee(SavedAmount);
}
