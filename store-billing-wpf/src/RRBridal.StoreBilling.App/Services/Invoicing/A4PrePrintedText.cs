using System;
using System.Globalization;
using System.Windows.Media;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Text rules for A4 Bilal Textiles pre-printed value-only invoices.</summary>
public static class A4PrePrintedText
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");

    public const string DefaultFontFamily = "Arial";
    public const string DefaultContinuedLabel = "Continued";

    public static string ResolveContinuedLabel(A4PrePrintedLayoutSettings? settings)
    {
        var label = settings?.ContinuedLabel?.Trim();
        return string.IsNullOrEmpty(label) ? DefaultContinuedLabel : label;
    }

    public static string FormatPercent(decimal percent)
    {
        if (percent <= 0)
            return "";
        return percent.ToString("0.##", In);
    }

    public static string FormatBillingAddress(string? customerName, string? customerPhone, int maxChars = 40)
    {
        var name = (customerName ?? "").Trim();
        var phone = (customerPhone ?? "").Trim();
        if (name.Length == 0 && phone.Length == 0)
            return "";
        if (phone.Length == 0)
            return Truncate(name, maxChars);
        if (name.Length == 0)
            return phone;
        var combined = $"{name}{Environment.NewLine}{phone}";
        return combined.Length <= maxChars ? combined : Truncate(name, maxChars);
    }

    public static string Truncate(string value, int maxChars)
    {
        if (maxChars < 1) maxChars = 1;
        if (value.Length <= maxChars) return value;
        return value.Substring(0, maxChars) + "...";
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
