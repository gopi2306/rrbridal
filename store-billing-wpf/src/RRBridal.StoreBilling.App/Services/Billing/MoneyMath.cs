using System.Globalization;

namespace RRBridal.StoreBilling.App.Services.Billing;

/// <summary>
/// Internal monetary precision is 4 decimal places; WPF UI and thermal print display 2 decimal places (en-IN).
/// Final bill payable is still rounded to whole rupee in totals; display shows 2 dp (e.g. ₹ 1,250.00).
/// </summary>
public static class MoneyMath
{
    public const int AmountDecimalPlaces = 4;
    public const int DisplayDecimalPlaces = 2;

    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    public static decimal RoundAmount(decimal value) =>
        Math.Round(value, AmountDecimalPlaces, MidpointRounding.AwayFromZero);

    public static decimal RoundDisplayAmount(decimal value) =>
        Math.Round(value, DisplayDecimalPlaces, MidpointRounding.AwayFromZero);

    public static string FormatAmount(decimal value) =>
        RoundDisplayAmount(value).ToString("N2", InCulture);

    public static string FormatRupee(decimal value) =>
        "₹ " + FormatAmount(value);

    public static string FormatPayable(decimal value) =>
        "₹ " + RoundDisplayAmount(value).ToString("N2", InCulture);

    /// <summary>Invariant format for editable money TextBoxes (2 dp).</summary>
    public static string FormatEditableAmount(decimal value) =>
        value == 0 ? "" : RoundDisplayAmount(value).ToString("0.00", CultureInfo.InvariantCulture);
}
