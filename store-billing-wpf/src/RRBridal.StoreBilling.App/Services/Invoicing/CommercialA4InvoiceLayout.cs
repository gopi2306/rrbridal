namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Reference dimensions for commercial A4 GST invoice (210 mm).</summary>
public static class CommercialA4InvoiceLayout
{
    public const double PageWidthMm = 210;
    public const double PageHeightMm = 297;
    public const double PageMarginMm = 8;

    /// <summary>Max line items on a page that has the seller/meta header but no totals footer.</summary>
    public const int LinesPerPage = 22;

    /// <summary>
    /// Max items on a single page with header + Subtotal/Total + declaration.
    /// Stay on one page whenever count is within this; only then spill to page 2.
    /// </summary>
    public const int LastPageLinesPerPage = 18;

    public const double TitlePt = 16;
    public const double BodyPt = 11;
    public const double SmallPt = 10;
    public const double TableHeaderPt = 10;
    public const double TableRowPt = 10;

    public const double MetaRowHeightMm = 7;
    public const double MetaLabelPt = 10;
    public const double TableRowHeightMm = 7;
    public const double TableCellPaddingHorizontalMm = 1.0;
    public const double TableCellPaddingVerticalMm = 0.6;
    public const double SectionPaddingMm = 1.5;
    public const double MetaLeftColumnWeight = 0.52;
    public const double MetaRightColumnWeight = 0.48;

    /// <summary>SI No, Description, HSN/SAC, Qty, Rate, per, Disc. %, Amount.</summary>
    public static readonly double[] LineColumnWeights = { 0.45, 2.55, 0.85, 0.65, 0.85, 0.4, 0.5, 1.15 };

    public static double[] ComputeColumnWidths(double contentWidth)
    {
        var sum = 0.0;
        foreach (var w in LineColumnWeights)
            sum += w;

        var widths = new double[LineColumnWeights.Length];
        for (var i = 0; i < LineColumnWeights.Length; i++)
            widths[i] = contentWidth * LineColumnWeights[i] / sum;
        return widths;
    }
}
