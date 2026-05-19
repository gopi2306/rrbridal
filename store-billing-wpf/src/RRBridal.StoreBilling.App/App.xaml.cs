using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App;

public partial class App : Application
{
    public static AppServices Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                args.Exception.Message,
                "RR Bridal Billing — error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        DotEnvLoader.Load();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Services = AppServices.CreateDefault();
        CounterConfigValidator.WarnIfDefaultDevice(Services.StoreContext);
        _ = RunStartupAsync();
    }

    private async Task RunStartupAsync()
    {
        try
        {
            string syncWarning = "";
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

            var localUsers = await Services.LocalAuth.GetAllUsersAsync().ConfigureAwait(true);
            if (localUsers.Count == 0)
            {
                syncWarning = string.IsNullOrEmpty(syncWarning)
                    ? $"No users found for store '{Services.StoreContext.StoreId}'. Check STORE_ID and backend."
                    : $"{syncWarning} No cached users found for store '{Services.StoreContext.StoreId}'.";
            }

            bool? loginOk = null;
            StoreUserRecord? authenticatedUser = null;

            await Dispatcher.InvokeAsync(() =>
            {
                var loginWindow = new LoginWindow(Services, syncWarning);
                loginOk = loginWindow.ShowDialog();
                authenticatedUser = loginWindow.AuthenticatedUser;
            });

            if (loginOk != true || authenticatedUser is null)
            {
                Shutdown();
                return;
            }

            Services.UserSession = new UserSession
            {
                LoggedInUser = authenticatedUser,
            };

            try
            {
                await Services.ShellBranding.RefreshAsync().ConfigureAwait(true);
            }
            catch { /* best-effort after login */ }

            await Dispatcher.InvokeAsync(() =>
            {
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
                mainWindow.Activate();
                mainWindow.Focus();
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not start billing: {ex.Message}",
                "RR Bridal Billing",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }
}
