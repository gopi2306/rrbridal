using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class CashDenominationRow : ObservableObject
{
    public int Denomination { get; }

    [ObservableProperty] private string _unitCountText = "0";

    public CashDenominationRow(int denomination)
    {
        Denomination = denomination;
    }

    public int UnitCount => int.TryParse(UnitCountText?.Trim(), out var n) && n >= 0 ? n : 0;

    public decimal Amount => Denomination * UnitCount;

    partial void OnUnitCountTextChanged(string value) => OnPropertyChanged(nameof(AmountDisplay));

    public string AmountDisplay => Amount.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("en-IN"));
}
