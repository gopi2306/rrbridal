namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Reference dimensions for A4 Tax Invoice (210 mm). Separate from commercial layout.</summary>
public static class TaxInvoiceA4Layout
{
    public const double PageWidthMm = 210;
    public const double PageHeightMm = 297;
    public const double PageMarginMm = 8;

    /// <summary>Fewer lines than commercial so GST split + bank box fit on last page.</summary>
    public const int LinesPerPage = 12;

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

    /// <summary>SI No, Description, HSN/SAC, Qty, Rate, per, Amount.</summary>
    public static readonly double[] LineColumnWeights = { 0.5, 3.0, 0.95, 0.75, 0.95, 0.5, 1.15 };

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
