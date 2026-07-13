using System.Text;
using RRBridal.StoreBilling.App.Models;

namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

public static class EplBarcodeLabelBuilder
{
    public const int MaxFieldLength = 30;

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
                    AppendDoubleRowEpl(sb, model, design);
                    remaining -= design.LabelsPerRow;
                }
                else
                {
                    AppendSingleCupEpl(sb, model, design);
                    remaining--;
                }
            }
        }

        return sb.ToString();
    }

    private static void AppendDoubleRowEpl(StringBuilder sb, BarcodeLabelRenderModel model, BarcodeLabelDesignConfig design)
    {
        sb.AppendLine("N");
        sb.AppendLine($"q{design.WidthDots * design.LabelsPerRow}");
        sb.AppendLine("Q260,24");
        AppendStickerEpl(sb, model, 0);
        AppendStickerEpl(sb, model, design.WidthDots);
        sb.AppendLine("P1");
    }

    private static void AppendSingleCupEpl(StringBuilder sb, BarcodeLabelRenderModel model, BarcodeLabelDesignConfig design)
    {
        sb.AppendLine("N");
        sb.AppendLine($"q{design.WidthDots}");
        sb.AppendLine("Q260,24");
        AppendStickerEpl(sb, model, 0);
        sb.AppendLine("P1");
    }

    private static void AppendStickerEpl(StringBuilder sb, BarcodeLabelRenderModel model, int xOffset)
    {
        foreach (var line in model.TextLines)
        {
            var weight = line.Bold ? 1 : 0;
            sb.AppendLine(
                $"A{line.XDots + xOffset},{line.YDots},0,{line.FontDots},{weight},{weight},N,\"{EscapeEpl(line.Text)}\"");
        }

        if (model.Barcode != null)
        {
            var barcode = model.Barcode;
            sb.AppendLine(
                $"B{barcode.XDots + xOffset},{barcode.YDots},0,1,2,2,{barcode.HeightDots},B,\"{EscapeEpl(barcode.Value)}\"");
            var humanWeight = barcode.Bold ? 1 : 0;
            sb.AppendLine(
                $"A{barcode.XDots + xOffset},{barcode.HumanTextYDots},0,{barcode.FontDots},{humanWeight},{humanWeight},N,\"{EscapeEpl(barcode.HumanText)}\"");
        }
    }

    private static string EscapeEpl(string value) =>
        value.Replace("\"", "\"\"", StringComparison.Ordinal);
}
