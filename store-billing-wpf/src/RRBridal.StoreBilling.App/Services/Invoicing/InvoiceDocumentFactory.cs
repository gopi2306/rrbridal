using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>
/// Builds the same invoice FlowDocuments used for print, for WhatsApp PDF export and shared print paths.
/// WhatsApp always uses full A4/A5 (never pre-printed stationery).
/// </summary>
public static class InvoiceDocumentFactory
{
    public static async Task<(FlowDocument Document, string ThermalText)> CreateAsync(
        AppServices services,
        ThermalInvoiceInput input,
        InvoicePrintFormat format,
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
        var doc = CreateFullDocument(input, assets, format, text);
        return (doc, text);
    }

    /// <summary>Full invoice layout for the format (no pre-printed blank forms).</summary>
    public static FlowDocument CreateFullDocument(
        ThermalInvoiceInput input,
        ThermalReceiptAssets assets,
        InvoicePrintFormat format,
        string? thermalText = null)
    {
        if (format == InvoicePrintFormat.Thermal)
        {
            var text = thermalText ?? ThermalInvoiceTextBuilder.Build(input);
            var fontSize = input.CharWidth >= 48 ? 9.0 : 10.0;
            return BillPrintService.CreateReceiptDocument(text, assets, fontSize);
        }

        if (format == InvoicePrintFormat.A4Commercial)
            return CommercialA4InvoiceDocumentBuilder.Create(input);

        if (format == InvoicePrintFormat.A4TaxInvoice)
            return TaxInvoiceA4DocumentBuilder.Create(input);

        var (pageW, pageH) = format == InvoicePrintFormat.A5
            ? (148.0, 210.0)
            : (210.0, 297.0);
        return A4InvoiceDocumentBuilder.Create(input, assets, pageW, pageH, linesPerPage: 10, a5Layout: null);
    }
}
