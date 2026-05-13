using System.Windows;
using System.Windows.Controls;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;

    public LoginWindow(LocalAuthService authService, string syncWarning = "")
    {
        InitializeComponent();
        _vm = new LoginViewModel(authService);
        if (!string.IsNullOrEmpty(syncWarning))
            _vm.SyncWarning = syncWarning;
        DataContext = _vm;

        _vm.LoginSucceeded += () => { DialogResult = true; };

        PasswordBox.PasswordChanged += (_, _) => _vm.Password = PasswordBox.Password;
    }

    public StoreUserRecord? AuthenticatedUser => _vm.AuthenticatedUser;
}
