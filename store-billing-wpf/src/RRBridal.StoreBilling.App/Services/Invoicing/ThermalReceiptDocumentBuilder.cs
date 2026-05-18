using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Media;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class ThermalReceiptDocumentBuilder
{
    public static ThermalReceiptAssets BuildAssets(ReceiptConfigDocument config, string billNo)
    {
        var store = config.Store;
        ImageSource? logo = null;
        if (!string.IsNullOrWhiteSpace(store.LogoFilePath))
            logo = ReceiptLogoCache.TryLoadLocal(store.LogoFilePath);

        ImageSource? barcode = null;
        if (store.ShowBillBarcode && !string.IsNullOrWhiteSpace(billNo))
            barcode = ThermalBarcodeGenerator.CreateCode128(billNo);

        var qrList = new List<ThermalQrAsset>();
        foreach (var slot in (store.QrSlots ?? new List<ReceiptQrSlotConfig>()).Take(3))
        {
            if (string.IsNullOrWhiteSpace(slot.Payload))
                continue;
            var img = ThermalBarcodeGenerator.CreateQrCode(slot.Payload, 180);
            if (img == null)
                continue;
            qrList.Add(new ThermalQrAsset
            {
                Image = img,
                Label = string.IsNullOrWhiteSpace(slot.Label) ? null : slot.Label.Trim(),
            });
        }

        return new ThermalReceiptAssets
        {
            Logo = logo,
            BillBarcode = barcode,
            BillNoLabel = billNo,
            QrCodes = qrList,
        };
    }

    public static async System.Threading.Tasks.Task<ThermalReceiptAssets> BuildAssetsAsync(
        ReceiptConfigDocument config,
        string billNo,
        ReceiptLogoCache logoCache,
        CancellationToken ct = default)
    {
        var store = config.Store;
        ImageSource? logo = ReceiptLogoCache.TryLoadLocal(store.LogoFilePath);
        if (logo == null && !string.IsNullOrWhiteSpace(store.LogoUrl))
            logo = await logoCache.TryLoadFromUrlAsync(store.LogoUrl, ct);

        var baseAssets = BuildAssets(config, billNo);
        return new ThermalReceiptAssets
        {
            Logo = logo ?? baseAssets.Logo,
            BillBarcode = baseAssets.BillBarcode,
            BillNoLabel = baseAssets.BillNoLabel,
            QrCodes = baseAssets.QrCodes,
        };
    }
}
