namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

/// <summary>Truncation for ~38 mm single sticker (2 labels per row on roll).</summary>
public static class BarcodeLabelTextLayout
{
    public const int CompanyMaxChars = 14;
    public const int ItemSingleLineMaxChars = 16;
    public const int BarcodeMaxChars = 13;

    public static string TruncateCompany(string? value) =>
        Truncate(value, CompanyMaxChars);

    public static string TruncateItemSingleLine(string? value) =>
        Truncate(value, ItemSingleLineMaxChars);

    public static string TruncateBarcode(string? value) =>
        Truncate(value, BarcodeMaxChars);

    private static string Truncate(string? value, int max)
    {
        var s = (value ?? "").Trim();
        return s.Length <= max ? s : s[..max];
    }
}
