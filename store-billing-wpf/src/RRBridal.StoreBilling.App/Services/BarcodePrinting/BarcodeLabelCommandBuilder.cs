using RRBridal.StoreBilling.App.Models;

namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

public static class BarcodeLabelCommandBuilder
{
    public static string BuildBatch(
        IEnumerable<BarcodePrintLineItem> lines,
        string companyName,
        BarcodePrinterLanguage language)
    {
        return language == BarcodePrinterLanguage.Tspl
            ? TsplBarcodeLabelBuilder.BuildBatch(lines, companyName)
            : EplBarcodeLabelBuilder.BuildBatch(lines, companyName);
    }

    public static string BuildBatch(
        IEnumerable<BarcodePrintLineItem> lines,
        string companyName,
        string printerQueueName) =>
        BuildBatch(lines, companyName, BarcodePrinterPreferences.ResolveLanguage(printerQueueName));
}
