namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

/// <summary>
/// Dot positions for one 38×33 mm sticker — must stay in sync with <see cref="TsplBarcodeLabelBuilder"/>.
/// </summary>
public static class BarcodeLabelPreviewLayout
{
    public const int CompanyX = 4;
    public const int CompanyY = 6;

    public const int ItemX = 4;
    public const int ItemY = 22;

    public const int BarcodeX = 4;
    public const int BarcodeY = 42;
    public const int BarcodeHeightDots = 50;

    public const int BarcodeTextX = 4;
    public const int BarcodeTextY = 98;

    public const int PriceLabelX = 198;
    public const int PriceLabelY = 22;

    public const int PriceValueX = 178;
    public const int PriceValueY = 42;

    public const int InclTaxX = 168;
    public const int InclTaxY = 72;

    public static int HeightDots =>
        BarcodeLabelDimensions.LabelHeightMm * BarcodeLabelDimensions.DotsPerMm;

    /// <summary>Approximate barcode width in dots (left column before price block).</summary>
    public static int BarcodeWidthDots => PriceLabelX - BarcodeX - 12;
}
