using System.Globalization;
using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

public sealed class BarcodeLabelLayout
{
    public required string CompanyName { get; init; }
    public required string ItemName { get; init; }
    public required string BarcodeValue { get; init; }
    public required string PriceText { get; init; }
    public required string Sku { get; init; }
    public int CopyCount { get; init; }

    public int PrintRowCount =>
        (CopyCount + BarcodeLabelDimensions.LabelsPerRow - 1) / BarcodeLabelDimensions.LabelsPerRow;

    public string CopySummary => CopyCount switch
    {
        1 => "×1 sticker (1 row, left cup only)",
        _ when CopyCount % BarcodeLabelDimensions.LabelsPerRow == 0 =>
            $"×{CopyCount} stickers ({PrintRowCount} rows × {BarcodeLabelDimensions.LabelsPerRow})",
        _ => $"×{CopyCount} stickers ({PrintRowCount} rows, last row 1 cup)",
    };

    public static IReadOnlyList<BarcodeLabelLayout> FromLines(
        IEnumerable<BarcodePrintLineItem> lines,
        string companyName)
    {
        var company = BarcodeLabelTextLayout.TruncateCompany(companyName);
        var list = new List<BarcodeLabelLayout>();

        foreach (var line in lines)
        {
            if (line.IsDraftRow || line.PrintQty <= 0)
                continue;

            var copies = (int)Math.Ceiling(line.PrintQty);
            if (copies < 1)
                continue;

            list.Add(new BarcodeLabelLayout
            {
                CompanyName = company,
                ItemName = (line.Item ?? "").Trim(),
                BarcodeValue = BarcodeLabelTextLayout.TruncateBarcode(line.BarcodeValue),
                PriceText = MoneyMath.RoundDisplayAmount(line.LabelPrice)
                    .ToString("0.00", CultureInfo.InvariantCulture),
                Sku = line.Code,
                CopyCount = copies,
            });
        }

        return list;
    }
}
