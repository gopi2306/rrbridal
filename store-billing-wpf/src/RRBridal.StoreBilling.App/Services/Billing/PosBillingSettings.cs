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

    /// <summary>Master switch for Bill on credit (pay-later) option.</summary>
    public bool EnableCreditBilling { get; set; } = true;

    /// <summary>When true, only customers marked isCreditCustomer can use Bill on credit.</summary>
    public bool CreditBillingRequireCreditCustomer { get; set; } = true;

    /// <summary>Minimum advance percent of payable at post (0 = no percent minimum).</summary>
    public decimal CreditBillingMinimumAdvancePercent { get; set; }

    /// <summary>Minimum advance amount in ₹ at post (0 = no amount minimum).</summary>
    public decimal CreditBillingMinimumAdvanceAmount { get; set; }

    /// <summary>When true, full payable may be put on credit with zero advance.</summary>
    public bool CreditBillingAllowZeroAdvance { get; set; } = true;

    /// <summary>When true, later collection may be partial; when false, only full balance settlement.</summary>
    public bool CreditBillingAllowPartialCollection { get; set; } = true;

    /// <summary>Max balance due per bill (0 = no limit).</summary>
    public decimal CreditBillingMaxBalancePerBill { get; set; }
}
