using System;
using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public enum CreditReceiptKind
{
    CreditInvoiceAtPost,
    BalanceCollection,
}

public sealed class CreditPaymentHistoryRow
{
    public string Kind { get; init; } = "";
    public string ReceivedAtDisplay { get; init; } = "";
    public decimal Amount { get; init; }
    public string Mode { get; init; } = "";
    public string Reference { get; init; } = "";
    public string ReceiptNo { get; init; } = "";
    public string ReceivedBy { get; init; } = "";
}

public sealed class CreditReceiptLine
{
    public int LineNo { get; init; }
    public string Description { get; init; } = "";
    public decimal Qty { get; init; }
    public decimal Rate { get; init; }
    public decimal Amount { get; init; }
}

public sealed class CreditReceiptPrintInput
{
    public required CreditReceiptKind Kind { get; init; }

    public required StoreProfile Store { get; init; }

    public int CharWidth { get; init; } = 48;

    public required string BillNo { get; init; }

    public string BillDate { get; init; } = "";

    public string? ReceiptNo { get; init; }

    public string CustomerName { get; init; } = "";

    public string CustomerPhone { get; init; } = "";

    public string CustomerCode { get; init; } = "";

    public string Salesman { get; init; } = "";

    public string Counter { get; init; } = "";

    public IReadOnlyList<CreditReceiptLine> Lines { get; init; } = Array.Empty<CreditReceiptLine>();

    public decimal TotalPayable { get; init; }

    public decimal AdvanceAtPost { get; init; }

    public decimal AmountPaidThisTime { get; init; }

    public decimal CumulativeAmountPaid { get; init; }

    public decimal BalanceDue { get; init; }

    public string Status { get; init; } = "";

    public string PaymentMode { get; init; } = "";

    public string Reference { get; init; } = "";

    public string ReceivedBy { get; init; } = "";

    public string ReceivedAtDisplay { get; init; } = "";

    public IReadOnlyList<CreditPaymentHistoryRow> PaymentHistory { get; init; } = Array.Empty<CreditPaymentHistoryRow>();

    public string DocumentTitle =>
        Kind == CreditReceiptKind.BalanceCollection ? "PAYMENT RECEIPT" : "CREDIT INVOICE";
}
