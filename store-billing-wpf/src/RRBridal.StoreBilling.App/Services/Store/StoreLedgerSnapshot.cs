using System;
using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class StoreLedgerSnapshot
{
    public IReadOnlyList<LedgerBillRow> Bills { get; init; } = Array.Empty<LedgerBillRow>();

    public IReadOnlyList<LedgerPaymentRow> Payments { get; init; } = Array.Empty<LedgerPaymentRow>();
}

public sealed class LedgerBillRow
{
    public required string BillNo { get; init; }

    public required string BillDate { get; init; }

    public required string CustomerName { get; init; }

    public decimal Payable { get; init; }

    public required string PostedAtUtc { get; init; }

    public required string Status { get; init; }

    public DateTime SortUtc { get; init; }
}

public sealed class LedgerPaymentRow
{
    /// <summary>Used for ordering (CreatedAt when present, else Mongo <c>_id</c> time).</summary>
    public DateTime SortUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public required string CreatedAtDisplay { get; init; }

    public required string InvoiceNo { get; init; }

    public required string Provider { get; init; }

    public decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string Status { get; init; }

    public required string ProviderReference { get; init; }
}
