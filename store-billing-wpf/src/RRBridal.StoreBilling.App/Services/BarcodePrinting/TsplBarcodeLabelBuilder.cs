using System.Text;
using RRBridal.StoreBilling.App.Models;

namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

public static class TsplBarcodeLabelBuilder
{
    public static string BuildBatch(
        IEnumerable<BarcodePrintLineItem> lines,
        string companyName,
        BarcodeLabelDesignConfig design)
    {
        var models = BarcodeLabelLayoutEngine.BuildRenderModels(lines, companyName, design);
        return BuildBatch(models, design);
    }

    public static string BuildBatch(IEnumerable<BarcodeLabelRenderModel> models, BarcodeLabelDesignConfig design)
    {
        var sb = new StringBuilder();
        foreach (var model in models)
        {
            var remaining = model.CopyCount;
            while (remaining > 0)
            {
                if (remaining >= design.LabelsPerRow)
                {
                    AppendDoubleRow(sb, model, design);
                    remaining -= design.LabelsPerRow;
                }
                else
                {
                    AppendSingleCup(sb, model, design);
                    remaining--;
                }
            }
        }

        return sb.ToString();
    }

    private static void AppendDoubleRow(StringBuilder sb, BarcodeLabelRenderModel model, BarcodeLabelDesignConfig design)
    {
        sb.Append(RowLabelSetup(design));
        AppendStickerContent(sb, model, 0);
        AppendStickerContent(sb, model, design.WidthDots);
        sb.AppendLine("PRINT 1,1");
    }

    private static void AppendSingleCup(StringBuilder sb, BarcodeLabelRenderModel model, BarcodeLabelDesignConfig design)
    {
        sb.Append(SingleLabelSetup(design));
        AppendStickerContent(sb, model, 0);
        sb.AppendLine("PRINT 1,1");
    }

    private static string RowLabelSetup(BarcodeLabelDesignConfig design) =>
        $"""
        SIZE {design.RowWidthMm} mm, {design.LabelHeightMm} mm
        GAP 3 mm, 0 mm
        DIRECTION 1
        REFERENCE 0,0
        OFFSET 0 mm
        SET PEEL OFF
        SET CUTTER OFF
        DENSITY 10
        CLS
        """;

    private static string SingleLabelSetup(BarcodeLabelDesignConfig design) =>
        $"""
        SIZE {design.LabelWidthMm} mm, {design.LabelHeightMm} mm
        GAP 3 mm, 0 mm
        DIRECTION 1
        REFERENCE 0,0
        OFFSET 0 mm
        SET PEEL OFF
        SET CUTTER OFF
        DENSITY 10
        CLS
        """;

    private static void AppendStickerContent(StringBuilder sb, BarcodeLabelRenderModel model, int xOffsetDots)
    {
        foreach (var line in model.TextLines)
        {
            var weight = line.Bold ? 1 : 0;
            sb.AppendLine(
                $"TEXT {line.XDots + xOffsetDots},{line.YDots},\"{line.FontDots}\",0,{weight},{weight},\"{EscapeTspl(line.Text)}\"");
        }

        if (model.Barcode != null)
        {
            var barcode = model.Barcode;
            sb.AppendLine(
                $"BARCODE {barcode.XDots + xOffsetDots},{barcode.YDots},\"128\",{barcode.HeightDots},1,0,2,2,\"{EscapeTspl(barcode.Value)}\"");
            var humanWeight = barcode.Bold ? 1 : 0;
            sb.AppendLine(
                $"TEXT {barcode.XDots + xOffsetDots},{barcode.HumanTextYDots},\"{barcode.FontDots}\",0,{humanWeight},{humanWeight},\"{EscapeTspl(barcode.HumanText)}\"");
        }
    }

    private static string EscapeTspl(string value) =>
        value.Replace("\"", "\\\"", StringComparison.Ordinal);
}
