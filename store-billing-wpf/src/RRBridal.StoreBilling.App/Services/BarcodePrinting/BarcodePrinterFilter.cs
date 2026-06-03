namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

/// <summary>
/// Barcode jobs use RAW EPL — virtual PDF/XPS queues produce corrupt .pdf files, not printable labels.
/// </summary>
public static class BarcodePrinterFilter
{
    private static readonly string[] BlockedFragments =
    [
        "print to pdf",
        "microsoft print to pdf",
        "save as pdf",
        "adobe pdf",
        "pdfwriter",
        "xps document",
        "xps writer",
        "onenote",
        "send to onenote",
        "fax",
    ];

    public static bool IsVirtualOrPdfQueue(string? printerFullName)
    {
        if (string.IsNullOrWhiteSpace(printerFullName))
            return false;

        var n = printerFullName.Trim();
        foreach (var fragment in BlockedFragments)
        {
            if (n.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return n.Contains(" PDF", StringComparison.OrdinalIgnoreCase)
               || n.EndsWith(" PDF", StringComparison.OrdinalIgnoreCase)
               || n.Contains("(PDF)", StringComparison.OrdinalIgnoreCase);
    }

    public static string VirtualPrinterWarning =>
        "This queue saves RAW EPL as a file (e.g. \"RR Bridal barcode.pdf\"), which is not a real PDF and will not open in a browser.\n\n" +
        "Select your physical label printer (TVS LP 46 NEO). Use the on-screen preview above, or \"Save label file\" for debugging.";
}
