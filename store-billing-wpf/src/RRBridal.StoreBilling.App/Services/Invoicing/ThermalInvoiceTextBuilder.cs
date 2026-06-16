using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RRBridal.StoreBilling.App.Services.Billing;

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
    public decimal LineDiscount { get; init; }
    public decimal TaxableAmount { get; init; }
    public decimal TaxAmount { get; init; }

    /// <summary>Tax-inclusive line total after discounts (matches payable per line).</summary>
    public decimal LineInclusiveAmount { get; init; }

    /// <summary>Amount column on pre-printed stationery (inclusive line total, not taxable).</summary>
    public decimal PrePrintedLineAmount()
    {
        if (LineInclusiveAmount > 0)
            return LineInclusiveAmount;
        var inclusive = TaxableAmount + TaxAmount;
        if (inclusive > 0)
            return inclusive;
        return Amount;
    }
}

public sealed class ThermalInvoiceInput
{
    public required StoreProfile Store { get; init; }

    public int CharWidth { get; init; } = 48;

    public required string BillNo { get; init; }

    public required string BillDate { get; init; }

    public required string UserName { get; init; }

    public required string Time { get; init; }

    public required string Counter { get; init; }

    public string CustomerName { get; init; } = "";

    public string CustomerPhone { get; init; } = "";

    public IReadOnlyList<InvoiceLineSnap> Lines { get; init; } = Array.Empty<InvoiceLineSnap>();

    public decimal SubTotal { get; init; }

    public decimal OriginalTaxTotal { get; init; }

    public decimal RevisedSubTotal { get; init; }

    public decimal TaxTotal { get; init; }

    public decimal ItemDiscountPercent { get; init; }

    public decimal ItemDiscount { get; init; }

    public decimal CashDiscAmount { get; init; }

    /// <summary>Manual bill discount (item + cash); excludes scheme/offer discounts.</summary>
    public decimal ManualDiscountAmount => ItemDiscount + CashDiscAmount;

    public decimal RoundOff { get; init; }

    public decimal Payable { get; init; }

    public decimal TotalQty { get; init; }

    public int ItemCount { get; init; }

    public decimal TotalMrp { get; init; }

    public decimal TotalLineAmount { get; init; }

    public decimal TotalTaxableAmount { get; init; }

    public decimal Savings { get; init; }

    public bool IsInterState { get; init; }

    public decimal CgstTotal { get; init; }

    public decimal SgstTotal { get; init; }

    public decimal IgstTotal { get; init; }

    public PaymentReceiptSnap? Payments { get; init; }

    public bool IsDuplicateCopy { get; init; }

    public string DuplicatePrintedBy { get; init; } = "";

    public DateTime? DuplicatePrintedAtUtc { get; init; }

    public bool Stitching { get; init; }

    public bool DoorDelivery { get; init; }

