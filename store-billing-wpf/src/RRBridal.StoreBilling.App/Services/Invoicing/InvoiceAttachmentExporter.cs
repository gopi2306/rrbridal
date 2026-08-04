using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        var (doc, _) = await InvoiceDocumentFactory.CreateAsync(services, input, InvoicePrintFormat.Thermal, ct);
        var fileName = SanitizeFileName($"{input.BillNo}.png");
        byte[] bytes = await Application.Current.Dispatcher.InvokeAsync(() => RenderThermalToPng(doc));
        return (bytes, "image/png", fileName);
    }

    public static async Task<(byte[] Bytes, string MimeType, string FileName)> ExportInvoicePdfAsync(
        AppServices services,
        ThermalInvoiceInput input,
        InvoicePrintFormat invoiceFormat,
        CancellationToken ct = default)
    {
        var (doc, _) = await InvoiceDocumentFactory.CreateAsync(services, input, invoiceFormat, ct);
        var billLabel = string.IsNullOrWhiteSpace(input.BillNo) ? "bill" : input.BillNo.Trim();
        var fileName = SanitizeFileName($"Invoice_{billLabel}.pdf");

        byte[] bytes = await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var pages = RenderDocumentPagesToJpeg(doc, invoiceFormat == InvoicePrintFormat.Thermal);
            return InvoicePdfBuilder.FromJpegPages(pages);
        });
        return (bytes, "application/pdf", fileName);
    }

    /// <summary>
    /// When Central attachmentType is document: PDF of selected invoice format.
    /// When image: thermal PNG only (Meta image header).
    /// </summary>
    public static async Task<(byte[] Bytes, string MimeType, string FileName)> ExportInvoiceAttachmentAsync(
        AppServices services,
        ThermalInvoiceInput input,
        InvoicePrintFormat invoiceFormat,
        string attachmentType,
        CancellationToken ct = default)
    {
        if (string.Equals(attachmentType?.Trim(), "document", StringComparison.OrdinalIgnoreCase))
            return await ExportInvoicePdfAsync(services, input, invoiceFormat, ct);
        return await ExportThermalPngAsync(services, input, ct);
    }

    public static Task<(byte[] Bytes, string MimeType, string FileName)> ExportThermalAttachmentAsync(
        AppServices services,
        ThermalInvoiceInput input,
        string attachmentType,
        CancellationToken ct = default) =>
        ExportInvoiceAttachmentAsync(services, input, InvoicePrintFormat.Thermal, attachmentType, ct);

    internal static byte[] RenderDocumentToPng(FlowDocument document) => RenderThermalToPng(document);

    private static byte[] RenderThermalToPng(FlowDocument document)
    {
        var bitmap = RenderThermalBitmap(document);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static List<(byte[] Jpeg, int WidthPx, int HeightPx)> RenderDocumentPagesToJpeg(
        FlowDocument document,
        bool thermalTallPage)
    {
        var pages = new List<(byte[] Jpeg, int WidthPx, int HeightPx)>();
        if (thermalTallPage)
        {
            var bitmap = RenderThermalBitmap(document);
            pages.Add(EncodeJpeg(bitmap));
            return pages;
        }

        var pageWidth = document.PageWidth > 0 ? document.PageWidth : 210.0 / 25.4 * 96.0;
        var pageHeight = document.PageHeight > 0 ? document.PageHeight : 297.0 / 25.4 * 96.0;
        document.ColumnWidth = pageWidth;

        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.PageSize = new Size(pageWidth, pageHeight);

        // Force layout so PageCount is accurate.
        _ = paginator.GetPage(0);
        var count = Math.Max(1, paginator.PageCount);
        for (var i = 0; i < count; i++)
        {
            var page = paginator.GetPage(i);
            var size = page.Size;
            var bitmap = new RenderTargetBitmap(
                Math.Max(1, (int)Math.Ceiling(size.Width)),
                Math.Max(1, (int)Math.Ceiling(size.Height)),
                96,
                96,
                PixelFormats.Pbgra32);
            bitmap.Render(page.Visual);
            pages.Add(EncodeJpeg(bitmap));
        }

        return pages;
    }

    private static RenderTargetBitmap RenderThermalBitmap(FlowDocument document)
    {
        const double widthPx = 80.0 / 25.4 * 96.0;
        document.PageWidth = widthPx;
        document.ColumnWidth = widthPx;
        document.PagePadding = new Thickness(8);
        document.PageHeight = double.NaN;

        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.PageSize = new Size(widthPx, 2400);

        var page = paginator.GetPage(0);
        var size = page.Size;
        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(size.Width)),
            Math.Max(1, (int)Math.Ceiling(size.Height)),
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(page.Visual);
        return bitmap;
    }

    private static (byte[] Jpeg, int WidthPx, int HeightPx) EncodeJpeg(BitmapSource bitmap)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return (ms.ToArray(), bitmap.PixelWidth, bitmap.PixelHeight);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "bill.pdf" : name;
    }
}

