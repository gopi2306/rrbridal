using System;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class PrinterQueueResolver
{
    public static string? ResolveFullName(string? queueNameHint, string? printerModelHint)
    {
        try
        {
            var all = InstalledPrinterDiscovery.ListAll();
            string? partial = null;

            foreach (var option in all)
            {
                if (!string.IsNullOrWhiteSpace(queueNameHint)
                    && string.Equals(option.FullName, queueNameHint.Trim(), StringComparison.OrdinalIgnoreCase))
                    return option.FullName;
            }

            foreach (var option in all)
            {
                if (!string.IsNullOrWhiteSpace(queueNameHint)
                    && MatchesHint(option, queueNameHint))
                {
                    partial ??= option.FullName;
                    break;
                }

                if (!string.IsNullOrWhiteSpace(printerModelHint)
                    && MatchesHint(option, printerModelHint))
                    return option.FullName;
            }

            return partial;
        }
        catch
        {
            return null;
        }
    }

    private static bool MatchesHint(PrinterOption option, string hint)
    {
        var h = hint.Trim();
        return option.FullName.Contains(h, StringComparison.OrdinalIgnoreCase)
            || option.Display.Contains(h, StringComparison.OrdinalIgnoreCase);
    }
}
