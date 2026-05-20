using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Auth;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly LocalAuthService _authService;
    private readonly ShellBrandingService _shellBranding;

    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private string _syncWarning = "";
    [ObservableProperty] private bool _isLoggingIn;
    [ObservableProperty] private string _companyTitle = "RR Bridal";
    [ObservableProperty] private string _storeDisplayName = "";
    [ObservableProperty] private string _tillDisplayLine = "";
    [ObservableProperty] private string _windowTitleText = "RR Bridal - Login";

    public StoreUserRecord? AuthenticatedUser { get; private set; }

    public string Password { private get; set; } = "";

    public LoginViewModel(LocalAuthService authService, ShellBrandingService shellBranding)
    {
        _authService = authService;
        _shellBranding = shellBranding;
    }

    public async Task RefreshBrandingAsync()
    {
        try
        {
            var snap = await _shellBranding.RefreshAsync();
            CompanyTitle = snap.CompanyTitle;
            StoreDisplayName = snap.StoreDisplayName;
            TillDisplayLine = snap.TillDisplayLine;
            WindowTitleText = $"{snap.CompanyTitle} — {snap.StoreDisplayName}";
        }
        catch { /* best-effort */ }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = "";

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Please enter your email.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your password.";
            return;
        }

        IsLoggingIn = true;
        try
        {
            var (user, error) = await _authService.TryLoginAsync(Email, Password);
            if (user is null)
            {
                ErrorMessage = string.IsNullOrEmpty(error)
                    ? "Invalid email or password."
                    : error;
                return;
            }

            AuthenticatedUser = user;
            // Invoke after IsLoggingIn clears so the window can close cleanly on the UI thread.
        }
        finally
        {
            IsLoggingIn = false;
            if (AuthenticatedUser != null)
                LoginSucceeded?.Invoke();
        }
    }

    public event System.Action? LoginSucceeded;
}
