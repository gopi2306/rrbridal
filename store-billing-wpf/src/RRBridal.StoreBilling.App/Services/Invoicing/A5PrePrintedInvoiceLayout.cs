namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Absolute mm positions for values on pre-printed A5 PAKEEZA-style invoice (148×210).</summary>
public static class A5PrePrintedInvoiceLayout
{
    public const double PageWidthMm = 148;
    public const double PageHeightMm = 210;

    /// <summary>Global nudge (positive = right). Meta/header fields use this.</summary>
    public const double OffsetXMm = 0;

    /// <summary>Global nudge (negative = up). Header + row 2 only; table uses TableOffsetYMm.</summary>
    public const double OffsetYMm = -8;

    /// <summary>Extra vertical nudge for line items + total (positive = down on paper).</summary>
    public const double TableOffsetYMm = 4;

    public const double BodyFontPt = 9.5;
    public const double TotalFontPt = 9;

    public const double TextBaselineNudgeMm = 0;

    // --- Row 1: BILL TO / INV. NO. / DATE value lines ---

    public const double BillToTopMm = 81;
    public const double BillToLeftMm = 34;
    public const double BillToWidthMm = 30;

    public const double InvNoTopMm = 81;
    public const double InvNoLeftMm = 79;
    public const double InvNoWidthMm = 32;

    public const double DateTopMm = 81;
    public const double DateLeftMm = 115;
    public const double DateWidthMm = 26;

    // --- Row 2: CONTACT / stitching / D/D ---

    public const double MetaRow2TopMm = 90;
    public const double ContactLeftMm = 40;
    public const double ContactWidthMm = 32;

    public const double StitchingTickLeftMm = 97;
    public const double StitchingTickTopMm = 91;
    public const double StitchingTickSizeMm = 5;

    public const double DeliveryDateLeftMm = 113;
    public const double DeliveryDateTopMm = 91;
    public const double DeliveryDateWidthMm = 28;

    // --- Line items (first row inside table body, below column headers) ---

    public const double TableTopMm = 108;
    public const double LineRowHeightMm = 7.2;
    public const int MaxLineRows = 12;

    public const double ColDescLeftMm = 25;
    public const double ColDescWidthMm = 56;
    public const double ColQtyLeftMm = 85;
    public const double ColQtyWidthMm = 9;
    public const double ColRateLeftMm = 90;
    public const double ColRateWidthMm = 15;
    public const double ColAmountLeftMm = 102;
    public const double ColAmountWidthMm = 18;

    public const double TotalAmountLeftMm = 102;
    public const double TotalAmountTopMm = 190;
    public const double TotalAmountWidthMm = 25;

    public static double X(double mm) => mm + OffsetXMm;
    public static double Y(double mm) => mm + OffsetYMm;
    public static double TableY(double mm) => mm + OffsetYMm + TableOffsetYMm;
}
