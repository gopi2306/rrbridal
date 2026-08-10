namespace RRBridal.StoreBilling.UiTests;

internal static class Wait
{
    public static T UntilNotNull<T>(
        Func<T?> probe,
        TimeSpan timeout,
        string description,
        TimeSpan? interval = null)
        where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var value = probe();
                if (value is not null)
                    return value;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            Thread.Sleep(interval ?? TimeSpan.FromMilliseconds(150));
        }

        throw new TimeoutException(
            $"Timed out after {timeout.TotalSeconds:0.#} seconds waiting for {description}.",
            lastError);
    }

    public static void Until(
        Func<bool> condition,
        TimeSpan timeout,
        string description,
        TimeSpan? interval = null)
    {
        UntilNotNull(
            () => condition() ? Marker.Instance : null,
            timeout,
            description,
            interval);
    }

    private sealed class Marker
    {
        public static Marker Instance { get; } = new();
    }
}
