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
    public decimal? MrpWithoutGst { get; init; }
    public decimal? SellingPriceWithoutGst { get; init; }
    public decimal? StorePriceWithoutGst { get; init; }
    public decimal? GstPercent { get; init; }

    public string? HsnSac { get; init; }

    public string? CategoryId { get; init; }
    public string? BrandId { get; init; }
    public string? OfferGroupId { get; init; }

    public decimal StockQty { get; init; }

    public IReadOnlyList<ProductMediaItem> MediaItems { get; init; } = System.Array.Empty<ProductMediaItem>();

    /// <summary>First non-empty image description, if any.</summary>
    public string? PrimaryImageDescription =>
        MediaItems.Select(m => m.Description).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));

    public decimal SuggestedRate => SellingPrice ?? StorePrice ?? Mrp ?? 0m;

    /// <summary>Ex-GST unit rate from master/derived fields (display/reporting; billing Without GST uses <see cref="SuggestedRate"/>).</summary>
    public decimal SuggestedExclusiveRate
    {
        get
        {
            var fromMaster = SellingPriceWithoutGst ?? StorePriceWithoutGst ?? MrpWithoutGst;
            if (fromMaster is > 0)
                return fromMaster.Value;

            var inclusive = SuggestedRate;
            if (inclusive <= 0)
                return 0m;
            var gst = SuggestedTaxPercent;
            if (gst <= 0)
                return inclusive;
            return System.Math.Round(inclusive / (1m + gst / 100m), 4, System.MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>
    /// GST % for billing. Uses product gstPercent when &gt; 0; otherwise infers from
    /// inclusive vs ex-GST master prices (common when withoutGst fields exist but gst% is 0).
    /// Null gstPercent with no price gap defaults to 18%.
    /// </summary>
    public decimal SuggestedTaxPercent
    {
        get
        {
            if (GstPercent is > 0)
                return GstPercent.Value;

            var inferred = InferGstPercentFromPrices();
            if (inferred > 0)
                return inferred;

            return GstPercent is null ? 18m : 0m;
        }
    }

    /// <summary>
    /// Effective customer unit price: inclusive when With GST;
    /// selling price + GST when Without GST (Rate = SuggestedRate as taxable base).
    /// </summary>
    public decimal SuggestedPayableUnitRate(bool pricesExcludeGst) =>
        pricesExcludeGst
            ? System.Math.Round(
                SuggestedRate * (1m + SuggestedTaxPercent / 100m),
                4,
                System.MidpointRounding.AwayFromZero)
            : SuggestedRate;

    private decimal InferGstPercentFromPrices()
    {
        var inclusive = SellingPrice ?? StorePrice ?? Mrp ?? 0m;
        var exclusive = SellingPriceWithoutGst ?? StorePriceWithoutGst ?? MrpWithoutGst ?? 0m;
        if (inclusive <= 0 || exclusive <= 0 || inclusive <= exclusive)
            return 0m;

        var pct = System.Math.Round((inclusive / exclusive - 1m) * 100m, 2);
        return pct is > 0 and <= 40 ? pct : 0m;
    }

    public string DisplayLine => $"{Sku} — {Name} — ₹{SuggestedRate:N2} ({SuggestedTaxPercent:N0}% GST)";
}
