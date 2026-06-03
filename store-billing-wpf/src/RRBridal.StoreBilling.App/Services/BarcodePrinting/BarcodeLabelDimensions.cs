namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

/// <summary>
/// Physical label size for 2-across roll media on TVS LP 46 NEO (~38 mm × 33 mm per sticker).
/// Full print row is two stickers side-by-side (~76 mm).
/// </summary>
public static class BarcodeLabelDimensions
{
    /// <summary>Stickers printed side-by-side on each row.</summary>
    public const int LabelsPerRow = 2;

    /// <summary>Width of one sticker.</summary>
    public const int LabelWidthMm = 38;

    public const int LabelHeightMm = 33;

    public static int RowWidthMm => LabelWidthMm * LabelsPerRow;

    /// <summary>203 DPI ≈ 8 dots/mm.</summary>
    public const int DotsPerMm = 8;

    public static int WidthDots => LabelWidthMm * DotsPerMm;

    /// <summary>Right edge of price column (keep all X below this).</summary>
    public static int MaxX => WidthDots - 8;
}
