using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Ui;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class OutboundDispatchPrintFlow
{
    public static Task<bool> PreviewParcelA4Async(AppServices services, BsonDocument dispatch) =>
        PreviewAsync(services, PrintArtifact.Parcel, PrintMedium.A4, new[] { dispatch });

    public static Task<bool> PreviewParcelThermalAsync(AppServices services, BsonDocument dispatch) =>
        PreviewAsync(services, PrintArtifact.Parcel, PrintMedium.Thermal, new[] { dispatch });

    public static Task<bool> PreviewBatchA4Async(
        AppServices services,
        IEnumerable<BsonDocument> dispatches) =>
        PreviewAsync(services, PrintArtifact.Batch, PrintMedium.A4, dispatches);

    public static Task<bool> PreviewBatchThermalAsync(
        AppServices services,
        IEnumerable<BsonDocument> dispatches) =>
        PreviewAsync(services, PrintArtifact.Batch, PrintMedium.Thermal, dispatches);

    public static Task<bool> PreviewChargeReceiptA4Async(AppServices services, BsonDocument dispatch) =>
        PreviewAsync(services, PrintArtifact.ChargeReceipt, PrintMedium.A4, new[] { dispatch });

    public static Task<bool> PreviewChargeReceiptThermalAsync(AppServices services, BsonDocument dispatch) =>
        PreviewAsync(services, PrintArtifact.ChargeReceipt, PrintMedium.Thermal, new[] { dispatch });

    public static Task<bool> PrintParcelA4Async(AppServices services, BsonDocument dispatch) =>
        PrintAsync(services, PrintArtifact.Parcel, PrintMedium.A4, new[] { dispatch });

    public static Task<bool> PrintParcelThermalAsync(AppServices services, BsonDocument dispatch) =>
        PrintAsync(services, PrintArtifact.Parcel, PrintMedium.Thermal, new[] { dispatch });

    public static Task<bool> PrintBatchA4Async(
        AppServices services,
        IEnumerable<BsonDocument> dispatches) =>
        PrintAsync(services, PrintArtifact.Batch, PrintMedium.A4, dispatches);

    public static Task<bool> PrintBatchThermalAsync(
        AppServices services,
        IEnumerable<BsonDocument> dispatches) =>
        PrintAsync(services, PrintArtifact.Batch, PrintMedium.Thermal, dispatches);

    public static Task<bool> PrintChargeReceiptA4Async(AppServices services, BsonDocument dispatch) =>
        PrintAsync(services, PrintArtifact.ChargeReceipt, PrintMedium.A4, new[] { dispatch });

    public static Task<bool> PrintChargeReceiptThermalAsync(AppServices services, BsonDocument dispatch) =>
        PrintAsync(services, PrintArtifact.ChargeReceipt, PrintMedium.Thermal, new[] { dispatch });

    private static async Task<bool> PreviewAsync(
        AppServices services,
        PrintArtifact artifact,
        PrintMedium medium,
        IEnumerable<BsonDocument> documents)
    {
        try
        {
            var rendered = await RenderAsync(services, artifact, medium, documents);
            if (rendered == null)
                return false;
            var preview = new InvoicePrintPreviewWindow(
                services,
                rendered.Document,
                rendered.Title,
                printInvoiceEnabled: true,
                forceThermalPrinter: medium == PrintMedium.Thermal,
                printerKindOverride: rendered.PrinterKind)
            {
                Owner = Application.Current?.MainWindow,
                Title = $"{rendered.Title} ({medium.ToString().ToLowerInvariant()})",
                Width = medium == PrintMedium.A4 ? 760 : 430,
                Height = medium == PrintMedium.A4 ? 840 : 620,
            };
            preview.ShowDialog();
            return preview.PrintSucceeded;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return false;
        }
    }

    private static async Task<bool> PrintAsync(
        AppServices services,
        PrintArtifact artifact,
        PrintMedium medium,
        IEnumerable<BsonDocument> documents)
    {
        try
        {
            var rendered = await RenderAsync(services, artifact, medium, documents);
            return rendered != null && BillPrintService.PrintDocument(
                Application.Current?.MainWindow,
                rendered.Document,
                services.ReceiptConfig.Current.Print,
                rendered.Title,
                rendered.PrinterKind);
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return false;
        }
    }

    private static async Task<RenderedArtifact?> RenderAsync(
        AppServices services,
        PrintArtifact artifact,
        PrintMedium medium,
        IEnumerable<BsonDocument> source)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(source);
        services.CentralAuthSession.ApplyTo(services.CentralApi);
        var (ready, message) = await services.ReceiptConfigSync.EnsureProfileReadyForPrintAsync();
        if (!ready)
        {
            AppDialog.Show(message, "Receipt settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        var documents = source.ToList();
        if (documents.Count == 0)
            throw new ArgumentException("At least one outbound dispatch is required.", nameof(source));
        var config = services.ReceiptConfig.Current;
        var width = config.Print.ReceiptCharWidth is >= 32 and <= 56
            ? config.Print.ReceiptCharWidth
            : 48;

        FlowDocument document;
        string title;
        switch (artifact)
        {
            case PrintArtifact.Parcel:
            {
                var input = OutboundDispatchPrintMapper.FromDocument(documents[0], config.Store, width);
                title = $"Parcel note {input.DispatchNo}";
                document = medium == PrintMedium.A4
                    ? OutboundDispatchA4DocumentBuilder.CreateParcelNote(input)
                    : Thermal(OutboundDispatchThermalTextBuilder.BuildParcelNote(input), width);
                break;
            }
            case PrintArtifact.Batch:
            {
                var input = OutboundDispatchPrintMapper.ToBatch(documents, config.Store, width);
                title = $"Dispatch batch {input.BatchNo}";
                document = medium == PrintMedium.A4
                    ? OutboundDispatchA4DocumentBuilder.CreateBatchSummary(input)
                    : Thermal(OutboundDispatchThermalTextBuilder.BuildBatchSummary(input), width);
                break;
            }
            case PrintArtifact.ChargeReceipt:
            {
                var input = OutboundDispatchPrintMapper.ToChargeReceipt(documents[0], config.Store, width);
                title = $"Dispatch charge receipt {input.ReceiptNo}";
                document = medium == PrintMedium.A4
                    ? OutboundDispatchA4DocumentBuilder.CreateChargeReceipt(input)
                    : Thermal(OutboundDispatchThermalTextBuilder.BuildChargeReceipt(input), width);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(artifact));
        }

        return new RenderedArtifact(
            document,
            title,
            medium == PrintMedium.A4 ? BillPrinterKind.OfficeInvoice : BillPrinterKind.Thermal);
    }

    private static FlowDocument Thermal(string text, int width) =>
        BillPrintService.CreateReceiptDocument(text, assets: null, width >= 48 ? 9.0 : 10.0);

    private static void ShowError(Exception exception) =>
        AppDialog.Show(
            $"Could not prepare outbound dispatch print:\n{exception.Message}",
            "Outbound dispatch print",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

    private enum PrintArtifact
    {
        Parcel,
        Batch,
        ChargeReceipt,
    }

    private enum PrintMedium
    {
        A4,
        Thermal,
    }

    private sealed record RenderedArtifact(
        FlowDocument Document,
        string Title,
        BillPrinterKind PrinterKind);
}
