using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Printing;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>
/// Lists installed Windows printers including network connections (e.g. \\pos\Canon…)
/// that may be missing from a plain <see cref="LocalPrintServer.GetPrintQueues()"/> call.
/// </summary>
public static class InstalledPrinterDiscovery
{
    private static readonly EnumeratedPrintQueueTypes[] AllQueueTypes =
    [
        EnumeratedPrintQueueTypes.Local,
        EnumeratedPrintQueueTypes.Connections,
        EnumeratedPrintQueueTypes.Shared,
    ];

    public static IReadOnlyList<PrinterOption> ListAll()
    {
        var map = new Dictionary<string, PrinterOption>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var server = new LocalPrintServer();
            foreach (PrintQueue pq in server.GetPrintQueues(AllQueueTypes))
            {
                var full = (pq.FullName ?? pq.Name ?? "").Trim();
                if (full.Length == 0)
                    continue;
                map[full] = new PrinterOption
                {
                    Display = FormatDisplay(pq.Name, full),
                    FullName = full,
                };
            }
        }
        catch
        {
            // fall back to InstalledPrinters only
        }

        try
        {
            foreach (string installed in PrinterSettings.InstalledPrinters)
            {
                var full = installed.Trim();
                if (full.Length == 0 || map.ContainsKey(full))
                    continue;
                map[full] = new PrinterOption
                {
                    Display = FormatDisplay(ExtractShortName(full), full),
                    FullName = full,
                };
            }
        }
        catch
        {
            // ignore
        }

        return map.Values
            .OrderBy(o => o.Display, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static PrintQueue? TryGetPrintQueue(string? printerFullName)
    {
        if (string.IsNullOrWhiteSpace(printerFullName))
            return null;

        var target = printerFullName.Trim();
        try
        {
            using var server = new LocalPrintServer();
            try
            {
                return server.GetPrintQueue(target);
            }
            catch
            {
                // match by full or short name
            }

            foreach (PrintQueue pq in server.GetPrintQueues(AllQueueTypes))
            {
                if (string.Equals(pq.FullName, target, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pq.Name, target, StringComparison.OrdinalIgnoreCase))
                    return pq;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string FormatDisplay(string? shortName, string fullName)
    {
        if (fullName.StartsWith(@"\\", StringComparison.Ordinal))
            return fullName;

        var name = shortName?.Trim();
        return string.IsNullOrEmpty(name) ? fullName : name;
    }

    private static string ExtractShortName(string fullName)
    {
        if (!fullName.StartsWith(@"\\", StringComparison.Ordinal))
            return fullName;

        var lastSlash = fullName.LastIndexOf('\\');
        return lastSlash >= 0 && lastSlash < fullName.Length - 1
            ? fullName[(lastSlash + 1)..]
            : fullName;
    }
}
