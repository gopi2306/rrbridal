using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public sealed class InvoiceLineSnap
{
    public int LineNo { get; init; }
    public string Description { get; init; } = "";
    public string Hsn { get; init; } = "";
    public decimal TaxPercent { get; init; }
    public decimal Qty { get; init; }
    public decimal Rate { get; init; }
    public decimal Mrp { get; init; }
    public decimal Amount { get; init; }
}

public sealed class ThermalInvoiceInput
{
    public required StoreProfile Store { get; init; }

    public int CharWidth { get; init; } = 42;

    public required string BillNo { get; init; }

    public required string BillDate { get; init; }

    public required string UserName { get; init; }

    public required string Time { get; init; }

    public required string Counter { get; init; }

    public IReadOnlyList<InvoiceLineSnap> Lines { get; init; } = Array.Empty<InvoiceLineSnap>();

    public decimal SubTotal { get; init; }

    public decimal TaxTotal { get; init; }

    public decimal ItemDiscount { get; init; }

    public decimal CashDiscAmount { get; init; }

    public decimal RoundOff { get; init; }

    public decimal Payable { get; init; }

    public decimal TotalQty { get; init; }

    public int ItemCount { get; init; }

    public decimal TotalMrp { get; init; }

    public decimal TotalLineAmount { get; init; }

    public decimal Savings { get; init; }

    public bool IsInterState { get; init; }

    public decimal CgstTotal { get; init; }

    public decimal SgstTotal { get; init; }

    public decimal IgstTotal { get; init; }
}

