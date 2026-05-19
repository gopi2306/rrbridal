using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;

    public LoginWindow(AppServices services, string syncWarning = "")
    {
        InitializeComponent();
        _vm = new LoginViewModel(services.LocalAuth, services.ShellBranding);
        if (!string.IsNullOrEmpty(syncWarning))
            _vm.SyncWarning = syncWarning;
        DataContext = _vm;

        _vm.LoginSucceeded += OnLoginSucceeded;
        Loaded += async (_, _) => await _vm.RefreshBrandingAsync();

        PasswordBox.PasswordChanged += (_, _) => _vm.Password = PasswordBox.Password;
        PasswordBox.KeyDown += OnPasswordKeyDown;
    }

    public StoreUserRecord? AuthenticatedUser => _vm.AuthenticatedUser;

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm.LoginCommand.CanExecute(null))
        {
            _vm.LoginCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnLoginSucceeded()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            if (!IsLoaded)
                return;
            try
            {
                DialogResult = true;
            }
            catch
            {
                Close();
            }
        });
    }
}
