namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

public enum BarcodePrinterLanguage
{
    /// <summary>EPL — Eltron-style (some desktop printers).</summary>
    Epl,

    /// <summary>TSPL — TVS LP 46 NEO / LP 46 family native language.</summary>
    Tspl,
}

/// <summary>Detects TVS LP 46 NEO and picks the best Windows print queue.</summary>
public static class BarcodePrinterPreferences
{
    public const string RecommendedModelName = "TVS LP 46 NEO";

    private static readonly string[] TvsLp46Fragments =
    [
        "lp 46 neo",
        "lp46 neo",
        "lp-46-neo",
        "lp 46",
        "lp46",
        "lp-46",
        "tvs lp",
        "lp 46 plus",
        "lp46 trio",
    ];

    private static readonly string[] EplDesktopFragments =
    [
        "lp346",
        "lp 346",
        "eltron",
    ];

    public static BarcodePrinterLanguage ResolveLanguage(string? printerQueueName)
    {
        if (string.IsNullOrWhiteSpace(printerQueueName))
            return BarcodePrinterLanguage.Tspl;

        var n = printerQueueName.Trim();
        if (MatchesAny(n, EplDesktopFragments) && !MatchesAny(n, TvsLp46Fragments))
            return BarcodePrinterLanguage.Epl;

        if (MatchesAny(n, TvsLp46Fragments))
            return BarcodePrinterLanguage.Tspl;

        // Default for Indian store label printers (TVS) when name is ambiguous
        return BarcodePrinterLanguage.Tspl;
    }

    public static int ScoreQueue(string queueName)
    {
        if (BarcodePrinterFilter.IsVirtualOrPdfQueue(queueName))
            return -1000;

        var n = queueName.Trim();
        var score = 0;

        if (n.Contains("neo", StringComparison.OrdinalIgnoreCase))
            score += 120;
        if (n.Contains("lp 46", StringComparison.OrdinalIgnoreCase) || n.Contains("lp46", StringComparison.OrdinalIgnoreCase))
            score += 100;
        if (n.Contains("tvs", StringComparison.OrdinalIgnoreCase))
            score += 80;
        if (n.Contains("barcode", StringComparison.OrdinalIgnoreCase) || n.Contains("label", StringComparison.OrdinalIgnoreCase))
            score += 40;
        if (MatchesAny(n, EplDesktopFragments))
            score += 30;

        // Deprioritize bill thermal printers
        if (n.Contains("rp 32", StringComparison.OrdinalIgnoreCase) || n.Contains("rp32", StringComparison.OrdinalIgnoreCase))
            score -= 50;
        if (n.Contains("receipt", StringComparison.OrdinalIgnoreCase) || n.Contains("thermal", StringComparison.OrdinalIgnoreCase))
            score -= 20;

        return score;
    }

    public static string? PickPreferredQueue(IEnumerable<string> queueNames)
    {
        return queueNames
            .Where(q => !BarcodePrinterFilter.IsVirtualOrPdfQueue(q))
            .OrderByDescending(ScoreQueue)
            .ThenBy(q => q, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static string LanguageHint(BarcodePrinterLanguage language) =>
        language == BarcodePrinterLanguage.Tspl
            ? "TSPL (TVS LP 46 NEO)"
            : "EPL";

    private static bool MatchesAny(string name, IEnumerable<string> fragments) =>
        fragments.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase));
}
