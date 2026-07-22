using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Store;
using RRBridal.StoreBilling.App.Services.Ui;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly AppServices _services;

    public BillingViewModel Billing { get; }

    public DashboardViewModel Dashboard { get; }

    public AnalyticsViewModel Analytics { get; }

    public OnlineSalesViewModel OnlineSales { get; }

    public QuotationViewModel Quotation { get; }

    public QuotationManagementViewModel QuotationManagement { get; }

    public CreditBillsViewModel CreditBills { get; }

    public LedgerViewModel Ledger { get; }

    public CustomerRegistrationViewModel CustomersRegistration { get; }

    public CustomersViewModel Customers { get; }

    public SalesmanViewModel Salesmen { get; }

    public SaleReturnViewModel SaleReturn { get; }

    public BillLookupViewModel BillLookup { get; }

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

    [ObservableProperty] private int _pendingNotificationCount;

    [ObservableProperty] private string _daySessionStatusChip = "Day: …";

    [ObservableProperty] private string _mongoHealthStatusChip = "Mongo: …";

    [ObservableProperty] private bool _isNavDrawerOpen;

    [ObservableProperty] private string _currentPageLabel = "Billing";

    [ObservableProperty] private double _shellWidth = 1280;

    [ObservableProperty] private LayoutBreakpoint _layoutBreakpoint = LayoutBreakpoint.Medium;

    public bool IsCompactLayout => LayoutBreakpoint == LayoutBreakpoint.Compact;

    public bool IsMediumOrWideLayout => LayoutBreakpoint is LayoutBreakpoint.Medium or LayoutBreakpoint.Wide;

    public bool ShowCompactHeaderDetails => !IsCompactLayout;

    public void UpdateShellLayout(double width)
    {
        ShellWidth = width;
        LayoutBreakpoint = WindowLayoutHelper.GetBreakpoint(width);
        OnPropertyChanged(nameof(IsCompactLayout));
        OnPropertyChanged(nameof(IsMediumOrWideLayout));
        OnPropertyChanged(nameof(ShowCompactHeaderDetails));
    }

    public bool IsPrimaryCounter => _services.StoreContext.IsPrimaryCounter;

    public bool ShowDashboardNav => IsPrimaryCounter;

    public bool ShowAnalyticsNav => IsPrimaryCounter;

    public bool ShowOnlineSalesNav => IsPrimaryCounter;

    public bool ShowCreditBillsNav => IsPrimaryCounter;

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
        Billing.NavigateToCustomerRegistration = () =>
        {
            CurrentPage = ShellPage.Customers;
            Customers.StartNewRegistration();
        };
        Billing.NavigateToSalesmen = () => CurrentPage = ShellPage.Salesmen;
        Billing.PostBillCanExecuteChanged += () => PostBillCommand.NotifyCanExecuteChanged();
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
        Dashboard.NavigateToOnlineSales = () => CurrentPage = ShellPage.OnlineSales;
        Dashboard.NavigateToCreditBills = () => CurrentPage = ShellPage.CreditBills;
        Analytics = new AnalyticsViewModel(services);
        OnlineSales = new OnlineSalesViewModel(services);
        Quotation = new QuotationViewModel(services);
        Quotation.Editor.NavigateToCustomerRegistration = () =>
        {
            CurrentPage = ShellPage.Customers;
            Customers.StartNewRegistration();
        };
        Quotation.Editor.NavigateToSalesmen = () => CurrentPage = ShellPage.Salesmen;
        Quotation.NavigateToQuotationList = () => CurrentPage = ShellPage.QuotationManagement;
        QuotationManagement = new QuotationManagementViewModel(services);
        QuotationManagement.OpenQuotation = quotationNo => _ = OpenQuotationAsync(quotationNo);
        QuotationManagement.ConvertQuotationToBilling = quotationNo => _ = ConvertQuotationToBillingAsync(quotationNo);
        QuotationManagement.CreateQuotation = () =>
        {
            Quotation.StartNew();
            CurrentPage = ShellPage.Quotation;
        };
        CreditBills = new CreditBillsViewModel(services);
        Ledger = new LedgerViewModel(services);
        Customers = new CustomersViewModel(services, Billing, () => CurrentPage = ShellPage.Billing);
        CustomersRegistration = new CustomerRegistrationViewModel(services, Billing, () => CurrentPage = ShellPage.Billing);
        Salesmen = new SalesmanViewModel(services);
        SaleReturn = new SaleReturnViewModel(services);
        AdjustmentBill = new AdjustmentBillViewModel(services);
        BillLookup = new BillLookupViewModel(services);
        DuplicatePrint = new DuplicatePrintViewModel(services);
        BarcodePrinting = new BarcodePrintingViewModel(services);
        DailyExpenses = new DailyExpenseViewModel(services);
        DayClose = new DayCloseViewModel(services);
        Settings = new SettingsViewModel(services);

        services.NotifyDaySessionChanged = () => _ = RefreshDaySessionStatusAsync();

        NotifyPageVisibility();
        _services.ShellBranding.BrandingChanged += OnBrandingChanged;
        _services.MongoHealth.StatusChanged += OnMongoHealthStatusChanged;
        MongoHealthStatusChip = _services.MongoHealth.StatusDescription;
        if (!IsPrimaryCounter && IsRestrictedPage(CurrentPage))
            CurrentPage = ShellPage.Billing;

        _ = RefreshBrandingAsync();
        _ = RefreshNotificationCountAsync();
        _ = RefreshDaySessionStatusAsync();
    }

    private void OnMongoHealthStatusChanged()
    {
        MongoHealthStatusChip = _services.MongoHealth.StatusDescription;
    }

    private static bool IsRestrictedPage(ShellPage page) =>
        page is ShellPage.Dashboard or ShellPage.Analytics or ShellPage.OnlineSales or ShellPage.CreditBills
            or ShellPage.Ledger or ShellPage.DailyExpenses or ShellPage.Settings;

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
        OnPropertyChanged(nameof(IsOnlineSalesPage));
        OnPropertyChanged(nameof(IsQuotationPage));
        OnPropertyChanged(nameof(IsQuotationManagementPage));
        OnPropertyChanged(nameof(IsCreditBillsPage));
        OnPropertyChanged(nameof(IsCustomersPage));
        OnPropertyChanged(nameof(IsSalesmenPage));
        OnPropertyChanged(nameof(IsLedgerPage));
        OnPropertyChanged(nameof(IsSaleReturnPage));
        OnPropertyChanged(nameof(IsBillLookupPage));
        OnPropertyChanged(nameof(IsAdjustmentsPage));
        OnPropertyChanged(nameof(IsDuplicateBillPage));
        OnPropertyChanged(nameof(IsBarcodesPage));
        OnPropertyChanged(nameof(IsDayClosePage));
        OnPropertyChanged(nameof(IsDailyExpensesPage));
        OnPropertyChanged(nameof(IsSettingsPage));
    }

    private void NotifyPageVisibility() => EnsurePageVisibilityFresh();

    private async Task OpenQuotationAsync(string quotationNo)
    {
        var doc = await _services.Quotations.GetByQuotationNoAsync(quotationNo);
        if (doc == null)
        {
            MessageBox.Show("Quotation not found.", "Quotations", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Quotation.LoadDocument(doc);
        CurrentPage = ShellPage.Quotation;
    }

    private async Task ConvertQuotationToBillingAsync(string quotationNo)
    {
        var doc = await _services.Quotations.GetByQuotationNoAsync(quotationNo);
        if (doc == null)
        {
            MessageBox.Show("Quotation not found.", "Quotations", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var status = doc.GetValue("status", "").AsString;
        if (!string.Equals(status, QuotationService.StatusOpen, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Only open quotations can be converted.", "Quotations", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Billing.LoadFromQuotationDocument(doc);
        CurrentPage = ShellPage.Billing;
    }

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

    public bool IsOnlineSalesPage => CurrentPage == ShellPage.OnlineSales;

    public bool IsQuotationPage => CurrentPage == ShellPage.Quotation;

    public bool IsQuotationManagementPage => CurrentPage == ShellPage.QuotationManagement;

    public bool IsCreditBillsPage => CurrentPage == ShellPage.CreditBills;

    public bool IsCustomersPage => CurrentPage == ShellPage.Customers;

    public bool IsSalesmenPage => CurrentPage == ShellPage.Salesmen;

    public bool IsLedgerPage => CurrentPage == ShellPage.Ledger;

    public bool IsSaleReturnPage => CurrentPage == ShellPage.SaleReturn;

    public bool IsBillLookupPage => CurrentPage == ShellPage.BillLookup;

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

        CurrentPageLabel = GetPageLabel(value);
        IsNavDrawerOpen = false;

        OnPropertyChanged(nameof(IsBillingPage));
        OnPropertyChanged(nameof(IsDashboardPage));
        OnPropertyChanged(nameof(IsAnalyticsPage));
        OnPropertyChanged(nameof(IsOnlineSalesPage));
        OnPropertyChanged(nameof(IsQuotationPage));
        OnPropertyChanged(nameof(IsQuotationManagementPage));
        OnPropertyChanged(nameof(IsCreditBillsPage));
        OnPropertyChanged(nameof(IsCustomersPage));
        OnPropertyChanged(nameof(IsSalesmenPage));
        OnPropertyChanged(nameof(IsLedgerPage));
        OnPropertyChanged(nameof(IsSaleReturnPage));
        OnPropertyChanged(nameof(IsBillLookupPage));
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
        if (value == ShellPage.OnlineSales)
            _ = OnlineSales.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.QuotationManagement)
            _ = QuotationManagement.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.CreditBills)
            _ = CreditBills.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.Ledger)
            _ = Ledger.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.Customers)
            _ = Customers.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.Salesmen)
            _ = Salesmen.RefreshCommand.ExecuteAsync(null);

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

    [RelayCommand]
    private void Navigate(ShellPage page)
    {
        if (!IsPrimaryCounter && IsRestrictedPage(page))
            return;
        CurrentPage = page;
    }

    [RelayCommand]
    private void ToggleNavDrawer() => IsNavDrawerOpen = !IsNavDrawerOpen;

    [RelayCommand]
    private void CloseNavDrawer() => IsNavDrawerOpen = false;

    [RelayCommand]
    private void NavigateDayClose() => CurrentPage = ShellPage.DayClose;

    private static string GetPageLabel(ShellPage page) => page switch
    {
        ShellPage.Billing => "Billing",
        ShellPage.Dashboard => "Dashboard",
        ShellPage.Analytics => "Analytics",
        ShellPage.OnlineSales => "Online Sales",
        ShellPage.Quotation => "Quotation",
        ShellPage.QuotationManagement => "Quotations",
        ShellPage.CreditBills => "Credit Bills",
        ShellPage.Customers => "Customers",
        ShellPage.Salesmen => "Salesman",
        ShellPage.Ledger => "Ledger",
        ShellPage.SaleReturn => "Returns",
        ShellPage.BillLookup => "Bill Lookup",
        ShellPage.DayClose => "Day Close",
        ShellPage.DuplicateBill => "Duplicate",
        ShellPage.Adjustments => "Adjustments",
        ShellPage.Barcodes => "Barcodes",
        ShellPage.DailyExpenses => "Expenses",
        ShellPage.Settings => "Settings",
        _ => "Billing",
    };

    [RelayCommand]
    private async Task SaveCustomerRegistration()
    {
        if (CurrentPage != ShellPage.Customers)
            return;
        await Customers.SaveCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void CancelCustomerRegistration()
    {
        if (CurrentPage != ShellPage.Customers)
            return;
        Customers.NewCustomerCommand.Execute(null);
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
        else if (CurrentPage == ShellPage.SaleReturn)
            _ = SaleReturn.AddExchangeProductFromSearchAsync("");
        else if (CurrentPage == ShellPage.BillLookup && BillLookup.IsReturnMode)
            _ = BillLookup.Return.AddExchangeProductFromSearchAsync("");
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
        switch (CurrentPage)
        {
            case ShellPage.Billing:
                Billing.ClearForNewBillCommand.Execute(null);
                RequestBillingSearchFocus();
                break;
            case ShellPage.Quotation:
                Quotation.StartNew();
                break;
            case ShellPage.Customers:
                Customers.StartNewRegistration();
                break;
            case ShellPage.Salesmen:
                Salesmen.NewSalesmanCommand.Execute(null);
                break;
            case ShellPage.SaleReturn:
                _ = SaleReturn.ClearFormCommand.ExecuteAsync(null);
                break;
            case ShellPage.BillLookup:
                _ = BillLookup.ResetForNewAsync();
                break;
            case ShellPage.Adjustments:
                _ = AdjustmentBill.ClearFormCommand.ExecuteAsync(null);
                break;
            case ShellPage.Barcodes:
                BarcodePrinting.ClearScreenCommand.Execute(null);
                break;
            case ShellPage.DailyExpenses:
                DailyExpenses.ClearEntryForm();
                break;
            case ShellPage.QuotationManagement:
                QuotationManagement.ClearFilters();
                _ = QuotationManagement.RefreshCommand.ExecuteAsync(null);
                break;
            case ShellPage.CreditBills:
                CreditBills.ClearFilters();
                _ = CreditBills.RefreshCommand.ExecuteAsync(null);
                break;
            case ShellPage.Dashboard:
                _ = Dashboard.RefreshCommand.ExecuteAsync(null);
                break;
            case ShellPage.Analytics:
                _ = Analytics.RefreshCommand.ExecuteAsync(null);
                break;
            case ShellPage.OnlineSales:
                _ = OnlineSales.RefreshCommand.ExecuteAsync(null);
                break;
            case ShellPage.Ledger:
                _ = Ledger.RefreshCommand.ExecuteAsync(null);
                break;
            case ShellPage.DayClose:
                _ = DayClose.RefreshCommand.ExecuteAsync(null);
                break;
            case ShellPage.DuplicateBill:
                _ = DuplicatePrint.OnPageOpenedAsync();
                break;
            default:
                break;
        }
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
    private void OpenOnlineSales()
    {
        CurrentPage = ShellPage.OnlineSales;
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
