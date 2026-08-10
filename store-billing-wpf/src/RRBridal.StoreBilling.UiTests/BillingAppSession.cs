using System.Diagnostics;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.UIA3;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.UiTests;

internal sealed class BillingAppSession : IDisposable
{
    private readonly Action<BillingAppSession> _onDisposed;
    private readonly FakeCentralServer _server;
    private readonly LocalMongoFixture _mongo;
    private readonly string _tempDirectory;
    private bool _disposed;

    private BillingAppSession(
        Application application,
        UIA3Automation automation,
        Window window,
        FakeCentralServer server,
        LocalMongoFixture mongo,
        string tempDirectory,
        Action<BillingAppSession> onDisposed)
    {
        Application = application;
        Automation = automation;
        Window = window;
        _server = server;
        _mongo = mongo;
        _tempDirectory = tempDirectory;
        _onDisposed = onDisposed;
    }

    public Application Application { get; }

    public UIA3Automation Automation { get; }

    public Window Window { get; private set; }

    public LoginPage Login => new(Window);

    public ShellPage Shell => new(Window);

    internal FakeCentralState CentralState => _server.State;

    internal string MongoDatabaseName => _mongo.DatabaseName;

    internal IMongoDatabase MongoDatabase => _mongo.Database;

    internal string DataDirectory => Path.Combine(_tempDirectory, "data");

    public static BillingAppSession Launch(
        UiTestLaunchOptions options,
        Action<BillingAppSession> onDisposed)
    {
        var exePath = UiTestEnvironment.AppExePath;
        var tempDirectory = Directory.CreateTempSubdirectory("rrbridal-ui-tests-").FullName;
        var dataDirectory = Path.Combine(tempDirectory, "data");
        var startupTracePath = Path.Combine(tempDirectory, "startup-trace.log");
        Directory.CreateDirectory(dataDirectory);

        FakeCentralServer? server = null;
        LocalMongoFixture? mongo = null;
        try
        {
            var scenario = options.CentralScenario with
            {
                PreferCentralOnline = options.Mode == PosMode.Online
                                      && options.CentralScenario.PreferCentralOnline,
                SeedOpenDaySession = options.SeedOpenDaySession
                                     || options.CentralScenario.SeedOpenDaySession,
            };
            server = FakeCentralServer.Start(scenario);
            mongo = LocalMongoFixture.CreateAsync(options).GetAwaiter().GetResult();
        }
        catch
        {
            mongo?.Dispose();
            server?.Dispose();
            TryDeleteDirectory(tempDirectory);
            throw;
        }

        var startInfo = new ProcessStartInfo(exePath)
        {
            WorkingDirectory = tempDirectory,
            UseShellExecute = false,
        };
        var childEnvironment = new Dictionary<string, string>
        {
            ["CENTRAL_API_BASE"] = server.BaseAddress.AbsoluteUri.TrimEnd('/'),
            ["PREFER_CENTRAL_ONLINE"] = options.Mode == PosMode.Online ? "true" : "false",
            ["STORE_POS_MODE"] = options.Mode == PosMode.Online ? "online" : "offline",
            ["STORE_MONGO_URI"] = mongo.ConnectionUri,
            ["STORE_MONGO_REQUIRE_READY"] = options.Mode == PosMode.Offline ? "true" : "false",
            ["STORE_ID"] = "ui-test-store",
            ["DEVICE_ID"] = "ui-test-device",
            ["POS_COUNTER"] = "1",
            ["SYNC_INTERVAL_MINUTES"] = "1440",
            ["RRBRIDAL_DATA_DIR"] = dataDirectory,
            ["RRBRIDAL_UI_TRACE_FILE"] = startupTracePath,
            ["RRBRIDAL_UI_AUTOMATION"] = "1",
        };
        foreach (var (name, value) in childEnvironment)
            startInfo.Environment[name] = value;

        // DotEnvLoader intentionally gives .env precedence. Keeping an isolated copy in the
        // working directory prevents a developer's repository .env from changing test behavior.
        File.WriteAllLines(
            Path.Combine(tempDirectory, ".env"),
            childEnvironment.Select(pair => $"{pair.Key}={pair.Value.Replace('\\', '/')}"));

        Application? application = null;
        UIA3Automation? automation = null;
        try
        {
            application = Application.Launch(startInfo);
            automation = new UIA3Automation();
            var window = WaitForInitialWindow(application, automation, server, startupTracePath);
            return new BillingAppSession(
                application,
                automation,
                window,
                server,
                mongo,
                tempDirectory,
                onDisposed);
        }
        catch
        {
            automation?.Dispose();
            TryTerminate(application);
            application?.Dispose();
            server.Dispose();
            mongo.Dispose();
            TryDeleteDirectory(tempDirectory);
            throw;
        }
    }

