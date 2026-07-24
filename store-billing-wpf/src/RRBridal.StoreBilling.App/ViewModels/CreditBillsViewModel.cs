using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class CreditBillsViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly AppServices _services;

    [ObservableProperty] private string _searchBillNo = "";
    [ObservableProperty] private string _searchCustomerName = "";
    [ObservableProperty] private string _searchCustomerPhone = "";
    [ObservableProperty] private string _selectedStatusFilter = "all";
    [ObservableProperty] private CreditBillSearchRow? _selectedBill;
    [ObservableProperty] private string _statusMessage = "Search credit (pay-later) bills.";
    [ObservableProperty] private string _balanceTillSummary = "—";
    [ObservableProperty] private string _pendingCountSummary = "—";

    public ObservableCollection<string> StatusFilterOptions { get; } = new()
    {
        "all",
        CreditBillDocumentReader.StatusPending,
        CreditBillDocumentReader.StatusPartial,
        CreditBillDocumentReader.StatusSettled,
    };

    public ObservableCollection<CreditBillSearchRow> Results { get; } = new();

    public CreditBillsViewModel(AppServices services)
    {
        _services = services;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadBalanceAsync();
        await Search();
    }

    [RelayCommand]
    private async Task ClearFiltersAndSearch()
    {
        ClearFilters();
        await LoadBalanceAsync();
        await Search();
    }

    public void ClearFilters()
    {
        SearchBillNo = "";
        SearchCustomerName = "";
        SearchCustomerPhone = "";
        SelectedStatusFilter = "all";
        SelectedBill = null;
        StatusMessage = "Search credit (pay-later) bills.";
    }

    [RelayCommand]
    private async Task Search()
    {
        StatusMessage = "Searching…";
        try
        {
            var rows = await _services.CreditBills.SearchAsync(
                _services.StoreContext.StoreId,
                string.IsNullOrWhiteSpace(SearchBillNo) ? null : SearchBillNo.Trim(),
                string.IsNullOrWhiteSpace(SearchCustomerName) ? null : SearchCustomerName.Trim(),
                string.IsNullOrWhiteSpace(SearchCustomerPhone) ? null : SearchCustomerPhone.Trim(),
                SelectedStatusFilter);

            Results.Clear();
            foreach (var r in rows)
                Results.Add(r);

            StatusMessage = rows.Count == 0 ? "No credit bills found." : $"{rows.Count} bill(s) found.";
            SelectedBill = Results.Count > 0 ? Results[0] : null;
        }
        catch (Exception ex)
        {
            StatusMessage = "Search failed: " + ex.Message;
        }
    }

    partial void OnSelectedBillChanged(CreditBillSearchRow? value) =>
        RecordPaymentCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanRecordPayment))]
    private async Task RecordPayment()
    {
        if (SelectedBill == null)
            return;

        _services.PosBillingSettings.Load();
        var allowPartial = _services.PosBillingSettings.Current.CreditBillingAllowPartialCollection;

        var dlg = new RecordCreditPaymentDialog(
            SelectedBill.BalanceDue,
            allowPartial,
            _services.StoreContext.StoreId,
            SelectedBill.BillNo,
            SelectedBill.CustomerPhone,
            null,
            _services.CustomerCreditNotes)
        {
            Owner = Application.Current.MainWindow,
        };
        if (dlg.ShowDialog() != true || !dlg.Confirmed)
            return;

        if (dlg.SelectedPaymentMode == CreditReceivedPaymentMode.CreditNote)
        {
            var consumed = await _services.CustomerCreditNotes.ConsumeAsync(
                dlg.TransactionNo,
                SelectedBill.BillNo,
                dlg.PaidAmount);
            if (!consumed)
            {
                AppDialog.Show("Could not apply credit note. Check balance and try again.", "Credit bills",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        else if (dlg.SelectedPaymentMode == CreditReceivedPaymentMode.Split)
        {
            foreach (var leg in dlg.PaymentLegs)
            {
                if (leg.Mode != CreditReceivedPaymentMode.CreditNote || leg.Amount <= 0)
                    continue;

                var consumed = await _services.CustomerCreditNotes.ConsumeAsync(
                    leg.Reference,
                    SelectedBill.BillNo,
                    leg.Amount);
                if (!consumed)
                {
                    AppDialog.Show($"Could not apply credit note {leg.Reference}.", "Credit bills",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
        }

        var user = _services.UserSession?.LoggedInUser.Name ?? "Unknown";
        var result = await _services.CreditBills.RecordPaymentAsync(
            _services.StoreContext.StoreId,
            SelectedBill.BillNo,
            dlg.PaidAmount,
            dlg.SelectedPaymentMode,
            dlg.TransactionNo,
            user,
            allowPartial,
            dlg.PaymentLegs.Count > 0 ? dlg.PaymentLegs : null);

        if (!result.Success)
        {
            AppDialog.Show(result.Error ?? "Could not save receipt.", "Credit bills",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StatusMessage = $"Receipt saved for {SelectedBill.BillNo}. Receipt no. {result.ReceiptNo}.";
        try
        {
            var storeId = _services.StoreContext.StoreId;
            var bill = await _services.CreditBills.GetPostedBillAsync(storeId, SelectedBill.BillNo);
            var receipt = string.IsNullOrWhiteSpace(result.ReceiptNo)
                ? null
                : await _services.CreditBills.GetPaymentReceiptAsync(storeId, result.ReceiptNo!);
            var config = _services.ReceiptConfig.Current;
            var charWidth = config.Print.ReceiptCharWidth is >= 32 and <= 56
                ? config.Print.ReceiptCharWidth
                : 48;

            CreditReceiptPrintInput creditInput;
            if (bill != null && receipt != null)
            {
                creditInput = CreditReceiptMapper.FromCollection(bill, receipt, config.Store, charWidth);
            }
            else
            {
                creditInput = new CreditReceiptPrintInput
                {
                    Kind = CreditReceiptKind.BalanceCollection,
                    Store = config.Store,
                    CharWidth = charWidth,
                    BillNo = SelectedBill.BillNo,
                    BillDate = SelectedBill.BillDate,
                    ReceiptNo = result.ReceiptNo,
                    CustomerName = SelectedBill.CustomerName,
                    CustomerPhone = SelectedBill.CustomerPhone,
                    TotalPayable = SelectedBill.TotalPayable,
                    AdvanceAtPost = SelectedBill.AdvancePaid,
                    AmountPaidThisTime = result.AmountPaid,
                    CumulativeAmountPaid = SelectedBill.AmountPaid + result.AmountPaid,
                    BalanceDue = result.BalanceDue,
                    Status = result.BalanceDue <= 0.009m
                        ? CreditBillDocumentReader.StatusSettled
                        : CreditBillDocumentReader.StatusPartial,
                    PaymentMode = dlg.SelectedPaymentMode.ToString(),
                    Reference = dlg.TransactionNo,
                    ReceivedBy = user,
                };
            }

            await CreditReceiptPrintFlow.ShowAsync(_services, creditInput);
        }
        catch
        {
            // best-effort print
        }

        await Refresh();
    }

    private bool CanRecordPayment() =>
        SelectedBill != null
        && (string.Equals(SelectedBill.Status, CreditBillDocumentReader.StatusPending, StringComparison.OrdinalIgnoreCase)
            || string.Equals(SelectedBill.Status, CreditBillDocumentReader.StatusPartial, StringComparison.OrdinalIgnoreCase));

    private async Task LoadBalanceAsync()
    {
        try
        {
            var bal = await _services.CreditBills.GetPendingBalanceAsync(_services.StoreContext.StoreId);
            BalanceTillSummary = MoneyMath.FormatRupee(bal.BalanceTill);
            PendingCountSummary = bal.PendingCount.ToString(InCulture);
        }
        catch
        {
            BalanceTillSummary = "—";
            PendingCountSummary = "—";
        }
    }
}
