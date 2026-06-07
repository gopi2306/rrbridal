using System.Globalization;
using System.Windows.Media;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Text rules for A5 pre-printed value-only invoices.</summary>
public static class A5PrePrintedText
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");

    public const string DefaultFontFamily = "Arial";

    public const string DefaultContinuedLabel = "Continued";

    public static string ResolveContinuedLabel(A5PrePrintedLayoutSettings? settings)
    {
        var label = settings?.ContinuedLabel?.Trim();
        return string.IsNullOrEmpty(label) ? DefaultContinuedLabel : label;
    }

    public static string FormatTotalQty(decimal totalQty) =>
        totalQty.ToString("0.###", In);

    /// <summary>Discount name column: fixed label + actual percent, e.g. "Discount 10%".</summary>
    public static string FormatDiscountName(decimal percent)
    {
        if (percent <= 0)
            return "Discount";
        return $"Discount {percent.ToString("0.##", In)}%";
    }

    /// <summary>Discount amount column: negative actual value, e.g. "-249.90".</summary>
    public static string FormatDiscountAmount(decimal manualDiscountAmount)
    {
        if (manualDiscountAmount <= 0)
            return "";
        return (-manualDiscountAmount).ToString("0.00", In);
    }

    /// <summary>First N characters of customer name, then "..." if longer.</summary>
    public static string FormatBillTo(string? customerName, int maxChars = 15)
    {
        var s = (customerName ?? "").Trim();
        if (s.Length == 0)
            return "";
        if (maxChars < 1)
            maxChars = 1;
        if (s.Length <= maxChars)
            return s;
        return s.Substring(0, maxChars) + "...";
    }

    public static FontFamily ResolvePrintFont(string? fontFamilyName)
    {
        var name = string.IsNullOrWhiteSpace(fontFamilyName) ? DefaultFontFamily : fontFamilyName.Trim();
        try
        {
            return new FontFamily(name);
        }
        catch
        {
            return new FontFamily(DefaultFontFamily);
        }
    }
}
