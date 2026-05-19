namespace RRBridal.StoreBilling.App.Services.Store;

internal static class CounterDisplayFormatter
{
    public static string Format(string? posCounter, string? deviceId)
    {
        var pos = posCounter?.Trim() ?? "";
        var dev = deviceId?.Trim() ?? "";
        if (!string.IsNullOrEmpty(pos) && !string.IsNullOrEmpty(dev))
            return $"POS{pos} · {dev}";
        if (!string.IsNullOrEmpty(pos))
            return $"POS{pos}";
        return dev;
    }
}
