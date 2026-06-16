using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Store;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public sealed class CashHandOverThermalInput
{
    public required StoreProfile Store { get; init; }
    public int CharWidth { get; init; } = 48;
    public required string BusinessDate { get; init; }
    public required string Counter { get; init; }
    public required string UserName { get; init; }
    public IReadOnlyList<CashDenominationLine> Denominations { get; init; } = Array.Empty<CashDenominationLine>();
    public decimal CashInHand { get; init; }
    public decimal MorningCash { get; init; }
    public decimal ExpectedCash { get; init; }
    public decimal Difference { get; init; }
    public string StatusLabel { get; init; } = "";
    public string? CashTaken { get; init; }
    public bool PrintAllDenominations { get; init; } = true;
}

public static class CashHandOverThermalTextBuilder
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");

    public static string Build(CashHandOverThermalInput input)
    {
        var w = Math.Clamp(input.CharWidth, 32, 56);
        var sb = new StringBuilder();

        void AddCenter(string? text)
        {
            foreach (var line in Wrap(text ?? "", w))
                sb.AppendLine(Center(line, w));
        }

        void AddRule() => sb.AppendLine(new string('-', w));

        AddCenter(input.Store.StoreName);
        AddCenter("CASH HAND OVER");
        AddRule();

        sb.AppendLine(TwoCols($"Date: {input.BusinessDate}", $"Counter: POS{input.Counter}", w));
        sb.AppendLine($"User: {Truncate(input.UserName, w - 6)}");
        AddRule();

        sb.AppendLine(FormatCols("Rs.", "Units", "Amount", w));
        foreach (var line in input.Denominations)
        {
            if (!input.PrintAllDenominations && line.UnitCount <= 0)
                continue;

            sb.AppendLine(FormatDenomRow(line.Denomination, line.UnitCount, line.Amount, w));
        }

        AddRule();
        sb.AppendLine(TwoCols("Cash In Hand:", FormatMoney(input.CashInHand), w));
        sb.AppendLine(TwoCols("Morning Cash:", FormatMoney(input.MorningCash), w));
        sb.AppendLine(TwoCols("Expected Cash:", FormatMoney(input.ExpectedCash), w));
        var diffLabel = input.Difference >= 0 ? $"+{FormatMoney(input.Difference)}" : FormatMoney(input.Difference);
        sb.AppendLine(TwoCols("Difference:", diffLabel, w));
        if (!string.IsNullOrWhiteSpace(input.StatusLabel))
            sb.AppendLine(TwoCols("Status:", input.StatusLabel, w));
        if (!string.IsNullOrWhiteSpace(input.CashTaken))
            sb.AppendLine(TwoCols("Cash taken:", Truncate(input.CashTaken, w - 12), w));

        AddRule();
        AddCenter("Thank you");
        return sb.ToString().TrimEnd();
    }

    private static string FormatCols(string c1, string c2, string c3, int w)
    {
        var rsW = 6;
        var unitsW = 8;
        var amountW = w - rsW - unitsW - 2;
        return Pad(rsW, c1) + " " + Pad(unitsW, c2, right: true) + " " + Pad(amountW, c3, right: true);
    }

    private static string FormatDenomRow(int denomination, int units, decimal amount, int w)
    {
        var rsW = 6;
        var unitsW = 8;
        var amountW = w - rsW - unitsW - 2;
        return Pad(rsW, denomination.ToString(In)) + " "
               + Pad(unitsW, units.ToString(In), right: true) + " "
               + Pad(amountW, FormatMoney(amount), right: true);
    }

    private static string FormatMoney(decimal value) => value.ToString("N2", In);

    private static string Pad(int width, string text, bool right = false)
    {
        if (text.Length >= width)
            return text[..width];
        var pad = width - text.Length;
        return right ? new string(' ', pad) + text : text + new string(' ', pad);
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
