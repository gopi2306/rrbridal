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
    public int MaxLineRows { get; set; } = 12;

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

    public string PrintFontFamily { get; set; } = "Arial";
    public int BillToMaxChars { get; set; } = 15;

    public static A5PrePrintedLayoutSettings CreateDefault() => new();

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
        PrintFontFamily = PrintFontFamily,
        BillToMaxChars = BillToMaxChars,
    };
}