    public string DeliveryDate { get; init; } = "";
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
            sb.AppendLine($"Customer Care: {s.CustomerCarePhone}".TrimEnd());
        var reg = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.FssaiNo)) reg.Add($"FSSAI: {s.FssaiNo}");
        if (!string.IsNullOrWhiteSpace(s.Gstin)) reg.Add($"GSTIN: {s.Gstin}");
        if (reg.Count > 0)
            sb.AppendLine(string.Join(" | ", reg));
        if (!string.IsNullOrWhiteSpace(s.BranchCode))
            sb.AppendLine($"Branch: {s.BranchCode}");
        sb.AppendLine();
        AddCenter("TAX INVOICE");
        if (input.IsDuplicateCopy)
        {
            AddCenter("*** DUPLICATE ***");
            if (!string.IsNullOrWhiteSpace(input.DuplicatePrintedBy) || input.DuplicatePrintedAtUtc.HasValue)
            {
                var when = input.DuplicatePrintedAtUtc.HasValue
                    ? input.DuplicatePrintedAtUtc.Value.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", In)
                    : "";
                var who = input.DuplicatePrintedBy?.Trim() ?? "";
                var reprint = string.IsNullOrEmpty(who) ? when : string.IsNullOrEmpty(when) ? who : $"Reprint: {when} by {who}";
                if (!string.IsNullOrEmpty(reprint))
                    AddCenter(reprint);
            }
        }

        AddRule();

        sb.AppendLine(LabeledRow("Bill No:", input.BillNo, "Bill Date:", input.BillDate, w));
        sb.AppendLine(LabeledRow("User:", input.UserName, "Time:", input.Time, w));
        sb.AppendLine(LabeledRow("Counter:", input.Counter, "", "", w));

        if (!string.IsNullOrWhiteSpace(input.CustomerName) || !string.IsNullOrWhiteSpace(input.CustomerPhone))
        {
            if (!string.IsNullOrWhiteSpace(input.CustomerName))
                sb.AppendLine(LabeledRow("Customer:", input.CustomerName, "", "", w));
            if (!string.IsNullOrWhiteSpace(input.CustomerPhone))
                sb.AppendLine(LabeledRow("Phone:", input.CustomerPhone, "", "", w));
        }

        AddRule();
        sb.AppendLine(PadHeader(TruncateCols("HSN  GST%  Qty   Rate   MRP    Amt", w), w));

        foreach (var line in input.Lines.Where(l => l.Amount > 0 || l.TaxableAmount > 0))
        {
            sb.AppendLine($"{line.LineNo} {Truncate(line.Description, w - 2)}");
            var hsn = string.IsNullOrWhiteSpace(line.Hsn) ? "—" : line.Hsn;
            var row =
                $"{hsn} {line.TaxPercent,5:0.##}% {line.Qty,5:0.###} {line.Rate,9:0.00} {line.Mrp,9:0.00} {line.TaxableAmount,11:0.00}";
            sb.AppendLine(PadRowNumeric(row, w));
        }

        AddRule();
        sb.AppendLine(TwoCols($"No. of items: {input.ItemCount}", $"Total qty: {input.TotalQty:0.###}", w));
        sb.AppendLine(TwoCols($"Total MRP: {input.TotalMrp:0.00}", $"Taxable amount: {input.TotalTaxableAmount:0.00}", w));

        if (input.ItemDiscount > 0 || input.CashDiscAmount > 0)
        {
            if (input.OriginalTaxTotal > 0)
                sb.AppendLine(TwoCols($"GST before disc: {input.OriginalTaxTotal:0.00}", "", w));
            if (input.RevisedSubTotal > 0)
                sb.AppendLine(TwoCols($"Revised sub total: {input.RevisedSubTotal:0.00}", "", w));
        }

        if (input.ItemDiscount > 0)
        if (input.CashDiscAmount > 0)
            sb.AppendLine(TwoCols($"Cash discount: {input.CashDiscAmount:0.00}", "", w));
        if (input.RoundOff != 0)
            sb.AppendLine(TwoCols($"Round off: {input.RoundOff:0.00}", "", w));

        sb.AppendLine(TwoCols("Other charges:", "0.00", w));
        AddRule();
        sb.AppendLine(Center($"Bill Amount .: {input.Payable:0.00}", w));
        AddRule();

        var pay = input.Payments;
        if (pay?.IsPreview == true)
            sb.AppendLine(Center("(Preview — payment not taken)", w));
        else if (pay != null)
        {
            if (pay.CashReceived is > 0)
                sb.AppendLine(TwoCols($"Cash Received: {pay.CashReceived:0.00}", "", w));
            if (pay.BalanceReturn is > 0)
                sb.AppendLine(TwoCols($"Balance Return: {pay.BalanceReturn:0.00}", "", w));
            sb.AppendLine(TwoCols("Cash paid:", $"{pay.CashPaid:0.00}", w));
            sb.AppendLine(TwoCols("Card paid:", $"{pay.CardPaid:0.00}", w));
            sb.AppendLine(TwoCols("Online paid:", $"{pay.UpiPaid:0.00}", w));
            sb.AppendLine(TwoCols("Credit Note paid:", $"{pay.CreditNotePaid:0.00}", w));
        }
        else
        {
            sb.AppendLine(TwoCols("Cash paid:", "0.00", w));
            sb.AppendLine(TwoCols("Card paid:", "0.00", w));
            sb.AppendLine(TwoCols("Online paid:", "0.00", w));
            sb.AppendLine(TwoCols("Credit Note paid:", "0.00", w));
        }

        if (input.Savings > 0)
            sb.AppendLine(Center($"You Have Saved (Rs.) : {input.Savings:0.00}", w));

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
            .Where(l => l.TaxableAmount > 0)
            .GroupBy(l => Math.Round(l.TaxPercent, 2))
            .OrderBy(g => g.Key);
        decimal sumGoods = 0, sumCgst = 0, sumSgst = 0, sumIgst = 0, sumGst = 0;
        foreach (var g in groups)
        {
            var goods = g.Sum(x => x.TaxableAmount);
            var tax = g.Sum(x => x.TaxAmount);
            sumGoods += goods;
            sumGst += tax;

            if (input.IsInterState)
            {
                sumIgst += tax;
                sb.AppendLine(PadRowNumeric($"{g.Key,0:0.##}% {goods,0:0.00} {tax,0:0.00} {tax,0:0.00}", w));
            }
            else
            {
                var half = MoneyMath.RoundAmount(tax / 2m);
                sumCgst += half;
                sumSgst += half;
                sb.AppendLine(PadRowNumeric($"{g.Key,0:0.##}% {goods,0:0.00} {half,0:0.00} {half,0:0.00} {tax,0:0.00}", w));
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

        return sb.ToString();
    }

    private static string LabeledRow(string l1, string v1, string l2, string v2, int w)
    {
        var left = string.IsNullOrEmpty(l2) ? $"{l1} {v1}".Trim() : $"{l1} {v1}";
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

    private static string TruncateCols(string s, int w) => s.Length > w ? s[..w] : s;

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
