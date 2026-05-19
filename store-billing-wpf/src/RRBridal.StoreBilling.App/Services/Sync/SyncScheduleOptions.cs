using System;

namespace RRBridal.StoreBilling.App.Services.Sync;

public sealed class SyncScheduleOptions
{
    private const int DefaultIntervalMinutes = 5;
    private const int MaxIntervalMinutes = 1440;

    public int IntervalMinutes { get; }

    public bool Enabled => IntervalMinutes > 0;

    public SyncScheduleOptions()
    {
        IntervalMinutes = ParseInterval(Environment.GetEnvironmentVariable("SYNC_INTERVAL_MINUTES"));
    }

    private static int ParseInterval(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DefaultIntervalMinutes;

        if (!int.TryParse(raw.Trim(), out var minutes))
            return 0;

        if (minutes <= 0)
            return 0;

        return Math.Min(minutes, MaxIntervalMinutes);
    }
}
