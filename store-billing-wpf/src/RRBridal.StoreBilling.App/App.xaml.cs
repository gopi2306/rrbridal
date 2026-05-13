using System.Threading;
using System.Windows;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App;

public partial class App : Application
{
    public static AppServices Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DotEnvLoader.Load();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Services = AppServices.CreateDefault();

        string syncWarning = "";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await ((Services.SyncEngine as Services.Sync.SyncEngine)?.SyncStoreUsersAsync(cts.Token)
                   ?? System.Threading.Tasks.Task.CompletedTask);
        }
        catch
        {
            syncWarning = "Could not reach the server to sync users.";
        }

        var localUsers = await Services.LocalAuth.GetAllUsersAsync();
        if (localUsers.Count == 0)
        {
            syncWarning = string.IsNullOrEmpty(syncWarning)
                ? $"No users found for store '{Services.StoreContext.StoreId}'. Check STORE_ID and backend."
                : $"{syncWarning} No cached users found for store '{Services.StoreContext.StoreId}'.";
        }

        var loginWindow = new LoginWindow(Services.LocalAuth, syncWarning);
        var result = loginWindow.ShowDialog();

        if (result != true || loginWindow.AuthenticatedUser is null)
        {
            Shutdown();
            return;
        }

        Services.UserSession = new UserSession
        {
            LoggedInUser = loginWindow.AuthenticatedUser,
        };

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }
}
