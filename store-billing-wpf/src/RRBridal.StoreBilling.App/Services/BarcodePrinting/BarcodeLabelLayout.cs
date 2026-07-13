using RRBridal.StoreBilling.App.Models;

namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

public sealed class BarcodeLabelLayout
{
    public required BarcodeLabelRenderModel RenderModel { get; init; }

    public string CompanyName => RenderModel.CompanyName;
    public string ItemName => RenderModel.TextLines.FirstOrDefault()?.Text ?? "";
    public string BarcodeValue => RenderModel.BarcodeValue;
    public string PriceText => RenderModel.TextLines
        .FirstOrDefault(t => t.FieldKey == "sellingPrice")?.Text ?? "";
    public string Sku => RenderModel.Sku;
    public int CopyCount => RenderModel.CopyCount;
    public int PrintRowCount => RenderModel.PrintRowCount;
    public string CopySummary => RenderModel.CopySummary;

    public static IReadOnlyList<BarcodeLabelLayout> FromLines(
        IEnumerable<BarcodePrintLineItem> lines,
        string companyName,
        BarcodeLabelDesignConfig design)
    {
        return BarcodeLabelLayoutEngine
            .BuildRenderModels(lines, companyName, design)
            .Select(model => new BarcodeLabelLayout { RenderModel = model })
            .ToList();
    }
}
