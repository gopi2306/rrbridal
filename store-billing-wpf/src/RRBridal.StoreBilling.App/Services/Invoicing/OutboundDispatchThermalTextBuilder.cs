using System;
using System.Globalization;
using System.Text;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class OutboundDispatchThermalTextBuilder
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");

    public static string BuildParcelNote(OutboundDispatchPrintInput input)
    {
        var text = new ThermalWriter(input.CharWidth);
        text.Header(input.Store, "PARCEL NOTE");
        text.Line($"Dispatch: {input.DispatchNo}");
        text.Line($"Batch: {input.BatchNo}");
        text.Line($"Bill: {input.BillNo}");
        text.Line($"Date: {input.Date}");
        text.Rule();
        text.Line($"To: {input.RecipientName}");
        text.Wrap($"Address: {input.RecipientAddress}");
        text.Line($"Phone: {input.RecipientPhone}");
        text.Rule();
        text.Line($"Carrier: {Join(input.CarrierName, input.CarrierType)}");
        text.Line($"Tracking: {input.TrackingNo}");
        text.Line($"Packages: {input.PackageCount}");
        text.Line($"Fee payer: {input.FeePayer}");
        text.Line($"Fee: {MoneyMath.FormatRupee(input.DispatchFee)}");
        text.Line($"Status: {input.Status}");
        text.Rule();
        text.Line("ITEMS");
        foreach (var line in input.Lines)
        {
            text.Wrap($"{line.Sku} {line.Description}".Trim());
            text.Line($"  Qty: {FormatQuantity(line.Quantity)}");
        }
        if (input.Lines.Count == 0)
            text.Line("(No item lines)");
        text.Rule();
        text.Line($"Prepared by: {input.PreparedBy}");
        text.Line("Prepared sign: __________________");
        text.Line("Handover to: ___________________");
        text.Line("Handover sign: _________________");
        text.Line("Recipient sign: ________________");
        return text.ToString();
    }

    public static string BuildBatchSummary(OutboundDispatchBatchPrintInput input)
    {
        var text = new ThermalWriter(input.CharWidth);
        text.Header(input.Store, "DISPATCH BATCH SUMMARY");
        text.Line($"Batch: {input.BatchNo}");
        text.Line($"Carrier: {Join(input.CarrierName, input.CarrierType)}");
        text.Line($"Date: {input.Date}");
        text.Rule();
        foreach (var row in input.Rows)
        {
            text.Line($"{row.DispatchNo} | Bill {row.BillNo}");
            text.Wrap($"{row.CustomerName} {row.CustomerPhone}".Trim());
            text.Line($"Track: {row.TrackingNo}");
            text.Line(
                $"Parcels {row.PackageCount} | {row.FeePayer} | {MoneyMath.FormatRupee(row.DispatchFee)}");
            text.Rule();
        }
        text.Pair("Total parcels", input.TotalParcels.ToString(In));
        text.Pair("Customer-paid fees", MoneyMath.FormatRupee(input.CustomerPaidFee));
        text.Pair("Store-paid fees", MoneyMath.FormatRupee(input.StorePaidFee));
        text.Rule();
        text.Line("Prepared by/sign: ______________");
        text.Line("Carrier received by: ___________");
        text.Line("Carrier signature: ______________");
        text.Line("Handover date/time: _____________");
        return text.ToString();
    }

    public static string BuildChargeReceipt(OutboundDispatchChargeReceiptPrintInput input)
    {
        var text = new ThermalWriter(input.CharWidth);
        text.Header(input.Store, "DISPATCH CHARGE RECEIPT");
        text.Line("Separate dispatch charge");
        text.Rule();
        text.Line($"Receipt no: {input.ReceiptNo}");
        text.Line($"Bill no: {input.BillNo}");
        text.Line($"Dispatch no: {input.DispatchNo}");
        text.Line($"Customer: {input.CustomerName}");
        text.Line($"Phone: {input.CustomerPhone}");
        text.Pair("Dispatch charge", MoneyMath.FormatRupee(input.Amount));
        text.Line($"Mode: {input.PaymentMode}");
        text.Line($"Reference: {input.Reference}");
        text.Line($"Date: {input.Date}");
        text.Line($"Received by: {input.ReceivedBy}");
        text.Rule();
        text.Line("Customer signature: _____________");
        return text.ToString();
    }

    private static string Join(string first, string second) =>
        string.IsNullOrWhiteSpace(first) ? second
        : string.IsNullOrWhiteSpace(second) ? first
        : $"{first} ({second})";

    private static string FormatQuantity(decimal quantity) =>
        quantity == decimal.Truncate(quantity)
            ? quantity.ToString("0", In)
            : quantity.ToString("0.##", In);

    private sealed class ThermalWriter
    {
        private readonly StringBuilder _builder = new();
        private readonly int _width;

        public ThermalWriter(int width) => _width = Math.Clamp(width, 32, 56);

        public void Header(StoreProfile store, string title)
        {
            Line(Center(store.StoreName));
            if (!string.IsNullOrWhiteSpace(store.Address))
                Wrap(store.Address);
            if (!string.IsNullOrWhiteSpace(store.CustomerCarePhone))
                Line(Center($"Ph: {store.CustomerCarePhone}"));
            Rule();
            Line(Center(title));
            Rule();
        }

        public void Line(string value = "") => _builder.AppendLine(value ?? "");
        public void Rule() => Line(new string('-', _width));

        public void Pair(string label, string value)
        {
            var spaces = _width - label.Length - value.Length;
            Line(spaces > 0
                ? label + new string(' ', spaces) + value
                : Truncate($"{label} {value}"));
        }

        public void Wrap(string value)
        {
            value ??= "";
            while (value.Length > _width)
            {
                var breakAt = value.LastIndexOf(' ', _width);
                if (breakAt <= 0)
                    breakAt = _width;
                Line(value[..breakAt].TrimEnd());
                value = value[breakAt..].TrimStart();
            }
            Line(value);
        }

        public override string ToString() => _builder.ToString().TrimEnd('\r', '\n');

        private string Center(string value)
        {
            value = Truncate(value ?? "");
            return value.PadLeft(value.Length + Math.Max(0, (_width - value.Length) / 2));
        }

        private string Truncate(string value) =>
            value.Length <= _width ? value : value[..(_width - 1)] + "…";
    }
}