/// <summary>Minimal PDF that embeds one or more JPEG page snapshots for WhatsApp document templates.</summary>
internal static class InvoicePdfBuilder
{
    public static byte[] FromJpeg(byte[] jpegBytes, int widthPx, int heightPx) =>
        FromJpegPages([(jpegBytes, widthPx, heightPx)]);

    public static byte[] FromJpegPages(IReadOnlyList<(byte[] Jpeg, int WidthPx, int HeightPx)> pages)
    {
        if (pages == null || pages.Count == 0)
            throw new ArgumentException("At least one PDF page is required.", nameof(pages));

        var objects = new List<(int Id, byte[] Body)>();
        // 1 Catalog, 2 Pages tree — page/image/content objects follow.
        objects.Add((1, Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>")));

        var pageObjectIds = new List<int>();
        var nextId = 3;
        foreach (var (jpeg, widthPx, heightPx) in pages)
        {
            var pageW = Math.Max(1.0, widthPx * 72.0 / 96.0);
            var pageH = Math.Max(1.0, heightPx * 72.0 / 96.0);
            var pageId = nextId++;
            var imageId = nextId++;
            var contentId = nextId++;
            pageObjectIds.Add(pageId);

            objects.Add((pageId, Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Fmt(pageW)} {Fmt(pageH)}] " +
                $"/Resources << /XObject << /Im0 {imageId} 0 R >> >> /Contents {contentId} 0 R >>")));

            var imgHeader = Encoding.ASCII.GetBytes(
                $"<< /Type /XObject /Subtype /Image /Width {widthPx} /Height {heightPx} " +
                $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
            var imgFooter = Encoding.ASCII.GetBytes("\nendstream");
            var imgBody = new byte[imgHeader.Length + jpeg.Length + imgFooter.Length];
            Buffer.BlockCopy(imgHeader, 0, imgBody, 0, imgHeader.Length);
            Buffer.BlockCopy(jpeg, 0, imgBody, imgHeader.Length, jpeg.Length);
            Buffer.BlockCopy(imgFooter, 0, imgBody, imgHeader.Length + jpeg.Length, imgFooter.Length);
            objects.Add((imageId, imgBody));

            var content = Encoding.ASCII.GetBytes($"q {Fmt(pageW)} 0 0 {Fmt(pageH)} 0 0 cm /Im0 Do Q");
            var contentObj = Encoding.ASCII.GetBytes($"<< /Length {content.Length} >>\nstream\n");
            var contentEnd = Encoding.ASCII.GetBytes("\nendstream");
            var contentBody = new byte[contentObj.Length + content.Length + contentEnd.Length];
            Buffer.BlockCopy(contentObj, 0, contentBody, 0, contentObj.Length);
            Buffer.BlockCopy(content, 0, contentBody, contentObj.Length, content.Length);
            Buffer.BlockCopy(contentEnd, 0, contentBody, contentObj.Length + content.Length, contentEnd.Length);
            objects.Add((contentId, contentBody));
        }

        var kids = string.Join(" ", pageObjectIds.ConvertAll(id => $"{id} 0 R"));
        objects.Insert(1, (2, Encoding.ASCII.GetBytes(
            $"<< /Type /Pages /Kids [{kids}] /Count {pageObjectIds.Count} >>")));

        // Re-sort by id because Insert shifted Pages to index 1 but ids must be unique and ordered for xref.
        objects.Sort((a, b) => a.Id.CompareTo(b.Id));

        using var ms = new MemoryStream();
        void WriteAscii(string s) => ms.Write(Encoding.ASCII.GetBytes(s));

        WriteAscii("%PDF-1.4\n");
        var maxId = objects[^1].Id;
        var offsets = new long[maxId + 1];
        foreach (var (id, body) in objects)
        {
            offsets[id] = ms.Position;
            WriteAscii($"{id} 0 obj\n");
            ms.Write(body);
            WriteAscii("\nendobj\n");
        }

        var xrefPos = ms.Position;
        WriteAscii($"xref\n0 {maxId + 1}\n");
        WriteAscii("0000000000 65535 f \n");
        for (var i = 1; i <= maxId; i++)
            WriteAscii($"{offsets[i]:D10} 00000 n \n");
        WriteAscii($"trailer\n<< /Size {maxId + 1} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF\n");
        return ms.ToArray();
    }

    private static string Fmt(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
