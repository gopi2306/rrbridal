using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly AppServices _services;

    public BillingViewModel Billing { get; }

    public DashboardViewModel Dashboard { get; }

    public AnalyticsViewModel Analytics { get; }

    public LedgerViewModel Ledger { get; }

    public CustomerRegistrationViewModel CustomersRegistration { get; }

    public SaleReturnViewModel SaleReturn { get; }

    public AdjustmentBillViewModel AdjustmentBill { get; }

    [ObservableProperty] private ShellPage _currentPage = ShellPage.Billing;

    public string LoggedInUserName => _services.UserSession?.LoggedInUser.Name ?? "Unknown";

    [ObservableProperty] private string _companyTitle = "RR Bridal";

    [ObservableProperty] private string _storeDisplayName = "";

    [ObservableProperty] private string _tillDisplayLine = "";

    [ObservableProperty] private string _windowTitleText = "RR Bridal";

    [ObservableProperty] private string _globalSearchText = "";

    [ObservableProperty] private int _pendingNotificationCount;

    public bool IsPrimaryCounter => _services.StoreContext.IsPrimaryCounter;

    public bool ShowDashboardNav => IsPrimaryCounter;

    public bool ShowAnalyticsNav => IsPrimaryCounter;

    public bool ShowLedgerNav => IsPrimaryCounter;

    public bool ShowSettingsNav => IsPrimaryCounter;

    public bool HasPendingNotifications => PendingNotificationCount > 0;

    public double NotificationBellOpacity => HasPendingNotifications ? 1.0 : 0.6;

    public string NotificationBellToolTip => HasPendingNotifications
        ? $"{PendingNotificationCount} pending sync item(s)"
        : "Notifications";

    partial void OnPendingNotificationCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasPendingNotifications));
        OnPropertyChanged(nameof(NotificationBellOpacity));
        OnPropertyChanged(nameof(NotificationBellToolTip));
    }

    public ShellViewModel(AppServices services)
    {
        _services = services;
        Billing = new BillingViewModel(services);
        Billing.NavigateToCustomerRegistration = () => CurrentPage = ShellPage.Customers;
        Billing.PostBillCanExecuteChanged += () => PostBillCommand.NotifyCanExecuteChanged();
        Billing.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BillingViewModel.SearchText) && CurrentPage == ShellPage.Billing)
                GlobalSearchText = Billing.SearchText;
        };
        Dashboard = new DashboardViewModel(services);
        Analytics = new AnalyticsViewModel(services);
        Ledger = new LedgerViewModel(services);
        CustomersRegistration = new CustomerRegistrationViewModel(services, Billing, () => CurrentPage = ShellPage.Billing);
        SaleReturn = new SaleReturnViewModel(services);
        AdjustmentBill = new AdjustmentBillViewModel(services);

        NotifyPageVisibility();
        _services.ShellBranding.BrandingChanged += OnBrandingChanged;
        if (!IsPrimaryCounter && IsRestrictedPage(CurrentPage))
            CurrentPage = ShellPage.Billing;

        _ = RefreshBrandingAsync();
        _ = RefreshNotificationCountAsync();
    }

    private static bool IsRestrictedPage(ShellPage page) =>
        page is ShellPage.Dashboard or ShellPage.Analytics or ShellPage.Ledger;

    private void OnBrandingChanged()
    {
        var snap = _services.ShellBranding.Current;
        CompanyTitle = snap.CompanyTitle;
        StoreDisplayName = snap.StoreDisplayName;
        TillDisplayLine = snap.TillDisplayLine;
        WindowTitleText = snap.WindowTitleText;
        Dashboard.ApplyBrandingFromShell();
        Ledger.ApplyBrandingFromShell();
    }

    public async Task RefreshBrandingAsync()
    {
        try
        {
            await _services.ShellBranding.RefreshAsync();
            OnBrandingChanged();
        }
        catch { /* best-effort */ }
    }

    public void EnsurePageVisibilityFresh()
    {
        OnPropertyChanged(nameof(IsBillingPage));
        OnPropertyChanged(nameof(IsDashboardPage));
        OnPropertyChanged(nameof(IsAnalyticsPage));
        OnPropertyChanged(nameof(IsCustomersPage));
        OnPropertyChanged(nameof(IsLedgerPage));
        OnPropertyChanged(nameof(IsSaleReturnPage));
        OnPropertyChanged(nameof(IsAdjustmentsPage));
    }

    private void NotifyPageVisibility() => EnsurePageVisibilityFresh();

    public async Task RefreshNotificationCountAsync()
    {
        try
        {
            PendingNotificationCount = await _services.OutboxNotifications.CountThisCounterPendingAsync();
        }
        catch
        {
            PendingNotificationCount = 0;
        }
    }

    public bool IsBillingPage => CurrentPage == ShellPage.Billing;

    public bool IsDashboardPage => CurrentPage == ShellPage.Dashboard;

    public bool IsAnalyticsPage => CurrentPage == ShellPage.Analytics;

    public bool IsCustomersPage => CurrentPage == ShellPage.Customers;

    public bool IsLedgerPage => CurrentPage == ShellPage.Ledger;

    public bool IsSaleReturnPage => CurrentPage == ShellPage.SaleReturn;

    public bool IsAdjustmentsPage => CurrentPage == ShellPage.Adjustments;

    partial void OnCurrentPageChanged(ShellPage value)
    {
        OnPropertyChanged(nameof(IsBillingPage));
        OnPropertyChanged(nameof(IsDashboardPage));
        OnPropertyChanged(nameof(IsAnalyticsPage));
        OnPropertyChanged(nameof(IsCustomersPage));
        OnPropertyChanged(nameof(IsLedgerPage));
        OnPropertyChanged(nameof(IsSaleReturnPage));
        OnPropertyChanged(nameof(IsAdjustmentsPage));

        if (value == ShellPage.Dashboard)
            _ = Dashboard.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.Analytics)
            _ = Analytics.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.Ledger)
            _ = Ledger.RefreshCommand.ExecuteAsync(null);
        GlobalSearchText = value == ShellPage.Billing ? Billing.SearchText : "";

        if (value == ShellPage.Billing)
            RequestBillingSearchFocus();

        PostBillCommand.NotifyCanExecuteChanged();
    }

    public void RequestBillingSearchFocus() =>
        _services.FocusSearch?.FocusGlobalSearch();

    partial void OnGlobalSearchTextChanged(string value)
    {
        if (CurrentPage == ShellPage.Billing)
            Billing.SearchText = value;
    }

    [RelayCommand]
    private void Navigate(ShellPage page)
    {
        if (!IsPrimaryCounter && IsRestrictedPage(page))
            return;
        CurrentPage = page;
    }

    [RelayCommand]
    private async Task SaveCustomerRegistration()
    {
        if (CurrentPage != ShellPage.Customers)
            return;
        await CustomersRegistration.SaveCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void CancelCustomerRegistration()
    {
        if (CurrentPage != ShellPage.Customers)
            return;
        CustomersRegistration.CancelCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var dlg = new SettingsDialog(_services) { Owner = Application.Current.MainWindow };
        dlg.ShowDialog();
        _ = RefreshBrandingAsync();
    }

    [RelayCommand]
    private async Task OpenNotifications()
    {
        var dlg = new NotificationsDialog(_services) { Owner = Application.Current.MainWindow };
        dlg.ShowDialog();
        await RefreshNotificationCountAsync();
    }

    [RelayCommand]
    private void FocusGlobalSearch() =>
        _services.FocusSearch?.FocusGlobalSearch();

    [RelayCommand]
    private void ShowHelp()
    {
        if (!EnsureBillingPage())
            return;
        Billing.ShowHelpCommand.Execute(null);
    }

    [RelayCommand]
    private void ClearForNewBill()
    {
        if (!EnsureBillingPage())
            return;
        Billing.ClearForNewBillCommand.Execute(null);
        RequestBillingSearchFocus();
    }

    private bool CanRunPostBill() => IsBillingPage && Billing.IsCustomerReadyForPost;

    [RelayCommand(CanExecute = nameof(CanRunPostBill))]
    private async Task PostBill()
    {
        if (!EnsureBillingPage())
            return;
        await Billing.PostBillCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void PrintStub()
    {
        if (!EnsureBillingPage())
            return;
        Billing.PrintStubCommand.Execute(null);
    }

    [RelayCommand]
    private void LogStub()
    {
        if (!EnsureBillingPage())
            return;
        Billing.LogStubCommand.Execute(null);
    }

    [RelayCommand]
    private void Logout()
    {
        var answer = MessageBox.Show(
            "Sign out and return to the login screen?",
            "RR Bridal Billing",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
            return;

        App.RequestUserLogout();
    }

    [RelayCommand]
    private static void CloseApp()
    {
        Application.Current.Shutdown();
    }

    private bool EnsureBillingPage()
    {
        if (CurrentPage != ShellPage.Billing)
        {
            MessageBox.Show(
                "Switch to Billing to use billing shortcuts (F1, F2, F9, F10 print, F11).",
                "RR Bridal Billing",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        return true;
    }
}
