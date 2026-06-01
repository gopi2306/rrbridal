using System.Globalization;

namespace RRBridal.StoreBilling.App.Services.Billing;

/// <summary>Monetary amount precision (4 decimal places). Final bill payable uses whole rupee via round-off.</summary>
public static class MoneyMath
{
    public const int AmountDecimalPlaces = 4;

    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    public static decimal RoundAmount(decimal value) =>
        Math.Round(value, AmountDecimalPlaces, MidpointRounding.AwayFromZero);

    public static string FormatAmount(decimal value) =>
        RoundAmount(value).ToString("N4", InCulture);

    public static string FormatRupee(decimal value) =>
        "₹ " + FormatAmount(value);

    public static string FormatPayable(decimal value) =>
        "₹ " + value.ToString("N0", InCulture);
}
