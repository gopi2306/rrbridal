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

    public decimal ReturnAmount => Math.Round(ReturnQty * Rate, 2, MidpointRounding.AwayFromZero);

    public decimal CgstPercent => IsIgst ? 0m : Math.Round(TaxPercent / 2m, 2);
    public decimal SgstPercent => IsIgst ? 0m : Math.Round(TaxPercent / 2m, 2);
    public decimal IgstPercent => IsIgst ? TaxPercent : 0m;

    public decimal CgstAmount => Math.Round(ReturnAmount * CgstPercent / 100m, 2);
    public decimal SgstAmount => Math.Round(ReturnAmount * SgstPercent / 100m, 2);
    public decimal IgstAmount => Math.Round(ReturnAmount * IgstPercent / 100m, 2);
    public decimal TaxAmount => CgstAmount + SgstAmount + IgstAmount;

    partial void OnIsSelectedChanged(bool value)
    {
        ReturnQty = value
            ? ReturnQty <= 0 ? OriginalQty : Math.Min(ReturnQty, OriginalQty)
            : 0;
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

        OnPropertyChanged(nameof(ReturnAmount));
        OnPropertyChanged(nameof(CgstAmount));
        OnPropertyChanged(nameof(SgstAmount));
        OnPropertyChanged(nameof(IgstAmount));
        OnPropertyChanged(nameof(TaxAmount));
    }
}
