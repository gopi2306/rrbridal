using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Crops empty margins from receipt logos so thermal print headers stay compact.</summary>
internal static class ReceiptLogoImageHelper
{
    public static ImageSource? LoadTrimmedForReceipt(string path, int decodePixelWidth)
    {
        var trimmed = TryLoadTrimmed(path, decodePixelWidth);
        return trimmed ?? LoadPlain(path, decodePixelWidth);
    }

    private static ImageSource? TryLoadTrimmed(string path, int decodePixelWidth)
    {
        try
        {
            using var original = new Bitmap(path);
            using var normalized = Ensure32bppArgb(original);
            using var trimmed = TrimEmptyMargins(normalized);
            return ToFrozenBitmapImage(trimmed, decodePixelWidth);
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? LoadPlain(string path, int decodePixelWidth)
    {
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.DecodePixelWidth = decodePixelWidth;
            img.UriSource = new Uri(path, UriKind.Absolute);
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap Ensure32bppArgb(Bitmap source)
    {
        if (source.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
            return (Bitmap)source.Clone();

        var clone = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(clone))
            g.DrawImage(source, 0, 0, source.Width, source.Height);
        return clone;
    }

    private static Bitmap TrimEmptyMargins(Bitmap source)
    {
        var minX = source.Width;
        var minY = source.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                if (!IsEmptyPixel(source.GetPixel(x, y)))
                {
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < minX || maxY < minY)
            return (Bitmap)source.Clone();

        var rect = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        return source.Clone(rect, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    }

    private static bool IsEmptyPixel(System.Drawing.Color c) =>
        c.A < 16 || (c.R > 248 && c.G > 248 && c.B > 248);

    private static ImageSource ToFrozenBitmapImage(Bitmap bitmap, int decodePixelWidth)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        var bytes = stream.ToArray();

        using var ms = new MemoryStream(bytes);
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.DecodePixelWidth = decodePixelWidth;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze();
        return img;
    }
}
