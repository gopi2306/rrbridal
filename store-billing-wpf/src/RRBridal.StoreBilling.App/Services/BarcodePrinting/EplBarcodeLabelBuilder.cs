using System.Text;
using RRBridal.StoreBilling.App.Models;

namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

public static class EplBarcodeLabelBuilder
{
    public const int MaxFieldLength = 30;

    public static string BuildBatch(IEnumerable<BarcodePrintLineItem> lines, string companyName)
    {
        var sb = new StringBuilder();
        var company = BarcodeLabelTextLayout.TruncateCompany(companyName);

        foreach (var layout in BarcodeLabelLayout.FromLines(lines, companyName))
        {
            var remaining = layout.CopyCount;
            while (remaining > 0)
            {
                if (remaining >= BarcodeLabelDimensions.LabelsPerRow)
                {
                    AppendDoubleRowEpl(sb, layout, company);
                    remaining -= BarcodeLabelDimensions.LabelsPerRow;
                }
                else
                {
                    AppendSingleCupEpl(sb, layout, company);
                    remaining--;
                }
            }
        }

        return sb.ToString();
    }

    private static void AppendDoubleRowEpl(StringBuilder sb, BarcodeLabelLayout layout, string company)
    {
        sb.AppendLine("N");
        sb.AppendLine($"q{BarcodeLabelDimensions.WidthDots * BarcodeLabelDimensions.LabelsPerRow}");
        sb.AppendLine("Q260,24");
        AppendStickerEpl(sb, layout, company, 0);
        AppendStickerEpl(sb, layout, company, BarcodeLabelDimensions.WidthDots);
        sb.AppendLine("P1");
    }

    private static void AppendSingleCupEpl(StringBuilder sb, BarcodeLabelLayout layout, string company)
    {
        sb.AppendLine("N");
        sb.AppendLine($"q{BarcodeLabelDimensions.WidthDots}");
        sb.AppendLine("Q260,24");
        AppendStickerEpl(sb, layout, company, 0);
        sb.AppendLine("P1");
    }

    private static void AppendStickerEpl(StringBuilder sb, BarcodeLabelLayout layout, string company, int xOffset)
    {
        var item = EscapeEpl(BarcodeLabelTextLayout.TruncateItemSingleLine(layout.ItemName));
        var barcode = EscapeEpl(BarcodeLabelTextLayout.TruncateBarcode(layout.BarcodeValue));
        var price = layout.PriceText;

        sb.AppendLine($"A{4 + xOffset},6,0,2,1,1,N,\"{EscapeEpl(company)}\"");
        sb.AppendLine($"A{4 + xOffset},22,0,2,1,1,N,\"{item}\"");
        sb.AppendLine($"B{4 + xOffset},42,0,1,2,2,50,B,\"{barcode}\"");
        sb.AppendLine($"A{4 + xOffset},98,0,1,1,1,N,\"{barcode}\"");
        sb.AppendLine($"A{198 + xOffset},22,0,1,1,1,N,\"PRICE :\"");
        sb.AppendLine($"A{178 + xOffset},42,0,3,1,1,N,\"{EscapeEpl(price)}\"");
        sb.AppendLine($"A{168 + xOffset},72,0,1,1,1,N,\"(incl tax)\"");
    }

    private static string EscapeEpl(string value) =>
        value.Replace("\"", "\"\"", StringComparison.Ordinal);
}