/// <summary>Plain-text thermal receipt (fixed-width, dashed rules).</summary>
public static class ThermalInvoiceTextBuilder
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");

    public static string Build(ThermalInvoiceInput input)
    {
        var w = Math.Clamp(input.CharWidth, 32, 56);
        var sb = new StringBuilder();
        var s = input.Store;

        void AddCenter(string? text)
        {
            foreach (var line in Wrap(text ?? "", w))
                sb.AppendLine(Center(line, w));
        }

        void AddRule() => sb.AppendLine(new string('-', w));

        AddCenter(s.StoreName);
        foreach (var line in Wrap(s.Address, w))
            sb.AppendLine(line);
        if (!string.IsNullOrWhiteSpace(s.CustomerCarePhone))
            sb.AppendLine($"Care: {s.CustomerCarePhone}".TrimEnd());
        var reg = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.FssaiNo)) reg.Add($"FSSAI: {s.FssaiNo}");
        if (!string.IsNullOrWhiteSpace(s.Gstin)) reg.Add($"GSTIN: {s.Gstin}");
        if (reg.Count > 0)
            sb.AppendLine(string.Join(" | ", reg));
        if (!string.IsNullOrWhiteSpace(s.BranchCode))
            sb.AppendLine($"Branch: {s.BranchCode}");
        sb.AppendLine();
        AddCenter("TAX INVOICE");
        AddRule();

        var left = $"Bill {input.BillNo}  {input.BillDate}";
        var right = $"{input.UserName}  {input.Time}";
        sb.AppendLine(TwoCols(left, right, w));
        sb.AppendLine(TwoCols($"Counter {input.Counter}", "", w));
        AddRule();

        foreach (var line in input.Lines.Where(l => l.Amount > 0))
        {
            sb.AppendLine($"{line.LineNo} {Truncate(line.Description, w - 2)}");
            var hsn = string.IsNullOrWhiteSpace(line.Hsn) ? "—" : line.Hsn;
            var row = $"{hsn}  {line.TaxPercent:0.##}%  {line.Qty:0.###}  {line.Rate:0.00}  {line.Mrp:0.00}  {line.Amount:0.00}";
            sb.AppendLine(PadRowNumeric(row, w));
        }

        AddRule();
        sb.AppendLine(TwoCols($"Items {input.ItemCount}", $"Qty {input.TotalQty:0.###}", w));
        sb.AppendLine(TwoCols($"Total MRP {input.TotalMrp:0.00}", $"Amt {input.TotalLineAmount:0.00}", w));
        sb.AppendLine(TwoCols("Other chg 0.00", $"Disc {input.ItemDiscount + input.CashDiscAmount:0.00}", w));
        AddRule();
        sb.AppendLine(Center($"BILL AMOUNT: {input.Payable:0.00}", w));
        AddRule();
        sb.AppendLine(TwoCols("Cash paid", "0.00", w));
        sb.AppendLine(TwoCols("Card paid", "0.00", w));
        sb.AppendLine(TwoCols("Online paid", "0.00", w));
        sb.AppendLine();
        if (input.Savings > 0)
            sb.AppendLine(Center($"You saved (Rs.): {input.Savings:0.00}", w));
        AddRule();

        if (input.IsInterState)
        {
            sb.AppendLine(Center("GST breakup (inter-state)", w));
            sb.AppendLine(PadHeader("Tax%  Goods    IGST   TotGST", w));
        }
        else
        {
            sb.AppendLine(Center("GST breakup (intra-state)", w));
            sb.AppendLine(PadHeader("Tax%  Goods   CGST    SGST   TotGST", w));
        }

        var groups = input.Lines
            .Where(l => l.Amount > 0)
            .GroupBy(l => Math.Round(l.TaxPercent, 2))
            .OrderBy(g => g.Key);
        decimal sumGoods = 0, sumCgst = 0, sumSgst = 0, sumIgst = 0, sumGst = 0;
        foreach (var g in groups)
        {
            var goods = g.Sum(x => x.Amount);
            var tax = g.Sum(x => x.Amount * (x.TaxPercent / 100m));
            sumGoods += goods;
            sumGst += tax;

            if (input.IsInterState)
            {
                sumIgst += tax;
                sb.AppendLine(PadRowNumeric($"{g.Key:0.##}% {goods:0.00} {tax:0.00} {tax:0.00}", w));
            }
            else
            {
                var half = Math.Round(tax / 2m, 2);
                sumCgst += half;
                sumSgst += half;
                sb.AppendLine(PadRowNumeric($"{g.Key:0.##}% {goods:0.00} {half:0.00} {half:0.00} {tax:0.00}", w));
            }
        }

        if (input.IsInterState)
            sb.AppendLine(PadRowNumeric($"TOTAL {sumGoods:0.00} {sumIgst:0.00} {sumGst:0.00}", w));
        else
            sb.AppendLine(PadRowNumeric($"TOTAL {sumGoods:0.00} {sumCgst:0.00} {sumSgst:0.00} {sumGst:0.00}", w));
        AddRule();

        foreach (var p in Wrap(s.TermsAndConditions, w))
            sb.AppendLine(p);
        foreach (var pl in s.PolicyLines ?? Enumerable.Empty<string>())
            AddCenter(pl);
        if (!string.IsNullOrWhiteSpace(s.Website))
            AddCenter(s.Website);
        AddCenter(s.ThankYouLine);
        sb.AppendLine();
        sb.AppendLine(Center("QR / barcode: not configured", w));

        return sb.ToString();
    }

    private static string Center(string text, int w)
    {
        if (text.Length >= w) return text[..w];
        var pad = w - text.Length;
        var left = pad / 2;
        return new string(' ', left) + text + new string(' ', w - text.Length - left);
    }

    private static string TwoCols(string left, string right, int w)
    {
        right ??= "";
        if (left.Length + right.Length + 1 <= w)
            return left + new string(' ', w - left.Length - right.Length) + right;
        return left;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s[..Math.Max(0, max - 1)] + "…";
    }

    private static IEnumerable<string> Wrap(string text, int w)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;
        var words = text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var word in words)
        {
            if (line.Length == 0)
            {
                line.Append(word);
                continue;
            }

            if (line.Length + 1 + word.Length <= w)
                line.Append(' ').Append(word);
            else
            {
                yield return line.ToString();
                line.Clear().Append(word);
            }
        }

        if (line.Length > 0)
            yield return line.ToString();
    }

    private static string PadHeader(string s, int w) => s.Length > w ? s[..w] : s;

    private static string PadRowNumeric(string s, int w) => s.Length > w ? s[..w] : s;
}
