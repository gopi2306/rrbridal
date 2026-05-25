using System;

using CommunityToolkit.Mvvm.ComponentModel;

using RRBridal.StoreBilling.App.Services.Billing;



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



    /// <summary>Tax-inclusive amount paid on the original bill for full line qty.</summary>

    public decimal OriginalPaidInclusive { get; init; }



    private decimal ReturnRatio => OriginalQty > 0 ? ReturnQty / OriginalQty : 0m;



    public decimal ReturnItemDiscount =>

        Math.Round(OriginalItemDiscount * ReturnRatio, 2, MidpointRounding.AwayFromZero);



    public decimal ReturnCashDiscount =>

        Math.Round(OriginalCashDiscount * ReturnRatio, 2, MidpointRounding.AwayFromZero);



    public decimal ReturnDiscountAmount => ReturnItemDiscount + ReturnCashDiscount;



    /// <summary>Proportional tax-inclusive refund (matches billing payable for returned qty).</summary>

    public decimal ReturnInclusive =>

        Math.Round(OriginalPaidInclusive * ReturnRatio, 2, MidpointRounding.AwayFromZero);



    /// <summary>Pre-discount inclusive value for summary display.</summary>

    public decimal GrossReturnAmount => ReturnInclusive + ReturnDiscountAmount;



    private GstTaxBreakdown ReturnBreakdown =>

        BillingDiscountCalculator.ReverseSplitFromInclusive(ReturnInclusive, TaxPercent, IsIgst);



    public decimal TaxableReturnAmount => ReturnBreakdown.Taxable;



    public decimal ReturnAmount => TaxableReturnAmount;



    public decimal CgstPercent => IsIgst ? 0m : Math.Round(TaxPercent / 2m, 2);

    public decimal SgstPercent => IsIgst ? 0m : Math.Round(TaxPercent / 2m, 2);

    public decimal IgstPercent => IsIgst ? TaxPercent : 0m;



    public decimal CgstAmount => ReturnBreakdown.Cgst;

    public decimal SgstAmount => ReturnBreakdown.Sgst;

    public decimal IgstAmount => ReturnBreakdown.Igst;

    public decimal TaxAmount => ReturnBreakdown.TotalTax;



    public decimal LineReturnTotal => ReturnInclusive;



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

        OnPropertyChanged(nameof(ReturnInclusive));

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


