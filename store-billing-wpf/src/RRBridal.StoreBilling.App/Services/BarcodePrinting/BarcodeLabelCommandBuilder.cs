using RRBridal.StoreBilling.App.Models;

namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

public static class BarcodeLabelCommandBuilder
{
    public static string BuildBatch(
        IEnumerable<BarcodePrintLineItem> lines,
        string companyName,
        BarcodePrinterLanguage language,
        BarcodeLabelDesignConfig design)
    {
        return language == BarcodePrinterLanguage.Tspl
            ? TsplBarcodeLabelBuilder.BuildBatch(lines, companyName, design)
            : EplBarcodeLabelBuilder.BuildBatch(lines, companyName, design);
    }

    public static string BuildBatch(
        IEnumerable<BarcodePrintLineItem> lines,
        string companyName,
        string printerQueueName,
        BarcodeLabelDesignConfig design) =>
        BuildBatch(lines, companyName, BarcodePrinterPreferences.ResolveLanguage(printerQueueName), design);
}
