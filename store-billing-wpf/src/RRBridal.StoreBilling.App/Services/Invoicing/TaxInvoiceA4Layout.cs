namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Reference dimensions for A4 Tax Invoice (210 mm). Separate from commercial layout.</summary>
public static class TaxInvoiceA4Layout
{
    public const double PageWidthMm = 210;
    public const double PageHeightMm = 297;
    public const double PageMarginMm = 8;

    /// <summary>Max line items on a page with seller/meta header but no GST/declaration footer.</summary>
    public const int LinesPerPage = 22;

    /// <summary>
    /// Max items on a single page with header + compacted totals/GST/declaration.
    /// 10–15 line bills stay on page 1; only larger bills spill to page 2.
    /// </summary>
    public const int LastPageLinesPerPage = 15;

    public const double TitlePt = 14;
    public const double BodyPt = 10;
    public const double SmallPt = 8.5;
    public const double TableHeaderPt = 9;
    public const double TableRowPt = 9;

    /// <summary>Compact fonts for totals / GST / declaration footer stack.</summary>
    public const double FooterBodyPt = 9;
    public const double FooterSmallPt = 8;

    public const double MetaRowHeightMm = 6;
    public const double MetaLabelPt = 9;
    public const double TableRowHeightMm = 6;
    public const double FooterTableRowHeightMm = 5;
    public const double TableCellPaddingHorizontalMm = 0.8;
    public const double TableCellPaddingVerticalMm = 0.35;
    public const double SectionPaddingMm = 1.0;
    public const double FooterSectionPaddingMm = 0.7;
    public const double MetaLeftColumnWeight = 0.52;
    public const double MetaRightColumnWeight = 0.48;

    /// <summary>SI No, Description, HSN/SAC, Qty, Rate, per, Amount.</summary>
    public static readonly double[] LineColumnWeights = { 0.45, 2.9, 0.9, 0.75, 0.95, 0.45, 1.35 };

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
