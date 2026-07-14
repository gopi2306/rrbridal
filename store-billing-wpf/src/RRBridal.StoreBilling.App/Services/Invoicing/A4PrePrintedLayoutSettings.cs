using System;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Persisted mm alignment and font for A4 Bilal Textiles pre-printed value-only invoices.</summary>
public sealed class A4PrePrintedLayoutSettings
{
    public double OffsetXMm { get; set; }
    public double OffsetYMm { get; set; }
    public double TableOffsetYMm { get; set; }
    public double BodyFontPt { get; set; } = 9;
    public double TotalFontPt { get; set; } = 9.5;
    public double TextBaselineNudgeMm { get; set; }

    public double BillToTopMm { get; set; } = 57;
    public double BillToLeftMm { get; set; } = 20;
    public double BillToWidthMm { get; set; } = 106;

    /// <summary>Bill no value — top row in right meta box (after BILL NO :).</summary>
    public double InvNoTopMm { get; set; } = 57;
    public double InvNoLeftMm { get; set; } = 140;
    public double InvNoWidthMm { get; set; } = 56;

    /// <summary>Date value — middle row (after DATE :).</summary>
    public double DateTopMm { get; set; } = 65;
    public double DateLeftMm { get; set; } = 140;
    public double DateWidthMm { get; set; } = 56;

    /// <summary>Order no value — bottom row (after ORDER NO :).</summary>
    public double OrderNoTopMm { get; set; } = 73;
    public double OrderNoLeftMm { get; set; } = 140;
    public double OrderNoWidthMm { get; set; } = 56;

    /// <summary>Vertical spacing between bill no / date / order no rows.</summary>
    public double MetaRowHeightMm { get; set; } = 8;

    public double TableTopMm { get; set; } = 90;
    public double LineRowHeightMm { get; set; } = 7;
    public int MaxLineRows { get; set; } = 13;

    public double ColSrLeftMm { get; set; } = 20;
    public double ColSrWidthMm { get; set; } = 8;
    public double ColParticularLeftMm { get; set; } = 28;
    public double ColParticularWidthMm { get; set; } = 42;
    public double ColHsnLeftMm { get; set; } = 70;
    public double ColHsnWidthMm { get; set; } = 14;
    public double ColPcsLeftMm { get; set; } = 84;
    public double ColPcsWidthMm { get; set; } = 10;
    public double ColMeterLeftMm { get; set; } = 94;
    public double ColMeterWidthMm { get; set; } = 12;
    public double ColBasicRateLeftMm { get; set; } = 106;
    public double ColBasicRateWidthMm { get; set; } = 16;
    public double ColCdPercentLeftMm { get; set; } = 122;
    public double ColCdPercentWidthMm { get; set; } = 10;
    public double ColLessPercentLeftMm { get; set; } = 132;
    public double ColLessPercentWidthMm { get; set; } = 10;
    public double ColTdPercentLeftMm { get; set; } = 142;
    public double ColTdPercentWidthMm { get; set; } = 10;
    public double ColNetRateLeftMm { get; set; } = 152;
    public double ColNetRateWidthMm { get; set; } = 16;
    public double ColAmountLeftMm { get; set; } = 168;
    public double ColAmountWidthMm { get; set; } = 22;

    public double TotalAmountLeftMm { get; set; } = 168;
    public double TotalAmountTopMm { get; set; } = 248;
    public double TotalAmountWidthMm { get; set; } = 22;

    public string ContinuedLabel { get; set; } = "Continued";
    public double ContinuedLeftMm { get; set; }
    public double ContinuedWidthMm { get; set; }

    public string PrintFontFamily { get; set; } = "Arial";
    public int BillToMaxChars { get; set; } = 40;

    public string DuplicateCopyLabel { get; set; } = "DUPLICATE COPY";
    public double DuplicateCopyTopMm { get; set; } = 52;
    public double DuplicateCopyLeftMm { get; set; } = 140;
    public double DuplicateCopyWidthMm { get; set; } = 50;
    public double DuplicateCopyFontPt { get; set; } = 10;

    public static A4PrePrintedLayoutSettings CreateDefault() => new();

