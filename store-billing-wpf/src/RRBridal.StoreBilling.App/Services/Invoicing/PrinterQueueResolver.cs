using System;
using System.Printing;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class PrinterQueueResolver
{
    public static string? ResolveFullName(string? queueNameHint, string? printerModelHint)
    {
        try
        {
            using var server = new LocalPrintServer();
            PrintQueue? exact = null;
            PrintQueue? contains = null;

            foreach (PrintQueue pq in server.GetPrintQueues())
            {
                if (!string.IsNullOrWhiteSpace(queueNameHint)
                    && string.Equals(pq.FullName, queueNameHint, StringComparison.OrdinalIgnoreCase))
                {
                    exact = pq;
                    break;
                }

                if (!string.IsNullOrWhiteSpace(queueNameHint)
                    && pq.Name.Contains(queueNameHint, StringComparison.OrdinalIgnoreCase)
                    && contains == null)
                    contains = pq;

                if (!string.IsNullOrWhiteSpace(printerModelHint)
                    && pq.Name.Contains(printerModelHint, StringComparison.OrdinalIgnoreCase)
                    && contains == null)
                    contains = pq;
            }

            return (exact ?? contains)?.FullName;
        }
        catch
        {
            return null;
        }
    }
}
