using System;
using System.Linq;

namespace RRBridal.StoreBilling.App.Services.Customers;

public static class PhoneE164Helper
{
    public static string ToWhatsAppE164(string? phone, string defaultCountryCode = "91")
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "";

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            return "";

        var cc = new string((defaultCountryCode ?? "91").Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(cc))
            cc = "91";

        if (digits.Length == 10)
            return cc + digits;

        if (digits.StartsWith(cc, StringComparison.Ordinal) && digits.Length >= cc.Length + 10)
            return digits;

        return digits;
    }

    public static bool CanSendWhatsApp(string? phone, string defaultCountryCode = "91")
    {
        var e164 = ToWhatsAppE164(phone, defaultCountryCode);
        return e164.Length >= 11;
    }
}
