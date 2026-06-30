namespace RRBridal.StoreBilling.App.Services.Salesmen;

public sealed class SalesmanRecord
{
    public string LocalMongoId { get; init; } = "";
    public string CentralId { get; init; } = "";
    public string SalesmanCode { get; init; } = "";
    public string Name { get; init; } = "";
    public string Phone { get; init; } = "";
    public bool IsActive { get; init; } = true;
    public string CentralSyncStatus { get; init; } = "synced";

    public string DisplayLabel => string.IsNullOrWhiteSpace(SalesmanCode)
        ? Name
        : $"{SalesmanCode} — {Name}";

    public override string ToString() => DisplayLabel;
}
