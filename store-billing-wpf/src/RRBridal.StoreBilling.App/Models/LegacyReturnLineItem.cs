using System;
using CommunityToolkit.Mvvm.ComponentModel;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Models;

/// <summary>Catalog return line for pre-system invoice returns (no original bill in store_bills).</summary>
public partial class LegacyReturnLineItem : ObservableObject
{
    [ObservableProperty] private decimal _qty = 1;

    public int LineNo { get; set; }
    public string CentralProductId { get; init; } = "";
    public string ProductCode { get; init; } = "";
    public string Description { get; init; } = "";
    public decimal Rate { get; init; }
    public decimal TaxPercent { get; init; }
    public bool IsIgst { get; init; }

    public decimal InclusiveAmount => MoneyMath.RoundAmount(Qty * Rate);

    public decimal GrossReturnAmount => InclusiveAmount;

    public decimal ReturnDiscountAmount => 0m;

    private GstTaxBreakdown TaxBreakdown =>
        BillingDiscountCalculator.ReverseSplitFromInclusive(InclusiveAmount, TaxPercent, IsIgst);

    public decimal TaxableReturnAmount => TaxBreakdown.Taxable;

    public decimal CgstAmount => TaxBreakdown.Cgst;
    public decimal SgstAmount => TaxBreakdown.Sgst;
    public decimal IgstAmount => TaxBreakdown.Igst;
    public decimal TaxAmount => TaxBreakdown.TotalTax;

    public decimal LineReturnTotal => InclusiveAmount;

    partial void OnQtyChanged(decimal value)
    {
        if (value < 0)
        {
            Qty = 0;
            return;
        }

        NotifyCalculated();
    }

    private void NotifyCalculated()
    {
        OnPropertyChanged(nameof(InclusiveAmount));
        OnPropertyChanged(nameof(GrossReturnAmount));
        OnPropertyChanged(nameof(TaxableReturnAmount));
        OnPropertyChanged(nameof(CgstAmount));
        OnPropertyChanged(nameof(SgstAmount));
        OnPropertyChanged(nameof(IgstAmount));
        OnPropertyChanged(nameof(TaxAmount));
        OnPropertyChanged(nameof(LineReturnTotal));
    }
}