    public void EnsureAlignmentDefaults()
    {
        var d = CreateDefault();
        if (BodyFontPt == 0) BodyFontPt = d.BodyFontPt;
        if (TotalFontPt == 0) TotalFontPt = d.TotalFontPt;
        if (BillToTopMm == 0) BillToTopMm = d.BillToTopMm;
        if (BillToLeftMm == 0) BillToLeftMm = d.BillToLeftMm;
        if (BillToWidthMm == 0) BillToWidthMm = d.BillToWidthMm;
        if (InvNoTopMm == 0) InvNoTopMm = d.InvNoTopMm;
        if (InvNoLeftMm == 0) InvNoLeftMm = d.InvNoLeftMm;
        if (InvNoWidthMm == 0) InvNoWidthMm = d.InvNoWidthMm;
        if (DateTopMm == 0) DateTopMm = d.DateTopMm;
        if (DateLeftMm == 0) DateLeftMm = d.DateLeftMm;
        if (DateWidthMm == 0) DateWidthMm = d.DateWidthMm;
        if (OrderNoTopMm == 0) OrderNoTopMm = d.OrderNoTopMm;
        if (OrderNoLeftMm == 0) OrderNoLeftMm = d.OrderNoLeftMm;
        if (OrderNoWidthMm == 0) OrderNoWidthMm = d.OrderNoWidthMm;
        if (MetaRowHeightMm == 0) MetaRowHeightMm = d.MetaRowHeightMm;
        FixLegacyMetaLayout(d);
        if (TableTopMm == 0) TableTopMm = d.TableTopMm;
        if (LineRowHeightMm == 0) LineRowHeightMm = d.LineRowHeightMm;
        if (MaxLineRows == 0) MaxLineRows = d.MaxLineRows;
        if (ColSrLeftMm == 0) ColSrLeftMm = d.ColSrLeftMm;
        if (ColSrWidthMm == 0) ColSrWidthMm = d.ColSrWidthMm;
        if (ColParticularLeftMm == 0) ColParticularLeftMm = d.ColParticularLeftMm;
        if (ColParticularWidthMm == 0) ColParticularWidthMm = d.ColParticularWidthMm;
        if (ColHsnLeftMm == 0) ColHsnLeftMm = d.ColHsnLeftMm;
        if (ColHsnWidthMm == 0) ColHsnWidthMm = d.ColHsnWidthMm;
        if (ColPcsLeftMm == 0) ColPcsLeftMm = d.ColPcsLeftMm;
        if (ColPcsWidthMm == 0) ColPcsWidthMm = d.ColPcsWidthMm;
        if (ColMeterLeftMm == 0) ColMeterLeftMm = d.ColMeterLeftMm;
        if (ColMeterWidthMm == 0) ColMeterWidthMm = d.ColMeterWidthMm;
        if (ColBasicRateLeftMm == 0) ColBasicRateLeftMm = d.ColBasicRateLeftMm;
        if (ColBasicRateWidthMm == 0) ColBasicRateWidthMm = d.ColBasicRateWidthMm;
        if (ColCdPercentLeftMm == 0) ColCdPercentLeftMm = d.ColCdPercentLeftMm;
        if (ColCdPercentWidthMm == 0) ColCdPercentWidthMm = d.ColCdPercentWidthMm;
        if (ColLessPercentLeftMm == 0) ColLessPercentLeftMm = d.ColLessPercentLeftMm;
        if (ColLessPercentWidthMm == 0) ColLessPercentWidthMm = d.ColLessPercentWidthMm;
        if (ColTdPercentLeftMm == 0) ColTdPercentLeftMm = d.ColTdPercentLeftMm;
        if (ColTdPercentWidthMm == 0) ColTdPercentWidthMm = d.ColTdPercentWidthMm;
        if (ColNetRateLeftMm == 0) ColNetRateLeftMm = d.ColNetRateLeftMm;
        if (ColNetRateWidthMm == 0) ColNetRateWidthMm = d.ColNetRateWidthMm;
        if (ColAmountLeftMm == 0) ColAmountLeftMm = d.ColAmountLeftMm;
        if (ColAmountWidthMm == 0) ColAmountWidthMm = d.ColAmountWidthMm;
        if (TotalAmountLeftMm == 0) TotalAmountLeftMm = d.TotalAmountLeftMm;
        if (TotalAmountTopMm == 0) TotalAmountTopMm = d.TotalAmountTopMm;
        if (TotalAmountWidthMm == 0) TotalAmountWidthMm = d.TotalAmountWidthMm;
        if (string.IsNullOrWhiteSpace(ContinuedLabel)) ContinuedLabel = d.ContinuedLabel;
        if (BillToMaxChars == 0) BillToMaxChars = d.BillToMaxChars;
        if (string.IsNullOrWhiteSpace(PrintFontFamily)) PrintFontFamily = d.PrintFontFamily;
        if (string.IsNullOrWhiteSpace(DuplicateCopyLabel)) DuplicateCopyLabel = d.DuplicateCopyLabel;
        if (DuplicateCopyTopMm == 0) DuplicateCopyTopMm = d.DuplicateCopyTopMm;
        if (DuplicateCopyLeftMm == 0) DuplicateCopyLeftMm = d.DuplicateCopyLeftMm;
        if (DuplicateCopyWidthMm == 0) DuplicateCopyWidthMm = d.DuplicateCopyWidthMm;
        if (DuplicateCopyFontPt == 0) DuplicateCopyFontPt = d.DuplicateCopyFontPt;
    }

