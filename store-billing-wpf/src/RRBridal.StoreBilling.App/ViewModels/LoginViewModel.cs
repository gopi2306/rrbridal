using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services.Auth;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly LocalAuthService _authService;

    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private string _syncWarning = "";
    [ObservableProperty] private bool _isLoggingIn;

    public StoreUserRecord? AuthenticatedUser { get; private set; }

    public string Password { private get; set; } = "";

    public LoginViewModel(LocalAuthService authService)
    {
        _authService = authService;
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
            var user = await _authService.ValidateAsync(Email, Password);
            if (user is null)
            {
                ErrorMessage = "Invalid email or password.";
                return;
            }

            AuthenticatedUser = user;
            LoginSucceeded?.Invoke();
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    public event System.Action? LoginSucceeded;
}
