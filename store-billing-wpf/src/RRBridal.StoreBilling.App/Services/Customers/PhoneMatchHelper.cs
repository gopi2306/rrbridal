using System;
using System.Linq;

namespace RRBridal.StoreBilling.App.Services.Customers;

public static class PhoneMatchHelper
{
    public static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "";

        var digits = OnlyDigits(phone);
        if (digits.Length >= 10)
            return digits[^10..];
        return digits;
    }

    public static bool IsPhoneLikeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;
        return OnlyDigits(query).Length >= 10;
    }

    public static bool PhoneMatches(string? stored, string? query)
    {
        var qNorm = NormalizePhone(query);
        if (string.IsNullOrEmpty(qNorm) || qNorm.Length < 10)
            return false;

        if (string.IsNullOrWhiteSpace(stored))
            return false;

        if (NormalizePhone(stored) == qNorm)
            return true;

        foreach (var segment in stored.Split(['/', ',', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (NormalizePhone(segment) == qNorm)
                return true;
        }

        var storedDigits = OnlyDigits(stored);
        return storedDigits.Contains(qNorm, StringComparison.Ordinal);
    }

    private static string OnlyDigits(string value) => new string(value.Where(char.IsDigit).ToArray());
}
