using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class ThermalBarcodeGenerator
{
    public static ImageSource? CreateCode128(string content, int width = 280, int height = 70)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 4,
                    PureBarcode = true,
                },
            };
            using var bitmap = writer.Write(content);
            return ToImageSource(bitmap);
        }
        catch
        {
            return null;
        }
    }

    public static ImageSource? CreateQrCode(string content, int size = 200)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new EncodingOptions
                {
                    Width = size,
                    Height = size,
                    Margin = 2,
                },
            };
            using var bitmap = writer.Write(content);
            return ToImageSource(bitmap);
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource ToImageSource(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = stream;
        img.EndInit();
        img.Freeze();
        return img;
    }
}
