namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Reference dimensions for retail A4 invoice (210 mm width); scale for A5.</summary>
public static class RetailInvoiceLayout
{
    public const double ReferencePageWidthMm = 210;
    public const double ReferencePageHeightMm = 297;

    /// <summary>Green border visible around cream panel.</summary>
    public const double PageInsetMm = 8;

    /// <summary>Semicircle arch radius equals half panel width.</summary>
    public const double ArchHeightMm = 18;

    public const double PanelPaddingMm = 10;

    public const double HeaderTitlePt = 22;
    public const double StoreNamePt = 16;
    public const double SubHeaderPt = 10;
    public const double BodyPt = 10;
    public const double TableHeaderPt = 10;
    public const double TermsPt = 8;

    public const double LogoMaxWidthMm = 14;
    public const double LogoMaxHeightMm = 18;

    public const int MinTableRows = 12;
    public const double TableRowHeightMm = 9;

    /// <summary>Column weights: Description (~65%), Qty, Rate, Amount.</summary>
    public static readonly double[] LineColumnWeights = { 5.5, 0.65, 0.9, 1.0 };

    public const double FooterTermsWidthRatio = 0.62;

    public static double Scale(double pageWidthMm) => pageWidthMm / ReferencePageWidthMm;

    public static double PanelWidthMm(double pageWidthMm) =>
        pageWidthMm - PageInsetMm * 2;

    public static double PanelBodyHeightMm(double pageHeightMm, double pageWidthMm)
    {
        var panelW = PanelWidthMm(pageWidthMm);
        var archH = panelW / 2;
        return pageHeightMm - PageInsetMm * 2 - archH;
    }
}
