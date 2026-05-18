using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RRBridal.StoreBilling.App.Models;

public partial class SaleReturnLineItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private decimal _returnQty;

    public int LineNo { get; init; }
    public string ProductCode { get; init; } = "";
    public string Description { get; init; } = "";
    public decimal OriginalQty { get; init; }
    public decimal Rate { get; init; }
    public decimal TaxPercent { get; init; }
    public bool IsIgst { get; init; }
    public decimal OriginalItemDiscount { get; init; }
    public decimal OriginalCashDiscount { get; init; }

    private decimal ReturnRatio => OriginalQty > 0 ? ReturnQty / OriginalQty : 0m;

    public decimal ReturnItemDiscount =>
        Math.Round(OriginalItemDiscount * ReturnRatio, 2, MidpointRounding.AwayFromZero);

    public decimal ReturnCashDiscount =>
        Math.Round(OriginalCashDiscount * ReturnRatio, 2, MidpointRounding.AwayFromZero);

    public decimal ReturnDiscountAmount => ReturnItemDiscount + ReturnCashDiscount;

    public decimal GrossReturnAmount => Math.Round(ReturnQty * Rate, 2, MidpointRounding.AwayFromZero);

    public decimal TaxableReturnAmount =>
        Math.Max(0m, GrossReturnAmount - ReturnItemDiscount - ReturnCashDiscount);

    /// <summary>Taxable line value after discounts (used for return subtotal).</summary>
    public decimal ReturnAmount => TaxableReturnAmount;

    public decimal CgstPercent => IsIgst ? 0m : Math.Round(TaxPercent / 2m, 2);
    public decimal SgstPercent => IsIgst ? 0m : Math.Round(TaxPercent / 2m, 2);
    public decimal IgstPercent => IsIgst ? TaxPercent : 0m;

    public decimal CgstAmount => Math.Round(TaxableReturnAmount * CgstPercent / 100m, 2);
    public decimal SgstAmount => Math.Round(TaxableReturnAmount * SgstPercent / 100m, 2);
    public decimal IgstAmount => Math.Round(TaxableReturnAmount * IgstPercent / 100m, 2);
    public decimal TaxAmount => CgstAmount + SgstAmount + IgstAmount;

    public decimal LineReturnTotal => TaxableReturnAmount + TaxAmount;

    partial void OnIsSelectedChanged(bool value)
    {
        ReturnQty = value
            ? ReturnQty <= 0 ? OriginalQty : Math.Min(ReturnQty, OriginalQty)
            : 0;
        NotifyReturnCalculatedProperties();
    }

    partial void OnReturnQtyChanged(decimal value)
    {
        if (value < 0)
        {
            ReturnQty = 0;
            return;
        }

        if (value > OriginalQty)
        {
            ReturnQty = OriginalQty;
            return;
        }

        NotifyReturnCalculatedProperties();
    }

    private void NotifyReturnCalculatedProperties()
    {
        OnPropertyChanged(nameof(ReturnItemDiscount));
        OnPropertyChanged(nameof(ReturnCashDiscount));
        OnPropertyChanged(nameof(ReturnDiscountAmount));
        OnPropertyChanged(nameof(GrossReturnAmount));
        OnPropertyChanged(nameof(TaxableReturnAmount));
        OnPropertyChanged(nameof(ReturnAmount));
        OnPropertyChanged(nameof(CgstAmount));
        OnPropertyChanged(nameof(SgstAmount));
        OnPropertyChanged(nameof(IgstAmount));
        OnPropertyChanged(nameof(TaxAmount));
        OnPropertyChanged(nameof(LineReturnTotal));
    }
}
