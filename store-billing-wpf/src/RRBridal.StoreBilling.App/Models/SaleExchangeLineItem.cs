using System;
using CommunityToolkit.Mvvm.ComponentModel;

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

    public decimal Amount => Math.Round(Qty * Rate, 2, MidpointRounding.AwayFromZero);

    public decimal CgstPercent => IsIgst ? 0m : Math.Round(TaxPercent / 2m, 2);
    public decimal SgstPercent => IsIgst ? 0m : Math.Round(TaxPercent / 2m, 2);
    public decimal IgstPercent => IsIgst ? TaxPercent : 0m;

    public decimal CgstAmount => Math.Round(Amount * CgstPercent / 100m, 2);
    public decimal SgstAmount => Math.Round(Amount * SgstPercent / 100m, 2);
    public decimal IgstAmount => Math.Round(Amount * IgstPercent / 100m, 2);
    public decimal TaxAmount => CgstAmount + SgstAmount + IgstAmount;
    public decimal Total => Amount + TaxAmount;

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
