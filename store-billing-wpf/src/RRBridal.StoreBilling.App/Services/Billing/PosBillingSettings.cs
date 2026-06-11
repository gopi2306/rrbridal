namespace RRBridal.StoreBilling.App.Services.Billing;

public sealed class PosBillingSettingsDocument
{
    public bool AllowDuplicatePrint { get; set; } = true;

    /// <summary>When true, Payment dialog can pay out credit note remaining balance as cash.</summary>
    public bool AllowCreditNoteRemainingCashout { get; set; }
}
