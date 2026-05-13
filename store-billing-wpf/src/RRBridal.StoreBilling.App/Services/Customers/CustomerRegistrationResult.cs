namespace RRBridal.StoreBilling.App.Services.Customers;

/// <summary>Outcome of persisting a new customer locally and attempting central sync.</summary>
public sealed class CustomerRegistrationResult
{
    public required string LocalMongoId { get; init; }

    public string? CentralCustomerId { get; init; }

    public required string CentralSyncStatus { get; init; }

    public string? CentralSyncWarning { get; init; }

    public required string CustomerName { get; init; }

    public required string CustomerPhone { get; init; }

    public required string DoorNo { get; init; }

    public required string Street { get; init; }

    public required string FullAddress { get; init; }

    /// <summary>Prefer central id when synced; otherwise local Mongo <c>_id</c>.</summary>
    public required string BillingCustomerCode { get; init; }
}
