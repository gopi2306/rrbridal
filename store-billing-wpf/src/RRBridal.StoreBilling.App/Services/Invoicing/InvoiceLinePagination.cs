using System;
using System.Collections.Generic;
using System.Linq;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Splits invoice line items into A5 pages.</summary>
public static class InvoiceLinePagination
{
    public const int MinLinesPerPage = 1;
    public const int MaxLinesPerPage = 24;

    public static IReadOnlyList<InvoiceLineSnap> ActiveLines(ThermalInvoiceInput input) =>
        input.Lines.Where(l => l.Amount > 0 || l.TaxableAmount > 0).ToList();

    public static int ClampLinesPerPage(int perPage)
    {
        if (perPage < MinLinesPerPage) return MinLinesPerPage;
        if (perPage > MaxLinesPerPage) return MaxLinesPerPage;
        return perPage;
    }

    public static List<IReadOnlyList<InvoiceLineSnap>> ChunkLines(IReadOnlyList<InvoiceLineSnap> lines, int perPage)
    {
        perPage = ClampLinesPerPage(perPage);
        if (lines.Count == 0)
            return new List<IReadOnlyList<InvoiceLineSnap>> { Array.Empty<InvoiceLineSnap>() };

        var chunks = new List<IReadOnlyList<InvoiceLineSnap>>();
        for (var i = 0; i < lines.Count; i += perPage)
        {
            var take = Math.Min(perPage, lines.Count - i);
            chunks.Add(lines.Skip(i).Take(take).ToList());
        }

        return chunks;
    }
}
