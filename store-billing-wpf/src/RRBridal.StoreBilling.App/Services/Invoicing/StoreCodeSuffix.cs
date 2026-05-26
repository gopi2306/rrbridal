using System.Linq;
using System.Text.RegularExpressions;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class StoreCodeSuffix
{
    private static readonly Regex TrailingDigits = new(@"\d+", RegexOptions.Compiled);

    /// <summary>Last 3-digit store segment for document numbers (e.g. store-001 → 001).</summary>
    public static string FormatLast3(string storeId)
    {
        var s = (storeId ?? "").Trim();
        if (s.Length == 0) return "000";

        var matches = TrailingDigits.Matches(s);
        if (matches.Count > 0)
        {
            var digits = matches[^1].Value;
            if (digits.Length >= 3) return digits[^3..];
            return digits.PadLeft(3, '0');
        }

        if (s.Length >= 3) return s[^3..];
        return s.PadLeft(3, '0');
    }
}
