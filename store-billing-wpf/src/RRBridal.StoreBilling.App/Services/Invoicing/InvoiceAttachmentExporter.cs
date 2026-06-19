using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class InvoiceAttachmentExporter
{
    public static async Task<(byte[] Bytes, string MimeType, string FileName)> ExportThermalPngAsync(
        AppServices services,
        ThermalInvoiceInput input,
        CancellationToken ct = default)
    {
        var (profileOk, profileMsg) = await services.ReceiptConfigSync.EnsureProfileReadyForPrintAsync(ct);
        if (!profileOk)
            throw new InvalidOperationException(profileMsg);

        var assets = await ThermalReceiptDocumentBuilder.BuildAssetsAsync(
            services.ReceiptConfig.Current,
            input.BillNo,
            services.ReceiptLogoCache,
            ct);

        var text = ThermalInvoiceTextBuilder.Build(input);
        var fontSize = input.CharWidth >= 48 ? 9.0 : 10.0;
        var doc = BillPrintService.CreateReceiptDocument(text, assets, fontSize);
        var fileName = SanitizeFileName($"{input.BillNo}.png");

        byte[] bytes = await Application.Current.Dispatcher.InvokeAsync(() => RenderDocumentToPng(doc));
        return (bytes, "image/png", fileName);
    }

    internal static byte[] RenderDocumentToPng(FlowDocument document)
    {
        const double widthPx = 80.0 / 25.4 * 96.0;
        document.PageWidth = widthPx;
        document.ColumnWidth = widthPx;
        document.PagePadding = new Thickness(8);
        document.PageHeight = double.NaN;

        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.PageSize = new Size(widthPx, 2400);

        var page = paginator.GetPage(0);
        var visual = page.Visual;
        var size = page.Size;

        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(size.Width)),
            Math.Max(1, (int)Math.Ceiling(size.Height)),
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "bill.png" : name;
    }
}
