namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

/// <summary>Maps printer dots (203 DPI) to on-screen pixels for WYSIWYG preview.</summary>
public static class BarcodeLabelPreviewScale
{
    /// <summary>~1.35 px/dot → ~410×355 px for a 304×264 dot label.</summary>
    public const double PixelsPerDot = 1.35;

    public static double LabelWidthPx => BarcodeLabelDimensions.WidthDots * PixelsPerDot;

    public static double LabelHeightPx => BarcodeLabelPreviewLayout.HeightDots * PixelsPerDot;

    public static double GapBetweenStickersPx => 6;

    public static double RowWidthPx => LabelWidthPx * 2 + GapBetweenStickersPx;

    public static double DotsToPx(double dots) => dots * PixelsPerDot;

    public static double LabelWidthPxFor(BarcodeLabelDesignConfig design) => design.WidthDots * PixelsPerDot;

    public static double LabelHeightPxFor(BarcodeLabelDesignConfig design) => design.HeightDots * PixelsPerDot;

    public static double RowWidthPxFor(BarcodeLabelDesignConfig design) =>
        LabelWidthPxFor(design) * Math.Max(1, design.LabelsPerRow) + GapBetweenStickersPx;
}
