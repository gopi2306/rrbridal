namespace RRBridal.StoreBilling.App.Services.Inventory;

public sealed class InventoryGridRow
{
    public string Sku { get; set; } = "";

    public string? UpcEanCode { get; set; }

    public string Product { get; set; } = "";

    public decimal WarehouseQty { get; set; }

    public decimal InTransitQty { get; set; }

    public decimal StoreQty { get; set; }

    public decimal? Mrp { get; set; }

    public decimal? StorePrice { get; set; }
}
