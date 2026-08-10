using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.Services.Ui;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App;

public partial class App : Application
{
    public static AppServices Services { get; private set; } = null!;

    private bool _reloginRequested;

    public static void RequestUserLogout()
    {
        if (Current is App app)
            app.RequestUserLogoutInternal();
    }

    private void RequestUserLogoutInternal()
    {
        _reloginRequested = true;
        Services.PeriodicSync.Stop();
        Services.CentralMode.Stop();
        Services.MongoHealth.Stop();
        var email = Services.UserSession?.LoggedInUser.Email;
        Services.UserSession = null;
        if (!string.IsNullOrWhiteSpace(email))
            _ = ReleaseSessionForEmailAsync(email);
        // Keep app alive — OnMainWindowClose would exit when billing window closes.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        if (MainWindow is Window main)
        {
            MainWindow = null;
            main.Close();
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        TraceAutomationStartup("OnStartup");
        UiDensityService.EnsureDefaults();
        RegisterDefaultWindowIcon();
        TraceAutomationStartup("UI defaults registered");
        DispatcherUnhandledException += (_, args) =>
        {
            AppDialog.Show(
                args.Exception.Message,
                "RR Bridal Billing — error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        DotEnvLoader.Load();
        TraceAutomationStartup("Environment loaded");
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        TraceAutomationStartup("Creating services");
        Services = AppServices.CreateDefault();
        TraceAutomationStartup("Services created");
        CounterConfigValidator.WarnIfDefaultDevice(Services.StoreContext);
        _ = RunStartupAsync();
    }

    private void RegisterDefaultWindowIcon()
    {
        var iconUri = new Uri("pack://application:,,,/Resources/Assets/TruBill.ico", UriKind.Absolute);
        var appIcon = BitmapFrame.Create(iconUri);
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window window && window.Icon == null)
                    window.Icon = appIcon;
            }));
    }

    private async Task RunStartupAsync()
    {
        try
        {
            TraceAutomationStartup("RunStartupAsync");
            // If this till is already Online locally, skip Mongo/ZeroTier gate immediately.
            // Do not wait on central inherit first — inherit must never re-enable the gate.
            var skipMongoGate = Services.CentralMode.IsOnlineMode
                                || !Services.StoreMongoOptions.RequireReady;

            // Inherit store-wide Online from central (public endpoint). Only promotes to Online.
            try
            {
                using var inheritCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await Services.CentralMode.SyncFromCentralStoreAsync(inheritCts.Token).ConfigureAwait(true);
            }
            catch
            {
                /* use local PreferCentralOnline */
            }

            skipMongoGate = skipMongoGate
                            || Services.CentralMode.IsOnlineMode
                            || !Services.StoreMongoOptions.RequireReady;

            // ZeroTier / parent-Mongo gate: Offline stores with STORE_MONGO_REQUIRE_READY=true.
            // Central Online (local or inherited) skips — counters talk only to CENTRAL_API_BASE.
            if (!skipMongoGate)
            {
                if (!await Services.MongoHealth.WaitUntilReadyAsync().ConfigureAwait(true))
                {
                    Shutdown();
                    return;
                }
            }

            string syncWarning = "";
            if (Services.CentralMode.IsOnlineMode)
            {
                // Online login uses central auth — no local store_users / Mongo required.
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    await Services.CentralMode.ProbeHealthAsync(cts.Token).ConfigureAwait(true);
                    if (!Services.CentralMode.IsCentralReachable)
                        syncWarning = "Central API unreachable. Login requires CENTRAL_API_BASE.";
                }
                catch
                {
                    syncWarning = "Could not reach the central server.";
                }
            }
            else
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await ((Services.SyncEngine as Services.Sync.SyncEngine)?.SyncStoreUsersAsync(cts.Token)
                           ?? Task.CompletedTask).ConfigureAwait(true);
                }
                catch
                {
                    syncWarning = "Could not reach the server to sync users.";
                }

                try
                {
                    await Services.ShellBranding.RefreshAsync().ConfigureAwait(true);
                }
                catch { /* best-effort before login */ }

                try
                {
                    var localUsers = await Services.LocalAuth.GetAllUsersAsync().ConfigureAwait(true);
                    if (localUsers.Count == 0)
                    {
                        syncWarning = string.IsNullOrEmpty(syncWarning)
                            ? $"No users found for store '{Services.StoreContext.StoreId}'. Check STORE_ID and backend."
                            : $"{syncWarning} No cached users found for store '{Services.StoreContext.StoreId}'.";
                    }
                }
                catch (Exception ex)
                {
                    syncWarning = string.IsNullOrEmpty(syncWarning)
                        ? $"Store MongoDB unavailable: {ex.Message}"
                        : $"{syncWarning} Store MongoDB unavailable: {ex.Message}";
                }
            }

            if (Services.CentralMode.IsOnlineMode)
            {
                try
                {
                    await Services.ShellBranding.RefreshAsync().ConfigureAwait(true);
                }
                catch { /* best-effort before login */ }
            }

            while (true)
            {
                TraceAutomationStartup("Showing login");
                if (!await TryShowLoginAsync(syncWarning).ConfigureAwait(true))
                {
                    Services.CentralMode.Stop();
                    Services.MongoHealth.Stop();
                    Shutdown();
                    return;
                }

                var authenticatedUser = _lastAuthenticatedUser!;
                Services.UserSession = new UserSession
                {
                    LoggedInUser = authenticatedUser,
                    SelectedBillingUser = authenticatedUser,
                };

                try
                {
                    await Services.ShellBranding.RefreshAsync().ConfigureAwait(true);
                }
                catch { /* best-effort after login */ }

                // After central JWT login, ensure store-wide Online is published and re-synced.
                if (Services.CentralMode.IsOnlineMode)
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        await Services.StoreInfo
                            .SetPreferCentralOnlineAsync(Services.StoreContext.StoreId, true, cts.Token)
                            .ConfigureAwait(true);
                        await Services.CentralMode.SyncFromCentralStoreAsync(cts.Token).ConfigureAwait(true);
                    }
                    catch { /* ignore */ }
                }

                _reloginRequested = false;
                await ShowMainWindowAsync().ConfigureAwait(true);

                if (_reloginRequested)
                {
                    syncWarning = "";
                    continue;
                }

                Services.MongoHealth.Stop();
                Services.CentralMode.Stop();
                Shutdown();
                return;
            }
        }
        catch (Exception ex)
        {
            TraceAutomationStartup($"Startup failed: {ex}");
            Services.CentralMode.Stop();
            Services.MongoHealth.Stop();
            AppDialog.Show(
                $"Could not start billing: {ex.Message}",
                "RR Bridal Billing",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static void TraceAutomationStartup(string message)
    {
        var path = Environment.GetEnvironmentVariable("RRBRIDAL_UI_TRACE_FILE");
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            File.AppendAllText(
                path,
                $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Test diagnostics must never affect production startup.
        }
    }

    private StoreUserRecord? _lastAuthenticatedUser;

    private async Task<bool> TryShowLoginAsync(string syncWarning)
    {
        bool? loginOk = null;
        StoreUserRecord? authenticatedUser = null;

        await Dispatcher.InvokeAsync(() =>
        {
            var loginWindow = new LoginWindow(Services, syncWarning);
            loginOk = loginWindow.ShowDialog();
            authenticatedUser = loginWindow.AuthenticatedUser;
        });

        _lastAuthenticatedUser = authenticatedUser;
        return loginOk == true && authenticatedUser is not null;
    }

    private async Task ShowMainWindowAsync()
    {
        var closed = new TaskCompletionSource();

        await Dispatcher.InvokeAsync(() =>
        {
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Closed += (_, _) =>
            {
                Services.PeriodicSync.Stop();
                Services.CentralMode.Stop();
                Services.MongoHealth.Stop();
                if (!_reloginRequested)
                    _ = ReleaseCurrentUserSessionAsync();
                if (ReferenceEquals(MainWindow, mainWindow))
                    MainWindow = null;
                closed.TrySetResult();
            };
            mainWindow.Show();
            mainWindow.Activate();
            mainWindow.Focus();
            Services.PeriodicSync.Start();
            Services.CentralMode.Start();
            Services.MongoHealth.Start();
        });

        await closed.Task.ConfigureAwait(true);
    }

    private static async Task ReleaseCurrentUserSessionAsync()
    {
        var email = Services.UserSession?.LoggedInUser.Email;
        if (string.IsNullOrWhiteSpace(email))
            return;

        await ReleaseSessionForEmailAsync(email).ConfigureAwait(false);
    }

    private static async Task ReleaseSessionForEmailAsync(string email)
    {
        try
        {
            if (Services.CentralMode.IsOnlineMode)
            {
                Services.CentralAuthClient.Logout();
                return;
            }

            await Services.LocalAuth.ReleaseSessionAsync(email).ConfigureAwait(false);
        }
        catch
        {
            /* best-effort */
        }
    }
}
