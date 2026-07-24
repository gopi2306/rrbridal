using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Store;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class LedgerViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private const int MaxBills = 200;
    private const int MaxPayments = 200;

    private readonly StoreLedgerService _ledgerService;
    private readonly StoreContext _storeContext;
    private readonly ShellBrandingService _shellBranding;
    private bool _suppressFilterRefresh;

    [ObservableProperty] private string _storeIdDisplay = "";

    [ObservableProperty] private string _storeDisplayName = "";

    [ObservableProperty] private string _tillDisplayLine = "";

    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty] private PosCounterFilterOption? _selectedPosCounterFilter;

    [ObservableProperty] private LedgerBillRow? _selectedBill;

    private readonly AppServices _services;

    public ObservableCollection<PosCounterFilterOption> PosCounterFilterOptions { get; } = new();

    public ObservableCollection<LedgerBillRow> Bills { get; } = new();

    public ObservableCollection<LedgerPaymentRow> Payments { get; } = new();

    public LedgerViewModel(AppServices services)
    {
        _services = services;
        _ledgerService = new StoreLedgerService(services.LocalDb);
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
        StatusMessage = "Loading ledger…";
        try
        {
            var storeId = _storeContext.StoreId;
            StoreIdDisplay = storeId;
            ApplyBrandingFromShell();

            await ReloadPosCounterFilterOptionsAsync(storeId);

            var posFilter = SelectedPosCounterFilter?.PosCounter;
            var snap = await _ledgerService.LoadAsync(
                storeId,
                MaxBills,
                MaxPayments,
                ReportScope.StoreWide,
                _storeContext.DeviceId,
                posFilter);

            Bills.Clear();
            foreach (var b in snap.Bills)
                Bills.Add(b);

            Payments.Clear();
            foreach (var p in snap.Payments)
                Payments.Add(p);

            var filterLabel = SelectedPosCounterFilter?.Label ?? "All counters";
            StatusMessage =
                $"Loaded {Bills.Count} bill(s), {Payments.Count} payment(s) ({filterLabel}). Updated {DateTime.Now.ToString("T", InCulture)}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not load ledger: " + ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanReprintDuplicate))]
    private async Task ReprintDuplicate()
    {
        if (SelectedBill == null)
            return;

        if (!_services.PosBillingSettings.Current.AllowDuplicatePrint)
        {
            AppDialog.Show("Duplicate bill printing is disabled in billing settings.", "Reprint",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var doc = await _services.BillDocuments.GetByBillNoAsync(SelectedBill.BillNo);
        if (doc == null || doc.GetValue("status", "posted").AsString != "posted")
        {
            AppDialog.Show("Only posted bills can be reprinted.", "Reprint",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var user = _services.UserSession?.LoggedInUser.Name ?? "Unknown";
        var input = _services.BillDocuments.MapToThermalInput(doc, isDuplicate: true, printedBy: user);
        if (await InvoicePrintFlow.ShowAsync(_services, input, printInvoiceEnabled: true))
        {
            await _services.BillDocuments.AppendPrintAuditAsync(SelectedBill.BillNo, "duplicate", user);
            StatusMessage = $"Duplicate reprinted for {SelectedBill.BillNo}.";
        }
    }

    private bool CanReprintDuplicate() =>
        SelectedBill != null && string.Equals(SelectedBill.Status, "posted", StringComparison.OrdinalIgnoreCase);

    partial void OnSelectedBillChanged(LedgerBillRow? value) =>
        ReprintDuplicateCommand.NotifyCanExecuteChanged();

    partial void OnSelectedPosCounterFilterChanged(PosCounterFilterOption? value)
    {
        if (!_suppressFilterRefresh)
            _ = Refresh();
    }

    private async Task ReloadPosCounterFilterOptionsAsync(string storeId)
    {
        var previous = SelectedPosCounterFilter?.PosCounter;
        var distinct = await _ledgerService.GetDistinctPosCountersAsync(storeId);

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
}