    /// <summary>v1 stacked bill no / date on one row, or date in a separate column.</summary>
    private void FixLegacyMetaLayout(A4PrePrintedLayoutSettings defaults)
    {
        var sameRow = Math.Abs(DateTopMm - InvNoTopMm) < 0.01;
        var dateFarRight = DateLeftMm > InvNoLeftMm + 12;
        if (!sameRow && !dateFarRight && OrderNoTopMm > 0)
            return;

        InvNoTopMm = defaults.InvNoTopMm;
        InvNoLeftMm = defaults.InvNoLeftMm;
        InvNoWidthMm = defaults.InvNoWidthMm;
        DateTopMm = defaults.DateTopMm;
        DateLeftMm = defaults.DateLeftMm;
        DateWidthMm = defaults.DateWidthMm;
        OrderNoTopMm = defaults.OrderNoTopMm;
        OrderNoLeftMm = defaults.OrderNoLeftMm;
        OrderNoWidthMm = defaults.OrderNoWidthMm;
        MetaRowHeightMm = defaults.MetaRowHeightMm;
        if (BillToTopMm is 62 or 0) BillToTopMm = defaults.BillToTopMm;
        if (TableTopMm is 98 or 0) TableTopMm = defaults.TableTopMm;
    }

    public A4PrePrintedLayoutSettings Clone() => new()
    {
        OffsetXMm = OffsetXMm,
        OffsetYMm = OffsetYMm,
        TableOffsetYMm = TableOffsetYMm,
        BodyFontPt = BodyFontPt,
        TotalFontPt = TotalFontPt,
        TextBaselineNudgeMm = TextBaselineNudgeMm,
        BillToTopMm = BillToTopMm,
        BillToLeftMm = BillToLeftMm,
        BillToWidthMm = BillToWidthMm,
        InvNoTopMm = InvNoTopMm,
        InvNoLeftMm = InvNoLeftMm,
        InvNoWidthMm = InvNoWidthMm,
        DateTopMm = DateTopMm,
        DateLeftMm = DateLeftMm,
        DateWidthMm = DateWidthMm,
        OrderNoTopMm = OrderNoTopMm,
        OrderNoLeftMm = OrderNoLeftMm,
        OrderNoWidthMm = OrderNoWidthMm,
        MetaRowHeightMm = MetaRowHeightMm,
        TableTopMm = TableTopMm,
        LineRowHeightMm = LineRowHeightMm,
        MaxLineRows = MaxLineRows,
        ColSrLeftMm = ColSrLeftMm,
        ColSrWidthMm = ColSrWidthMm,
        ColParticularLeftMm = ColParticularLeftMm,
        ColParticularWidthMm = ColParticularWidthMm,
        ColHsnLeftMm = ColHsnLeftMm,
        ColHsnWidthMm = ColHsnWidthMm,
        ColPcsLeftMm = ColPcsLeftMm,
        ColPcsWidthMm = ColPcsWidthMm,
        ColMeterLeftMm = ColMeterLeftMm,
        ColMeterWidthMm = ColMeterWidthMm,
        ColBasicRateLeftMm = ColBasicRateLeftMm,
        ColBasicRateWidthMm = ColBasicRateWidthMm,
        ColCdPercentLeftMm = ColCdPercentLeftMm,
        ColCdPercentWidthMm = ColCdPercentWidthMm,
        ColLessPercentLeftMm = ColLessPercentLeftMm,
        ColLessPercentWidthMm = ColLessPercentWidthMm,
        ColTdPercentLeftMm = ColTdPercentLeftMm,
        ColTdPercentWidthMm = ColTdPercentWidthMm,
        ColNetRateLeftMm = ColNetRateLeftMm,
        ColNetRateWidthMm = ColNetRateWidthMm,
        ColAmountLeftMm = ColAmountLeftMm,
        ColAmountWidthMm = ColAmountWidthMm,
        TotalAmountLeftMm = TotalAmountLeftMm,
        TotalAmountTopMm = TotalAmountTopMm,
        TotalAmountWidthMm = TotalAmountWidthMm,
        ContinuedLabel = ContinuedLabel,
        ContinuedLeftMm = ContinuedLeftMm,
        ContinuedWidthMm = ContinuedWidthMm,
        PrintFontFamily = PrintFontFamily,
        BillToMaxChars = BillToMaxChars,
        DuplicateCopyLabel = DuplicateCopyLabel,
        DuplicateCopyTopMm = DuplicateCopyTopMm,
        DuplicateCopyLeftMm = DuplicateCopyLeftMm,
        DuplicateCopyWidthMm = DuplicateCopyWidthMm,
        DuplicateCopyFontPt = DuplicateCopyFontPt,
    };
}
