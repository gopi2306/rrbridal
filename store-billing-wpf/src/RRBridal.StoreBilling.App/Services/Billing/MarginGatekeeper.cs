using System.Globalization;

namespace RRBridal.StoreBilling.App.Services.Billing;

/// <summary>
/// Enforces minimum rate = cost × (1 + marginPercent/100). Warn-only; does not block billing.
/// </summary>
public static class MarginGatekeeper
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    public static decimal? MinimumRate(decimal costPrice, decimal marginPercent)
    {
        if (costPrice <= 0 || marginPercent <= 0) return null;
        return Math.Round(costPrice * (1m + marginPercent / 100m), 2, MidpointRounding.AwayFromZero);
    }

    public static bool TryBuildWarning(
        string productLabel,
        decimal rate,
        decimal costPrice,
        decimal marginPercent,
        out string message)
    {
        message = "";
        var floor = MinimumRate(costPrice, marginPercent);
        if (!floor.HasValue || rate >= floor.Value) return false;

        message =
            $"Rate {FormatRupee(rate)} is below the minimum {FormatRupee(floor.Value)} " +
            $"(cost {FormatRupee(costPrice)} + {marginPercent:N1}% margin) for {productLabel}.";
        return true;
    }

    private static string FormatRupee(decimal value) => "₹ " + value.ToString("N2", InCulture);
}
