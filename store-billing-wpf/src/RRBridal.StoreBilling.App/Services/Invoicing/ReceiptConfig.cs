using System;
using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public enum InvoicePrintFormat
{
    Thermal,
    A4,
    A5,
}

public sealed class ReceiptConfigDocument
{
    public StoreProfile Store { get; set; } = new();

    public ReceiptPrintSettings Print { get; set; } = new();

    public DateTime? LastReceiptSettingsSyncUtc { get; set; }
}

public sealed class ReceiptQrSlotConfig
{
    public string Label { get; set; } = "";

    public string Payload { get; set; } = "";
}

public sealed class StoreProfile
{
    public string StoreName { get; set; } = "RR Bridal";

    public string Address { get; set; } = "";

    public string CustomerCarePhone { get; set; } = "";

    public string Gstin { get; set; } = "";

    public string FssaiNo { get; set; } = "";

    public string BranchCode { get; set; } = "";

    public string Website { get; set; } = "";

    public string TermsAndConditions { get; set; } =
        "Goods once sold are subject to store policy. Please verify bill before leaving.";

    public List<string> PolicyLines { get; set; } = new()
    {
        "Please retain this invoice for exchange reference.",
        "Thank you for shopping with us.",
    };

    public string ThankYouLine { get; set; } = "Thank you for shopping — visit again!";

    public string? LogoUrl { get; set; }

    public string? LogoFilePath { get; set; }

    public List<ReceiptQrSlotConfig> QrSlots { get; set; } = new();

    public bool ShowBillBarcode { get; set; } = true;
}

public sealed class ReceiptPrintSettings
{
    /// <summary>Windows print queue full name, e.g. "\\\\server\\POS80" or local queue name.</summary>
    public string? BillPrinterFullName { get; set; }

    public bool AlwaysUsePrintDialog { get; set; }

    /// <summary>Monospace receipt width in characters (80mm thermal ~42–48).</summary>
    public int ReceiptCharWidth { get; set; } = 48;

    public InvoicePrintFormat PrintFormat { get; set; } = InvoicePrintFormat.Thermal;

    public string? CentralPrinterHint { get; set; }

    public string? CentralPrinterModel { get; set; }

    /// <summary>When A5 format: print data values only on pre-printed stationery.</summary>
    public bool A5PrePrintedEnabled { get; set; }

    /// <summary>When PrintFormat is A4 or A5: also print 80mm thermal receipt before the invoice.</summary>
    public bool AlsoPrintThermalFirst { get; set; }

    /// <summary>mm alignment and font for A5 pre-printed value-only mode.</summary>
    public A5PrePrintedLayoutSettings A5PrePrintedLayout { get; set; } = A5PrePrintedLayoutSettings.CreateDefault();
}
