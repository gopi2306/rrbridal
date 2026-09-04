using System;

namespace RRBridal.StoreBilling.App.Services.Billing;

/// <summary>Suggested card processing fee from Settings (% + flat) against a card base amount.</summary>
public static class CardChargeCalculator
{
    /// <summary>
    /// cardChargeDefault = Round(cardBase × percent/100) + (cardBase &gt; 0 ? flat : 0).
    /// Returns 0 when disabled or cardBase ≤ 0.
    /// </summary>
    public static decimal ComputeSuggested(
        decimal cardBase,
        bool enabled,
        decimal percent,
        decimal flatAmount)
    {
        if (!enabled || cardBase <= 0)
            return 0m;

        var pct = Math.Max(0m, percent);
        var flat = Math.Max(0m, flatAmount);
        var fromPercent = MoneyMath.RoundDisplayAmount(cardBase * (pct / 100m));
        return MoneyMath.RoundDisplayAmount(fromPercent + flat);
    }
}
