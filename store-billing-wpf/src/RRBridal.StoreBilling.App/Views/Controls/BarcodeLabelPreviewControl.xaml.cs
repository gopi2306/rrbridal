using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RRBridal.StoreBilling.App.Services.BarcodePrinting;
using RRBridal.StoreBilling.App.Services.Invoicing;

namespace RRBridal.StoreBilling.App.Views.Controls;

public partial class BarcodeLabelPreviewControl
{
    private static readonly FontFamily UiFont = new("Segoe UI");

    public BarcodeLabelPreviewControl()
    {
        InitializeComponent();
        SizeCanvas(LabelCanvas);
        SizeCanvas(RightCanvas);
    }

    public void ApplyLayout(BarcodeLabelLayout layout)
    {
        SkuHeader.Text = $"SKU {layout.Sku} · {layout.BarcodeValue}";
        SizeCaption.Text =
            $"Row {BarcodeLabelDimensions.RowWidthMm} × {BarcodeLabelDimensions.LabelHeightMm} mm " +
            $"({BarcodeLabelDimensions.LabelsPerRow} × {BarcodeLabelDimensions.LabelWidthMm} mm cups) · " +
            BarcodePrinterPreferences.RecommendedModelName;
        CopyCaption.Text = layout.CopySummary;
        CopyCaption.Visibility = Visibility.Visible;

        RenderLabel(LabelCanvas, layout);
        RenderLabel(RightCanvas, layout);
    }

    private static void SizeCanvas(Canvas canvas)
    {
        canvas.Width = BarcodeLabelPreviewScale.LabelWidthPx;
        canvas.Height = BarcodeLabelPreviewScale.LabelHeightPx;
    }

    private static void RenderLabel(Canvas canvas, BarcodeLabelLayout layout)
    {
        canvas.Children.Clear();

        var company = BarcodeLabelTextLayout.TruncateCompany(layout.CompanyName);
        var item = BarcodeLabelTextLayout.TruncateItemSingleLine(layout.ItemName);
        var barcode = BarcodeLabelTextLayout.TruncateBarcode(layout.BarcodeValue);

        PlaceText(canvas, company, BarcodeLabelPreviewLayout.CompanyX, BarcodeLabelPreviewLayout.CompanyY,
            fontDots: 2, bold: true);
        PlaceText(canvas, item, BarcodeLabelPreviewLayout.ItemX, BarcodeLabelPreviewLayout.ItemY,
            fontDots: 2, bold: false);

        var barcodeW = (int)BarcodeLabelPreviewScale.DotsToPx(BarcodeLabelPreviewLayout.BarcodeWidthDots);
        var barcodeH = (int)BarcodeLabelPreviewScale.DotsToPx(BarcodeLabelPreviewLayout.BarcodeHeightDots);
        var img = ThermalBarcodeGenerator.CreateCode128(barcode, barcodeW, barcodeH);
        if (img != null)
        {
            var image = new Image
            {
                Source = img,
                Width = barcodeW,
                Height = barcodeH,
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true,
            };
            Canvas.SetLeft(image, BarcodeLabelPreviewScale.DotsToPx(BarcodeLabelPreviewLayout.BarcodeX));
            Canvas.SetTop(image, BarcodeLabelPreviewScale.DotsToPx(BarcodeLabelPreviewLayout.BarcodeY));
            canvas.Children.Add(image);
        }

        PlaceText(canvas, barcode, BarcodeLabelPreviewLayout.BarcodeTextX, BarcodeLabelPreviewLayout.BarcodeTextY,
            fontDots: 1, bold: true);

        PlaceText(canvas, "PRICE :", BarcodeLabelPreviewLayout.PriceLabelX, BarcodeLabelPreviewLayout.PriceLabelY,
            fontDots: 1, bold: false, rightAlign: true);
        PlaceText(canvas, layout.PriceText, BarcodeLabelPreviewLayout.PriceValueX, BarcodeLabelPreviewLayout.PriceValueY,
            fontDots: 3, bold: true, rightAlign: true);
        PlaceText(canvas, "(incl tax)", BarcodeLabelPreviewLayout.InclTaxX, BarcodeLabelPreviewLayout.InclTaxY,
            fontDots: 1, bold: false, rightAlign: true);
    }

    private static void PlaceText(
        Canvas canvas,
        string text,
        int xDots,
        int yDots,
        int fontDots,
        bool bold,
        bool rightAlign = false)
    {
        var fontSize = FontSizeFromTspl(fontDots);
        var tb = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = fontSize,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            Foreground = Brushes.Black,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var typeface = new Typeface(UiFont, FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(canvas).PixelsPerDip);

        var left = BarcodeLabelPreviewScale.DotsToPx(xDots);
        if (rightAlign)
        {
            var maxRight = BarcodeLabelPreviewScale.DotsToPx(BarcodeLabelDimensions.MaxX);
            left = Math.Max(0, maxRight - formatted.Width);
        }

        Canvas.SetLeft(tb, left);
        Canvas.SetTop(tb, BarcodeLabelPreviewScale.DotsToPx(yDots));
        canvas.Children.Add(tb);
    }

    private static double FontSizeFromTspl(int fontDots) => fontDots switch
    {
        1 => 7.5,
        2 => 9.5,
        3 => 13.5,
        _ => 9.5,
    };
}
