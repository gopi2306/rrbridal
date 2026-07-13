using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Products;

namespace RRBridal.StoreBilling.App.Models;

public partial class BarcodePrintLineItem : ObservableObject
{
    [ObservableProperty] private int _lineNo;
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _item = "";
    [ObservableProperty] private decimal _costPrice;
    [ObservableProperty] private decimal _mrp;
    [ObservableProperty] private decimal _sellingPrice;
    [ObservableProperty] private decimal _storePrice;
    [ObservableProperty] private decimal _gstPercent;
    [ObservableProperty] private decimal _printQty;
    [ObservableProperty] private bool _isDraftRow;

    public string Packing { get; set; } = "—";
    public string BatchNo { get; set; } = "—";
    public string PkdDate { get; set; } = "—";
    public string ExpDate { get; set; } = "—";
    public string? ShortName { get; set; }
    public string? Alias { get; set; }

    public string ItemName => (Item ?? "").Trim();
    public string SizeNote => string.IsNullOrWhiteSpace(Alias) ? "" : Alias.Trim();

    public decimal LabelPrice => StorePrice > 0 ? StorePrice : SellingPrice > 0 ? SellingPrice : Mrp;

    public string BarcodeValue =>
        !string.IsNullOrWhiteSpace(UpcEanCode) ? UpcEanCode.Trim() : Code.Trim();

    public string? UpcEanCode { get; set; }

    public static BarcodePrintLineItem FromCatalog(CatalogProduct p, int lineNo) =>
        new()
        {
            LineNo = lineNo,
            Code = p.Sku,
            Item = p.Name,
            ShortName = p.ShortName,
            Alias = p.Alias,
            CostPrice = p.CostPrice ?? 0m,
            Mrp = p.Mrp ?? 0m,
            SellingPrice = p.SellingPrice ?? 0m,
            StorePrice = p.StorePrice ?? 0m,
            GstPercent = p.GstPercent ?? 0m,
            PrintQty = 1m,
            IsDraftRow = false,
            UpcEanCode = string.IsNullOrWhiteSpace(p.UpcEanCode) ? null : p.UpcEanCode.Trim(),
        };

    public static BarcodePrintLineItem CreateDraftRow() =>
        new() { IsDraftRow = true };
}
