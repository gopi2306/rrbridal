using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Inventory;
using RRBridal.StoreBilling.App.Services.Store;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.ViewModels;

public sealed class DaySessionRollupRowView
{
    public required string PosCounter { get; init; }
    public required string Status { get; init; }
    public required string OpeningCashDisplay { get; init; }
    public required string ExpectedCashDisplay { get; init; }
    public required string ActualCashDisplay { get; init; }
    public required string DifferenceDisplay { get; init; }
    public required string ClosedBy { get; init; }
}

public sealed class SalesmanFilterOption
{
    public string? GroupKey { get; init; }
    public required string Label { get; init; }

    public static SalesmanFilterOption All { get; } = new() { Label = "All salesman" };
}

public sealed class BrandFilterOption
{
    public string? BrandId { get; init; }
    public required string Label { get; init; }

    public static BrandFilterOption All { get; } = new() { Label = "All brands" };
}

public partial class DashboardViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly AppServices _services;
    private readonly StoreDashboardService _dashboardService;
    private readonly DayBillingCloseService _dayCloseService;
    private readonly InventoryGridClient _inventoryClient;
    private readonly StoreContext _storeContext;
    private readonly ShellBrandingService _shellBranding;
    private readonly SalesmanSalesAggregationService _salesmanAggregation;
    private readonly StockSalesAggregationService _stockSalesAggregation;
    private readonly BillMarginAggregationService _billMarginAggregation;
    private bool _suppressFilterRefresh;

    [ObservableProperty] private string _storeIdDisplay = "";

    [ObservableProperty] private string _storeDisplayName = "";

    [ObservableProperty] private string _tillDisplayLine = "";

    [ObservableProperty] private ReportScope _reportScope = ReportScope.StoreWide;

    [ObservableProperty] private string _storeWideTodaySummary = "";

    [ObservableProperty] private string _billsTodaySummary = "—";

    [ObservableProperty] private string _billsWeekSummary = "—";

    [ObservableProperty] private string _productCacheSummary = "—";

    [ObservableProperty] private string _totalAvailableQtySummary = "—";

    [ObservableProperty] private string _codBalanceTillSummary = "—";

    [ObservableProperty] private string _creditPendingBalanceSummary = "—";

    [ObservableProperty] private string _creditPendingCountSummary = "—";

    public Action? NavigateToOnlineSales { get; set; }

    public Action? NavigateToCreditBills { get; set; }

    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty] private string _dayCloseStatusMessage = "";

    [ObservableProperty] private DateTime _selectedCloseDate = DateTime.Today;

    [ObservableProperty] private string _dayCloseBillCountSummary = "—";

    [ObservableProperty] private string _dayCloseQtySummary = "—";

    [ObservableProperty] private string _dayCloseAmountSummary = "—";

    [ObservableProperty] private string _dayCloseCashSummary = "—";

    [ObservableProperty] private string _dayCloseCardSummary = "—";

    [ObservableProperty] private string _dayCloseUpiSummary = "—";

    [ObservableProperty] private string _dayCloseCreditNoteSummary = "";

    [ObservableProperty] private string _dayCloseReturnCountSummary = "—";

    [ObservableProperty] private string _dayCloseReturnTotalSummary = "—";

    [ObservableProperty] private string _dayCloseCashRefundSummary = "—";

    [ObservableProperty] private string _dayCloseCashRefundDetailSummary = "";

    [ObservableProperty] private bool _dayCloseShowCashRefundDetail;

    [ObservableProperty] private string _dayCloseCnIssuedSummary = "—";

    [ObservableProperty] private string _dayCloseDailyExpensesSummary = "—";

    [ObservableProperty] private string _dayCloseNetCashSummary = "—";

    [ObservableProperty] private string _dayCloseNetCardSummary = "—";

    [ObservableProperty] private string _dayCloseNetUpiSummary = "—";

    [ObservableProperty] private string _dayCloseActualHandInSummary = "—";

    [ObservableProperty] private string _dayCloseExpectedCashSummary = "—";

    [ObservableProperty] private string _dayCloseDepositsSummary = "—";

    [ObservableProperty] private string _dayCloseWithdrawalsSummary = "—";

    [ObservableProperty] private string _dayCloseOnlineCodPendingSummary = "—";

    public ObservableCollection<DaySessionRollupRowView> StoreRollupRows { get; } = new();

    [ObservableProperty] private string _inventorySearchText = "";

    [ObservableProperty] private InventoryStockFilter _inventoryStockFilter = InventoryStockFilter.All;

    [ObservableProperty] private string _inventoryHint = "Search SKU, barcode, or product name in local store inventory.";

    [ObservableProperty] private PosCounterFilterOption? _selectedPosCounterFilter;

    public bool IsPrimaryCounter => _storeContext.IsPrimaryCounter;

    public ObservableCollection<PosCounterFilterOption> PosCounterFilterOptions { get; } = new();

    public IReadOnlyList<InventoryStockFilterOption> StockFilterOptions { get; } =
    [
        new(InventoryStockFilter.All, "All products"),
        new(InventoryStockFilter.InStock, "In stock only"),
        new(InventoryStockFilter.OutOfStock, "Out of stock only"),
    ];

    [ObservableProperty] private int _inventoryPage = 1;

    [ObservableProperty] private int _inventoryPageSize = 100;

    [ObservableProperty] private int _inventoryTotal;

    [ObservableProperty] private int _inventoryTotalPages;

    [ObservableProperty] private string _inventoryPagerLabel = "";

    public ObservableCollection<DashboardRecentBill> RecentBills { get; } = new();

    public ObservableCollection<CreditBillSearchRow> PendingCreditBills { get; } = new();

    public ObservableCollection<DayCloseInvoiceRow> DayInvoices { get; } = new();

    public ObservableCollection<DayCloseReturnRow> DayReturns { get; } = new();

    public ObservableCollection<DayCloseStockExceptionRow> StockExceptions { get; } = new();

    public ObservableCollection<InventoryGridRow> InventoryRows { get; } = new();

    public ObservableCollection<StoreBillListRow> BillListRows { get; } = new();

    public ObservableCollection<SalesmanSalesSummaryRow> SalesmanSummaryRows { get; } = new();

    public ObservableCollection<StoreBillListRow> SalesmanBillRows { get; } = new();

    [ObservableProperty] private SalesmanSalesSummaryRow? _selectedSalesmanSummary;

    [ObservableProperty] private string _salesmanTabStatusMessage = "";

    [ObservableProperty] private string _salesmanBillTotalsSummary = "";

    [ObservableProperty] private string _filterSalesmanCode = "";

    [ObservableProperty] private string _filterSalesmanGroupKey = "";

    [ObservableProperty] private SalesmanFilterOption? _selectedSalesmanFilter;

    public ObservableCollection<SalesmanFilterOption> SalesmanFilterOptions { get; } = new();

    [ObservableProperty] private string _filterInvoiceNo = "";
    [ObservableProperty] private string _filterCustomerName = "";
    [ObservableProperty] private string _filterCustomerMobile = "";
    [ObservableProperty] private DateTime _filterBusinessDate = DateTime.Today;
    [ObservableProperty] private bool _filterUseDateRange;
    [ObservableProperty] private DateTime? _filterDateFrom = DateTime.Today;
    [ObservableProperty] private DateTime? _filterDateTo = DateTime.Today;
    [ObservableProperty] private string _billListStatusMessage = "";
    [ObservableProperty] private string _billListTotalsSummary = "";

    public ObservableCollection<BillMarginRow> BillMarginRows { get; } = new();

    [ObservableProperty] private string _billMarginStatusMessage = "";
    [ObservableProperty] private string _billMarginTotalsSummary = "";
    [ObservableProperty] private string _billMarginBillCountSummary = "—";
    [ObservableProperty] private string _billMarginTotalCostSummary = "—";
    [ObservableProperty] private string _billMarginTotalSellingSummary = "—";
    [ObservableProperty] private string _billMarginTotalDiscountSummary = "—";
    [ObservableProperty] private string _billMarginTotalMarginSummary = "—";
    [ObservableProperty] private string _billMarginTotalMarginPercentSummary = "—";

    [ObservableProperty] private StockSalesViewMode _stockSalesViewMode = StockSalesViewMode.BrandWise;

    [ObservableProperty] private string _filterStockProductSearch = "";

    [ObservableProperty] private string _filterStockBrandName = "";

    [ObservableProperty] private BrandFilterOption? _selectedStockBrandFilter;

    [ObservableProperty] private string _stockSalesStatusMessage = "";

    [ObservableProperty] private string _stockSalesTotalsSummary = "";

    [ObservableProperty] private StockAvailabilityFilterOption? _selectedStockAvailabilityFilter;

    public bool IsStockBrandFilterActive =>
        !string.IsNullOrWhiteSpace(SelectedStockBrandFilter?.BrandId)
        || !string.IsNullOrWhiteSpace(FilterStockBrandName);

    public bool IsStockSalesBrandView =>
        StockSalesViewMode == StockSalesViewMode.BrandWise && !IsStockBrandFilterActive;

    public bool IsStockSalesProductView =>
        StockSalesViewMode == StockSalesViewMode.ProductWise || IsStockBrandFilterActive;

    public ObservableCollection<BrandFilterOption> StockSalesBrandFilterOptions { get; } = new();

    public ObservableCollection<BrandSalesSummaryRow> StockSalesBrandRows { get; } = new();

    public ObservableCollection<ProductSalesSummaryRow> StockSalesProductRows { get; } = new();

    public IReadOnlyList<StockSalesViewModeOption> StockSalesViewModeOptions { get; } =
    [
        new(StockSalesViewMode.BrandWise, "Brand wise"),
        new(StockSalesViewMode.ProductWise, "Product wise"),
    ];

    [ObservableProperty] private StockSalesViewModeOption? _selectedStockSalesViewModeOption;

    public IReadOnlyList<StockAvailabilityFilterOption> StockAvailabilityFilterOptions { get; } =
    [
        new(StockAvailabilityFilter.All, "All stock"),
        new(StockAvailabilityFilter.InStock, "Available qty > 0"),
        new(StockAvailabilityFilter.OutOfStock, "Available qty = 0"),
        new(StockAvailabilityFilter.NegativeStock, "Available qty < 0"),
    ];

    public Action<string>? NavigateToReturnForBill { get; set; }
    public Action<string>? NavigateToAdjustmentForBill { get; set; }

    public DashboardViewModel(AppServices services)
    {
        _services = services;
        _dashboardService = new StoreDashboardService(services.LocalDb);
        _dayCloseService = new DayBillingCloseService(services.LocalDb, services.ProductCatalog, services.StoreAuditLog);
        _inventoryClient = services.InventoryGrid;
        _storeContext = services.StoreContext;
        _shellBranding = services.ShellBranding;
        _salesmanAggregation = new SalesmanSalesAggregationService(services.LocalDb);
        _stockSalesAggregation = new StockSalesAggregationService(services.LocalDb);
        _billMarginAggregation = new BillMarginAggregationService(services.LocalDb);
        PosCounterFilterOptions.Add(new PosCounterFilterOption(null, "All counters"));
        SelectedPosCounterFilter = PosCounterFilterOptions[0];
        SalesmanFilterOptions.Add(SalesmanFilterOption.All);
        SelectedSalesmanFilter = SalesmanFilterOption.All;
        StockSalesBrandFilterOptions.Add(BrandFilterOption.All);
        SelectedStockBrandFilter = BrandFilterOption.All;
        SelectedStockSalesViewModeOption = StockSalesViewModeOptions[0];
        SelectedStockAvailabilityFilter = StockAvailabilityFilterOptions[0];
        ApplyBrandingFromShell();
    }

    public void ApplyBrandingFromShell()
    {
        StoreDisplayName = _shellBranding.Current.StoreDisplayName;
        TillDisplayLine = _shellBranding.Current.TillDisplayLine;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        StatusMessage = "Loading store metrics…";
        try
        {
            var storeId = _storeContext.StoreId;
            StoreIdDisplay = storeId;
            ApplyBrandingFromShell();

            await ReloadPosCounterFilterOptionsAsync(storeId);

            var posFilter = SelectedPosCounterFilter?.PosCounter;
            var scope = ReportScope.StoreWide;
            var snap = await _dashboardService.LoadAsync(
                storeId,
                scope,
                _storeContext.DeviceId,
                posFilter);

            BillsTodaySummary = $"{snap.BillsTodayCount} bills · {MoneyMath.FormatRupee(snap.BillsTodayRevenue)}";
            StoreWideTodaySummary = string.IsNullOrWhiteSpace(posFilter) && snap.StoreWideBillsTodayCount is int st
                ? $"Store-wide today: {st} bills · {MoneyMath.FormatRupee(snap.StoreWideBillsTodayRevenue ?? 0)}"
                : "";
            BillsWeekSummary = $"{snap.BillsLast7DaysCount} bills · {MoneyMath.FormatRupee(snap.BillsLast7DaysRevenue)}";
            ProductCacheSummary = snap.ProductCacheCount.ToString(InCulture);
            TotalAvailableQtySummary = snap.TotalAvailableQty.ToString("N2", InCulture);

            var codBal = await _services.OnlineCodBills.GetPendingBalanceAsync(storeId);
            CodBalanceTillSummary = MoneyMath.FormatRupee(codBal.BalanceTill);

            var creditBal = await _services.CreditBills.GetPendingBalanceAsync(storeId);
            CreditPendingBalanceSummary = MoneyMath.FormatRupee(creditBal.BalanceTill);
            CreditPendingCountSummary = creditBal.PendingCount.ToString(InCulture);

            PendingCreditBills.Clear();
            foreach (var row in await _services.CreditBills.GetPendingListAsync(storeId, limit: 20))
                PendingCreditBills.Add(row);

            RecentBills.Clear();
            foreach (var r in snap.RecentBills)
                RecentBills.Add(r);

            var filterLabel = SelectedPosCounterFilter?.Label ?? "All counters";
            StatusMessage = $"Metrics updated {DateTime.Now.ToString("T", InCulture)} · {filterLabel}";

            await LoadDayCloseAsync(storeId, posFilter);
            await LoadSalesmanSummaryAsync();
            await LoadBillMargin();
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not load metrics: " + ex.Message;
        }
    }

    partial void OnSelectedSalesmanSummaryChanged(SalesmanSalesSummaryRow? value)
    {
        if (value == null)
        {
            if (SelectedSalesmanFilter?.GroupKey != null)
                return;
            FilterSalesmanGroupKey = "";
            FilterSalesmanCode = "";
            _ = LoadSalesmanBillsAsync();
            return;
        }

        FilterSalesmanGroupKey = value.GroupKey;
        FilterSalesmanCode = value.SalesmanCode;
        _suppressFilterRefresh = true;
        SelectedSalesmanFilter = SalesmanFilterOptions.FirstOrDefault(o =>
            o.GroupKey != null && string.Equals(o.GroupKey, value.GroupKey, StringComparison.OrdinalIgnoreCase))
            ?? SalesmanFilterOption.All;
        _suppressFilterRefresh = false;
        _ = LoadSalesmanBillsAsync();
    }

    partial void OnSelectedSalesmanFilterChanged(SalesmanFilterOption? value)
    {
        if (_suppressFilterRefresh)
            return;

        if (value?.GroupKey == null)
        {
            FilterSalesmanGroupKey = "";
            FilterSalesmanCode = "";
            _suppressFilterRefresh = true;
            SelectedSalesmanSummary = null;
            _suppressFilterRefresh = false;
            _ = LoadSalesmanBillsAsync();
            _ = LoadBillList();
            _ = LoadBillMargin();
            return;
        }

        FilterSalesmanGroupKey = value.GroupKey;
        var match = SalesmanSummaryRows.FirstOrDefault(r =>
            string.Equals(r.GroupKey, value.GroupKey, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            FilterSalesmanCode = match.SalesmanCode;
            _suppressFilterRefresh = true;
            SelectedSalesmanSummary = match;
            _suppressFilterRefresh = false;
        }
        else
        {
            FilterSalesmanCode = "";
            _ = LoadSalesmanBillsAsync();
        }

        _ = LoadBillList();
        _ = LoadBillMargin();
    }

    [RelayCommand]
    private async Task LoadSalesmanSummary()
    {
        await LoadSalesmanSummaryAsync();
    }

    private async Task LoadSalesmanSummaryAsync()
    {
        SalesmanTabStatusMessage = "Loading salesman totals…";
        try
        {
            var rows = await _salesmanAggregation.AggregateAsync(
                _storeContext.StoreId,
                SelectedPosCounterFilter?.PosCounter,
                FilterUseDateRange ? null : FilterBusinessDate,
                FilterUseDateRange,
                FilterUseDateRange ? FilterDateFrom : null,
                FilterUseDateRange ? FilterDateTo : null);

            SalesmanSummaryRows.Clear();
            foreach (var row in rows)
                SalesmanSummaryRows.Add(row);

            RebuildSalesmanFilterOptions(rows);

            SalesmanTabStatusMessage = $"{rows.Count} salesman group(s) · updated {DateTime.Now:T}";

            if (SelectedSalesmanSummary != null)
            {
                var restored = rows.FirstOrDefault(r =>
                    string.Equals(r.GroupKey, SelectedSalesmanSummary.GroupKey, StringComparison.OrdinalIgnoreCase));
                _suppressFilterRefresh = true;
                SelectedSalesmanSummary = restored ?? rows.FirstOrDefault();
                _suppressFilterRefresh = false;
            }
            else if (rows.Count > 0)
            {
                _suppressFilterRefresh = true;
                SelectedSalesmanSummary = rows[0];
                _suppressFilterRefresh = false;
            }

            await LoadSalesmanBillsAsync();
        }
        catch (Exception ex)
        {
            SalesmanTabStatusMessage = "Could not load salesman totals: " + ex.Message;
        }
    }

    private void RebuildSalesmanFilterOptions(IReadOnlyList<SalesmanSalesSummaryRow> rows)
    {
        var selectedKey = SelectedSalesmanFilter?.GroupKey;
        SalesmanFilterOptions.Clear();
        SalesmanFilterOptions.Add(SalesmanFilterOption.All);
        foreach (var row in rows)
        {
            SalesmanFilterOptions.Add(new SalesmanFilterOption
            {
                GroupKey = row.GroupKey,
                Label = row.DisplayLabel,
            });
        }

        SelectedSalesmanFilter = selectedKey == null
            ? SalesmanFilterOption.All
            : SalesmanFilterOptions.FirstOrDefault(o => o.GroupKey == selectedKey) ?? SalesmanFilterOption.All;
    }

    private async Task LoadSalesmanBillsAsync()
    {
        try
        {
            var query = new StoreBillListQuery
            {
                BusinessDate = FilterUseDateRange ? null : FilterBusinessDate,
                UseDateRange = FilterUseDateRange,
                DateFrom = FilterUseDateRange ? FilterDateFrom : null,
                DateTo = FilterUseDateRange ? FilterDateTo : null,
                PosCounterFilter = SelectedPosCounterFilter?.PosCounter,
                SalesmanGroupKey = string.IsNullOrWhiteSpace(FilterSalesmanGroupKey) ? null : FilterSalesmanGroupKey,
                SalesmanCode = string.IsNullOrWhiteSpace(FilterSalesmanGroupKey) ? null : FilterSalesmanCode,
            };

            var snap = await _services.StoreBillList.LoadAsync(_storeContext.StoreId, query);
            SalesmanBillRows.Clear();
            foreach (var row in snap.Rows)
                SalesmanBillRows.Add(row);

            var filterLabel = string.IsNullOrWhiteSpace(FilterSalesmanGroupKey)
                ? "All salesman"
                : SelectedSalesmanSummary?.DisplayLabel ?? FilterSalesmanGroupKey;
            SalesmanBillTotalsSummary =
                $"{filterLabel}: {snap.Rows.Count} bill(s) · Qty {snap.Rows.Sum(r => r.TotalQty):N2} · " +
                $"Payable {MoneyMath.FormatRupee(snap.TotalPayable)} · Cash {MoneyMath.FormatRupee(snap.TotalCash)}";
        }
        catch (Exception ex)
        {
            SalesmanBillTotalsSummary = "Could not load bills: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task LoadDayClose()
    {
        await LoadDayCloseAsync(_storeContext.StoreId, SelectedPosCounterFilter?.PosCounter);
    }

    [RelayCommand]
    private void OpenOnlineSalesPage() => NavigateToOnlineSales?.Invoke();

    [RelayCommand]
    private void OpenCreditBillsPage() => NavigateToCreditBills?.Invoke();

    [RelayCommand]
    private async Task ApproveStockException(string? billNo)
    {
        if (string.IsNullOrWhiteSpace(billNo))
            return;

        var confirm = MessageBox.Show(
            $"Approve stock decrement for all pending exception lines on bill {billNo}?\n\nLocal stock may go negative.",
            "Approve stock exception",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        var user = _services.UserSession?.LoggedInUser.Name ?? "Unknown";
        try
        {
            var (success, message) = await _dayCloseService.ApproveStockExceptionsAsync(
                _storeContext.StoreId,
                billNo,
                user);

            DayCloseStatusMessage = message;
            if (success)
                await LoadDayCloseAsync(_storeContext.StoreId, SelectedPosCounterFilter?.PosCounter);
        }
        catch (Exception ex)
        {
            DayCloseStatusMessage = "Approve failed: " + ex.Message;
        }
    }

    private async Task LoadDayCloseAsync(string storeId, string? posFilter)
    {
        DayCloseStatusMessage = "Loading day close…";
        try
        {
            var snap = await _services.DaySessions.LoadDayCloseWithSessionAsync(storeId, SelectedCloseDate, posFilter);

            DayCloseBillCountSummary = snap.BillCount.ToString(InCulture);
            DayCloseQtySummary = snap.TotalQty.ToString("N2", InCulture);
            DayCloseAmountSummary = MoneyMath.FormatRupee(snap.TotalAmount);
            DayCloseCashSummary = MoneyMath.FormatRupee(snap.CashTotal);
            DayCloseCardSummary = MoneyMath.FormatRupee(snap.CardTotal);
            DayCloseUpiSummary = MoneyMath.FormatRupee(snap.UpiTotal);
            DayCloseCreditNoteSummary = snap.CreditNoteTotal > 0
                ? MoneyMath.FormatRupee(snap.CreditNoteTotal)
                : "—";

            DayCloseReturnCountSummary = snap.ReturnCount.ToString(InCulture);
            DayCloseReturnTotalSummary = snap.ReturnTotalAmount > 0
                ? MoneyMath.FormatRupee(snap.ReturnTotalAmount)
                : "—";
            DayCloseCashRefundSummary = snap.CashRefundTotal > 0
                ? MoneyMath.FormatRupee(snap.CashRefundTotal)
                : "—";
            DayCloseShowCashRefundDetail = snap.CashRefundTotal > 0;
            DayCloseCashRefundDetailSummary = snap.CashRefundTotal > 0
                ? $"Returns: {MoneyMath.FormatRupee(snap.ReturnCashRefundTotal)} · CN cashout: {MoneyMath.FormatRupee(snap.CreditNoteCashoutTotal)}"
                : "";
            DayCloseCnIssuedSummary = snap.CreditNoteIssuedTotal > 0
                ? MoneyMath.FormatRupee(snap.CreditNoteIssuedTotal)
                : "—";
            DayCloseDailyExpensesSummary = snap.DailyExpensesTotal > 0
                ? MoneyMath.FormatRupee(snap.DailyExpensesTotal)
                : "—";
            DayCloseNetCashSummary = MoneyMath.FormatRupee(snap.NetCashInHand);
            DayCloseNetCardSummary = MoneyMath.FormatRupee(snap.NetCardInHand);
            DayCloseNetUpiSummary = MoneyMath.FormatRupee(snap.NetUpiInHand);
            DayCloseActualHandInSummary = MoneyMath.FormatRupee(snap.ActualHandInTotal);
            DayCloseExpectedCashSummary = MoneyMath.FormatRupee(snap.ExpectedCash);
            DayCloseDepositsSummary = snap.DepositsTotal > 0 ? MoneyMath.FormatRupee(snap.DepositsTotal) : "—";
            DayCloseWithdrawalsSummary = snap.WithdrawalsTotal > 0 ? MoneyMath.FormatRupee(snap.WithdrawalsTotal) : "—";

            var onlineCodPending = await _services.OnlineCodBills.GetPendingTotalForBusinessDateAsync(
                storeId, SelectedCloseDate);
            DayCloseOnlineCodPendingSummary = onlineCodPending > 0
                ? MoneyMath.FormatRupee(onlineCodPending)
                : "—";

            StoreRollupRows.Clear();
            if (_storeContext.IsPrimaryCounter)
            {
                var businessDate = DaySessionService.FormatBusinessDate(SelectedCloseDate);
                var rollup = await _services.DaySessions.GetStoreRollupAsync(storeId, businessDate);
                foreach (var row in rollup.Counters)
                {
                    StoreRollupRows.Add(new DaySessionRollupRowView
                    {
                        PosCounter = $"POS{row.PosCounter}",
                        Status = row.Status,
                        OpeningCashDisplay = MoneyMath.FormatRupee(row.OpeningCash),
                        ExpectedCashDisplay = row.ExpectedCash > 0 ? MoneyMath.FormatRupee(row.ExpectedCash) : "—",
                        ActualCashDisplay = row.ActualCashCounted > 0 ? MoneyMath.FormatRupee(row.ActualCashCounted) : "—",
                        DifferenceDisplay = row.Status == DaySessionStatus.Closed
                            ? MoneyMath.FormatRupee(row.CashDifference)
                            : "—",
                        ClosedBy = row.ClosedBy ?? "—",
                    });
                }
            }

            DayInvoices.Clear();
            foreach (var row in snap.Invoices)
                DayInvoices.Add(row);

            DayReturns.Clear();
            foreach (var row in snap.Returns)
                DayReturns.Add(row);

            StockExceptions.Clear();
            foreach (var row in snap.StockExceptions)
                StockExceptions.Add(row);

            var dateLabel = SelectedCloseDate.ToString("dd-MMM-yyyy", InCulture);
            var filterLabel = SelectedPosCounterFilter?.Label ?? "All counters";
            DayCloseStatusMessage =
                $"{snap.BillCount} bill(s), {snap.ReturnCount} return(s) on {dateLabel} · {filterLabel} · updated {DateTime.Now.ToString("T", InCulture)}";
        }
        catch (Exception ex)
        {
            DayCloseStatusMessage = "Could not load day close: " + ex.Message;
        }
    }

    partial void OnSelectedCloseDateChanged(DateTime value)
    {
        if (!_suppressFilterRefresh)
            _ = LoadDayCloseAsync(_storeContext.StoreId, SelectedPosCounterFilter?.PosCounter);
    }

    partial void OnSelectedPosCounterFilterChanged(PosCounterFilterOption? value)
    {
        if (!_suppressFilterRefresh)
            _ = Refresh();
    }

    partial void OnInventoryStockFilterChanged(InventoryStockFilter value)
    {
        InventoryPage = 1;
        _ = LoadInventoryGridAsync();
    }

    private async Task ReloadPosCounterFilterOptionsAsync(string storeId)
    {
        var previous = SelectedPosCounterFilter?.PosCounter;
        var distinct = await _dashboardService.GetDistinctPosCountersAsync(storeId);

        _suppressFilterRefresh = true;
        try
        {
            PosCounterFilterOptions.Clear();
            PosCounterFilterOptions.Add(new PosCounterFilterOption(null, "All counters"));
            foreach (var p in distinct)
                PosCounterFilterOptions.Add(new PosCounterFilterOption(p, $"POS{p}"));

            SelectedPosCounterFilter = PosCounterFilterOptions.FirstOrDefault(o =>
                string.Equals(o.PosCounter, previous, StringComparison.OrdinalIgnoreCase))
                ?? PosCounterFilterOptions[0];
        }
        finally
        {
            _suppressFilterRefresh = false;
        }
    }

    [RelayCommand]
    private async Task SearchInventory()
    {
        InventoryPage = 1;
        await LoadInventoryGridAsync();
    }

    [RelayCommand]
    private async Task InventoryNextPage()
    {
        if (InventoryTotalPages == 0 || InventoryPage >= InventoryTotalPages)
            return;
        InventoryPage++;
        await LoadInventoryGridAsync();
    }

    [RelayCommand]
    private async Task InventoryPreviousPage()
    {
        if (InventoryPage <= 1)
            return;
        InventoryPage--;
        await LoadInventoryGridAsync();
    }

    [RelayCommand]
    private async Task AdjustInventoryStock(InventoryGridRow? row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.Sku))
            return;

        if (!InventoryAdjustStockDialog.TryShow(
                Application.Current.MainWindow!,
                row,
                out var mode,
                out var quantity,
                out var reason))
        {
            return;
        }

        InventoryHint = "Applying adjustment…";
        try
        {
            var (success, message) = await _services.InventoryAdjustments.AdjustAsync(
                row.Sku,
                row.StoreQty,
                mode,
                quantity,
                reason);

            InventoryHint = message;
            if (success)
                await LoadInventoryGridAsync();
        }
        catch (Exception ex)
        {
            InventoryHint = "Could not adjust stock. " + ex.Message;
        }
    }

    private async Task LoadInventoryGridAsync()
    {
        InventoryRows.Clear();
        InventoryHint = "Loading…";
        InventoryPagerLabel = "";
        try
        {
            var storeId = _storeContext.StoreId;
            var result = await _inventoryClient.SearchAsync(
                InventorySearchText,
                storeId,
                InventoryPage,
                InventoryPageSize,
                InventoryStockFilter);

            foreach (var r in result.Data)
                InventoryRows.Add(r);

            InventoryTotal = result.Total;
            InventoryTotalPages = result.TotalPages;

            if (result.Total == 0)
            {
                InventoryHint = "No local inventory rows. Sync products/transfers or try another search.";
                InventoryPagerLabel = "";
            }
            else
            {
                InventoryPagerLabel = $"Page {result.Page} of {result.TotalPages} ({result.Total} matches)";
                InventoryHint =
                    $"{result.Data.Count} row(s) on this page · filter: {GetStockFilterLabel(InventoryStockFilter)} · store «{storeId}».";
            }
        }
        catch (Exception ex)
        {
            InventoryHint = "Could not read local store inventory. " + ex.Message;
            InventoryPagerLabel = "";
        }
    }

    private static string GetStockFilterLabel(InventoryStockFilter filter) =>
        filter switch
        {
            InventoryStockFilter.InStock => "In stock only",
            InventoryStockFilter.OutOfStock => "Out of stock only",
            _ => "All products",
        };

    [RelayCommand]
    private async Task LoadBillList()
    {
        BillListStatusMessage = "Loading bills…";
        try
        {
            var query = new StoreBillListQuery
            {
                InvoiceNo = string.IsNullOrWhiteSpace(FilterInvoiceNo) ? null : FilterInvoiceNo.Trim(),
                CustomerName = string.IsNullOrWhiteSpace(FilterCustomerName) ? null : FilterCustomerName.Trim(),
                CustomerMobile = string.IsNullOrWhiteSpace(FilterCustomerMobile) ? null : FilterCustomerMobile.Trim(),
                BusinessDate = FilterUseDateRange ? null : FilterBusinessDate,
                UseDateRange = FilterUseDateRange,
                DateFrom = FilterUseDateRange ? FilterDateFrom : null,
                DateTo = FilterUseDateRange ? FilterDateTo : null,
                PosCounterFilter = SelectedPosCounterFilter?.PosCounter,
                SalesmanGroupKey = string.IsNullOrWhiteSpace(FilterSalesmanGroupKey) ? null : FilterSalesmanGroupKey,
                SalesmanCode = string.IsNullOrWhiteSpace(FilterSalesmanCode) ? null : FilterSalesmanCode,
            };

            var snap = await _services.StoreBillList.LoadAsync(_storeContext.StoreId, query);

            BillListRows.Clear();
            foreach (var row in snap.Rows)
                BillListRows.Add(row);

            BillListTotalsSummary =
                $"{snap.Rows.Count} bill(s) · Payable {MoneyMath.FormatRupee(snap.TotalPayable)} · " +
                $"Cash {MoneyMath.FormatRupee(snap.TotalCash)} · Card {MoneyMath.FormatRupee(snap.TotalCard)} · " +
                $"UPI {MoneyMath.FormatRupee(snap.TotalUpi)} · CN {MoneyMath.FormatRupee(snap.TotalCreditNote)}";

            var dateLabel = FilterUseDateRange
                ? $"{FilterDateFrom:dd-MMM-yyyy} – {FilterDateTo:dd-MMM-yyyy}"
                : FilterBusinessDate.ToString("dd-MMM-yyyy", InCulture);
            var trunc = snap.WasTruncated ? $" (showing first {snap.Rows.Count} of {snap.TotalMatched})" : "";
            BillListStatusMessage = $"Updated {DateTime.Now:T} · {dateLabel}{trunc}";
        }
        catch (Exception ex)
        {
            BillListStatusMessage = "Could not load bills: " + ex.Message;
            BillListTotalsSummary = "";
        }
    }

    [RelayCommand]
    private async Task DownloadDayCloseReport()
    {
        var counterSuffix = string.IsNullOrWhiteSpace(SelectedPosCounterFilter?.PosCounter)
            ? ""
            : $"-pos{SelectedPosCounterFilter.PosCounter}";
        var dateStr = SelectedCloseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var baseName = $"day-close-{_storeContext.StoreId}-{dateStr}{counterSuffix}";

        var dlg = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|Excel workbook (*.xlsx)|*.xlsx",
            FileName = $"{baseName}.csv",
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            var data = await _services.DayCloseReports.LoadAsync(
                _storeContext.StoreId,
                SelectedCloseDate,
                SelectedPosCounterFilter?.PosCounter,
                StoreDisplayName);

            if (dlg.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                DayCloseExcelExporter.ExportToFile(dlg.FileName, data);
            else
                DayCloseCsvExporter.ExportToFile(dlg.FileName, data);

            DayCloseStatusMessage = $"Exported report to {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Export failed: " + ex.Message, "Day close", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void DownloadBillListReport()
    {
        if (BillListRows.Count == 0)
        {
            MessageBox.Show("No bills to export. Run Search first.", "All bills", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"store-bills-{DateTime.Now:yyyyMMdd-HHmm}.csv",
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            BillListCsvExporter.ExportToFile(dlg.FileName, BillListRows.ToList());
            BillListStatusMessage = $"Exported {BillListRows.Count} row(s) to {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Export failed: " + ex.Message, "All bills", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task LoadBillMargin()
    {
        BillMarginStatusMessage = "Loading bill margin…";
        try
        {
            var query = new BillMarginQuery
            {
                InvoiceNo = string.IsNullOrWhiteSpace(FilterInvoiceNo) ? null : FilterInvoiceNo.Trim(),
                BusinessDate = FilterUseDateRange ? null : FilterBusinessDate,
                UseDateRange = FilterUseDateRange,
                DateFrom = FilterUseDateRange ? FilterDateFrom : null,
                DateTo = FilterUseDateRange ? FilterDateTo : null,
                PosCounterFilter = SelectedPosCounterFilter?.PosCounter,
                SalesmanGroupKey = string.IsNullOrWhiteSpace(FilterSalesmanGroupKey) ? null : FilterSalesmanGroupKey,
                SalesmanCode = string.IsNullOrWhiteSpace(FilterSalesmanCode) ? null : FilterSalesmanCode,
            };

            var snap = await _billMarginAggregation.LoadAsync(_storeContext.StoreId, query);

            BillMarginRows.Clear();
            foreach (var row in snap.Rows)
                BillMarginRows.Add(row);

            BillMarginBillCountSummary = snap.Rows.Count.ToString(InCulture);
            BillMarginTotalCostSummary = MoneyMath.FormatRupee(snap.TotalCost);
            BillMarginTotalSellingSummary = MoneyMath.FormatRupee(snap.TotalSelling);
            BillMarginTotalDiscountSummary = MoneyMath.FormatRupee(snap.TotalDiscount);
            BillMarginTotalMarginSummary = MoneyMath.FormatRupee(snap.TotalMargin);
            BillMarginTotalMarginPercentSummary =
                snap.TotalMarginPercent.ToString("N2", InCulture) + "%";

            BillMarginTotalsSummary =
                $"{snap.Rows.Count} bill(s) · Cost {MoneyMath.FormatRupee(snap.TotalCost)} · " +
                $"Selling {MoneyMath.FormatRupee(snap.TotalSelling)} · Discount {MoneyMath.FormatRupee(snap.TotalDiscount)} · " +
                $"Margin {MoneyMath.FormatRupee(snap.TotalMargin)} ({snap.TotalMarginPercent:N2}%)";

            var dateLabel = FilterUseDateRange
                ? $"{FilterDateFrom:dd-MMM-yyyy} – {FilterDateTo:dd-MMM-yyyy}"
                : FilterBusinessDate.ToString("dd-MMM-yyyy", InCulture);
            var trunc = snap.WasTruncated ? $" (showing first {snap.Rows.Count} of {snap.TotalMatched})" : "";
            BillMarginStatusMessage = $"Updated {DateTime.Now:T} · {dateLabel} · amounts ex GST{trunc}";
        }
        catch (Exception ex)
        {
            BillMarginStatusMessage = "Could not load bill margin: " + ex.Message;
            BillMarginTotalsSummary = "";
            BillMarginBillCountSummary = "—";
            BillMarginTotalCostSummary = "—";
            BillMarginTotalSellingSummary = "—";
            BillMarginTotalDiscountSummary = "—";
            BillMarginTotalMarginSummary = "—";
            BillMarginTotalMarginPercentSummary = "—";
        }
    }

    [RelayCommand]
    private void DownloadBillMarginExcel()
    {
        if (BillMarginRows.Count == 0)
        {
            MessageBox.Show("No rows to export. Run Search first.", "Bill margin", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            FileName = $"bill-margin-{DateTime.Now:yyyyMMdd-HHmm}.xlsx",
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            BillMarginExcelExporter.ExportToFile(dlg.FileName, BillMarginRows.ToList());
            BillMarginStatusMessage = $"Exported {BillMarginRows.Count} row(s) to {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Export failed: " + ex.Message, "Bill margin", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ViewBillDetail(StoreBillListRow? row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.BillNo))
            return;

        var vm = new BillDetailDialogViewModel(_services, _services.StoreBillList);
        vm.NavigateToReturnForBill = billNo =>
        {
            NavigateToReturnForBill?.Invoke(billNo);
        };
        vm.NavigateToAdjustmentForBill = billNo =>
        {
            NavigateToAdjustmentForBill?.Invoke(billNo);
        };

        await vm.LoadAsync(row.BillNo);
        var dialog = new BillDetailDialog(vm)
        {
            Owner = Application.Current.MainWindow,
        };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void RedirectBillRelated(StoreBillListRow? row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.BillNo))
            return;

        if (row.HasReturn)
            NavigateToReturnForBill?.Invoke(row.BillNo);
        else if (row.HasAdjustment)
            NavigateToAdjustmentForBill?.Invoke(row.BillNo);
    }

    partial void OnSelectedStockSalesViewModeOptionChanged(StockSalesViewModeOption? value)
    {
        if (value == null)
            return;

        StockSalesViewMode = value.Mode;
        NotifyStockSalesViewVisibility();
    }

    partial void OnStockSalesViewModeChanged(StockSalesViewMode value) => NotifyStockSalesViewVisibility();

    partial void OnSelectedStockBrandFilterChanged(BrandFilterOption? value) => NotifyStockSalesViewVisibility();

    partial void OnFilterStockBrandNameChanged(string value) => NotifyStockSalesViewVisibility();

    private void NotifyStockSalesViewVisibility()
    {
        OnPropertyChanged(nameof(IsStockBrandFilterActive));
        OnPropertyChanged(nameof(IsStockSalesBrandView));
        OnPropertyChanged(nameof(IsStockSalesProductView));
    }

    [RelayCommand]
    private async Task LoadStockSales()
    {
        StockSalesStatusMessage = "Loading stock sales…";
        try
        {
            await RebuildStockBrandFilterOptionsAsync();

            var result = await _stockSalesAggregation.AggregateAsync(
                _storeContext.StoreId,
                SelectedPosCounterFilter?.PosCounter,
                FilterUseDateRange ? null : FilterBusinessDate,
                FilterUseDateRange,
                FilterUseDateRange ? FilterDateFrom : null,
                FilterUseDateRange ? FilterDateTo : null,
                SelectedStockBrandFilter?.BrandId,
                string.IsNullOrWhiteSpace(FilterStockBrandName) ? null : FilterStockBrandName.Trim(),
                string.IsNullOrWhiteSpace(FilterStockProductSearch) ? null : FilterStockProductSearch.Trim(),
                SelectedStockAvailabilityFilter?.Filter ?? StockAvailabilityFilter.All);

            StockSalesBrandRows.Clear();
            foreach (var row in result.BrandRows)
                StockSalesBrandRows.Add(row);

            StockSalesProductRows.Clear();
            foreach (var row in result.ProductRows)
                StockSalesProductRows.Add(row);

            var dateLabel = FilterUseDateRange
                ? $"{FilterDateFrom:dd-MMM-yyyy} – {FilterDateTo:dd-MMM-yyyy}"
                : FilterBusinessDate.ToString("dd-MMM-yyyy", InCulture);
            var viewLabel = IsStockSalesProductView ? "product" : "brand";
            StockSalesTotalsSummary =
                $"{result.BillCount} bill(s) · {result.ProductRows.Count} product(s) · Sold qty {result.TotalQty:N2} · Available qty {result.TotalAvailableQty:N2} · Amount {MoneyMath.FormatRupee(result.TotalAmount)}";
            StockSalesStatusMessage =
                $"{(IsStockSalesProductView ? result.ProductRows.Count : result.BrandRows.Count)} {viewLabel} row(s) · {dateLabel} · updated {DateTime.Now:T}";
            NotifyStockSalesViewVisibility();
        }
        catch (Exception ex)
        {
            StockSalesStatusMessage = "Could not load stock sales: " + ex.Message;
            StockSalesTotalsSummary = "";
        }
    }

    private async Task RebuildStockBrandFilterOptionsAsync()
    {
        var brands = await _services.MasterData.GetAsync("brands");
        var previous = SelectedStockBrandFilter?.BrandId;

        StockSalesBrandFilterOptions.Clear();
        StockSalesBrandFilterOptions.Add(BrandFilterOption.All);
        foreach (var brand in brands.Where(b => b.IsActive).OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
        {
            var label = string.IsNullOrWhiteSpace(brand.Code)
                ? brand.Name
                : $"{brand.Code} — {brand.Name}";
            StockSalesBrandFilterOptions.Add(new BrandFilterOption
            {
                BrandId = brand.Id,
                Label = label,
            });
        }

        SelectedStockBrandFilter = StockSalesBrandFilterOptions.FirstOrDefault(o =>
            o.BrandId != null && string.Equals(o.BrandId, previous, StringComparison.OrdinalIgnoreCase))
            ?? BrandFilterOption.All;
    }
}

public sealed class StockSalesViewModeOption
{
    public StockSalesViewModeOption(StockSalesViewMode mode, string label)
    {
        Mode = mode;
        Label = label;
    }

    public StockSalesViewMode Mode { get; }
    public string Label { get; }
}
