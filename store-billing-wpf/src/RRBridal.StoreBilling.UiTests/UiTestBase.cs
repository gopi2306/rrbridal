namespace RRBridal.StoreBilling.UiTests;

public abstract class UiTestBase
{
    private readonly BillingAppFixture _fixture;

    protected UiTestBase(BillingAppFixture fixture)
    {
        _fixture = fixture;
    }

    internal void RunWithDiagnostics(string testName, Action<BillingAppSession> test)
        => RunWithDiagnostics(testName, UiTestLaunchOptions.Online, test);

    internal void RunWithDiagnostics(
        string testName,
        UiTestLaunchOptions options,
        Action<BillingAppSession> test)
    {
        using var session = _fixture.Launch(options);
        try
        {
            test(session);
        }
        catch
        {
            session.CaptureDiagnostics(testName);
            throw;
        }
    }
}
