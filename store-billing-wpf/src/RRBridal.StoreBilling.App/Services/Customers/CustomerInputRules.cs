using System.Linq;

namespace RRBridal.StoreBilling.App.Services.Customers;

/// <summary>Character and length rules for customer fields (ASCII only — no Unicode symbols or emoji).</summary>
public static class CustomerInputRules
{
    public const int MobileLength = 10;
    public const int TelephoneMaxLength = 15;
    public const int CustomerCodeMaxLength = 9;
    public const int NameMaxLength = 60;
    public const int GstinLength = 15;

    public static bool IsAcceptableMobile(string text) =>
        text.Length <= MobileLength && text.All(char.IsAsciiDigit);

    /// <summary>Landline with optional STD code, e.g. 040-23456789.</summary>
    public static bool IsAcceptableTelephone(string text) =>
        text.Length <= TelephoneMaxLength && text.All(c => char.IsAsciiDigit(c) || c is '-' or ' ');

    /// <summary>Codes look like CUST-0001: letters, digits and hyphen.</summary>
    public static bool IsAcceptableCustomerCode(string text) =>
        text.Length <= CustomerCodeMaxLength && text.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    /// <summary>Person or shop names: letters, digits, space and . ' - &amp;</summary>
    public static bool IsAcceptableName(string text) =>
        text.Length <= NameMaxLength
        && text.All(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '.' or '\'' or '-' or '&');

    public static bool IsAcceptableGstin(string text) =>
        text.Length <= GstinLength && text.All(char.IsAsciiLetterOrDigit);
}
