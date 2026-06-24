using System;
using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Payments;

/// <summary>Ezetap / Razorpay POS Bridge credentials and wired device (saved locally per till).</summary>
public sealed class RazorpayPosSettingsDocument
{
    public bool Enabled { get; set; }

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

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(AppKey)
        && !string.IsNullOrWhiteSpace(DeviceId)
        && !string.IsNullOrWhiteSpace(ApiBaseUrl);

    public static string GetConfigurationStatusMessage(RazorpayPosSettingsDocument settings)
    {
        if (settings.IsConfigured)
            return "Card and UPI payments will be sent to the wired POS terminal.";

        if (!settings.Enabled)
            return "Card and UPI will be recorded manually when posting bills (no POS redirect).";

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(settings.Username))
            missing.Add("username");
        if (string.IsNullOrWhiteSpace(settings.AppKey))
            missing.Add("app key");
        if (string.IsNullOrWhiteSpace(settings.DeviceId))
            missing.Add("device ID");
        if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
            missing.Add("API base URL");

        var missingText = missing.Count > 0
            ? $" Missing: {string.Join(", ", missing)}."
            : "";
        return $"POS is enabled but incomplete.{missingText} Card and UPI will be recorded manually until configured.";
    }

    /// <summary>Ensures pushTo deviceId includes |ezetap_android when only serial is entered.</summary>
    public static string NormalizeDeviceId(string? deviceId)
    {
        var id = (deviceId ?? "").Trim();
        if (string.IsNullOrEmpty(id))
            return id;
        return id.Contains('|', StringComparison.Ordinal) ? id : $"{id}|ezetap_android";
    }
}
