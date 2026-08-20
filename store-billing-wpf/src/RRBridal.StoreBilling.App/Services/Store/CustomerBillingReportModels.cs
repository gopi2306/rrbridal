using System;
using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class CustomerBillingReportQuery
{
    public required DateTime From { get; init; }
    public required DateTime To { get; init; }
    public string? StoreCode { get; init; }
    public string? StoreName { get; init; }
    public string? CustomerSearch { get; init; }
    public string? CustomerCode { get; init; }
    public string? CustomerPhone { get; init; }
    public string? PosCounter { get; init; }
    public int Limit { get; init; } = 10_000;
}

public sealed class CustomerBillingReportResponse
{
    public CustomerBillingReportPeriod Period { get; init; } = new();
    public CustomerBillingReportFilters Filters { get; init; } = new();
    public int Limit { get; init; }
    public bool Truncated { get; init; }
    public int Total { get; init; }
    public CustomerBillingTotals Totals { get; init; } = new();
    public IReadOnlyList<CustomerBillingCustomerRow> Data { get; init; } = [];
}

public sealed class CustomerBillingReportPeriod
{
    public string From { get; init; } = "";
    public string To { get; init; } = "";
    public string Timezone { get; init; } = "";
    public string StoreCode { get; init; } = "";
    public string StoreName { get; init; } = "";
    public string? PosCounter { get; init; }
}

public sealed class CustomerBillingReportFilters
{
    public string? CustomerSearch { get; init; }
    public string? CustomerCode { get; init; }
    public string? CustomerPhone { get; init; }
}

public sealed class CustomerBillingPaymentSplits
{
    public decimal Cash { get; set; }
    public decimal Card { get; set; }
    public decimal Upi { get; set; }
    public decimal CreditNote { get; set; }
}

public sealed class CustomerBillingReturnDetail
{
    public string ReturnNo { get; init; } = "";
    public string ReturnDate { get; init; } = "";
    public string Kind { get; init; } = "";
    public string ReturnMode { get; init; } = "";
    public string CreditNoteNo { get; init; } = "";
    public decimal Qty { get; init; }
    public decimal Amount { get; init; }
}

public sealed class CustomerBillingLineDetail
{
    public int LineNo { get; init; }
    public string Sku { get; init; } = "";
    public string Description { get; init; } = "";
    public string Hsn { get; init; } = "";
    public decimal Qty { get; init; }
    public decimal Rate { get; init; }
    public decimal Amount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
}

public sealed class CustomerBillingBillDetail
{
    public string BillNo { get; init; } = "";
    public string BillDate { get; init; } = "";
    public string PosCounter { get; init; } = "";
    public string CustomerCode { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public decimal Qty { get; init; }
    public decimal GrossAmount { get; init; }
    public decimal ReturnAmount { get; init; }
    public decimal NetAmount { get; init; }
    public CustomerBillingPaymentSplits Payments { get; init; } = new();
    public IReadOnlyList<CustomerBillingReturnDetail> Returns { get; init; } = [];
    public IReadOnlyList<CustomerBillingLineDetail> Lines { get; init; } = [];
}

public sealed class CustomerBillingCustomerRow
{
    public string CustomerKey { get; init; } = "";
    public string CustomerCode { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public int BillCount { get; set; }
    public decimal Qty { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal ReturnAmount { get; set; }
    public decimal NetAmount { get; set; }
    public CustomerBillingPaymentSplits Payments { get; init; } = new();
    public List<CustomerBillingBillDetail> Bills { get; init; } = [];
}

public sealed class CustomerBillingTotals
{
    public int CustomerCount { get; set; }
    public int BillCount { get; set; }
    public decimal Qty { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal ReturnAmount { get; set; }
    public decimal NetAmount { get; set; }
    public CustomerBillingPaymentSplits Payments { get; init; } = new();
}
