namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Persisted mm alignment and font for A5 pre-printed value-only invoices.</summary>
public sealed class A5PrePrintedLayoutSettings
{
    public double OffsetXMm { get; set; }
    public double OffsetYMm { get; set; } = -8;
    public double TableOffsetYMm { get; set; } = 4;

    public double BodyFontPt { get; set; } = 9.5;
    public double TotalFontPt { get; set; } = 9;
    public double TextBaselineNudgeMm { get; set; }

    public double BillToTopMm { get; set; } = 81;
    public double BillToLeftMm { get; set; } = 34;
    public double BillToWidthMm { get; set; } = 30;

    public double InvNoTopMm { get; set; } = 81;
    public double InvNoLeftMm { get; set; } = 79;
    public double InvNoWidthMm { get; set; } = 32;

    public double DateTopMm { get; set; } = 81;
    public double DateLeftMm { get; set; } = 115;
    public double DateWidthMm { get; set; } = 26;

    public double MetaRow2TopMm { get; set; } = 90;
    public double ContactLeftMm { get; set; } = 40;
    public double ContactWidthMm { get; set; } = 32;

    public double StitchingTickLeftMm { get; set; } = 97;
    public double StitchingTickTopMm { get; set; } = 91;
    public double StitchingTickSizeMm { get; set; } = 5;

    public double DeliveryDateLeftMm { get; set; } = 113;
    public double DeliveryDateTopMm { get; set; } = 91;
    public double DeliveryDateWidthMm { get; set; } = 28;

    public double TableTopMm { get; set; } = 108;
    public double LineRowHeightMm { get; set; } = 7.2;
    public int MaxLineRows { get; set; } = 10;

    public double ColDescLeftMm { get; set; } = 25;
    public double ColDescWidthMm { get; set; } = 56;
    public double ColQtyLeftMm { get; set; } = 85;
    public double ColQtyWidthMm { get; set; } = 9;
    public double ColRateLeftMm { get; set; } = 90;
    public double ColRateWidthMm { get; set; } = 15;
    public double ColAmountLeftMm { get; set; } = 102;
    public double ColAmountWidthMm { get; set; } = 18;

    public double TotalAmountLeftMm { get; set; } = 102;
    public double TotalAmountTopMm { get; set; } = 190;
    public double TotalAmountWidthMm { get; set; } = 25;

    public double DiscountPercentLeftMm { get; set; } = 25;
    public double DiscountPercentTopMm { get; set; } = 176;
    public double DiscountPercentWidthMm { get; set; } = 56;

    public double DiscountAmountLeftMm { get; set; } = 102;
    public double DiscountAmountTopMm { get; set; } = 176;
    public double DiscountAmountWidthMm { get; set; } = 25;

    public double TotalQtyLeftMm { get; set; } = 85;
    public double TotalQtyTopMm { get; set; } = 176;
    public double TotalQtyWidthMm { get; set; } = 9;

    public string PrintFontFamily { get; set; } = "Arial";
    public int BillToMaxChars { get; set; } = 15;

    public static A5PrePrintedLayoutSettings CreateDefault() => new();

