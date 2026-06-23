namespace RRBridal.StoreBilling.App.Services.Payments;

/// <summary>Ezetap / Razorpay POS Bridge credentials and wired device (saved locally per till).</summary>
public sealed class RazorpayPosSettingsDocument
{
    public bool Enabled { get; set; } = true;

    /// <summary>Merchant API username from Razorpay / Ezetap.</summary>
    public string Username { get; set; } = "";

    /// <summary>App key from Razorpay / Ezetap.</summary>
    public string AppKey { get; set; } = "";

    /// <summary>Base URL ending with / e.g. https://www.ezetap.com/api/3.0/p2padapter/</summary>
    public string ApiBaseUrl { get; set; } = "https://www.ezetap.com/api/3.0/p2padapter/";

    /// <summary>pushTo.deviceId — terminal serial + suffix, e.g. ABC123|ezetap_android</summary>
    public string DeviceId { get; set; } = "";

    public int StatusPollIntervalMs { get; set; } = 2000;

    public int StatusTimeoutSeconds { get; set; } = 120;

    /// <summary>Ensures pushTo deviceId includes |ezetap_android when only serial is entered.</summary>
    public static string NormalizeDeviceId(string? deviceId)
    {
        var id = (deviceId ?? "").Trim();
        if (string.IsNullOrEmpty(id))
            return id;
        return id.Contains('|', StringComparison.Ordinal) ? id : $"{id}|ezetap_android";
    }
}
