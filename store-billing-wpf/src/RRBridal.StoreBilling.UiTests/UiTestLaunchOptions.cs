namespace RRBridal.StoreBilling.UiTests;

internal enum PosMode
{
    Online,
    Offline,
}

internal sealed record UiTestLaunchOptions
{
    public static UiTestLaunchOptions Online { get; } = new();

    public static UiTestLaunchOptions Offline { get; } = new()
    {
        Mode = PosMode.Offline,
        SeedOpenDaySession = true,
    };

    public PosMode Mode { get; init; } = PosMode.Online;

    public bool SeedOpenDaySession { get; init; }

    public string? MongoUri { get; init; }

    public FakeCentralScenario CentralScenario { get; init; } = new();
}
