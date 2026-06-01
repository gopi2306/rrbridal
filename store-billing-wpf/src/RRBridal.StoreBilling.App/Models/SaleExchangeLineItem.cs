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

    public decimal CgstAmount => MoneyMath.RoundAmount(Amount * CgstPercent / 100m);
    public decimal SgstAmount => MoneyMath.RoundAmount(Amount * SgstPercent / 100m);
    public decimal IgstAmount => MoneyMath.RoundAmount(Amount * IgstPercent / 100m);
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
