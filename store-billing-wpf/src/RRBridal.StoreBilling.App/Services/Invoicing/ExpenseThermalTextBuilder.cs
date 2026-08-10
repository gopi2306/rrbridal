using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public sealed class ExpenseThermalInput
{
    public required StoreProfile Store { get; init; }
    public int CharWidth { get; init; } = 48;
    public string ExpenseNo { get; init; } = "";
    public string BusinessDate { get; init; } = "";
    public string SupplierName { get; init; } = "";
    public string SupplierGstin { get; init; } = "";
    public string SupplierInvoiceNo { get; init; } = "";
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public decimal TaxableAmount { get; init; }
    public decimal CgstAmount { get; init; }
    public decimal SgstAmount { get; init; }
    public decimal IgstAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public string PaymentSummary { get; init; } = "";
    public string Status { get; init; } = "posted";
}

public static class ExpenseThermalTextBuilder
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");

    public static string Build(ExpenseThermalInput input)
    {
        var width = Math.Clamp(input.CharWidth, 32, 56);
        var text = new StringBuilder();
        Center(input.Store.StoreName);
        Center("EXPENSE VOUCHER");
        Rule();
        Pair("Expense no:", input.ExpenseNo);
        Pair("Date:", input.BusinessDate);
        Pair("Supplier:", input.SupplierName);
        if (!string.IsNullOrWhiteSpace(input.SupplierGstin)) Pair("GSTIN:", input.SupplierGstin);
        if (!string.IsNullOrWhiteSpace(input.SupplierInvoiceNo)) Pair("Invoice:", input.SupplierInvoiceNo);
        if (!string.IsNullOrWhiteSpace(input.Category)) Pair("Category:", input.Category);
        AddWrapped($"Description: {input.Description}");
        Rule();
        Pair("Taxable:", Money(input.TaxableAmount));
        if (input.CgstAmount > 0) Pair("CGST:", Money(input.CgstAmount));
        if (input.SgstAmount > 0) Pair("SGST:", Money(input.SgstAmount));
        if (input.IgstAmount > 0) Pair("IGST:", Money(input.IgstAmount));
        Pair("TOTAL:", Money(input.TotalAmount));
        Rule();
        AddWrapped($"Payments: {input.PaymentSummary}");
        Pair("Status:", input.Status.ToUpperInvariant());
        Rule();
        Center("Authorised expense voucher");
        return text.ToString().TrimEnd();

        void Rule() => text.AppendLine(new string('-', width));
        void Center(string value)
        {
            foreach (var line in Wrap(value, width))
            {
                var left = Math.Max(0, (width - line.Length) / 2);
                text.AppendLine(new string(' ', left) + line);
            }
        }
        void Pair(string left, string right)
        {
            right ??= "";
            if (left.Length + right.Length + 1 <= width)
                text.AppendLine(left + new string(' ', width - left.Length - right.Length) + right);
            else
                AddWrapped($"{left} {right}");
        }
        void AddWrapped(string value)
        {
            foreach (var line in Wrap(value, width)) text.AppendLine(line);
        }
    }

    private static string Money(decimal value) => value.ToString("N2", In);

    private static IEnumerable<string> Wrap(string? value, int width)
    {
        var words = (value ?? "").Split([' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var word in words)
        {
            if (line.Length == 0)
            {
                line.Append(word.Length <= width ? word : word[..width]);
                continue;
            }
            if (line.Length + word.Length + 1 <= width)
                line.Append(' ').Append(word);
            else
            {
                yield return line.ToString();
                line.Clear().Append(word.Length <= width ? word : word[..width]);
            }
        }
        if (line.Length > 0) yield return line.ToString();
    }
}
