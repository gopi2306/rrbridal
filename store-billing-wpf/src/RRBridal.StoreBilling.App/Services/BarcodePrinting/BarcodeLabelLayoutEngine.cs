using System.Globalization;
using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

public static class BarcodeLabelLayoutEngine
{
    public static IReadOnlyList<BarcodeLabelRenderModel> BuildRenderModels(
        IEnumerable<BarcodePrintLineItem> lines,
        string companyName,
        BarcodeLabelDesignConfig design)
    {
        var company = BarcodeLabelTextLayout.TruncateCompany(companyName);
        var list = new List<BarcodeLabelRenderModel>();

        foreach (var line in lines)
        {
            if (line.IsDraftRow || line.PrintQty <= 0)
                continue;

            var copies = (int)Math.Ceiling(line.PrintQty);
            if (copies < 1)
                continue;

            list.Add(BuildRenderModel(line, company, design, copies));
        }

        return list;
    }

    public static BarcodeLabelRenderModel BuildRenderModel(
        BarcodePrintLineItem line,
        string companyName,
        BarcodeLabelDesignConfig design,
        int copyCount)
    {
        return design.IsRetailStacked
            ? BuildRetailStacked(line, companyName, design, copyCount)
            : BuildBrandPrice(line, companyName, design, copyCount);
    }

    private static BarcodeLabelRenderModel BuildRetailStacked(
        BarcodePrintLineItem line,
        string companyName,
        BarcodeLabelDesignConfig design,
        int copyCount)
    {
        var offsetX = MmToDots(design.PrintOffsetMm.Horizontal, design.DotsPerMm);
        var offsetY = MmToDots(design.PrintOffsetMm.Vertical, design.DotsPerMm);
        var widthDots = design.WidthDots;
        var heightDots = design.HeightDots;
        var textLines = new List<BarcodeLabelTextElement>();
        var y = 8 + offsetY;

        if (design.Fields.ProductName)
        {
            var text = ResolveProductName(line, design);
            textLines.Add(CreateTextElement(text, "productName", design, widthDots, y, offsetX));
            y += LineSpacing(design.ResolveStyle("productName"));
        }

        if (design.Fields.DesignSku)
        {
            var text = $"{design.Text.DesignNoPrefix.Trim()} {line.Code}".Trim();
            textLines.Add(CreateTextElement(text, "designSku", design, widthDots, y, offsetX));
            y += LineSpacing(design.ResolveStyle("designSku"));
        }

        if (design.Fields.SellingPrice)
        {
            var text = $"{design.Text.PricePrefix.Trim()} {FormatPrice(line.LabelPrice, design.Text.PriceStyle)}".Trim();
            var underline = string.Equals(design.Decoration, "price_underline", StringComparison.OrdinalIgnoreCase);
            textLines.Add(CreateTextElement(text, "sellingPrice", design, widthDots, y, offsetX, underline));
            y += LineSpacing(design.ResolveStyle("sellingPrice"));
        }

        if (design.Fields.SizeNote && !string.IsNullOrWhiteSpace(line.SizeNote))
        {
            var text = $"{design.Text.NotePrefix.Trim()} {line.SizeNote}".Trim();
            textLines.Add(CreateTextElement(text, "sizeNote", design, widthDots, y, offsetX));
            y += LineSpacing(design.ResolveStyle("sizeNote"));
        }

        if (design.Fields.BatchNumber && !string.IsNullOrWhiteSpace(line.BatchNo) && line.BatchNo != "—")
        {
            var text = $"Batch: {line.BatchNo}";
            textLines.Add(CreateTextElement(text, "batchNumber", design, widthDots, y, offsetX));
            y += LineSpacing(design.ResolveStyle("batchNumber"));
        }

        if (design.Fields.ExpiryDate && !string.IsNullOrWhiteSpace(line.ExpDate) && line.ExpDate != "—")
        {
            var text = $"Exp: {line.ExpDate}";
            textLines.Add(CreateTextElement(text, "expiryDate", design, widthDots, y, offsetX));
            y += LineSpacing(design.ResolveStyle("expiryDate"));
        }

        if (design.Fields.BrandName && !string.IsNullOrWhiteSpace(design.CustomBrandText))
        {
            textLines.Add(CreateTextElement(design.CustomBrandText.Trim(), "brandName", design, widthDots, y, offsetX));
            y += LineSpacing(design.ResolveStyle("brandName"));
        }

        var barcodeValue = BarcodeLabelTextLayout.TruncateBarcode(line.BarcodeValue);
        var humanText = FormatHumanBarcode(barcodeValue, design.Text.BarcodeHumanText);
        var barcodeHeightDots = Math.Max(20, MmToDots(design.Barcode.HeightMm, design.DotsPerMm));
        var barcodeStyle = design.ResolveStyle("barcodeNumber", 7, true);
        var humanFontDots = PtToFontDots(barcodeStyle.SizePt);
        var humanArea = humanFontDots * 10 + 6;
        var barcodeY = Math.Max(y + 4, heightDots - barcodeHeightDots - humanArea - 6) + offsetY;
        var barcodeX = AlignX(humanText, design.Text.Alignment, widthDots, humanFontDots, barcodeStyle.IsBold, offsetX);

        var barcode = new BarcodeLabelBarcodeElement
        {
            Value = barcodeValue,
            HumanText = humanText,
            XDots = barcodeX,
            YDots = barcodeY,
            HeightDots = barcodeHeightDots,
            HumanTextYDots = barcodeY + barcodeHeightDots + 4,
            FontDots = humanFontDots,
            Bold = barcodeStyle.IsBold,
        };

        return new BarcodeLabelRenderModel
        {
            Design = design,
            Sku = line.Code,
            BarcodeValue = barcodeValue,
            CompanyName = companyName,
            TextLines = textLines,
            Barcode = barcode,
            Decoration = design.Decoration,
            OffsetXDots = offsetX,
            OffsetYDots = offsetY,
            CopyCount = copyCount,
        };
    }

