using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Auth;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AppServices _services;
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

    private static readonly TimeSpan ErrorDisplayDuration = TimeSpan.FromSeconds(5);
    private int _errorVersion;
    private string _password = "";

    public string Password
    {
        private get => _password;
        set
        {
            if (_password == value)
                return;
            _password = value;
            ErrorMessage = "";
        }
    }

    partial void OnEmailChanged(string value) => ErrorMessage = "";

    partial void OnErrorMessageChanged(string value)
    {
        var version = ++_errorVersion;
        if (string.IsNullOrEmpty(value))
            return;
        _ = HideErrorAfterDelayAsync(version);
    }

    private async Task HideErrorAfterDelayAsync(int version)
    {
        await Task.Delay(ErrorDisplayDuration);
        if (version == _errorVersion)
            ErrorMessage = "";
    }

    public LoginViewModel(AppServices services)
    {
        _services = services;
        _authService = services.LocalAuth;
        _shellBranding = services.ShellBranding;
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
        Email = Email.Trim();

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Please enter your email.";
            return;
        }

        if (!IsValidEmail(Email))
        {
            ErrorMessage = "Please enter a valid email address (e.g. name@example.com).";
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
            if (_services.CentralMode.IsOnlineMode)
            {
                var (ok, error, user) = await _services.CentralAuthClient.LoginWithUserAsync(Email, Password, default);
                if (!ok || user is null)
                {
                    ErrorMessage = string.IsNullOrWhiteSpace(error)
                        ? "Invalid email or password."
                        : error;
                    return;
                }

                AuthenticatedUser = user;
            }
            else
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
            }
            // Invoke after IsLoggingIn clears so the window can close cleanly on the UI thread.
        }
        finally
        {
            IsLoggingIn = false;
            if (AuthenticatedUser != null)
                LoginSucceeded?.Invoke();
        }
    }

    public event Action? LoginSucceeded;

    public const int EmailMaxLength = 254;

    private static readonly Regex EmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$",
        RegexOptions.CultureInvariant);

    public static bool IsValidEmail(string? email)
    {
        var value = email?.Trim() ?? "";
        return value.Length is > 0 and <= EmailMaxLength && EmailPattern.IsMatch(value);
    }
}
