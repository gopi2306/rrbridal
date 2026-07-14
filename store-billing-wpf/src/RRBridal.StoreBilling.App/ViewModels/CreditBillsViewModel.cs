using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
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

        var dlg = new RecordCreditPaymentDialog(SelectedBill.BalanceDue, allowPartial)
        {
            Owner = Application.Current.MainWindow,
        };
        if (dlg.ShowDialog() != true || !dlg.Confirmed)
            return;

        var user = _services.UserSession?.LoggedInUser.Name ?? "Unknown";
        var result = await _services.CreditBills.RecordPaymentAsync(
            _services.StoreContext.StoreId,
            SelectedBill.BillNo,
            dlg.PaidAmount,
            dlg.SelectedPaymentMode,
            dlg.TransactionNo,
            user,
            allowPartial);

        if (!result.Success)
        {
            MessageBox.Show(result.Error ?? "Could not record payment.", "Credit bills",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StatusMessage = $"Payment recorded for {SelectedBill.BillNo}. Receipt {result.ReceiptNo}.";
        try
        {
            await PaymentReceiptPrintFlow.ShowAsync(
                _services,
                new PaymentReceiptPrintInput
                {
                    ReceiptNo = result.ReceiptNo ?? "",
                    BillNo = SelectedBill.BillNo,
                    CustomerName = SelectedBill.CustomerName,
                    CustomerPhone = SelectedBill.CustomerPhone,
                    AmountPaid = result.AmountPaid,
                    BalanceDue = result.BalanceDue,
                    PaymentMode = dlg.SelectedPaymentMode.ToString(),
                    Reference = dlg.TransactionNo,
                });
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
