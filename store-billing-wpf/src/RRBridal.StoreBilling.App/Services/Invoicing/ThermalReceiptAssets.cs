using System.Collections.Generic;
using System.Windows.Media;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public sealed class ThermalReceiptAssets
{
    public ImageSource? Logo { get; init; }

    public ImageSource? BillBarcode { get; init; }

    public string? BillNoLabel { get; init; }

    public IReadOnlyList<ThermalQrAsset> QrCodes { get; init; } = new List<ThermalQrAsset>();
}

public sealed class ThermalQrAsset
{
    public required ImageSource Image { get; init; }

    public string? Label { get; init; }
}
