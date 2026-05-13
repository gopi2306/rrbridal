namespace RRBridal.StoreBilling.App.Services.Masters;

public sealed class MasterItem
{
    public required string Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public bool IsActive { get; init; } = true;

    public string DisplayText => $"{Code} — {Name}";
}