    private static BarcodeLabelRenderModel BuildBrandPrice(
        BarcodePrintLineItem line,
        string companyName,
        BarcodeLabelDesignConfig design,
        int copyCount)
    {
        var scaleX = design.WidthDots / 304.0;
        var scaleY = design.HeightDots / 264.0;
        var offsetX = MmToDots(design.PrintOffsetMm.Horizontal, design.DotsPerMm);
        var offsetY = MmToDots(design.PrintOffsetMm.Vertical, design.DotsPerMm);

        var company = BarcodeLabelTextLayout.TruncateCompany(companyName);
        var item = BarcodeLabelTextLayout.TruncateItemSingleLine(line.ItemName);
        var barcodeValue = BarcodeLabelTextLayout.TruncateBarcode(line.BarcodeValue);
        var price = MoneyMath.RoundDisplayAmount(line.LabelPrice).ToString("0.00", CultureInfo.InvariantCulture);

        var textLines = new List<BarcodeLabelTextElement>
        {
            new()
            {
                Text = company,
                FieldKey = "productName",
                XDots = Scale(4, scaleX) + offsetX,
                YDots = Scale(6, scaleY) + offsetY,
                FontDots = 2,
                Bold = true,
                Alignment = "left",
            },
            new()
            {
                Text = item,
                FieldKey = "productName",
                XDots = Scale(4, scaleX) + offsetX,
                YDots = Scale(22, scaleY) + offsetY,
                FontDots = 2,
                Bold = false,
                Alignment = "left",
            },
            new()
            {
                Text = "PRICE :",
                FieldKey = "sellingPrice",
                XDots = Scale(198, scaleX) + offsetX,
                YDots = Scale(22, scaleY) + offsetY,
                FontDots = 1,
                Bold = false,
                Alignment = "right",
            },
            new()
            {
                Text = price,
                FieldKey = "sellingPrice",
                XDots = Scale(178, scaleX) + offsetX,
                YDots = Scale(42, scaleY) + offsetY,
                FontDots = 3,
                Bold = true,
                Alignment = "right",
            },
            new()
            {
                Text = "(incl tax)",
                FieldKey = "sellingPrice",
                XDots = Scale(168, scaleX) + offsetX,
                YDots = Scale(72, scaleY) + offsetY,
                FontDots = 1,
                Bold = false,
                Alignment = "right",
            },
        };

        var barcode = new BarcodeLabelBarcodeElement
        {
            Value = barcodeValue,
            HumanText = barcodeValue,
            XDots = Scale(4, scaleX) + offsetX,
            YDots = Scale(42, scaleY) + offsetY,
            HeightDots = Scale(50, scaleY),
            HumanTextYDots = Scale(98, scaleY) + offsetY,
            FontDots = 1,
            Bold = true,
        };

        return new BarcodeLabelRenderModel
        {
            Design = design,
            Sku = line.Code,
            BarcodeValue = barcodeValue,
            CompanyName = company,
            TextLines = textLines,
            Barcode = barcode,
            Decoration = design.Decoration,
            OffsetXDots = offsetX,
            OffsetYDots = offsetY,
            CopyCount = copyCount,
        };
    }

