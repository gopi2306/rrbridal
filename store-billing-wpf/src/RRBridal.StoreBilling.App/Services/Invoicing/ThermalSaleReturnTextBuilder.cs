using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public sealed class SaleReturnLineSnap
{
    public string Description { get; init; } = "";
    public decimal Qty { get; init; }
    public decimal Rate { get; init; }
    public decimal Amount { get; init; }
}

public sealed class ThermalSaleReturnInput
{
    public required StoreProfile Store { get; init; }

    public int CharWidth { get; init; } = 48;

    public required string ReturnNo { get; init; }

    public required string OriginalBillNo { get; init; }

    public required string UserName { get; init; }

    public required string Counter { get; init; }

    public required string ReturnDate { get; init; }

    public required string ReturnTime { get; init; }

    public required string ReturnModeLabel { get; init; }

    public string? CreditNoteNo { get; init; }

    public IReadOnlyList<SaleReturnLineSnap> Lines { get; init; } = Array.Empty<SaleReturnLineSnap>();

    public decimal TotalQty { get; init; }

    public int ItemCount { get; init; }

    public decimal GrossAmount { get; init; }

    public decimal TaxTotal { get; init; }

    public decimal ReturnAmount { get; init; }

    public bool IsInterState { get; init; }

    public decimal CgstTotal { get; init; }

    public decimal SgstTotal { get; init; }

    public decimal IgstTotal { get; init; }

    public decimal CashRefunded { get; init; }

    public bool IsDuplicateCopy { get; init; }

    public bool IsLegacy { get; init; }

    public string? OriginalBillDateDisplay { get; init; }
}

/// <summary>Plain-text thermal sales return / credit note receipt.</summary>
public static class ThermalSaleReturnTextBuilder
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");

    public static string Build(ThermalSaleReturnInput input)
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
            sb.AppendLine(Center(line, w));
        if (!string.IsNullOrWhiteSpace(s.Gstin))
            sb.AppendLine(Center($"GSTIN: {s.Gstin}", w));
        if (input.IsDuplicateCopy)
        {
            sb.AppendLine();
            AddCenter("*** DUPLICATE ***");
        }
        sb.AppendLine(Center($"{input.ReturnDate} {input.ReturnTime}", w));
        sb.AppendLine();
        AddCenter("SALES RETURN");
        if (input.IsLegacy)
            AddCenter("(Pre-system invoice)");
        AddRule();

        sb.AppendLine(LabeledRow("SR #:", input.ReturnNo, "User:", input.UserName, w));
        sb.AppendLine(LabeledRow("Counter:", input.Counter, "Date:", input.ReturnDate, w));
        sb.AppendLine(LabeledRow("Bill:", input.OriginalBillNo, "Mode:", input.ReturnModeLabel, w));
        if (input.IsLegacy && !string.IsNullOrWhiteSpace(input.OriginalBillDateDisplay))
            sb.AppendLine(LabeledRow("Inv date:", input.OriginalBillDateDisplay, "", "", w));
        if (!string.IsNullOrWhiteSpace(input.CreditNoteNo))
            sb.AppendLine(LabeledRow("Credit Note:", input.CreditNoteNo, "", "", w));

        AddRule();
        sb.AppendLine(ItemHeader(w));

        foreach (var line in input.Lines)
        {
            sb.AppendLine(Truncate(line.Description, w));
            sb.AppendLine(ItemRow(line.Qty, line.Rate, line.Amount, w));
        }

        AddRule();
        sb.AppendLine(TwoCols($"Tot Qty: {input.TotalQty:0.###}", $"Tot Items: {input.ItemCount}", w));
        sb.AppendLine(TwoCols($"Gross Amt: {input.GrossAmount:0.00}", $"{TaxSummaryLabel(input)} {TaxSummaryValue(input)}", w));
        AddRule();
        sb.AppendLine(Center($"Return Amt : {input.ReturnAmount:0.00}", w));
        if (input.CashRefunded > 0)
            sb.AppendLine(Center($"Cash refunded : {input.CashRefunded:0.00}", w));
        if (!string.IsNullOrWhiteSpace(input.CreditNoteNo))
            sb.AppendLine(Center($"(Redeemable on billing)", w));

        return sb.ToString();
    }

    private static string TaxSummaryLabel(ThermalSaleReturnInput input) =>
        input.IsInterState ? "Tot IGST:" : "Tot CGST+S:";

    private static string TaxSummaryValue(ThermalSaleReturnInput input) =>
        input.IsInterState
            ? $"{input.IgstTotal:0.00}"
            : $"{input.CgstTotal + input.SgstTotal:0.00}";

    private static string ItemHeader(int w)
    {
        const string header = "Item Name              Qty   Rate   Amount";
        return header.Length > w ? header[..w] : header;
    }

    private static string ItemRow(decimal qty, decimal rate, decimal amount, int w)
    {
        var row = $"{qty,5:0.###} {rate,8:0.00} {amount,9:0.00}";
        return row.Length > w ? row[..w] : row.PadLeft(w);
    }

    private static string LabeledRow(string l1, string v1, string l2, string v2, int w)
    {
        var left = $"{l1} {v1}".Trim();
        var right = string.IsNullOrEmpty(l2) ? "" : $"{l2} {v2}".Trim();
        return TwoCols(left, right, w);
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
            return left + new string(' ', Math.Max(1, w - left.Length - right.Length)) + right;
        return left.Length > w ? left[..w] : left;
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
}
