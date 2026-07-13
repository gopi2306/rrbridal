using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using RRBridal.StoreBilling.App.Services.BarcodePrinting;
using RRBridal.StoreBilling.App.Services.Invoicing;

namespace RRBridal.StoreBilling.App.Views.Controls;

public partial class BarcodeLabelPreviewControl
{
    private static readonly FontFamily UiFont = new("Segoe UI");

    public BarcodeLabelPreviewControl()
    {
        InitializeComponent();
    }

    public void ApplyLayout(BarcodeLabelLayout layout)
    {
        var model = layout.RenderModel;
        var design = model.Design;

        SkuHeader.Text = $"SKU {model.Sku} · {model.BarcodeValue}";
        SizeCaption.Text =
            $"Row {design.RowWidthMm} × {design.LabelHeightMm} mm " +
            $"({design.LabelsPerRow} × {design.LabelWidthMm} mm) · {design.Name}";
        CopyCaption.Text = layout.CopySummary;
        CopyCaption.Visibility = Visibility.Visible;

        SizeCanvas(LabelCanvas, design);
        if (design.LabelsPerRow > 1)
        {
            RightCanvas.Visibility = Visibility.Visible;
            SizeCanvas(RightCanvas, design);
            RenderLabel(RightCanvas, model, design.WidthDots);
        }
        else
        {
            RightCanvas.Visibility = Visibility.Collapsed;
        }

        RenderLabel(LabelCanvas, model, 0);
    }

    private static void SizeCanvas(Canvas canvas, BarcodeLabelDesignConfig design)
    {
        canvas.Width = BarcodeLabelPreviewScale.LabelWidthPxFor(design);
        canvas.Height = BarcodeLabelPreviewScale.LabelHeightPxFor(design);
    }

    private static void RenderLabel(Canvas canvas, BarcodeLabelRenderModel model, int xOffsetDots)
    {
        canvas.Children.Clear();
        var design = model.Design;

        if (!string.Equals(model.Decoration, "none", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(model.Decoration, "price_underline", StringComparison.OrdinalIgnoreCase))
        {
            var border = new Rectangle
            {
                Width = canvas.Width - 2,
                Height = canvas.Height - 2,
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                RadiusX = string.Equals(model.Decoration, "rounded_border", StringComparison.OrdinalIgnoreCase) ? 6 : 0,
                RadiusY = string.Equals(model.Decoration, "rounded_border", StringComparison.OrdinalIgnoreCase) ? 6 : 0,
            };
            Canvas.SetLeft(border, 1);
            Canvas.SetTop(border, 1);
            canvas.Children.Add(border);
        }

        foreach (var line in model.TextLines)
        {
            PlaceText(canvas, line.Text, line.XDots + xOffsetDots, line.YDots, line.FontDots, line.Bold, line.Alignment, design.WidthDots);
            if (line.Underline)
            {
                var underline = new Line
                {
                    X1 = BarcodeLabelPreviewScale.DotsToPx(line.XDots + xOffsetDots),
                    X2 = BarcodeLabelPreviewScale.DotsToPx(line.XDots + xOffsetDots + design.WidthDots / 2),
                    Y1 = BarcodeLabelPreviewScale.DotsToPx(line.YDots + 12),
                    Y2 = BarcodeLabelPreviewScale.DotsToPx(line.YDots + 12),
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                };
                canvas.Children.Add(underline);
            }
        }

        if (model.Barcode == null)
            return;

        var barcode = model.Barcode;
        var barcodeW = (int)Math.Max(40, BarcodeLabelPreviewScale.DotsToPx(design.WidthDots * 0.75));
        var barcodeH = (int)Math.Max(24, BarcodeLabelPreviewScale.DotsToPx(barcode.HeightDots));
        var img = ThermalBarcodeGenerator.CreateCode128(barcode.Value, barcodeW, barcodeH);
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
            Canvas.SetLeft(image, BarcodeLabelPreviewScale.DotsToPx(barcode.XDots + xOffsetDots));
            Canvas.SetTop(image, BarcodeLabelPreviewScale.DotsToPx(barcode.YDots));
            canvas.Children.Add(image);
        }

        PlaceText(
            canvas,
            barcode.HumanText,
            barcode.XDots + xOffsetDots,
            barcode.HumanTextYDots,
            barcode.FontDots,
            barcode.Bold,
            design.Text.Alignment,
            design.WidthDots);
    }

    private static void PlaceText(
        Canvas canvas,
        string text,
        int xDots,
        int yDots,
        int fontDots,
        bool bold,
        string alignment,
        int widthDots,
        bool rightAlign = false)
    {
        var fontSize = FontSizeFromTspl(fontDots);
        var tb = new TextBlock
        {
            Text = text,
            FontFamily = UiFont,
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
        if (rightAlign || string.Equals(alignment, "right", StringComparison.OrdinalIgnoreCase))
        {
            var maxRight = BarcodeLabelPreviewScale.DotsToPx(widthDots - 8);
            left = Math.Max(0, maxRight - formatted.Width);
        }
        else if (string.Equals(alignment, "center", StringComparison.OrdinalIgnoreCase))
        {
            left = Math.Max(0, BarcodeLabelPreviewScale.DotsToPx(widthDots / 2.0) - formatted.Width / 2);
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
