using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class CommercialA4InvoiceVisuals
{
    public static readonly Brush BorderBrush = Brushes.Black;
    public static readonly Brush TextBrush = Brushes.Black;
    public static readonly FontFamily BodyFont = new("Arial");

    public static TextBlock Text(
        string text,
        double fontSize,
        FontWeight? weight = null,
        TextAlignment align = TextAlignment.Left,
        bool wrap = true,
        VerticalAlignment verticalAlign = VerticalAlignment.Center)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = BodyFont,
            FontSize = fontSize,
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = TextBrush,
            TextAlignment = align,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalAlignment = verticalAlign,
        };
    }

    public static Border BorderedCell(UIElement child, Thickness? border = null, Thickness? padding = null)
    {
        return new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = border ?? new Thickness(1),
            Padding = padding ?? new Thickness(3, 2, 3, 2),
            SnapsToDevicePixels = true,
            Child = child,
        };
    }

    public static StackPanel MetaLabelValueStack(string label, string value, double fontSize)
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        if (!string.IsNullOrEmpty(label))
            stack.Children.Add(Text(label, fontSize, FontWeights.Bold, verticalAlign: VerticalAlignment.Top));
        stack.Children.Add(Text(value, fontSize, verticalAlign: VerticalAlignment.Top));
        return stack;
    }

    /// <summary>Two-field meta row (Invoice No. / Dated style) without duplicate outer padding.</summary>
    public static Border MetaSplitRowCell(
        string leftLabel,
        string leftValue,
        string rightLabel,
        string rightValue,
        double fontSize,
        Thickness padding,
        bool drawBottomBorder)
    {
        var grid = new Grid { SnapsToDevicePixels = true };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var leftWrap = new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(padding.Left, padding.Top, padding.Right / 2, padding.Bottom),
            SnapsToDevicePixels = true,
            Child = MetaLabelValueStack(leftLabel, leftValue, fontSize),
        };
        Grid.SetColumn(leftWrap, 0);
        grid.Children.Add(leftWrap);

        var rightWrap = new Border
        {
            Padding = new Thickness(padding.Right / 2, padding.Top, padding.Right, padding.Bottom),
            SnapsToDevicePixels = true,
            Child = MetaLabelValueStack(rightLabel, rightValue, fontSize),
        };
        Grid.SetColumn(rightWrap, 1);
        grid.Children.Add(rightWrap);

        return new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, drawBottomBorder ? 1 : 0),
            SnapsToDevicePixels = true,
            Child = grid,
        };
    }

    public static Border MetaBlockCell(UIElement content, Thickness padding, bool drawBottomBorder)
    {
        return new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, drawBottomBorder ? 1 : 0),
            Padding = padding,
            SnapsToDevicePixels = true,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = content,
        };
    }
}
