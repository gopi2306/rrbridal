namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

public sealed class BarcodeLabelTextElement
{
    public required string Text { get; init; }
    public required string FieldKey { get; init; }
    public int XDots { get; init; }
    public int YDots { get; init; }
    public int FontDots { get; init; }
    public bool Bold { get; init; }
    public string Alignment { get; init; } = "left";
    public bool Underline { get; init; }
}

public sealed class BarcodeLabelBarcodeElement
{
    public required string Value { get; init; }
    public required string HumanText { get; init; }
    public int XDots { get; init; }
    public int YDots { get; init; }
    public int HeightDots { get; init; }
    public int HumanTextYDots { get; init; }
    public int FontDots { get; init; } = 1;
    public bool Bold { get; init; } = true;
}

public sealed class BarcodeLabelRenderModel
{
    public required BarcodeLabelDesignConfig Design { get; init; }
    public required string Sku { get; init; }
    public required string BarcodeValue { get; init; }
    public required string CompanyName { get; init; }
    public IReadOnlyList<BarcodeLabelTextElement> TextLines { get; init; } = Array.Empty<BarcodeLabelTextElement>();
    public BarcodeLabelBarcodeElement? Barcode { get; init; }
    public string Decoration { get; init; } = "none";
    public int OffsetXDots { get; init; }
    public int OffsetYDots { get; init; }
    public int CopyCount { get; init; }

    public int LabelsPerRow => Math.Max(1, Design.LabelsPerRow);

    public int PrintRowCount => (CopyCount + LabelsPerRow - 1) / LabelsPerRow;

    public string CopySummary => CopyCount switch
    {
        1 => $"×1 sticker (1 row, left cup only)",
        _ when CopyCount % LabelsPerRow == 0 =>
            $"×{CopyCount} stickers ({PrintRowCount} rows × {LabelsPerRow})",
        _ => $"×{CopyCount} stickers ({PrintRowCount} rows, last row 1 cup)",
    };
}
