using System.Windows.Media;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Text rules for A5 pre-printed value-only invoices.</summary>
public static class A5PrePrintedText
{
    public const int BillToMaxChars = 15;

    public static readonly FontFamily PrintFont = new("Arial");

    /// <summary>First 15 characters of customer name, then "..." if longer.</summary>
    public static string FormatBillTo(string? customerName, int maxChars = BillToMaxChars)
    {
        var s = (customerName ?? "").Trim();
        if (s.Length == 0)
            return "";
        if (s.Length <= maxChars)
            return s;
        return s.Substring(0, maxChars) + "...";
    }
}
