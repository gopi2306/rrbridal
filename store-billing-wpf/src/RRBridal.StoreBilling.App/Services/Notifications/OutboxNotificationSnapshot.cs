using System.Collections.Generic;

namespace RRBridal.StoreBilling.App.Services.Notifications;

public sealed class OutboxNotificationSnapshot
{
    public int ThisCounterPendingCount { get; init; }

    public int StoreWidePendingCount { get; init; }

    public string? LastSyncError { get; init; }

    public string? LastAutoSyncAt { get; init; }

    public IReadOnlyList<NotificationItem> Items { get; init; } = [];
}
