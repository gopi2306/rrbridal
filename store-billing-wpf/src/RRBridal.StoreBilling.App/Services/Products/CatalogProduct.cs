using System.Collections.Generic;
using System.Linq;

namespace RRBridal.StoreBilling.App.Services.Products;

public sealed class CatalogProduct
{
    public required string CentralId { get; init; }
    public required string Sku { get; init; }
    public string? UpcEanCode { get; init; }
    public required string Name { get; init; }
    public string? ShortName { get; init; }
    public string? Alias { get; init; }
    public decimal? CostPrice { get; init; }
    public decimal? MarginPercent { get; init; }
    public decimal? Mrp { get; init; }
    public decimal? SellingPrice { get; init; }
    public decimal? StorePrice { get; init; }
    public decimal? GstPercent { get; init; }

    public string? HsnSac { get; init; }

    public decimal StockQty { get; init; }

    public IReadOnlyList<ProductMediaItem> MediaItems { get; init; } = System.Array.Empty<ProductMediaItem>();

    /// <summary>First non-empty image description, if any.</summary>
    public string? PrimaryImageDescription =>
        MediaItems.Select(m => m.Description).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));

    public decimal SuggestedRate => SellingPrice ?? StorePrice ?? Mrp ?? 0m;

    public decimal SuggestedTaxPercent => GstPercent ?? 18m;

    public string DisplayLine => $"{Sku} — {Name} — ₹{SuggestedRate:N2} ({SuggestedTaxPercent:N0}% GST)";
}
