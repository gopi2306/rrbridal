using System.Collections.Concurrent;

namespace RRBridal.StoreBilling.UiTests;

public sealed class BillingAppFixture : IDisposable
{
    private readonly ConcurrentDictionary<BillingAppSession, byte> _sessions = new();

    internal BillingAppSession Launch(UiTestLaunchOptions? options = null)
    {
        var session = BillingAppSession.Launch(options ?? UiTestLaunchOptions.Online, Remove);
        _sessions.TryAdd(session, 0);
        return session;
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Keys)
            session.Dispose();

        _sessions.Clear();
    }

    private void Remove(BillingAppSession session)
    {
        _sessions.TryRemove(session, out _);
    }
}
