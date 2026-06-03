using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class InvoiceImageScaling
{
    public static Image CreateWpfImage(ImageSource source, double targetWidth, double? maxHeightPx = null)
    {
        var (width, height) = ComputeScaledSize(source, targetWidth, maxHeightPx);
        return new Image
        {
            Source = source,
            Stretch = Stretch.Uniform,
            Width = width,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
        };
    }

    public static (double Width, double Height) ComputeScaledSize(
        ImageSource source,
        double targetWidth,
        double? maxHeightPx)
    {
        if (source.Width <= 0 || source.Height <= 0)
            return (targetWidth, double.NaN);

        var scale = targetWidth / source.Width;
        var height = source.Height * scale;
        if (maxHeightPx.HasValue && height > maxHeightPx.Value)
        {
            scale = maxHeightPx.Value / source.Height;
            return (source.Width * scale, maxHeightPx.Value);
        }

        return (targetWidth, height);
    }

    public static double MmToPx(double mm) => mm / 25.4 * 96.0;
}
