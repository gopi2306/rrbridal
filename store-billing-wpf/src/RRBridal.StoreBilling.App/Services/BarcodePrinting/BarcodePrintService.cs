using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services;

namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

public sealed class BarcodePrintService
{
    private readonly BarcodeLabelDesignStore _designStore;

    public BarcodePrintService(BarcodeLabelDesignStore designStore)
    {
        _designStore = designStore;
    }

    public (bool Ok, string Message) PrintLabels(
        IEnumerable<BarcodePrintLineItem> lines,
        string companyName,
        string printerQueueName)
    {
        var printable = lines.Where(l => !l.IsDraftRow && l.PrintQty > 0).ToList();
        if (printable.Count == 0)
            return (false, "Enter quantity on at least one line before printing.");

        if (BarcodePrinterFilter.IsVirtualOrPdfQueue(printerQueueName))
            return (false, $"Cannot print labels to a PDF or virtual queue. Select your {BarcodePrinterPreferences.RecommendedModelName} queue.");

        var design = _designStore.ResolveDesign();
        var language = BarcodePrinterPreferences.ResolveLanguage(printerQueueName);
        var payload = BarcodeLabelCommandBuilder.BuildBatch(printable, companyName, language, design);
        if (string.IsNullOrWhiteSpace(payload))
            return (false, "No label data to print.");

        if (UiAutomationSimulation.IsEnabled)
        {
            UiAutomationSimulation.RecordJson(
                "print-jobs",
                "barcode-label-print-job",
                $"{companyName}|{printerQueueName}|{payload}",
                new
                {
                    jobName = "RR Bridal barcode labels",
                    printerKind = "BarcodeLabel",
                    requestedQueue = printerQueueName,
                    commandLanguage = language.ToString(),
                    design = design.Name,
                    content = payload,
                });
            var simulatedCount = printable.Sum(l => (int)Math.Ceiling(l.PrintQty));
            return (true, $"Simulated {simulatedCount} label(s) for {printerQueueName}.");
        }

        if (!RawPrinterHelper.SendStringToPrinter(printerQueueName, payload, out var error))
            return (false, error ?? "Print failed.");

        var labelCount = printable.Sum(l => (int)Math.Ceiling(l.PrintQty));
        var langHint = BarcodePrinterPreferences.LanguageHint(language);
        return (true, $"Sent {labelCount} label(s) to {printerQueueName} ({langHint}, {design.Name}).");
    }
}
