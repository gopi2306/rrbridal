namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

public static class BarcodeLabelDesignDefaults
{
    public static BarcodeLabelDesignConfig LegacyBrandPrice() => new()
    {
        Name = "Legacy brand + price",
        LayoutStyle = "brand_price",
        PrinterProfileId = "tvs-lp46-neo",
        LabelWidthMm = 38,
        LabelHeightMm = 33,
        LabelsPerRow = 2,
        Dpi = 203,
        Fields = new BarcodeLabelFieldsConfig
        {
            ProductName = true,
            DesignSku = false,
            SellingPrice = true,
            SizeNote = false,
        },
        Text = new BarcodeLabelTextConfig
        {
            ProductNameSource = "itemName",
            PriceStyle = "decimal",
            Alignment = "left",
            BarcodeHumanText = "raw",
        },
        Barcode = new BarcodeLabelBarcodeConfig { HeightMm = 6.25, WidthMm = 24 },
        Decoration = "none",
    };

    public static BarcodeLabelDesignConfig RetailStackedDefault() => new()
    {
        Name = "Retail stacked (default)",
        LayoutStyle = "retail_stacked",
        PrinterProfileId = "tsc-ttp-244-pro",
        LabelWidthMm = 50,
        LabelHeightMm = 38,
        LabelsPerRow = 2,
        Dpi = 203,
        Fields = new BarcodeLabelFieldsConfig
        {
            ProductName = true,
            DesignSku = true,
            SellingPrice = true,
            SizeNote = true,
        },
        Text = new BarcodeLabelTextConfig
        {
            ProductNameSource = "itemName",
            DesignNoPrefix = "D.No:",
            PricePrefix = "Price ₹:",
            NotePrefix = "Note:",
            PriceStyle = "whole",
            BarcodeHumanText = "sku_spaced",
            Alignment = "center",
        },
        Barcode = new BarcodeLabelBarcodeConfig { HeightMm = 12, WidthMm = 42 },
        Styles = new Dictionary<string, BarcodeLabelFieldStyleConfig>
        {
            ["productName"] = new() { SizePt = 6, Weight = "bold" },
            ["designSku"] = new() { SizePt = 5.5, Weight = "bold" },
            ["sellingPrice"] = new() { SizePt = 5.5, Weight = "bold" },
            ["sizeNote"] = new() { SizePt = 5.5, Weight = "bold" },
            ["barcodeNumber"] = new() { SizePt = 7, Weight = "bold" },
        },
        Decoration = "price_underline",
    };
}
