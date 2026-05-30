using System;
using CommunityToolkit.Mvvm.ComponentModel;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Models;

public partial class BillingLineItem : ObservableObject
{
    [ObservableProperty] private int lineNo;

    /// <summary>Trailing scratch row for typing the next product code.</summary>
    [ObservableProperty] private bool isEntryRow;

    [ObservableProperty] private string centralProductId = "";

    [ObservableProperty] private string productCode = "";

    [ObservableProperty] private string description = "";

    [ObservableProperty] private string hsnCode = "";

    [ObservableProperty] private decimal qty;

    [ObservableProperty] private decimal rate;

    [ObservableProperty] private decimal amount;

    /// <summary>Item discount share (₹) taken from tax-inclusive line total.</summary>
    [ObservableProperty] private decimal discountAmount;

    /// <summary>Cash discount share (₹) taken from tax-inclusive line total.</summary>
    [ObservableProperty] private decimal cashDiscountAmount;

    [ObservableProperty] private decimal mrp;

    [ObservableProperty] private decimal costPrice;

    [ObservableProperty] private decimal marginPercent;

    [ObservableProperty] private decimal taxPercent;

    [ObservableProperty] private bool isIgst;

    [ObservableProperty] private decimal cgstPercent;
    [ObservableProperty] private decimal sgstPercent;
    [ObservableProperty] private decimal igstPercent;
    [ObservableProperty] private decimal cgstAmount;
    [ObservableProperty] private decimal sgstAmount;
    [ObservableProperty] private decimal igstAmount;
    [ObservableProperty] private decimal taxAmount;

    private decimal _originalTaxAmount;
    private decimal _originalInclusiveAmount;
    private decimal _revisedAmount;
    private decimal _revisedTaxAmount;
    private decimal _revisedInclusiveAmount;

    public decimal OriginalTaxAmount
    {
        get => _originalTaxAmount;
        private set => SetProperty(ref _originalTaxAmount, value);
    }

    public decimal OriginalInclusiveAmount
    {
        get => _originalInclusiveAmount;
        private set => SetProperty(ref _originalInclusiveAmount, value);
    }

    public decimal RevisedAmount
    {
        get => _revisedAmount;
        private set => SetProperty(ref _revisedAmount, value);
    }

    public decimal RevisedTaxAmount
    {
        get => _revisedTaxAmount;
        private set => SetProperty(ref _revisedTaxAmount, value);
    }

    public decimal RevisedInclusiveAmount
    {
        get => _revisedInclusiveAmount;
        private set => SetProperty(ref _revisedInclusiveAmount, value);
    }

    partial void OnQtyChanged(decimal value) => Recalc();
    partial void OnRateChanged(decimal value) => Recalc();
    partial void OnTaxPercentChanged(decimal value) => RecalcTax();
    partial void OnIsIgstChanged(bool value) => RecalcTax();
    partial void OnDiscountAmountChanged(decimal value) => RecalcTax();
    partial void OnCashDiscountAmountChanged(decimal value) => RecalcTax();

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

        var original = BillingDiscountCalculator.ComputeOriginalTax(Amount, TaxPercent, IsIgst);
        OriginalTaxAmount = original.TotalTax;
        OriginalInclusiveAmount = original.Inclusive;

        var revised = BillingDiscountCalculator.ComputeRevisedFromInclusiveDiscounts(
            OriginalInclusiveAmount, DiscountAmount, CashDiscountAmount, TaxPercent, IsIgst);

        RevisedAmount = revised.Taxable;
        CgstAmount = revised.Cgst;
        SgstAmount = revised.Sgst;
        IgstAmount = revised.Igst;
        TaxAmount = revised.TotalTax;
        RevisedTaxAmount = revised.TotalTax;
        RevisedInclusiveAmount = revised.Inclusive;
    }
}