    private static BarcodeLabelTextElement CreateTextElement(
        string text,
        string fieldKey,
        BarcodeLabelDesignConfig design,
        int widthDots,
        int yDots,
        int offsetX,
        bool underline = false)
    {
        var style = design.ResolveStyle(fieldKey);
        var fontDots = PtToFontDots(style.SizePt);
        return new BarcodeLabelTextElement
        {
            Text = text,
            FieldKey = fieldKey,
            XDots = AlignX(text, design.Text.Alignment, widthDots, fontDots, style.IsBold, offsetX),
            YDots = yDots,
            FontDots = fontDots,
            Bold = style.IsBold,
            Alignment = design.Text.Alignment,
            Underline = underline,
        };
    }

    private static string ResolveProductName(BarcodePrintLineItem line, BarcodeLabelDesignConfig design)
    {
        if (string.Equals(design.Text.ProductNameSource, "alias", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(line.Alias))
        {
            return line.Alias.Trim();
        }

        if (string.Equals(design.Text.ProductNameSource, "shortName", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(line.ShortName))
        {
            return line.ShortName.Trim();
        }

        return line.ItemName;
    }

    private static string FormatPrice(decimal price, string priceStyle)
    {
        var rounded = MoneyMath.RoundDisplayAmount(price);
        return string.Equals(priceStyle, "whole", StringComparison.OrdinalIgnoreCase)
            ? Math.Round(rounded, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)
            : rounded.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatHumanBarcode(string barcode, string style)
    {
        if (string.Equals(style, "sku_spaced", StringComparison.OrdinalIgnoreCase))
        {
            var chars = barcode.Where(char.IsLetterOrDigit).ToArray();
            if (chars.Length == 0) return barcode;
            return "* " + string.Join(" ", chars) + " *";
        }

        return barcode;
    }

    private static int AlignX(string text, string alignment, int widthDots, int fontDots, bool bold, int offsetX)
    {
        var charWidth = fontDots * (bold ? 7 : 6);
        var textWidth = Math.Max(charWidth, text.Length * charWidth);
        return alignment.ToLowerInvariant() switch
        {
            "center" => Math.Max(4, (widthDots - textWidth) / 2) + offsetX,
            "right" => Math.Max(4, widthDots - textWidth - 4) + offsetX,
            _ => 4 + offsetX,
        };
    }

    private static int LineSpacing(BarcodeLabelFieldStyleConfig style) =>
        Math.Max(12, (int)Math.Round(style.SizePt * 2.2));

    private static int PtToFontDots(double sizePt) =>
        sizePt <= 5.5 ? 1 : sizePt <= 6.5 ? 2 : 3;

    private static int MmToDots(double mm, int dotsPerMm) =>
        Math.Max(0, (int)Math.Round(mm * dotsPerMm));

    private static int Scale(int value, double factor) => Math.Max(0, (int)Math.Round(value * factor));
}
