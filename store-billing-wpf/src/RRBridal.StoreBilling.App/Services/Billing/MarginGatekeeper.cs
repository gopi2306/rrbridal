namespace RRBridal.StoreBilling.App.Services.Billing;

/// <summary>
/// Enforces minimum rate = cost × (1 + marginPercent/100). Warn-only; does not block billing.
/// </summary>
public static class MarginGatekeeper
{
    public static decimal? MinimumRate(decimal costPrice, decimal marginPercent)
    {
        if (costPrice <= 0 || marginPercent <= 0) return null;
        return MoneyMath.RoundAmount(costPrice * (1m + marginPercent / 100m));
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
            $"Rate {MoneyMath.FormatRupee(rate)} is below the minimum {MoneyMath.FormatRupee(floor.Value)} " +
            $"(cost {MoneyMath.FormatRupee(costPrice)} + {marginPercent:N1}% margin) for {productLabel}.";
        return true;
    }
}
