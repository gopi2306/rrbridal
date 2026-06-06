namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Absolute mm positions for values on pre-printed A5 PAKEEZA-style invoice (148×210).</summary>
public sealed class A5PrePrintedInvoiceLayout
{
    public const double PageWidthMm = 148;
    public const double PageHeightMm = 210;

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

    public double MetaRow2TopMm { get; }
    public double ContactLeftMm { get; }
    public double ContactWidthMm { get; }

    public double StitchingTickLeftMm { get; }
    public double StitchingTickTopMm { get; }
    public double StitchingTickSizeMm { get; }

    public double DeliveryDateLeftMm { get; }
    public double DeliveryDateTopMm { get; }
    public double DeliveryDateWidthMm { get; }

    public double TableTopMm { get; }
    public double LineRowHeightMm { get; }
    public int MaxLineRows { get; }

    public double ColDescLeftMm { get; }
    public double ColDescWidthMm { get; }
    public double ColQtyLeftMm { get; }
    public double ColQtyWidthMm { get; }
    public double ColRateLeftMm { get; }
    public double ColRateWidthMm { get; }
    public double ColAmountLeftMm { get; }
    public double ColAmountWidthMm { get; }

    public double TotalAmountLeftMm { get; }
    public double TotalAmountTopMm { get; }
    public double TotalAmountWidthMm { get; }

    public double DiscountPercentLeftMm { get; }
    public double DiscountPercentTopMm { get; }
    public double DiscountPercentWidthMm { get; }

    public double DiscountAmountLeftMm { get; }
    public double DiscountAmountTopMm { get; }
    public double DiscountAmountWidthMm { get; }

    public double TotalQtyLeftMm { get; }
    public double TotalQtyTopMm { get; }
    public double TotalQtyWidthMm { get; }

    private A5PrePrintedInvoiceLayout(A5PrePrintedLayoutSettings s)
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
        MetaRow2TopMm = s.MetaRow2TopMm;
        ContactLeftMm = s.ContactLeftMm;
        ContactWidthMm = s.ContactWidthMm;
        StitchingTickLeftMm = s.StitchingTickLeftMm;
        StitchingTickTopMm = s.StitchingTickTopMm;
        StitchingTickSizeMm = s.StitchingTickSizeMm;
        DeliveryDateLeftMm = s.DeliveryDateLeftMm;
        DeliveryDateTopMm = s.DeliveryDateTopMm;
        DeliveryDateWidthMm = s.DeliveryDateWidthMm;
        TableTopMm = s.TableTopMm;
        LineRowHeightMm = s.LineRowHeightMm;
        MaxLineRows = s.MaxLineRows;
        ColDescLeftMm = s.ColDescLeftMm;
        ColDescWidthMm = s.ColDescWidthMm;
        ColQtyLeftMm = s.ColQtyLeftMm;
        ColQtyWidthMm = s.ColQtyWidthMm;
        ColRateLeftMm = s.ColRateLeftMm;
        ColRateWidthMm = s.ColRateWidthMm;
        ColAmountLeftMm = s.ColAmountLeftMm;
        ColAmountWidthMm = s.ColAmountWidthMm;
        TotalAmountLeftMm = s.TotalAmountLeftMm;
        TotalAmountTopMm = s.TotalAmountTopMm;
        TotalAmountWidthMm = s.TotalAmountWidthMm;
        DiscountPercentLeftMm = s.DiscountPercentLeftMm;
        DiscountPercentTopMm = s.DiscountPercentTopMm;
        DiscountPercentWidthMm = s.DiscountPercentWidthMm;
        DiscountAmountLeftMm = s.DiscountAmountLeftMm;
        DiscountAmountTopMm = s.DiscountAmountTopMm;
        DiscountAmountWidthMm = s.DiscountAmountWidthMm;
        TotalQtyLeftMm = s.TotalQtyLeftMm;
        TotalQtyTopMm = s.TotalQtyTopMm;
        TotalQtyWidthMm = s.TotalQtyWidthMm;
    }

    public static A5PrePrintedInvoiceLayout FromSettings(A5PrePrintedLayoutSettings settings) =>
        new(settings ?? A5PrePrintedLayoutSettings.CreateDefault());

    public double X(double mm) => mm + OffsetXMm;
    public double Y(double mm) => mm + OffsetYMm;
    public double TableY(double mm) => mm + OffsetYMm + TableOffsetYMm;
}