    /// <summary>Fills mm positions missing from older saved configs (0 = unset).</summary>
    public void EnsureAlignmentDefaults()
    {
        var d = CreateDefault();
        if (OffsetYMm == 0) OffsetYMm = d.OffsetYMm;
        if (TableOffsetYMm == 0) TableOffsetYMm = d.TableOffsetYMm;
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
        if (MetaRow2TopMm == 0) MetaRow2TopMm = d.MetaRow2TopMm;
        if (ContactLeftMm == 0) ContactLeftMm = d.ContactLeftMm;
        if (ContactWidthMm == 0) ContactWidthMm = d.ContactWidthMm;
        if (StitchingTickLeftMm == 0) StitchingTickLeftMm = d.StitchingTickLeftMm;
        if (StitchingTickTopMm == 0) StitchingTickTopMm = d.StitchingTickTopMm;
        if (StitchingTickSizeMm == 0) StitchingTickSizeMm = d.StitchingTickSizeMm;
        if (DeliveryDateLeftMm == 0) DeliveryDateLeftMm = d.DeliveryDateLeftMm;
        if (DeliveryDateTopMm == 0) DeliveryDateTopMm = d.DeliveryDateTopMm;
        if (DeliveryDateWidthMm == 0) DeliveryDateWidthMm = d.DeliveryDateWidthMm;
        if (TableTopMm == 0) TableTopMm = d.TableTopMm;
        if (LineRowHeightMm == 0) LineRowHeightMm = d.LineRowHeightMm;
        if (MaxLineRows == 0) MaxLineRows = d.MaxLineRows;
        if (ColDescLeftMm == 0) ColDescLeftMm = d.ColDescLeftMm;
        if (ColDescWidthMm == 0) ColDescWidthMm = d.ColDescWidthMm;
        if (ColQtyLeftMm == 0) ColQtyLeftMm = d.ColQtyLeftMm;
        if (ColQtyWidthMm == 0) ColQtyWidthMm = d.ColQtyWidthMm;
        if (ColRateLeftMm == 0) ColRateLeftMm = d.ColRateLeftMm;
        if (ColRateWidthMm == 0) ColRateWidthMm = d.ColRateWidthMm;
        if (ColAmountLeftMm == 0) ColAmountLeftMm = d.ColAmountLeftMm;
        if (ColAmountWidthMm == 0) ColAmountWidthMm = d.ColAmountWidthMm;
        if (TotalAmountLeftMm == 0) TotalAmountLeftMm = d.TotalAmountLeftMm;
        if (TotalAmountTopMm == 0) TotalAmountTopMm = d.TotalAmountTopMm;
        if (TotalAmountWidthMm == 0) TotalAmountWidthMm = d.TotalAmountWidthMm;
        if (DiscountPercentLeftMm == 0) DiscountPercentLeftMm = d.DiscountPercentLeftMm;
        if (DiscountPercentTopMm == 0) DiscountPercentTopMm = d.DiscountPercentTopMm;
        if (DiscountPercentWidthMm == 0) DiscountPercentWidthMm = d.DiscountPercentWidthMm;
        if (DiscountAmountLeftMm == 0) DiscountAmountLeftMm = d.DiscountAmountLeftMm;
        if (DiscountAmountTopMm == 0) DiscountAmountTopMm = d.DiscountAmountTopMm;
        if (DiscountAmountWidthMm == 0) DiscountAmountWidthMm = d.DiscountAmountWidthMm;
        if (TotalQtyLeftMm == 0) TotalQtyLeftMm = d.TotalQtyLeftMm;
        if (TotalQtyTopMm == 0) TotalQtyTopMm = d.TotalQtyTopMm;
        if (TotalQtyWidthMm == 0) TotalQtyWidthMm = d.TotalQtyWidthMm;
        if (BillToMaxChars == 0) BillToMaxChars = d.BillToMaxChars;
        if (string.IsNullOrWhiteSpace(PrintFontFamily)) PrintFontFamily = d.PrintFontFamily;
    }

    public A5PrePrintedLayoutSettings Clone() => new()
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
        MetaRow2TopMm = MetaRow2TopMm,
        ContactLeftMm = ContactLeftMm,
        ContactWidthMm = ContactWidthMm,
        StitchingTickLeftMm = StitchingTickLeftMm,
        StitchingTickTopMm = StitchingTickTopMm,
        StitchingTickSizeMm = StitchingTickSizeMm,
        DeliveryDateLeftMm = DeliveryDateLeftMm,
        DeliveryDateTopMm = DeliveryDateTopMm,
        DeliveryDateWidthMm = DeliveryDateWidthMm,
        TableTopMm = TableTopMm,
        LineRowHeightMm = LineRowHeightMm,
        MaxLineRows = MaxLineRows,
        ColDescLeftMm = ColDescLeftMm,
        ColDescWidthMm = ColDescWidthMm,
        ColQtyLeftMm = ColQtyLeftMm,
        ColQtyWidthMm = ColQtyWidthMm,
        ColRateLeftMm = ColRateLeftMm,
        ColRateWidthMm = ColRateWidthMm,
        ColAmountLeftMm = ColAmountLeftMm,
        ColAmountWidthMm = ColAmountWidthMm,
        TotalAmountLeftMm = TotalAmountLeftMm,
        TotalAmountTopMm = TotalAmountTopMm,
        TotalAmountWidthMm = TotalAmountWidthMm,
        DiscountPercentLeftMm = DiscountPercentLeftMm,
        DiscountPercentTopMm = DiscountPercentTopMm,
        DiscountPercentWidthMm = DiscountPercentWidthMm,
        DiscountAmountLeftMm = DiscountAmountLeftMm,
        DiscountAmountTopMm = DiscountAmountTopMm,
        DiscountAmountWidthMm = DiscountAmountWidthMm,
        TotalQtyLeftMm = TotalQtyLeftMm,
        TotalQtyTopMm = TotalQtyTopMm,
        TotalQtyWidthMm = TotalQtyWidthMm,
        PrintFontFamily = PrintFontFamily,
        BillToMaxChars = BillToMaxChars,
    };
}
