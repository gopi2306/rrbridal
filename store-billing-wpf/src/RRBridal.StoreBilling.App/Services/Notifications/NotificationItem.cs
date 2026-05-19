namespace RRBridal.StoreBilling.App.Services.Notifications;

public sealed class NotificationItem
{
    public string EventId { get; init; } = "";

    public string Type { get; init; } = "";

    public string CreatedAtDisplay { get; init; } = "";

    public string Detail { get; init; } = "";

    public string DeviceId { get; init; } = "";
}
