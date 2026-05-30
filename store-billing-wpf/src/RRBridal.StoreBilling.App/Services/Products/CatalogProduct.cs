namespace RRBridal.StoreBilling.App.Services.Products;

public sealed class CatalogProduct
{
    public required string CentralId { get; init; }
    public required string Sku { get; init; }
    public required string Name { get; init; }
    public decimal? CostPrice { get; init; }
    public decimal? MarginPercent { get; init; }
    public decimal? Mrp { get; init; }
    public decimal? SellingPrice { get; init; }
    public decimal? StorePrice { get; init; }
    public decimal? GstPercent { get; init; }

    public string? HsnSac { get; init; }

    public decimal StockQty { get; init; }

    public decimal SuggestedRate => SellingPrice ?? StorePrice ?? Mrp ?? 0m;

    public decimal SuggestedTaxPercent => GstPercent ?? 18m;

    public string DisplayLine => $"{Sku} — {Name} — ₹{SuggestedRate:N2} ({SuggestedTaxPercent:N0}% GST)";
}
