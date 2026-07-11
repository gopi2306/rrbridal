using System;
using System.Text;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Converts rupee amounts to English words for invoice printing.</summary>
public static class IndianAmountInWords
{
    private static readonly string[] Ones =
    [
        "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
        "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen",
        "Seventeen", "Eighteen", "Nineteen",
    ];

    private static readonly string[] Tens =
    [
        "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety",
    ];

    public static string ForRupee(decimal amount)
    {
        var rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        var rupees = (long)Math.Truncate(rounded);
        var paise = (int)Math.Round((rounded - rupees) * 100m, 0, MidpointRounding.AwayFromZero);

        if (rupees == 0 && paise == 0)
            return "INR Zero Only";

        var sb = new StringBuilder("INR ");
        if (rupees > 0)
            sb.Append(ConvertWhole(rupees));

        if (paise > 0)
        {
            if (rupees > 0)
                sb.Append(" and ");
            sb.Append(ConvertWhole(paise));
            sb.Append(" Paise");
        }

        sb.Append(" Only");
        return sb.ToString();
    }

    private static string ConvertWhole(long number)
    {
        if (number == 0)
            return "Zero";

        var parts = new StringBuilder();

        AppendScaled(parts, number / 10_000_000, "Crore");
        number %= 10_000_000;

        AppendScaled(parts, number / 100_000, "Lakh");
        number %= 100_000;

        AppendScaled(parts, number / 1000, "Thousand");
        number %= 1000;

        AppendScaled(parts, number / 100, "Hundred");
        number %= 100;

        if (number > 0)
        {
            if (parts.Length > 0)
                parts.Append(' ');
            parts.Append(ConvertBelowHundred((int)number));
        }

        return parts.ToString().Trim();
    }

    private static void AppendScaled(StringBuilder sb, long value, string scale)
    {
        if (value <= 0)
            return;

        if (sb.Length > 0)
            sb.Append(' ');

        sb.Append(ConvertBelowThousand((int)value));
        sb.Append(' ');
        sb.Append(scale);
    }

    private static string ConvertBelowThousand(int number)
    {
        if (number >= 100)
        {
            var hundreds = number / 100;
            var rest = number % 100;
            return rest == 0
                ? $"{Ones[hundreds]} Hundred"
                : $"{Ones[hundreds]} Hundred {ConvertBelowHundred(rest)}";
        }

        return ConvertBelowHundred(number);
    }

    private static string ConvertBelowHundred(int number)
    {
        if (number < 20)
            return Ones[number];

        var ten = number / 10;
        var one = number % 10;
        return one == 0 ? Tens[ten] : $"{Tens[ten]} {Ones[one]}";
    }
}
