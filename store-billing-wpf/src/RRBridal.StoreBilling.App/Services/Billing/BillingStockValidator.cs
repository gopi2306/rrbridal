using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services.Products;

namespace RRBridal.StoreBilling.App.Services.Billing;

public sealed record StockShortLine(
    string Sku,
    string Description,
    decimal Requested,
    decimal Available);

public static class BillingStockValidator
{
    public static async Task<IReadOnlyList<StockShortLine>> FindStockShortfallsAsync(
        ProductCatalogService catalog,
        IEnumerable<BillingLineItem> lines,
        CancellationToken ct = default)
    {
        var shorts = new List<StockShortLine>();
        foreach (var line in lines.Where(l => l.Amount > 0 && !string.IsNullOrWhiteSpace(l.ProductCode)))
        {
            var available = await catalog.GetAvailableStockAsync(line.CentralProductId, line.ProductCode, ct);
            if (available < line.Qty)
            {
                shorts.Add(new StockShortLine(
                    line.ProductCode.Trim(),
                    line.Description ?? "",
                    line.Qty,
                    available));
            }
        }

        return shorts;
    }

    public static BsonArray ToStockExceptionsBson(IReadOnlyList<StockShortLine> shorts)
    {
        var arr = new BsonArray();
        foreach (var s in shorts)
        {
            arr.Add(new BsonDocument
            {
                { "sku", s.Sku },
                { "description", s.Description },
                { "requestedQty", (double)s.Requested },
                { "availableQty", (double)s.Available },
                { "stockDecremented", false },
            });
        }

        return arr;
    }

    public static HashSet<string> ShortSkus(IReadOnlyList<StockShortLine> shorts) =>
        shorts.Select(s => s.Sku).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
