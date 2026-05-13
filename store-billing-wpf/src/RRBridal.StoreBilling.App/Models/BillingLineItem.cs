using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RRBridal.StoreBilling.App.Models;

public partial class BillingLineItem : ObservableObject
{
    [ObservableProperty] private int lineNo;

    [ObservableProperty] private string centralProductId = "";

    [ObservableProperty] private string productCode = "";

    [ObservableProperty] private string description = "";

    [ObservableProperty] private string hsnCode = "";

    [ObservableProperty] private decimal qty;

    [ObservableProperty] private decimal rate;

    [ObservableProperty] private decimal amount;

    [ObservableProperty] private decimal mrp;

    [ObservableProperty] private decimal taxPercent;

    [ObservableProperty] private bool isIgst;

    [ObservableProperty] private decimal cgstPercent;
    [ObservableProperty] private decimal sgstPercent;
    [ObservableProperty] private decimal igstPercent;
    [ObservableProperty] private decimal cgstAmount;
    [ObservableProperty] private decimal sgstAmount;
    [ObservableProperty] private decimal igstAmount;
    [ObservableProperty] private decimal taxAmount;

    partial void OnQtyChanged(decimal value) => Recalc();
    partial void OnRateChanged(decimal value) => Recalc();
    partial void OnTaxPercentChanged(decimal value) => RecalcTax();
    partial void OnIsIgstChanged(bool value) => RecalcTax();

    private void Recalc()
    {
        Amount = Math.Round(Qty * Rate, 2, MidpointRounding.AwayFromZero);
        RecalcTax();
    }

    private void RecalcTax()
    {
        if (IsIgst)
        {
            CgstPercent = 0;
            SgstPercent = 0;
            IgstPercent = TaxPercent;
        }
        else
        {
            CgstPercent = Math.Round(TaxPercent / 2m, 2);
            SgstPercent = Math.Round(TaxPercent / 2m, 2);
            IgstPercent = 0;
        }

        CgstAmount = Math.Round(Amount * CgstPercent / 100m, 2);
        SgstAmount = Math.Round(Amount * SgstPercent / 100m, 2);
        IgstAmount = Math.Round(Amount * IgstPercent / 100m, 2);
        TaxAmount = CgstAmount + SgstAmount + IgstAmount;
    }
}
