using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RRBridal.StoreBilling.App.Models;

public partial class AdjustmentLineItem : ObservableObject
{
    public int LineNo { get; init; }
    public string ProductCode { get; init; } = "";
    public string Description { get; init; } = "";
    public decimal TaxPercent { get; init; }
    public bool IsIgst { get; init; }

    public decimal OriginalQty { get; init; }
    public decimal OriginalRate { get; init; }
    public decimal OriginalAmount { get; init; }

    [ObservableProperty] private decimal _adjustedQty;
    [ObservableProperty] private decimal _adjustedRate;

    public decimal AdjustedAmount => Math.Round(AdjustedQty * AdjustedRate, 2, MidpointRounding.AwayFromZero);
    public decimal DiffAmount => AdjustedAmount - OriginalAmount;

    public decimal DiffCgst => IsIgst ? 0m : Math.Round(DiffAmount * (TaxPercent / 2m) / 100m, 2);
    public decimal DiffSgst => IsIgst ? 0m : Math.Round(DiffAmount * (TaxPercent / 2m) / 100m, 2);
    public decimal DiffIgst => IsIgst ? Math.Round(DiffAmount * TaxPercent / 100m, 2) : 0m;
    public decimal DiffTax => DiffCgst + DiffSgst + DiffIgst;

    partial void OnAdjustedQtyChanged(decimal value) => NotifyComputed();
    partial void OnAdjustedRateChanged(decimal value) => NotifyComputed();

    private void NotifyComputed()
    {
        OnPropertyChanged(nameof(AdjustedAmount));
        OnPropertyChanged(nameof(DiffAmount));
        OnPropertyChanged(nameof(DiffCgst));
        OnPropertyChanged(nameof(DiffSgst));
        OnPropertyChanged(nameof(DiffIgst));
        OnPropertyChanged(nameof(DiffTax));
    }
}
