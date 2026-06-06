using System;
using CommunityToolkit.Mvvm.ComponentModel;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Models;

public partial class SaleExchangeLineItem : ObservableObject
{
    [ObservableProperty] private decimal _qty = 1;

    public string CentralProductId { get; init; } = "";
    public string ProductCode { get; init; } = "";
    public string Description { get; init; } = "";
    public decimal AvailableQty { get; init; }
    public decimal Rate { get; init; }
    public decimal Mrp { get; init; }
    public decimal TaxPercent { get; init; }
    public bool IsIgst { get; init; }

    public decimal Amount => MoneyMath.RoundAmount(Qty * Rate);

    public decimal CgstPercent => IsIgst ? 0m : Math.Round(TaxPercent / 2m, 2);
    public decimal SgstPercent => IsIgst ? 0m : Math.Round(TaxPercent / 2m, 2);
    public decimal IgstPercent => IsIgst ? TaxPercent : 0m;

    private GstTaxBreakdown TaxBreakdown =>
        BillingDiscountCalculator.ReverseSplitFromInclusive(Amount, TaxPercent, IsIgst);

    public decimal CgstAmount => TaxBreakdown.Cgst;
    public decimal SgstAmount => TaxBreakdown.Sgst;
    public decimal IgstAmount => TaxBreakdown.Igst;
    public decimal TaxAmount => TaxBreakdown.TotalTax;
    public decimal Total => TaxBreakdown.Inclusive;

    partial void OnQtyChanged(decimal value)
    {
        if (value < 0)
        {
            Qty = 0;
            return;
        }

        if (AvailableQty > 0 && value > AvailableQty)
        {
            Qty = AvailableQty;
            return;
        }

        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(CgstAmount));
        OnPropertyChanged(nameof(SgstAmount));
        OnPropertyChanged(nameof(IgstAmount));
        OnPropertyChanged(nameof(TaxAmount));
        OnPropertyChanged(nameof(Total));
    }
}
