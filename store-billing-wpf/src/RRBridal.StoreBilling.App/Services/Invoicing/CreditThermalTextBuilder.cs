using System;
using System.Globalization;
using System.Text;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class CreditThermalTextBuilder
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");

    public static string Build(CreditReceiptPrintInput input)
    {
        var w = Math.Clamp(input.CharWidth, 32, 56);
        var sb = new StringBuilder();
        void Line(string s = "") => sb.AppendLine(s);
        void Rule() => Line(new string('-', w));

        var store = input.Store;
        Line(Center(store.StoreName, w));
        if (!string.IsNullOrWhiteSpace(store.Address))
            Line(Wrap(store.Address.Trim(), w));
        if (!string.IsNullOrWhiteSpace(store.Gstin))
            Line(Center($"GSTIN: {store.Gstin}", w));
        if (!string.IsNullOrWhiteSpace(store.CustomerCarePhone))
            Line(Center($"Ph: {store.CustomerCarePhone}", w));
        Rule();
        Line(Center(input.DocumentTitle, w));
        Rule();
        Line($"Bill: {input.BillNo}");
        if (!string.IsNullOrWhiteSpace(input.BillDate))
            Line($"Date: {input.BillDate}");
        if (!string.IsNullOrWhiteSpace(input.ReceiptNo))
            Line($"Receipt: {input.ReceiptNo}");
        if (!string.IsNullOrWhiteSpace(input.CustomerName))
            Line($"Customer: {input.CustomerName}");
        if (!string.IsNullOrWhiteSpace(input.CustomerPhone))
            Line($"Mobile: {input.CustomerPhone}");
        if (!string.IsNullOrWhiteSpace(input.Status))
            Line($"Status: {input.Status}");
        Rule();

        foreach (var line in input.Lines)
        {
            var desc = string.IsNullOrWhiteSpace(line.Description) ? "#" + line.LineNo : line.Description;
            Line(Truncate(desc, w));
            Line($"  {FmtQty(line.Qty)} x {Money(line.Rate)} = {Money(line.Amount)}");
        }

        Rule();
        Line(Pair("Total", MoneyMath.FormatRupee(input.TotalPayable), w));
        Line(Pair("Advance", MoneyMath.FormatRupee(input.AdvanceAtPost), w));
        Line(Pair("Paid now", MoneyMath.FormatRupee(input.AmountPaidThisTime), w));
        Line(Pair("Paid total", MoneyMath.FormatRupee(input.CumulativeAmountPaid), w));
        Line(Pair("Balance due", MoneyMath.FormatRupee(input.BalanceDue), w));
        Rule();
        if (!string.IsNullOrWhiteSpace(input.PaymentMode))
            Line($"Mode: {input.PaymentMode}");
        if (!string.IsNullOrWhiteSpace(input.Reference))
            Line($"Ref: {input.Reference}");
        if (!string.IsNullOrWhiteSpace(input.ReceivedBy))
            Line($"By: {input.ReceivedBy}");
        if (!string.IsNullOrWhiteSpace(input.ReceivedAtDisplay))
            Line($"At: {input.ReceivedAtDisplay}");

        if (input.PaymentHistory.Count > 0)
        {
            Rule();
            Line("PAYMENT HISTORY");
            foreach (var row in input.PaymentHistory)
            {
                var label = string.IsNullOrWhiteSpace(row.Kind) ? "pay" : row.Kind;
                Line($"{row.ReceivedAtDisplay} [{label}]");
                Line($"  {MoneyMath.FormatRupee(row.Amount)}  {row.Mode}");
                if (!string.IsNullOrWhiteSpace(row.Reference))
                    Line($"  Ref: {row.Reference}");
            }
        }

        Rule();
        if (!string.IsNullOrWhiteSpace(store.ThankYouLine))
            Line(Center(store.ThankYouLine, w));
        return sb.ToString().TrimEnd();
    }

    private static string Money(decimal v) => v.ToString("N2", In);

    private static string FmtQty(decimal q) =>
        q == Math.Truncate(q) ? ((int)q).ToString(In) : q.ToString("0.##", In);

    private static string Center(string text, int w)
    {
        text ??= "";
        if (text.Length >= w)
            return text[..w];
        var pad = (w - text.Length) / 2;
        return new string(' ', pad) + text;
    }

    private static string Truncate(string text, int w) =>
        text.Length <= w ? text : text[..(w - 1)] + "…";

    private static string Wrap(string text, int w)
    {
        if (text.Length <= w)
            return text;
        var parts = new System.Collections.Generic.List<string>();
        for (var i = 0; i < text.Length; i += w)
            parts.Add(text.Substring(i, Math.Min(w, text.Length - i)));
        return string.Join(Environment.NewLine, parts);
    }

    private static string Pair(string label, string value, int w)
    {
        var space = w - label.Length - value.Length;
        if (space < 1)
            return Truncate(label + " " + value, w);
        return label + new string(' ', space) + value;
    }
}
