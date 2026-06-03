using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class RetailInvoiceVisuals
{
    public static readonly Color PageGreen = Color.FromRgb(0x1B, 0x43, 0x32);
    public static readonly Color PanelCream = Color.FromRgb(0xF5, 0xF0, 0xE1);

    public static readonly Brush PageGreenBrush = CreateFrozenBrush(PageGreen);
    public static readonly Brush PanelCreamBrush = CreateFrozenBrush(PanelCream);
    public static readonly Brush BorderBrush = CreateFrozenBrush(Colors.Black);
    public static readonly Brush TextBrush = CreateFrozenBrush(Colors.Black);

    public static readonly FontFamily HeadingFont = new("Georgia, Times New Roman");
    public static readonly FontFamily BodyFont = new("Georgia, Times New Roman");

    public static Brush CreatePatternBrush()
    {
        var tile = new DrawingGroup();
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 0.5);
        pen.Freeze();

        var geo = new GeometryGroup();
        geo.Children.Add(new LineGeometry(new Point(0, 8), new Point(8, 0)));
        geo.Children.Add(new LineGeometry(new Point(8, 8), new Point(16, 0)));
        geo.Children.Add(new LineGeometry(new Point(0, 0), new Point(8, 8)));
        geo.Children.Add(new LineGeometry(new Point(8, 0), new Point(16, 8)));
        geo.Freeze();

        tile.Children.Add(new GeometryDrawing(null, pen, geo));
        tile.Freeze();

        var brush = new DrawingBrush(tile)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 16, 16),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
        brush.Freeze();
        return brush;
    }

    /// <summary>Semicircular arch on top + rectangular body.</summary>
    public static PathGeometry BuildArchPanelGeometry(double width, double height)
    {
        var archRadius = width / 2;
        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Point(0, height),
            IsClosed = true,
        };
        figure.Segments.Add(new LineSegment(new Point(0, archRadius), true));
        figure.Segments.Add(new ArcSegment(
            new Point(width, archRadius),
            new Size(archRadius, archRadius),
            0,
            false,
            SweepDirection.Clockwise,
            true));
        figure.Segments.Add(new LineSegment(new Point(width, height), true));
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    public static FrameworkElement CreateCheckbox(bool isChecked, double size)
    {
        var host = new Grid { Width = size, Height = size, Margin = new Thickness(2, 0, 4, 0) };
        host.Children.Add(new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            Width = size,
            Height = size,
        });
        if (isChecked)
        {
            host.Children.Add(new TextBlock
            {
                Text = "✓",
                FontSize = size * 0.75,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        return host;
    }

    public static Image CreateWatermarkImage(ImageSource logo, double maxWidth, double opacity = 0.08)
    {
        var img = InvoiceImageScaling.CreateWpfImage(logo, maxWidth, maxWidth);
        img.Opacity = opacity;
        img.HorizontalAlignment = HorizontalAlignment.Center;
        img.VerticalAlignment = VerticalAlignment.Center;
        return img;
    }

    public static Border CreateUnderlineField(string label, string value, double labelWidth, double fontSize, double fieldWidth)
    {
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        row.Children.Add(new TextBlock
        {
            Text = label,
            FontFamily = BodyFont,
            FontSize = fontSize,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Bottom,
        });

        var valuePanel = new StackPanel { Orientation = Orientation.Vertical };
        if (!string.IsNullOrWhiteSpace(value))
        {
            valuePanel.Children.Add(new TextBlock
            {
                Text = value,
                FontFamily = BodyFont,
                FontSize = fontSize,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(4, 0, 0, 0),
            });
        }

        valuePanel.Children.Add(new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Height = 1,
            Margin = new Thickness(4, string.IsNullOrWhiteSpace(value) ? 14 : 2, 0, 0),
            Width = fieldWidth > 0 ? fieldWidth : double.NaN,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        Grid.SetColumn(valuePanel, 1);
        row.Children.Add(valuePanel);
        return new Border { Child = row, Background = Brushes.Transparent };
    }

    public static TextBlock Text(string content, double fontSize, FontWeight? weight = null, TextAlignment align = TextAlignment.Left)
    {
        return new TextBlock
        {
            Text = content,
            FontFamily = BodyFont,
            FontSize = fontSize,
            FontWeight = weight ?? FontWeights.Normal,
            TextAlignment = align,
            Foreground = TextBrush,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
