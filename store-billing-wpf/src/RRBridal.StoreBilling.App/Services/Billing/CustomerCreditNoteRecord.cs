namespace RRBridal.StoreBilling.App.Services.Billing;

public sealed class CustomerCreditNoteRecord
{
    public required string CreditNoteNo { get; init; }
    public required string ReturnNo { get; init; }
    public required string OriginalBillNo { get; init; }
    public required string CustomerCode { get; init; }
    public required string CustomerPhone { get; init; }
    public required string CustomerName { get; init; }
    public required decimal Amount { get; init; }
    public required decimal RemainingAmount { get; init; }
    public required decimal TotalApplied { get; init; }
    public required string Status { get; init; }
    public string? ConsumedBillNo { get; init; }
    public string? LastAppliedBillNo { get; init; }
}
