using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.Services.Store;
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

    public DuplicatePrintViewModel DuplicatePrint { get; }

    public BarcodePrintingViewModel BarcodePrinting { get; }

    public DailyExpenseViewModel DailyExpenses { get; }

    public DayCloseViewModel DayClose { get; }

    public SettingsViewModel Settings { get; }

    private ShellPage _lastPage = ShellPage.Billing;

    [ObservableProperty] private ShellPage _currentPage = ShellPage.Billing;

    public string LoggedInUserName => _services.UserSession?.LoggedInUser.Name ?? "Unknown";

    [ObservableProperty] private string _companyTitle = "RR Bridal";

    [ObservableProperty] private string _storeDisplayName = "";

    [ObservableProperty] private string _tillDisplayLine = "";

    [ObservableProperty] private string _windowTitleText = "RR Bridal";

    [ObservableProperty] private string _globalSearchText = "";

    [ObservableProperty] private int _pendingNotificationCount;

    [ObservableProperty] private string _daySessionStatusChip = "Day: …";

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
        Dashboard.NavigateToReturnForBill = billNo =>
        {
            CurrentPage = ShellPage.SaleReturn;
            SaleReturn.OriginalBillNo = billNo ?? "";
            _ = SaleReturn.LoadBillByNoAsync(billNo ?? "", skipDuplicateChecks: true);
        };
        Dashboard.NavigateToAdjustmentForBill = billNo =>
        {
            CurrentPage = ShellPage.Adjustments;
            AdjustmentBill.OriginalBillNo = billNo ?? "";
            _ = AdjustmentBill.LoadBillByNoAsync(billNo ?? "");
        };
        Analytics = new AnalyticsViewModel(services);
        Ledger = new LedgerViewModel(services);
        CustomersRegistration = new CustomerRegistrationViewModel(services, Billing, () => CurrentPage = ShellPage.Billing);
        SaleReturn = new SaleReturnViewModel(services);
        AdjustmentBill = new AdjustmentBillViewModel(services);
        DuplicatePrint = new DuplicatePrintViewModel(services);
        BarcodePrinting = new BarcodePrintingViewModel(services);
        DailyExpenses = new DailyExpenseViewModel(services);
        DayClose = new DayCloseViewModel(services);
        Settings = new SettingsViewModel(services);

        services.NotifyDaySessionChanged = () => _ = RefreshDaySessionStatusAsync();

        NotifyPageVisibility();
        _services.ShellBranding.BrandingChanged += OnBrandingChanged;
        if (!IsPrimaryCounter && IsRestrictedPage(CurrentPage))
            CurrentPage = ShellPage.Billing;

        _ = RefreshBrandingAsync();
        _ = RefreshNotificationCountAsync();
        _ = RefreshDaySessionStatusAsync();
    }

    private static bool IsRestrictedPage(ShellPage page) =>
        page is ShellPage.Dashboard or ShellPage.Analytics or ShellPage.Ledger or ShellPage.DailyExpenses or ShellPage.Settings;

    private void OnBrandingChanged()
    {
        var snap = _services.ShellBranding.Current;
        CompanyTitle = snap.CompanyTitle;
        StoreDisplayName = snap.StoreDisplayName;
        TillDisplayLine = snap.TillDisplayLine;
        WindowTitleText = snap.WindowTitleText;
        Dashboard.ApplyBrandingFromShell();
        DailyExpenses.ApplyBrandingFromShell();
        DayClose.ApplyBrandingFromShell();
        Ledger.ApplyBrandingFromShell();
        BarcodePrinting.ApplyBrandingFromShell();
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
        OnPropertyChanged(nameof(IsDuplicateBillPage));
        OnPropertyChanged(nameof(IsBarcodesPage));
        OnPropertyChanged(nameof(IsDayClosePage));
        OnPropertyChanged(nameof(IsDailyExpensesPage));
        OnPropertyChanged(nameof(IsSettingsPage));
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

    public bool IsDuplicateBillPage => CurrentPage == ShellPage.DuplicateBill;

    public bool IsBarcodesPage => CurrentPage == ShellPage.Barcodes;

    public bool IsDailyExpensesPage => CurrentPage == ShellPage.DailyExpenses;

    public bool IsDayClosePage => CurrentPage == ShellPage.DayClose;

    public bool IsSettingsPage => CurrentPage == ShellPage.Settings;

    public async Task RefreshDaySessionStatusAsync()
    {
        try
        {
            var storeId = _services.StoreContext.StoreId;
            var pos = _services.StoreContext.PosCounter;
            var businessDate = DaySessionService.FormatBusinessDate(DateTime.Today);
            var session = await _services.DaySessions.GetSessionAsync(storeId, businessDate, pos);
            if (session == null)
                DaySessionStatusChip = "Day: Not opened";
            else if (string.Equals(session.Status, DaySessionStatus.Closed, StringComparison.OrdinalIgnoreCase))
                DaySessionStatusChip = "Day: Closed";
            else
                DaySessionStatusChip = "Day: Open";
        }
        catch
        {
            DaySessionStatusChip = "Day: …";
        }
    }

    partial void OnCurrentPageChanged(ShellPage value)
    {
        if (_lastPage == ShellPage.Settings && value != ShellPage.Settings)
            _ = RefreshBrandingAsync();

        OnPropertyChanged(nameof(IsBillingPage));
        OnPropertyChanged(nameof(IsDashboardPage));
        OnPropertyChanged(nameof(IsAnalyticsPage));
        OnPropertyChanged(nameof(IsCustomersPage));
        OnPropertyChanged(nameof(IsLedgerPage));
        OnPropertyChanged(nameof(IsSaleReturnPage));
        OnPropertyChanged(nameof(IsAdjustmentsPage));
        OnPropertyChanged(nameof(IsDuplicateBillPage));
        OnPropertyChanged(nameof(IsBarcodesPage));
        OnPropertyChanged(nameof(IsDayClosePage));
        OnPropertyChanged(nameof(IsDailyExpensesPage));
        OnPropertyChanged(nameof(IsSettingsPage));

        if (value == ShellPage.Settings)
            _ = Settings.LoadReceiptSettingsAsync(tryPullIfLoggedIn: true);

        if (value == ShellPage.Dashboard)
            _ = Dashboard.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.Analytics)
            _ = Analytics.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.Ledger)
            _ = Ledger.RefreshCommand.ExecuteAsync(null);
        GlobalSearchText = value == ShellPage.Billing ? Billing.SearchText : "";

        if (value == ShellPage.Billing)
            RequestBillingSearchFocus();
        if (value == ShellPage.Barcodes)
            RequestBarcodeSkuFocus();
        if (value == ShellPage.DailyExpenses)
            _ = DailyExpenses.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.DayClose)
        {
            _ = DayClose.RefreshCommand.ExecuteAsync(null);
            _ = RefreshDaySessionStatusAsync();
        }

        PostBillCommand.NotifyCanExecuteChanged();
        _lastPage = value;
    }

    public void RequestBillingSearchFocus() =>
        _services.FocusSearch?.FocusBillingProductSearch();

    public void RequestBarcodeSkuFocus() =>
        _services.FocusBarcodeSkuEntry?.Invoke();

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
    private void NavigateDayClose() => CurrentPage = ShellPage.DayClose;

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
        if (!IsPrimaryCounter)
            return;
        CurrentPage = ShellPage.Settings;
    }

    [RelayCommand]
    private async Task OpenNotifications()
    {
        var dlg = new NotificationsDialog(_services) { Owner = Application.Current.MainWindow };
        dlg.ShowDialog();
        await RefreshNotificationCountAsync();
    }

    [RelayCommand]
    private void FocusGlobalSearch()
    {
        if (CurrentPage == ShellPage.Billing)
            _services.FocusSearch?.FocusBillingProductSearch();
        else if (CurrentPage == ShellPage.Barcodes)
            RequestBarcodeSkuFocus();
        else
            _services.FocusSearch?.FocusGlobalSearch();
    }

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
    private async Task OpenDuplicateBill()
    {
        CurrentPage = ShellPage.DuplicateBill;
        await DuplicatePrint.OnPageOpenedAsync();
    }

    [RelayCommand]
    private async Task HoldBill()
    {
        if (!EnsureBillingPage())
            return;
        await Billing.HoldBillCommand.ExecuteAsync(null);
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
                "Switch to Billing to use billing shortcuts (F1, F2, F8 hold, F9 post, F10 print).",
                "RR Bridal Billing",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        return true;
    }
}
