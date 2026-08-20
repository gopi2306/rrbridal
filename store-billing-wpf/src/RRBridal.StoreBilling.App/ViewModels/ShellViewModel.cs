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

    public ReportsViewModel Reports { get; }

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

    public OutboundDispatchViewModel OutboundDispatch { get; }

    public AdjustmentBillViewModel AdjustmentBill { get; }

    public DuplicatePrintViewModel DuplicatePrint { get; }

    public BarcodePrintingViewModel BarcodePrinting { get; }

    public DailyExpenseViewModel DailyExpenses { get; }

    public DayCloseViewModel DayClose { get; }

    public VoucherGatewayViewModel Vouchers { get; }

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
    [ObservableProperty] private string _centralOnlineStatusChip = "Central: Offline";

    [ObservableProperty] private bool _isNavDrawerOpen;

    [ObservableProperty] private bool _isSidebarVisible;

    [ObservableProperty] private bool _preferSidebarMenu;

    [ObservableProperty] private string _currentPageLabel = "Billing";

    [ObservableProperty] private string _footerHintText = "Ctrl+G Go To · Ctrl+A Billing · F9 Post · F1 Shortcuts";

    [ObservableProperty] private double _shellWidth = 1280;

    [ObservableProperty] private LayoutBreakpoint _layoutBreakpoint = LayoutBreakpoint.Medium;

    public bool IsCompactLayout => LayoutBreakpoint == LayoutBreakpoint.Compact;

    public bool IsMediumOrWideLayout => LayoutBreakpoint is LayoutBreakpoint.Medium or LayoutBreakpoint.Wide;

    public bool ShowCompactHeaderDetails => !IsCompactLayout;

    /// <summary>Persistent left rail — only when Settings enables sidebar and layout is not compact.</summary>
    public bool ShowPersistentSidebar => PreferSidebarMenu && IsSidebarVisible && !IsCompactLayout;

    /// <summary>Header quick-nav strip when sidebar mode is off.</summary>
    public bool ShowHeaderNav => !PreferSidebarMenu;

    public void UpdateShellLayout(double width)
    {
        ShellWidth = width;
        LayoutBreakpoint = WindowLayoutHelper.GetBreakpoint(width);
        UiDensityService.Apply(LayoutBreakpoint);
        OnPropertyChanged(nameof(IsCompactLayout));
        OnPropertyChanged(nameof(IsMediumOrWideLayout));
        OnPropertyChanged(nameof(ShowCompactHeaderDetails));
        if (IsCompactLayout && IsSidebarVisible)
            IsSidebarVisible = false;
        NotifyShellNavVisibility();
    }

    public void ApplyShellUiSettings()
    {
        PreferSidebarMenu = _services.ShellUiSettings.Current.ShowSidebarMenu;
        if (PreferSidebarMenu)
            IsSidebarVisible = !IsCompactLayout;
        else
        {
            IsSidebarVisible = false;
            IsNavDrawerOpen = false;
        }
        NotifyShellNavVisibility();
    }

    private void NotifyShellNavVisibility()
    {
        OnPropertyChanged(nameof(ShowPersistentSidebar));
        OnPropertyChanged(nameof(ShowHeaderNav));
    }

    partial void OnPreferSidebarMenuChanged(bool value) => NotifyShellNavVisibility();

    partial void OnIsSidebarVisibleChanged(bool value) => NotifyShellNavVisibility();

    public bool IsPrimaryCounter => _services.StoreContext.IsPrimaryCounter;

    public bool ShowBillingNav => CanAccess(ShellPage.Billing);

    public bool ShowVouchersNav => CanAccess(ShellPage.Vouchers);

    public bool ShowQuotationsNav => CanAccess(ShellPage.QuotationManagement);

    public bool ShowBarcodesNav => CanAccess(ShellPage.Barcodes);

    public bool ShowDashboardNav => CanAccess(ShellPage.Dashboard);

    public bool ShowAnalyticsNav => CanAccess(ShellPage.Analytics);

    public bool ShowCustomerBillingReportNav => CanAccess(ShellPage.CustomerBillingReport);

    public bool ShowReportsNav => ShowDashboardNav || ShowAnalyticsNav || ShowCustomerBillingReportNav
                                  || ShowLedgerNav || ShowBillLookupNav;

    public bool ShowOnlineSalesNav => CanAccess(ShellPage.OnlineSales);

    public bool ShowCreditBillsNav => CanAccess(ShellPage.CreditBills);

    public bool ShowCustomersNav => CanAccess(ShellPage.Customers);

    public bool ShowSalesmanNav => CanAccess(ShellPage.Salesmen);

    public bool ShowLedgerNav => CanAccess(ShellPage.Ledger);

    public bool ShowReturnsNav => CanAccess(ShellPage.SaleReturn);

    public bool ShowBillLookupNav => CanAccess(ShellPage.BillLookup);

    public bool ShowOutboundDispatchNav => CanAccess(ShellPage.OutboundDispatch);

    public bool ShowDayCloseNav => CanAccess(ShellPage.DayClose);

    public bool ShowDuplicateNav => CanAccess(ShellPage.DuplicateBill);

    public bool ShowAdjustmentsNav => CanAccess(ShellPage.Adjustments);

    public bool ShowDailyExpensesNav => CanAccess(ShellPage.DailyExpenses);

    public bool ShowSettingsNav => CanAccess(ShellPage.Settings);

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
        Dashboard.NavigateToOnlineSales = () =>
        {
            if (CanAccess(ShellPage.OnlineSales))
                CurrentPage = ShellPage.OnlineSales;
        };
        Dashboard.NavigateToCreditBills = () =>
        {
            if (CanAccess(ShellPage.CreditBills))
                CurrentPage = ShellPage.CreditBills;
        };
        Analytics = new AnalyticsViewModel(services);
        Reports = new ReportsViewModel(services);
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
        OutboundDispatch = new OutboundDispatchViewModel(services);
        BillLookup.OpenDispatchForBill = billNo =>
        {
            if (!CanAccess(ShellPage.OutboundDispatch))
            {
                AppDialog.Show("This counter does not have access to Outbound Dispatch.",
                    "Outbound Dispatch", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            CurrentPage = ShellPage.OutboundDispatch;
            _ = OutboundDispatch.OpenBillAsync(billNo);
        };
        DuplicatePrint = new DuplicatePrintViewModel(services);
        BarcodePrinting = new BarcodePrintingViewModel(services);
        DailyExpenses = new DailyExpenseViewModel(services);
        DayClose = new DayCloseViewModel(services);
        Vouchers = new VoucherGatewayViewModel
        {
            OpenVoucher = OpenVoucherFromGateway,
            OpenCodReceipt = () =>
            {
                if (CanAccess(ShellPage.OnlineSales))
                    CurrentPage = ShellPage.OnlineSales;
            },
        };
        Settings = new SettingsViewModel(services);

        services.NotifyDaySessionChanged = () => _ = RefreshDaySessionStatusAsync();
        services.ShellUiSettings.Changed += () =>
            Application.Current.Dispatcher.Invoke(ApplyShellUiSettings);
        services.PosBillingSettings.Changed += () =>
            Application.Current.Dispatcher.Invoke(RefreshScreenAccessNav);

        ApplyShellUiSettings();
        NotifyPageVisibility();
        _services.ShellBranding.BrandingChanged += OnBrandingChanged;
        _services.MongoHealth.StatusChanged += OnMongoHealthStatusChanged;
        _services.CentralMode.StatusChanged += OnCentralModeStatusChanged;
        RefreshConnectionStatusChips();
        if (!CanAccess(CurrentPage))
            CurrentPage = FirstAccessiblePage();

        _ = RefreshBrandingAsync();
        _ = RefreshNotificationCountAsync();
        _ = RefreshDaySessionStatusAsync();
    }

    private void OnMongoHealthStatusChanged()
    {
        RefreshConnectionStatusChips();
    }

    private void OnCentralModeStatusChanged()
    {
        RefreshConnectionStatusChips();
    }

    private void RefreshConnectionStatusChips()
    {
        CentralOnlineStatusChip = _services.CentralMode.StatusChipText;
        MongoHealthStatusChip = _services.CentralMode.IsOnlineMode
            ? "Mongo: not required (Online)"
            : _services.MongoHealth.StatusDescription;
    }

    public bool CanAccess(ShellPage page)
    {
        var counter = _services.StoreContext.PosCounter?.Trim() ?? "1";

        var access = _services.PosBillingSettings.Current.ScreenAccess
                     ?? CounterScreenAccessSettings.CreateDefaults();

        return page switch
        {
            ShellPage.Billing => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.Billing), counter),
            ShellPage.Vouchers => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.Vouchers), counter),
            ShellPage.Quotation or ShellPage.QuotationManagement =>
                access.IsCounterAllowed(nameof(CounterScreenAccessSettings.Quotations), counter),
            ShellPage.Barcodes => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.Barcodes), counter),
            ShellPage.Dashboard => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.Dashboard), counter),
            ShellPage.Analytics => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.Analytics), counter),
            ShellPage.CustomerBillingReport => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.CustomerBillingReport), counter),
            ShellPage.OnlineSales => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.OnlineSales), counter),
            ShellPage.CreditBills => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.CreditBills), counter),
            ShellPage.Customers => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.Customers), counter),
            ShellPage.Salesmen => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.Salesman), counter),
            ShellPage.Ledger => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.Ledger), counter),
            ShellPage.SaleReturn => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.Returns), counter),
            ShellPage.BillLookup => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.BillLookup), counter),
            ShellPage.OutboundDispatch => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.OutboundDispatch), counter),
            ShellPage.DayClose => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.DayClose), counter),
            ShellPage.DuplicateBill => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.Duplicate), counter),
            ShellPage.Adjustments => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.Adjustments), counter),
            ShellPage.DailyExpenses => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.DailyExpenses), counter),
            ShellPage.Settings => access.IsCounterAllowed(nameof(CounterScreenAccessSettings.Settings), counter),
            _ => true,
        };
    }

    public void RefreshScreenAccessNav()
    {
        OnPropertyChanged(nameof(ShowBillingNav));
        OnPropertyChanged(nameof(ShowVouchersNav));
        OnPropertyChanged(nameof(ShowQuotationsNav));
        OnPropertyChanged(nameof(ShowBarcodesNav));
        OnPropertyChanged(nameof(ShowDashboardNav));
        OnPropertyChanged(nameof(ShowAnalyticsNav));
        OnPropertyChanged(nameof(ShowCustomerBillingReportNav));
        OnPropertyChanged(nameof(ShowReportsNav));
        OnPropertyChanged(nameof(ShowOnlineSalesNav));
        OnPropertyChanged(nameof(ShowCreditBillsNav));
        OnPropertyChanged(nameof(ShowCustomersNav));
        OnPropertyChanged(nameof(ShowSalesmanNav));
        OnPropertyChanged(nameof(ShowLedgerNav));
        OnPropertyChanged(nameof(ShowReturnsNav));
        OnPropertyChanged(nameof(ShowBillLookupNav));
        OnPropertyChanged(nameof(ShowOutboundDispatchNav));
        OnPropertyChanged(nameof(ShowDayCloseNav));
        OnPropertyChanged(nameof(ShowDuplicateNav));
        OnPropertyChanged(nameof(ShowAdjustmentsNav));
        OnPropertyChanged(nameof(ShowDailyExpensesNav));
        OnPropertyChanged(nameof(ShowSettingsNav));
        if (!CanAccess(CurrentPage))
            CurrentPage = FirstAccessiblePage();
    }

    private ShellPage FirstAccessiblePage()
    {
        foreach (var page in new[]
                 {
                     ShellPage.Billing,
                     ShellPage.Vouchers,
                     ShellPage.QuotationManagement,
                     ShellPage.Barcodes,
                     ShellPage.Dashboard,
                     ShellPage.Analytics,
                     ShellPage.CustomerBillingReport,
                     ShellPage.OnlineSales,
                     ShellPage.CreditBills,
                     ShellPage.Customers,
                     ShellPage.Salesmen,
                     ShellPage.Ledger,
                     ShellPage.SaleReturn,
                     ShellPage.BillLookup,
                     ShellPage.OutboundDispatch,
                     ShellPage.DayClose,
                     ShellPage.DuplicateBill,
                     ShellPage.Adjustments,
                     ShellPage.DailyExpenses,
                     ShellPage.Settings,
                 })
        {
            if (CanAccess(page))
                return page;
        }

        return ShellPage.Billing;
    }

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
        OnPropertyChanged(nameof(IsCustomerBillingReportPage));
        OnPropertyChanged(nameof(IsOnlineSalesPage));
        OnPropertyChanged(nameof(IsQuotationPage));
        OnPropertyChanged(nameof(IsQuotationManagementPage));
        OnPropertyChanged(nameof(IsCreditBillsPage));
        OnPropertyChanged(nameof(IsCustomersPage));
        OnPropertyChanged(nameof(IsSalesmenPage));
        OnPropertyChanged(nameof(IsLedgerPage));
        OnPropertyChanged(nameof(IsSaleReturnPage));
        OnPropertyChanged(nameof(IsBillLookupPage));
        OnPropertyChanged(nameof(IsOutboundDispatchPage));
        OnPropertyChanged(nameof(IsAdjustmentsPage));
        OnPropertyChanged(nameof(IsDuplicateBillPage));
        OnPropertyChanged(nameof(IsBarcodesPage));
        OnPropertyChanged(nameof(IsDayClosePage));
        OnPropertyChanged(nameof(IsDailyExpensesPage));
        OnPropertyChanged(nameof(IsVouchersPage));
        OnPropertyChanged(nameof(IsSettingsPage));
    }

    private void NotifyPageVisibility() => EnsurePageVisibilityFresh();

    private async Task OpenQuotationAsync(string quotationNo)
    {
        var doc = await _services.Quotations.GetByQuotationNoAsync(quotationNo);
        if (doc == null)
        {
            AppDialog.Show("Quotation not found.", "Quotations", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            AppDialog.Show("Quotation not found.", "Quotations", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var status = doc.GetValue("status", "").AsString;
        if (!string.Equals(status, QuotationService.StatusOpen, StringComparison.OrdinalIgnoreCase))
        {
            AppDialog.Show("Only open quotations can be converted.", "Quotations", MessageBoxButton.OK, MessageBoxImage.Information);
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

    public bool IsCustomerBillingReportPage => CurrentPage == ShellPage.CustomerBillingReport;

    public bool IsOnlineSalesPage => CurrentPage == ShellPage.OnlineSales;

    public bool IsQuotationPage => CurrentPage == ShellPage.Quotation;

    public bool IsQuotationManagementPage => CurrentPage == ShellPage.QuotationManagement;

    public bool IsCreditBillsPage => CurrentPage == ShellPage.CreditBills;

    public bool IsCustomersPage => CurrentPage == ShellPage.Customers;

    public bool IsSalesmenPage => CurrentPage == ShellPage.Salesmen;

    public bool IsLedgerPage => CurrentPage == ShellPage.Ledger;

    public bool IsSaleReturnPage => CurrentPage == ShellPage.SaleReturn;

    public bool IsBillLookupPage => CurrentPage == ShellPage.BillLookup;

    public bool IsOutboundDispatchPage => CurrentPage == ShellPage.OutboundDispatch;

    public bool IsAdjustmentsPage => CurrentPage == ShellPage.Adjustments;

    public bool IsDuplicateBillPage => CurrentPage == ShellPage.DuplicateBill;

    public bool IsBarcodesPage => CurrentPage == ShellPage.Barcodes;

    public bool IsDailyExpensesPage => CurrentPage == ShellPage.DailyExpenses;

    public bool IsVouchersPage => CurrentPage == ShellPage.Vouchers;

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
        FooterHintText = GetFooterHint(value);
        IsNavDrawerOpen = false;

        OnPropertyChanged(nameof(IsBillingPage));
        OnPropertyChanged(nameof(IsDashboardPage));
        OnPropertyChanged(nameof(IsAnalyticsPage));
        OnPropertyChanged(nameof(IsCustomerBillingReportPage));
        OnPropertyChanged(nameof(IsOnlineSalesPage));
        OnPropertyChanged(nameof(IsQuotationPage));
        OnPropertyChanged(nameof(IsQuotationManagementPage));
        OnPropertyChanged(nameof(IsCreditBillsPage));
        OnPropertyChanged(nameof(IsCustomersPage));
        OnPropertyChanged(nameof(IsSalesmenPage));
        OnPropertyChanged(nameof(IsLedgerPage));
        OnPropertyChanged(nameof(IsSaleReturnPage));
        OnPropertyChanged(nameof(IsBillLookupPage));
        OnPropertyChanged(nameof(IsOutboundDispatchPage));
        OnPropertyChanged(nameof(IsAdjustmentsPage));
        OnPropertyChanged(nameof(IsDuplicateBillPage));
        OnPropertyChanged(nameof(IsBarcodesPage));
        OnPropertyChanged(nameof(IsDayClosePage));
        OnPropertyChanged(nameof(IsDailyExpensesPage));
        OnPropertyChanged(nameof(IsVouchersPage));
        OnPropertyChanged(nameof(IsSettingsPage));

        if (value == ShellPage.Settings)
            _ = Settings.LoadReceiptSettingsAsync(tryPullIfLoggedIn: true);

        if (value == ShellPage.Dashboard)
            _ = Dashboard.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.Analytics)
            _ = Analytics.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.CustomerBillingReport)
            Reports.OnNavigated();
        if (value == ShellPage.OnlineSales)
            _ = OnlineSales.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.QuotationManagement)
            _ = QuotationManagement.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.CreditBills)
            _ = CreditBills.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.Ledger)
            _ = Ledger.RefreshCommand.ExecuteAsync(null);
        if (value == ShellPage.OutboundDispatch)
            _ = OutboundDispatch.RefreshCommand.ExecuteAsync(null);
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
        if (!CanAccess(page))
            return;
        CurrentPage = page;
    }

    [RelayCommand]
    private void ToggleNavDrawer()
    {
        if (IsNavDrawerOpen)
        {
            IsNavDrawerOpen = false;
            return;
        }

        if (!PreferSidebarMenu || IsCompactLayout)
        {
            IsNavDrawerOpen = true;
            return;
        }

        IsSidebarVisible = !IsSidebarVisible;
    }

    [RelayCommand]
    private void CloseNavDrawer() => IsNavDrawerOpen = false;

    [RelayCommand]
    private void NavigateDayClose()
    {
        if (!CanAccess(ShellPage.DayClose))
            return;
        CurrentPage = ShellPage.DayClose;
    }

    private void OpenVoucherFromGateway(VoucherKind kind)
    {
        switch (kind)
        {
            case VoucherKind.Sales:
                if (!CanAccess(ShellPage.Billing))
                    return;
                CurrentPage = ShellPage.Billing;
                RequestBillingSearchFocus();
                break;
            case VoucherKind.Receipt:
                if (!CanAccess(ShellPage.CreditBills))
                    return;
                CurrentPage = ShellPage.CreditBills;
                break;
            case VoucherKind.Payment:
                _ = PostPaymentVoucherAsync();
                break;
            case VoucherKind.CreditNote:
                if (!CanAccess(ShellPage.SaleReturn))
                    return;
                OpenCreditNoteVoucher();
                break;
            case VoucherKind.Journal:
                if (!CanAccess(ShellPage.Adjustments))
                    return;
                CurrentPage = ShellPage.Adjustments;
                break;
            case VoucherKind.DailyExpense:
                if (!CanAccess(ShellPage.DailyExpenses))
                    return;
                CurrentPage = ShellPage.DailyExpenses;
                break;
        }
    }

    private void OpenCreditNoteVoucher()
    {
        var owner = Application.Current?.MainWindow;
        if (owner == null)
            return;

        if (!CreditNoteChooserDialog.TryShow(owner, out var path))
            return;

        if (path == CreditNoteVoucherPath.BillReturn)
        {
            SaleReturn.SourceMode = SaleReturnSourceMode.SystemBill;
            SaleReturn.ReturnMode = ReturnMode.CreditNote;
            CurrentPage = ShellPage.SaleReturn;
            return;
        }

        _ = OpenDirectCustomerCreditNoteAsync(owner);
    }

    private async Task OpenDirectCustomerCreditNoteAsync(Window owner)
    {
        var created = await DirectCustomerCreditNoteDialog.TryShowAndPostAsync(owner, _services);
        if (created == null)
            return;

        AppDialog.Show(
            $"Credit note {created} created for the customer.\nIt can be applied on Billing / Credit Bills.",
            "Direct Customer Credit Note",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async Task PostPaymentVoucherAsync()
    {
        var owner = Application.Current?.MainWindow;
        if (owner == null)
            return;

        if (!CashMovementDialog.TryShow(owner, out var amount, out var description))
            return;

        var businessDate = DaySessionService.FormatBusinessDate(DateTime.Today);
        var (success, message) = await _services.CashMovements.PostMovementAsync(
            CashMovementType.CashWithdrawal,
            description,
            amount,
            businessDate);

        AppDialog.Show(
            message,
            "Payment voucher",
            MessageBoxButton.OK,
            success ? MessageBoxImage.Information : MessageBoxImage.Warning);

        if (success)
        {
            _services.NotifyDaySessionChanged?.Invoke();
            CurrentPage = ShellPage.DayClose;
            _ = DayClose.RefreshCommand.ExecuteAsync(null);
        }
    }

    private static string GetPageLabel(ShellPage page) => page switch
    {
        ShellPage.Billing => "Billing",
        ShellPage.Dashboard => "Dashboard",
        ShellPage.Analytics => "Analytics",
        ShellPage.CustomerBillingReport => "Reports",
        ShellPage.OnlineSales => "Online Sales",
        ShellPage.Quotation => "Quotation",
        ShellPage.QuotationManagement => "Quotations",
        ShellPage.CreditBills => "Credit Bills",
        ShellPage.Customers => "Customers",
        ShellPage.Salesmen => "Salesman",
        ShellPage.Ledger => "Ledger",
        ShellPage.SaleReturn => "Returns",
        ShellPage.BillLookup => "Bill Lookup",
        ShellPage.OutboundDispatch => "Outbound Dispatch",
        ShellPage.DayClose => "Day Close",
        ShellPage.DuplicateBill => "Duplicate",
        ShellPage.Adjustments => "Adjustments",
        ShellPage.Barcodes => "Barcodes",
        ShellPage.DailyExpenses => "Expenses",
        ShellPage.Vouchers => "Vouchers",
        ShellPage.Settings => "Settings",
        _ => "Billing",
    };

    private static string GetFooterHint(ShellPage page) => page switch
    {
        ShellPage.Billing => "Billing · F3/Ctrl+F product · F8/Ctrl+H hold · F9/Ctrl+Enter post · F10/Ctrl+P print · F1 help",
        ShellPage.Quotation or ShellPage.QuotationManagement => "Quotations · Ctrl+Q list · F2/Ctrl+N new · F1 shortcuts",
        ShellPage.Barcodes => "Barcodes · F5 print · F6 list · F7 clear · Ctrl+B open · F1 shortcuts",
        ShellPage.Customers => "Customers · F4/Ctrl+S save · Esc/F2 new · Ctrl+U open · F1 shortcuts",
        ShellPage.SaleReturn => "Returns · Ctrl+R · F2 clear · F3 search exchange · F1 shortcuts",
        ShellPage.DayClose => "Day Close · Ctrl+W · F2 refresh · F1 shortcuts",
        ShellPage.OutboundDispatch => "Outbound Dispatch · Search posted bills · manage status · print parcel and batch documents",
        ShellPage.Dashboard => "Dashboard · Ctrl+D · F2 refresh · F1 shortcuts",
        ShellPage.CustomerBillingReport => "Reports · Customer Billing · Supplier-wise · Fast sellers · Going out of stock · Excel download",
        ShellPage.Vouchers => "Vouchers · Ctrl+V · 1–6 select · Enter create · F1 shortcuts",
        _ => "Ctrl+G Go To · Ctrl+V Vouchers · Ctrl+A Billing · Ctrl+U Customers · F1 all shortcuts",
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
        if (!CanAccess(ShellPage.Settings))
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
        AppDialog.Show(
            "GO TO (navigation)\n" +
            "• Ctrl+G — show / hide Go To menu\n" +
            "• Ctrl+V Vouchers · Ctrl+A Billing · Ctrl+Q Quotations · Ctrl+B Barcodes\n" +
            "• Ctrl+R Returns · Ctrl+T Adjustments · Ctrl+J Duplicate\n" +
            "• Ctrl+O Online Sales · Ctrl+I Credit Bills\n" +
            "• Ctrl+U Customers · Ctrl+M Salesman\n" +
            "• Ctrl+D Dashboard · Ctrl+Y Analytics · Ctrl+L Ledger · Ctrl+K Bill Lookup\n" +
            "• Ctrl+W Day Close · Ctrl+E Expenses · Ctrl+, Settings\n\n" +
            "VOUCHERS (Ctrl+V)\n" +
            "• 1 Sales · 2 Receipt · 3 Payment · 4 Credit Note · 5 Journal · 6 Daily Expense\n" +
            "• Enter — open selected voucher screen\n\n" +
            "ACTIONS\n" +
            "• F2 / Ctrl+N — new / clear / refresh (page-aware)\n" +
            "• F3 / Ctrl+F — focus search / product code\n" +
            "• F8 / Ctrl+H — hold bill (Billing)\n" +
            "• F9 / Ctrl+Enter — post bill (Billing)\n" +
            "• F10 / Ctrl+P — print preview (Billing)\n" +
            "• F4 / Ctrl+S — save customer (Customers)\n" +
            "• F11 — duplicate bill · F12 — exit\n" +
            "• Esc — close Go To drawer / reset customer form\n\n" +
            "BARCODES\n" +
            "• F5 print · F6 item list · F7 clear\n\n" +
            "Press F1 anytime for this guide.",
            "Keyboard shortcuts — TruBilling",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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
            case ShellPage.OutboundDispatch:
                _ = OutboundDispatch.RefreshCommand.ExecuteAsync(null);
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
        if (!CanAccess(ShellPage.DuplicateBill))
            return;
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
        var answer = AppDialog.Show(
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
            AppDialog.Show(
                "Switch to Billing to use billing shortcuts (F1, F2, F8 hold, F9 post, F10 print).",
                "RR Bridal Billing",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        return true;
    }
}
