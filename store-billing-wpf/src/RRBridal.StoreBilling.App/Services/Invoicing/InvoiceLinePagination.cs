using System;
using System.Collections.Generic;
using System.Linq;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Splits invoice line items into pages.</summary>
public static class InvoiceLinePagination
{
    public const int MinLinesPerPage = 1;
    public const int MaxLinesPerPage = 28;

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

    /// <summary>
    /// Prefers a single page when line count fits with the totals footer
    /// (<paramref name="lastPageMax"/>). Only paginates when items exceed that —
    /// then fills each prior page to <paramref name="pageMax"/> and keeps the footer
    /// on the last page (possibly a footer-only page when the prior page holds all items).
    /// </summary>
    public static List<IReadOnlyList<InvoiceLineSnap>> ChunkLinesFillThenFooterPage(
        IReadOnlyList<InvoiceLineSnap> lines,
        int pageMax,
        int lastPageMax)
    {
        pageMax = ClampLinesPerPage(pageMax);
        lastPageMax = ClampLinesPerPage(Math.Min(lastPageMax, pageMax));

        if (lines.Count == 0)
            return new List<IReadOnlyList<InvoiceLineSnap>> { Array.Empty<InvoiceLineSnap>() };

        // Default: one page with header + lines + footer.
        if (lines.Count <= lastPageMax)
            return new List<IReadOnlyList<InvoiceLineSnap>> { lines.ToList() };

        var chunks = new List<IReadOnlyList<InvoiceLineSnap>>();
        var index = 0;
        while (index < lines.Count)
        {
            var remaining = lines.Count - index;

            if (remaining <= lastPageMax)
            {
                chunks.Add(lines.Skip(index).Take(remaining).ToList());
                break;
            }

            // Items no longer fit with footer on this page: fill with items, footer on a later page.
            if (remaining <= pageMax)
            {
                chunks.Add(lines.Skip(index).Take(remaining).ToList());
                chunks.Add(Array.Empty<InvoiceLineSnap>());
                break;
            }

            chunks.Add(lines.Skip(index).Take(pageMax).ToList());
            index += pageMax;
        }

        return chunks;
    }
}
