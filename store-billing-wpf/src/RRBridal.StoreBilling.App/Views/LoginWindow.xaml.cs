using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.Services.Ui;
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
        Loaded += async (_, _) =>
        {
            WindowLayoutHelper.CenterOnScreen(this);
            await _vm.RefreshBrandingAsync();
            UpdatePlaceholders();
            UpdateBrandPanelVisibility(ActualWidth);
            if (string.IsNullOrWhiteSpace(_vm.Email))
                EmailBox.Focus();
            else
                PasswordBox.Focus();
        };

        PasswordBox.KeyDown += OnPasswordKeyDown;
        EmailBox.KeyDown += OnEmailKeyDown;
    }

    public StoreUserRecord? AuthenticatedUser => _vm.AuthenticatedUser;

    private void OnEmailKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        PasswordBox.Focus();
        e.Handled = true;
    }

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm.LoginCommand.CanExecute(null))
        {
            _vm.LoginCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void LoginWindow_OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateBrandPanelVisibility(e.NewSize.Width);

    private void UpdateBrandPanelVisibility(double width)
    {
        var showBrand = width >= 640;
        BrandPanel.Visibility = showBrand ? Visibility.Visible : Visibility.Collapsed;
        BrandColumn.Width = showBrand ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    private void EmailBox_OnGotFocus(object sender, RoutedEventArgs e) => UpdatePlaceholders();
    private void EmailBox_OnLostFocus(object sender, RoutedEventArgs e) => UpdatePlaceholders();
    private void EmailBox_OnTextChanged(object sender, TextChangedEventArgs e) => UpdatePlaceholders();

    private void PasswordBox_OnGotFocus(object sender, RoutedEventArgs e)
    {
        _vm.Password = PasswordBox.Password;
        UpdatePlaceholders();
    }

    private void PasswordBox_OnLostFocus(object sender, RoutedEventArgs e) => UpdatePlaceholders();

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.Password = PasswordBox.Password;
        UpdatePlaceholders();
    }

    private void UpdatePlaceholders()
    {
        EmailPlaceholder.Visibility = string.IsNullOrEmpty(EmailBox.Text) && !EmailBox.IsKeyboardFocusWithin
            ? Visibility.Visible
            : Visibility.Collapsed;
        PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password) && !PasswordBox.IsKeyboardFocusWithin
            ? Visibility.Visible
            : Visibility.Collapsed;
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
