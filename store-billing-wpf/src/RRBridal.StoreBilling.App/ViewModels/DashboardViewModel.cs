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

public partial class DashboardViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly AppServices _services;
    private readonly StoreDashboardService _dashboardService;
    private readonly DayBillingCloseService _dayCloseService;
    private readonly InventoryGridClient _inventoryClient;
    private readonly StoreContext _storeContext;
    private readonly ShellBrandingService _shellBranding;
    private bool _suppressFilterRefresh;

    [ObservableProperty] private string _storeIdDisplay = "";

    [ObservableProperty] private string _storeDisplayName = "";

    [ObservableProperty] private string _tillDisplayLine = "";

    [ObservableProperty] private ReportScope _reportScope = ReportScope.StoreWide;

    [ObservableProperty] private string _storeWideTodaySummary = "";

    [ObservableProperty] private string _billsTodaySummary = "—";

    [ObservableProperty] private string _billsWeekSummary = "—";

    [ObservableProperty] private string _pendingOutboxSummary = "—";

    [ObservableProperty] private string _syncSummary = "—";

    [ObservableProperty] private string _productCacheSummary = "—";

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

    public ObservableCollection<DayCloseInvoiceRow> DayInvoices { get; } = new();

    public ObservableCollection<DayCloseReturnRow> DayReturns { get; } = new();

    public ObservableCollection<DayCloseStockExceptionRow> StockExceptions { get; } = new();

    public ObservableCollection<InventoryGridRow> InventoryRows { get; } = new();

    public ObservableCollection<StoreBillListRow> BillListRows { get; } = new();

    [ObservableProperty] private string _filterInvoiceNo = "";
    [ObservableProperty] private string _filterCustomerName = "";
    [ObservableProperty] private string _filterCustomerMobile = "";
    [ObservableProperty] private DateTime _filterBusinessDate = DateTime.Today;
    [ObservableProperty] private bool _filterUseDateRange;
    [ObservableProperty] private DateTime? _filterDateFrom = DateTime.Today;
    [ObservableProperty] private DateTime? _filterDateTo = DateTime.Today;
    [ObservableProperty] private string _billListStatusMessage = "";
    [ObservableProperty] private string _billListTotalsSummary = "";

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
        PosCounterFilterOptions.Add(new PosCounterFilterOption(null, "All counters"));
        SelectedPosCounterFilter = PosCounterFilterOptions[0];
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
            PendingOutboxSummary = snap.PendingOutboxCount.ToString(InCulture);
            SyncSummary = string.IsNullOrWhiteSpace(snap.SyncUpdatedAt)
                ? $"Cursor {snap.SyncCursor}"
                : $"Cursor {snap.SyncCursor} · last pull {snap.SyncUpdatedAt}";
            ProductCacheSummary = snap.ProductCacheCount.ToString(InCulture);

            RecentBills.Clear();
            foreach (var r in snap.RecentBills)
                RecentBills.Add(r);

            var filterLabel = SelectedPosCounterFilter?.Label ?? "All counters";
            StatusMessage = $"Metrics updated {DateTime.Now.ToString("T", InCulture)} · {filterLabel}";

            await LoadDayCloseAsync(storeId, posFilter);
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not load metrics: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task LoadDayClose()
    {
        await LoadDayCloseAsync(_storeContext.StoreId, SelectedPosCounterFilter?.PosCounter);
    }

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
}
