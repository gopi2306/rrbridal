using System.Text.Json.Serialization;

namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

public sealed class BarcodeLabelDesignDocument
{
    [JsonPropertyName("design")]
    public BarcodeLabelDesignConfig? Design { get; set; }

    [JsonPropertyName("printerProfile")]
    public BarcodePrinterProfileConfig? PrinterProfile { get; set; }

    [JsonPropertyName("lastSyncedAt")]
    public DateTime? LastSyncedAt { get; set; }
}

public sealed class BarcodePrinterProfileConfig
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("dpi")]
    public int Dpi { get; set; } = 203;

    [JsonPropertyName("labelWidthMm")]
    public int LabelWidthMm { get; set; } = 50;

    [JsonPropertyName("labelHeightMm")]
    public int LabelHeightMm { get; set; } = 38;

    [JsonPropertyName("labelsPerRow")]
    public int LabelsPerRow { get; set; } = 2;
}

public sealed class BarcodeLabelDesignConfig
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Default";

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("layoutStyle")]
    public string LayoutStyle { get; set; } = "retail_stacked";

    [JsonPropertyName("printerProfileId")]
    public string PrinterProfileId { get; set; } = "tsc-ttp-244-pro";

    [JsonPropertyName("labelWidthMm")]
    public int LabelWidthMm { get; set; } = 50;

    [JsonPropertyName("labelHeightMm")]
    public int LabelHeightMm { get; set; } = 38;

    [JsonPropertyName("labelsPerRow")]
    public int LabelsPerRow { get; set; } = 2;

    [JsonPropertyName("dpi")]
    public int Dpi { get; set; } = 203;

    [JsonPropertyName("fields")]
    public BarcodeLabelFieldsConfig Fields { get; set; } = new();

    [JsonPropertyName("text")]
    public BarcodeLabelTextConfig Text { get; set; } = new();

    [JsonPropertyName("barcode")]
    public BarcodeLabelBarcodeConfig Barcode { get; set; } = new();

    [JsonPropertyName("styles")]
    public Dictionary<string, BarcodeLabelFieldStyleConfig> Styles { get; set; } = new();

    [JsonPropertyName("decoration")]
    public string Decoration { get; set; } = "none";

    [JsonPropertyName("printOffsetMm")]
    public BarcodeLabelPrintOffsetConfig PrintOffsetMm { get; set; } = new();

    [JsonPropertyName("customBrandText")]
    public string? CustomBrandText { get; set; }

    public int DotsPerMm => Math.Max(1, (int)Math.Round(Dpi / 25.4));

    public int WidthDots => LabelWidthMm * DotsPerMm;

    public int HeightDots => LabelHeightMm * DotsPerMm;

    public int RowWidthMm => LabelWidthMm * Math.Max(1, LabelsPerRow);

    public bool IsRetailStacked =>
        string.Equals(LayoutStyle, "retail_stacked", StringComparison.OrdinalIgnoreCase);

    public BarcodeLabelFieldStyleConfig ResolveStyle(string key, double defaultSizePt = 5.5, bool defaultBold = true)
    {
        if (Styles.TryGetValue(key, out var style) && style != null)
            return style;
        return new BarcodeLabelFieldStyleConfig { SizePt = defaultSizePt, Weight = defaultBold ? "bold" : "regular" };
    }
}

public sealed class BarcodeLabelFieldsConfig
{
    [JsonPropertyName("productName")]
    public bool ProductName { get; set; } = true;

    [JsonPropertyName("designSku")]
    public bool DesignSku { get; set; } = true;

    [JsonPropertyName("sellingPrice")]
    public bool SellingPrice { get; set; } = true;

    [JsonPropertyName("sizeNote")]
    public bool SizeNote { get; set; } = true;

    [JsonPropertyName("batchNumber")]
    public bool BatchNumber { get; set; }

    [JsonPropertyName("expiryDate")]
    public bool ExpiryDate { get; set; }

    [JsonPropertyName("brandName")]
    public bool BrandName { get; set; }
}

public sealed class BarcodeLabelTextConfig
{
    [JsonPropertyName("productNameSource")]
    public string ProductNameSource { get; set; } = "itemName";

    [JsonPropertyName("designNoPrefix")]
    public string DesignNoPrefix { get; set; } = "D.No:";

    [JsonPropertyName("pricePrefix")]
    public string PricePrefix { get; set; } = "Price ₹:";

    [JsonPropertyName("notePrefix")]
    public string NotePrefix { get; set; } = "Note:";

    [JsonPropertyName("priceStyle")]
    public string PriceStyle { get; set; } = "whole";

    [JsonPropertyName("barcodeHumanText")]
    public string BarcodeHumanText { get; set; } = "sku_spaced";

    [JsonPropertyName("alignment")]
    public string Alignment { get; set; } = "center";
}

public sealed class BarcodeLabelBarcodeConfig
{
    [JsonPropertyName("heightMm")]
    public double HeightMm { get; set; } = 12;

    [JsonPropertyName("widthMm")]
    public double WidthMm { get; set; } = 42;
}

public sealed class BarcodeLabelFieldStyleConfig
{
    [JsonPropertyName("sizePt")]
    public double SizePt { get; set; } = 5.5;

    [JsonPropertyName("weight")]
    public string Weight { get; set; } = "bold";

    public bool IsBold => !string.Equals(Weight, "regular", StringComparison.OrdinalIgnoreCase);
}

public sealed class BarcodeLabelPrintOffsetConfig
{
    [JsonPropertyName("vertical")]
    public double Vertical { get; set; }

    [JsonPropertyName("horizontal")]
    public double Horizontal { get; set; }
}
