using System;
using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public sealed class OutboundDispatchPrintLine
{
    public string Sku { get; init; } = "";
    public string Description { get; init; } = "";
    public decimal Quantity { get; init; }
}

public sealed class OutboundDispatchPrintInput
{
    public required StoreProfile Store { get; init; }
    public int CharWidth { get; init; } = 48;
    public string DispatchNo { get; init; } = "";
    public string BatchNo { get; init; } = "";
    public string BillNo { get; init; } = "";
    public string RecipientName { get; init; } = "";
    public string RecipientAddress { get; init; } = "";
    public string RecipientPhone { get; init; } = "";
    public string CarrierType { get; init; } = "";
    public string CarrierName { get; init; } = "";
    public string TrackingNo { get; init; } = "";
    public int PackageCount { get; init; }
    public string FeePayer { get; init; } = "";
    public decimal DispatchFee { get; init; }
    public string Status { get; init; } = "";
    public string Date { get; init; } = "";
    public string PreparedBy { get; init; } = "";
    public IReadOnlyList<OutboundDispatchPrintLine> Lines { get; init; } =
        Array.Empty<OutboundDispatchPrintLine>();
}

public sealed class OutboundDispatchBatchRow
{
    public string DispatchNo { get; init; } = "";
    public string BillNo { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public string TrackingNo { get; init; } = "";
    public int PackageCount { get; init; }
    public string FeePayer { get; init; } = "";
    public decimal DispatchFee { get; init; }
}

public sealed class OutboundDispatchBatchPrintInput
{
    public required StoreProfile Store { get; init; }
    public int CharWidth { get; init; } = 48;
    public string BatchNo { get; init; } = "";
    public string CarrierName { get; init; } = "";
    public string CarrierType { get; init; } = "";
    public string Date { get; init; } = "";
    public IReadOnlyList<OutboundDispatchBatchRow> Rows { get; init; } =
        Array.Empty<OutboundDispatchBatchRow>();

    public int TotalParcels => System.Linq.Enumerable.Sum(Rows, row => row.PackageCount);
    public decimal CustomerPaidFee => System.Linq.Enumerable.Sum(
        Rows, row => string.Equals(row.FeePayer, "customer", StringComparison.OrdinalIgnoreCase)
            ? row.DispatchFee
            : 0m);
    public decimal StorePaidFee => System.Linq.Enumerable.Sum(
        Rows, row => string.Equals(row.FeePayer, "store", StringComparison.OrdinalIgnoreCase)
            ? row.DispatchFee
            : 0m);
}

public sealed class OutboundDispatchChargeReceiptPrintInput
{
    public required StoreProfile Store { get; init; }
    public int CharWidth { get; init; } = 48;
    public string ReceiptNo { get; init; } = "";
    public string BillNo { get; init; } = "";
    public string DispatchNo { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public decimal Amount { get; init; }
    public string PaymentMode { get; init; } = "";
    public string Reference { get; init; } = "";
    public string Date { get; init; } = "";
    public string ReceivedBy { get; init; } = "";
}
