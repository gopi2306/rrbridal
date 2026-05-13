using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public sealed class ReceiptConfigDocument
{
    public StoreProfile Store { get; set; } = new();

    public ReceiptPrintSettings Print { get; set; } = new();
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
}

public sealed class ReceiptPrintSettings
{
    /// <summary>Windows print queue full name, e.g. "\\\\server\\POS80" or local queue name.</summary>
    public string? BillPrinterFullName { get; set; }

    public bool AlwaysUsePrintDialog { get; set; }

    /// <summary>Monospace receipt width in characters (80mm thermal ~42–48).</summary>
    public int ReceiptCharWidth { get; set; } = 42;
}
