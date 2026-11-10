using System.Linq;
using System.Text.RegularExpressions;

namespace RRBridal.StoreBilling.App.Services.Salesmen;

public static class SalesmanInputRules
{
    public const int NameMaxLength = 50;
    public const int PhoneLength = 10;
    public const int SearchMaxLength = 50;

    private static readonly Regex NamePattern = new(@"^[A-Za-z][A-Za-z .'\-]*$", RegexOptions.CultureInvariant);
    private static readonly Regex MobilePattern = new(@"^[6-9]\d{9}$", RegexOptions.CultureInvariant);

    public static bool IsAllDigits(string value) => value.Length > 0 && value.All(char.IsAsciiDigit);

    /// <summary>Allowed while typing a name: letters, space, dot, apostrophe, hyphen.</summary>
    public static bool IsAcceptableNameInput(string proposed) =>
        proposed.Length <= NameMaxLength
        && proposed.All(c => char.IsAsciiLetter(c) || c is ' ' or '.' or '\'' or '-');

    public static bool IsAcceptablePhoneInput(string proposed) =>
        proposed.Length <= PhoneLength && proposed.All(char.IsAsciiDigit);

    /// <summary>Allowed while typing a search: free text up to the max length; a number stops at 10 digits.</summary>
    public static bool IsAcceptableSearchInput(string proposed) =>
        proposed.Length <= SearchMaxLength
        && (!IsAllDigits(proposed.Trim()) || proposed.Trim().Length <= PhoneLength);

    public static string? ValidateName(string? name)
    {
        var value = name?.Trim() ?? "";
        if (value.Length == 0)
            return "Salesman name is required.";
        if (value.Length > NameMaxLength)
            return $"Salesman name must be at most {NameMaxLength} characters.";
        if (!NamePattern.IsMatch(value))
            return "Salesman name can contain only letters, spaces, dot ( . ), apostrophe ( ' ) and hyphen ( - ).";
        return null;
    }

    /// <summary>Phone is optional; when given it must be a 10-digit Indian mobile number.</summary>
    public static string? ValidatePhone(string? phone)
    {
        var value = phone?.Trim() ?? "";
        if (value.Length == 0)
            return null;
        if (!MobilePattern.IsMatch(value))
            return "Enter a valid 10-digit mobile number starting with 6, 7, 8 or 9.";
        return null;
    }

    /// <summary>Search accepts a name (letters, spaces, . ' -) or a phone number (up to 10 digits).</summary>
    public static string? ValidateSearch(string? search)
    {
        var value = search?.Trim() ?? "";
        if (value.Length == 0)
            return null;
        if (value.Length > SearchMaxLength)
            return $"Search text must be at most {SearchMaxLength} characters.";
        if (IsAllDigits(value))
            return value.Length <= PhoneLength
                ? null
                : $"Phone number search can have at most {PhoneLength} digits.";
        if (!NamePattern.IsMatch(value))
            return "Invalid search. Enter a salesman name (letters only) or a phone number (digits only).";
        return null;
    }
}
