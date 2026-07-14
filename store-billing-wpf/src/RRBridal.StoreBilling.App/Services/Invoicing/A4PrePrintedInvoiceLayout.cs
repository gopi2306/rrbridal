namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Absolute mm positions for values on Bilal Textiles A4 pre-printed invoice (210×297).</summary>
public sealed class A4PrePrintedInvoiceLayout
{
    public const double PageWidthMm = 210;
    public const double PageHeightMm = 297;

    public double OffsetXMm { get; }
    public double OffsetYMm { get; }
    public double TableOffsetYMm { get; }
    public double BodyFontPt { get; }
    public double TotalFontPt { get; }
    public double TextBaselineNudgeMm { get; }

    public double BillToTopMm { get; }
    public double BillToLeftMm { get; }
    public double BillToWidthMm { get; }

    public double InvNoTopMm { get; }
    public double InvNoLeftMm { get; }
    public double InvNoWidthMm { get; }

    public double DateTopMm { get; }
    public double DateLeftMm { get; }
    public double DateWidthMm { get; }

    public double OrderNoTopMm { get; }
    public double OrderNoLeftMm { get; }
    public double OrderNoWidthMm { get; }

    public double TableTopMm { get; }
    public double LineRowHeightMm { get; }
    public int MaxLineRows { get; }

    public double ColSrLeftMm { get; }
    public double ColSrWidthMm { get; }
    public double ColParticularLeftMm { get; }
    public double ColParticularWidthMm { get; }
    public double ColHsnLeftMm { get; }
    public double ColHsnWidthMm { get; }
    public double ColPcsLeftMm { get; }
    public double ColPcsWidthMm { get; }
    public double ColMeterLeftMm { get; }
    public double ColMeterWidthMm { get; }
    public double ColBasicRateLeftMm { get; }
    public double ColBasicRateWidthMm { get; }
    public double ColCdPercentLeftMm { get; }
    public double ColCdPercentWidthMm { get; }
    public double ColLessPercentLeftMm { get; }
    public double ColLessPercentWidthMm { get; }
    public double ColTdPercentLeftMm { get; }
    public double ColTdPercentWidthMm { get; }
    public double ColNetRateLeftMm { get; }
    public double ColNetRateWidthMm { get; }
    public double ColAmountLeftMm { get; }
    public double ColAmountWidthMm { get; }

    public double TotalAmountLeftMm { get; }
    public double TotalAmountTopMm { get; }
    public double TotalAmountWidthMm { get; }

    private A4PrePrintedInvoiceLayout(A4PrePrintedLayoutSettings s)
    {
        OffsetXMm = s.OffsetXMm;
        OffsetYMm = s.OffsetYMm;
        TableOffsetYMm = s.TableOffsetYMm;
        BodyFontPt = s.BodyFontPt;
        TotalFontPt = s.TotalFontPt;
        TextBaselineNudgeMm = s.TextBaselineNudgeMm;
        BillToTopMm = s.BillToTopMm;
        BillToLeftMm = s.BillToLeftMm;
        BillToWidthMm = s.BillToWidthMm;
        InvNoTopMm = s.InvNoTopMm;
        InvNoLeftMm = s.InvNoLeftMm;
        InvNoWidthMm = s.InvNoWidthMm;
        DateTopMm = s.DateTopMm;
        DateLeftMm = s.DateLeftMm;
        DateWidthMm = s.DateWidthMm;
        OrderNoTopMm = s.OrderNoTopMm;
        OrderNoLeftMm = s.OrderNoLeftMm;
        OrderNoWidthMm = s.OrderNoWidthMm;
        TableTopMm = s.TableTopMm;
        LineRowHeightMm = s.LineRowHeightMm;
        MaxLineRows = s.MaxLineRows;
        ColSrLeftMm = s.ColSrLeftMm;
        ColSrWidthMm = s.ColSrWidthMm;
        ColParticularLeftMm = s.ColParticularLeftMm;
        ColParticularWidthMm = s.ColParticularWidthMm;
        ColHsnLeftMm = s.ColHsnLeftMm;
        ColHsnWidthMm = s.ColHsnWidthMm;
        ColPcsLeftMm = s.ColPcsLeftMm;
        ColPcsWidthMm = s.ColPcsWidthMm;
        ColMeterLeftMm = s.ColMeterLeftMm;
        ColMeterWidthMm = s.ColMeterWidthMm;
        ColBasicRateLeftMm = s.ColBasicRateLeftMm;
        ColBasicRateWidthMm = s.ColBasicRateWidthMm;
        ColCdPercentLeftMm = s.ColCdPercentLeftMm;
        ColCdPercentWidthMm = s.ColCdPercentWidthMm;
        ColLessPercentLeftMm = s.ColLessPercentLeftMm;
        ColLessPercentWidthMm = s.ColLessPercentWidthMm;
        ColTdPercentLeftMm = s.ColTdPercentLeftMm;
        ColTdPercentWidthMm = s.ColTdPercentWidthMm;
        ColNetRateLeftMm = s.ColNetRateLeftMm;
        ColNetRateWidthMm = s.ColNetRateWidthMm;
        ColAmountLeftMm = s.ColAmountLeftMm;
        ColAmountWidthMm = s.ColAmountWidthMm;
        TotalAmountLeftMm = s.TotalAmountLeftMm;
        TotalAmountTopMm = s.TotalAmountTopMm;
        TotalAmountWidthMm = s.TotalAmountWidthMm;
    }

    public static A4PrePrintedInvoiceLayout FromSettings(A4PrePrintedLayoutSettings settings) =>
        new(settings ?? A4PrePrintedLayoutSettings.CreateDefault());

    public double X(double mm) => mm + OffsetXMm;
    public double Y(double mm) => mm + OffsetYMm;
    public double TableY(double mm) => mm + OffsetYMm + TableOffsetYMm;
}
