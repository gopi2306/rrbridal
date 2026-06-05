using System.Windows.Media;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Text rules for A5 pre-printed value-only invoices.</summary>
public static class A5PrePrintedText
{
    public const string DefaultFontFamily = "Arial";

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
