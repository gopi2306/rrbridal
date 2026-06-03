using System.Text;
using RRBridal.StoreBilling.App.Models;

namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

/// <summary>TSPL for TVS LP 46 NEO — 2 identical stickers per row on 2-up media.</summary>
public static class TsplBarcodeLabelBuilder
{
    private static string RowLabelSetup =>
        $"""
        SIZE {BarcodeLabelDimensions.RowWidthMm} mm, {BarcodeLabelDimensions.LabelHeightMm} mm
        GAP 3 mm, 0 mm
        DIRECTION 1
        REFERENCE 0,0
        OFFSET 0 mm
        SET PEEL OFF
        SET CUTTER OFF
        DENSITY 10
        CLS
        """;

    private static string SingleLabelSetup =>
        $"""
        SIZE {BarcodeLabelDimensions.LabelWidthMm} mm, {BarcodeLabelDimensions.LabelHeightMm} mm
        GAP 3 mm, 0 mm
        DIRECTION 1
        REFERENCE 0,0
        OFFSET 0 mm
        SET PEEL OFF
        SET CUTTER OFF
        DENSITY 10
        CLS
        """;

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
                    AppendDoubleRow(sb, layout, company);
                    remaining -= BarcodeLabelDimensions.LabelsPerRow;
                }
                else
                {
                    AppendSingleCup(sb, layout, company);
                    remaining--;
                }
            }
        }

        return sb.ToString();
    }

    private static void AppendDoubleRow(StringBuilder sb, BarcodeLabelLayout layout, string company)
    {
        sb.Append(RowLabelSetup);
        AppendStickerContent(sb, layout, company, xOffsetDots: 0);
        AppendStickerContent(sb, layout, company, xOffsetDots: BarcodeLabelDimensions.WidthDots);
        sb.AppendLine("PRINT 1,1");
    }

    private static void AppendSingleCup(StringBuilder sb, BarcodeLabelLayout layout, string company)
    {
        sb.Append(SingleLabelSetup);
        AppendStickerContent(sb, layout, company, xOffsetDots: 0);
        sb.AppendLine("PRINT 1,1");
    }

    private static void AppendStickerContent(
        StringBuilder sb,
        BarcodeLabelLayout layout,
        string company,
        int xOffsetDots)
    {
        var item = EscapeTspl(BarcodeLabelTextLayout.TruncateItemSingleLine(layout.ItemName));
        var barcode = EscapeTspl(BarcodeLabelTextLayout.TruncateBarcode(layout.BarcodeValue));
        var price = EscapeTspl(layout.PriceText);
        company = EscapeTspl(company);

        sb.AppendLine($"TEXT {4 + xOffsetDots},6,\"2\",0,1,1,\"{company}\"");
        sb.AppendLine($"TEXT {4 + xOffsetDots},22,\"2\",0,1,1,\"{item}\"");
        sb.AppendLine($"BARCODE {4 + xOffsetDots},42,\"128\",50,1,0,2,2,\"{barcode}\"");
        sb.AppendLine($"TEXT {4 + xOffsetDots},98,\"1\",0,1,1,\"{barcode}\"");

        sb.AppendLine($"TEXT {198 + xOffsetDots},22,\"1\",0,1,1,\"PRICE :\"");
        sb.AppendLine($"TEXT {178 + xOffsetDots},42,\"3\",0,1,1,\"{price}\"");
        sb.AppendLine($"TEXT {168 + xOffsetDots},72,\"1\",0,1,1,\"(incl tax)\"");
    }

    private static string EscapeTspl(string value) =>
        value.Replace("\"", "\\\"", StringComparison.Ordinal);
}
