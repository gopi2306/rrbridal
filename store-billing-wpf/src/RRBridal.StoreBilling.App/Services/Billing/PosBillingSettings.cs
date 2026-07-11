namespace RRBridal.StoreBilling.App.Services.Billing;

public sealed class PosBillingSettingsDocument
{
    public bool AllowDuplicatePrint { get; set; } = true;

    /// <summary>When true, adding an SKU already on the bill asks before increasing its quantity.</summary>
    public bool ConfirmDuplicateProductAdd { get; set; } = true;

    /// <summary>When true, Payment dialog can pay out credit note remaining balance as cash.</summary>
    public bool AllowCreditNoteRemainingCashout { get; set; }

    /// <summary>How many billing line-item and product-pick columns to show.</summary>
    public BillingLineItemDetailLevel LineItemDetailLevel { get; set; } = BillingLineItemDetailLevel.Full;

    /// <summary>When true, alteration amounts are GST-inclusive and split using each line's tax %.</summary>
    public bool AlterationGstIncluded { get; set; }

    /// <summary>When true, a bill may have multiple partial return transactions; fully returned lines are disabled.</summary>
    public bool AllowMultipleReturnsPerBill { get; set; }
}