    public Window RefreshWindow()
    {
        Window = Wait.UntilNotNull(
            () => Application.GetMainWindow(Automation),
            UiTestEnvironment.ActionTimeout,
            "the billing application's active window");
        return Window;
    }

    private static Window WaitForInitialWindow(
        Application application,
        UIA3Automation automation,
        FakeCentralServer server,
        string startupTracePath)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < UiTestEnvironment.StartupTimeout)
        {
            if (application.HasExited)
                throw new InvalidOperationException(
                    $"The billing process exited before showing a window (exit code {application.ExitCode}).");

            var window = application.GetMainWindow(automation, TimeSpan.FromSeconds(1));
            if (window is not null)
                return window;

            Thread.Sleep(100);
        }

        var startupTrace = File.Exists(startupTracePath)
            ? File.ReadAllText(startupTracePath)
            : "<no startup trace>";
        throw new TimeoutException(
            $"The billing process stayed running but exposed no window within {UiTestEnvironment.StartupTimeout.TotalSeconds:N0} seconds. "
            + $"Fake central requests: {server.RequestSummary}. Startup trace: {startupTrace}");
    }

    public ShellPage WaitForShell()
    {
        Window = Wait.UntilNotNull(
            () =>
            {
                var candidate = Application.GetMainWindow(Automation);
                return candidate is not null && new ShellPage(candidate).IsDisplayed()
                    ? candidate
                    : null;
            },
            UiTestEnvironment.StartupTimeout,
            "the billing shell after login");
        return new ShellPage(Window);
    }

    public void CaptureDiagnostics(string testName)
    {
        var safeName = string.Concat(testName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var directory = Path.Combine(
            UiTestEnvironment.ArtifactsDirectory,
            $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{safeName}");
        Directory.CreateDirectory(directory);

        try
        {
            Capture.Element(Window).ToFile(Path.Combine(directory, "window.png"));
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(directory, "screenshot-error.txt"), ex.ToString());
        }

        try
        {
            var tree = new StringBuilder();
            AppendTree(Window, tree, 0);
            File.WriteAllText(Path.Combine(directory, "ui-tree.txt"), tree.ToString());
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(directory, "ui-tree-error.txt"), ex.ToString());
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        TryTerminate(Application);
        Automation.Dispose();
        Application.Dispose();
        _server.Dispose();
        _mongo.Dispose();
        TryDeleteDirectory(_tempDirectory);
        _onDisposed(this);
    }

    private static void AppendTree(AutomationElement element, StringBuilder output, int depth)
    {
        output
            .Append(' ', depth * 2)
            .Append(ReadProperty(() => element.ControlType.ToString()))
            .Append(" Name=\"").Append(ReadProperty(() => element.Name)).Append('"')
            .Append(" AutomationId=\"").Append(ReadProperty(() => element.AutomationId)).AppendLine("\"");

        foreach (var child in element.FindAllChildren())
            AppendTree(child, output, depth + 1);
    }

    private static string ReadProperty(Func<string?> read)
    {
        try
        {
            return read() ?? "";
        }
        catch
        {
            return "<unsupported>";
        }
    }

    private static void TryTerminate(Application? application)
    {
        if (application is null)
            return;

        try
        {
            if (!application.HasExited)
            {
                using var process = Process.GetProcessById(application.ProcessId);
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5_000);
            }
        }
        catch
        {
            // Cleanup is best-effort; the fixture makes another pass at all tracked sessions.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
                return;
            }
            catch when (attempt < 2)
            {
                Thread.Sleep(150);
            }
            catch
            {
                // Temp cleanup must not hide a test result.
            }
        }
    }
}
