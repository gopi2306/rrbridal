using Xunit;

namespace RRBridal.StoreBilling.UiTests;

internal static class UiTestEnvironment
{
    private const string AppExeVariable = "RRBRIDAL_UI_APP_EXE";
    private const string ArtifactsVariable = "RRBRIDAL_UI_ARTIFACTS";

    public static TimeSpan StartupTimeout => ReadTimeout("RRBRIDAL_UI_STARTUP_TIMEOUT_SECONDS", 30);

    public static TimeSpan ActionTimeout => ReadTimeout("RRBRIDAL_UI_ACTION_TIMEOUT_SECONDS", 10);

    public static string AppExePath
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(AppExeVariable);
            if (!string.IsNullOrWhiteSpace(configured))
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));

            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "RRBridal.StoreBilling.App", "bin", "Debug", "net9.0-windows",
                "RRBridal.StoreBilling.App.exe"));
        }
    }

    public static string ArtifactsDirectory
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(ArtifactsVariable);
            return string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(AppContext.BaseDirectory, "ui-test-artifacts")
                : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));
        }
    }

    public static void SkipUnlessRunnable()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "WPF UI tests require Windows.");
        Skip.IfNot(Environment.UserInteractive, "WPF UI tests require an interactive desktop session.");
        Skip.If(
            string.Equals(
                Environment.GetEnvironmentVariable("RRBRIDAL_UI_TESTS_ENABLED"),
                "0",
                StringComparison.OrdinalIgnoreCase),
            "WPF UI tests were force-disabled with RRBRIDAL_UI_TESTS_ENABLED=0.");
        Skip.IfNot(
            File.Exists(AppExePath),
            $"Billing executable not found at '{AppExePath}'. Build the app or set {AppExeVariable}.");
    }

    private static TimeSpan ReadTimeout(string variable, int fallbackSeconds)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return int.TryParse(value, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromSeconds(fallbackSeconds);
    }
}
