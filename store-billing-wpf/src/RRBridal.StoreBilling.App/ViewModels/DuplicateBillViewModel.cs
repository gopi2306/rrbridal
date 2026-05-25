using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Invoicing;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class DuplicateBillViewModel : ObservableObject
{
    private readonly AppServices _services;

    [ObservableProperty] private string _searchInvoiceNo = "";
    [ObservableProperty] private string _searchCustomerName = "";
    [ObservableProperty] private DateTime? _searchDateFrom;
    [ObservableProperty] private DateTime? _searchDateTo;
    [ObservableProperty] private BillSearchRow? _selectedBill;
    [ObservableProperty] private string _statusMessage = "Search posted bills to reprint a duplicate copy.";
    [ObservableProperty] private string _detailText = "";

    public ObservableCollection<BillSearchRow> Results { get; } = new();

    public DuplicateBillViewModel(AppServices services)
    {
        _services = services;
    }

    [RelayCommand]
    private async Task Search()
    {
        StatusMessage = "Searching…";
        try
        {
            var rows = await _services.BillDocuments.SearchBillsAsync(
                string.IsNullOrWhiteSpace(SearchInvoiceNo) ? null : SearchInvoiceNo.Trim(),
                SearchDateFrom,
                SearchDateTo,
                string.IsNullOrWhiteSpace(SearchCustomerName) ? null : SearchCustomerName.Trim(),
                status: "posted",
                limit: 100);

            Results.Clear();
            foreach (var r in rows)
                Results.Add(r);

            StatusMessage = rows.Count == 0 ? "No posted bills found." : $"{rows.Count} bill(s) found.";
            SelectedBill = Results.Count > 0 ? Results[0] : null;
            UpdateDetail();
        }
        catch (Exception ex)
        {
            StatusMessage = "Search failed: " + ex.Message;
        }
    }

    partial void OnSelectedBillChanged(BillSearchRow? value)
    {
        UpdateDetail();
        PrintDuplicateCommand.NotifyCanExecuteChanged();
    }

    private void UpdateDetail()
    {
        if (SelectedBill == null)
        {
            DetailText = "";
            return;
        }

        DetailText =
            $"Bill: {SelectedBill.BillNo}\n" +
            $"Date: {SelectedBill.BillDate}\n" +
            $"Customer: {SelectedBill.CustomerName}\n" +
            $"Phone: {SelectedBill.CustomerPhone}\n" +
            $"Payable: ₹ {SelectedBill.Payable:N2}\n" +
            $"Counter: {SelectedBill.CounterDisplay}\n" +
            $"Posted: {SelectedBill.PostedAtUtc}";
    }

    [RelayCommand(CanExecute = nameof(CanPrintDuplicate))]
    private async Task PrintDuplicate()
    {
        if (SelectedBill == null)
            return;

        if (!_services.PosBillingSettings.Current.AllowDuplicatePrint)
        {
            MessageBox.Show("Duplicate bill printing is disabled in billing settings.", "Duplicate print",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var doc = await _services.BillDocuments.GetByBillNoAsync(SelectedBill.BillNo);
        if (doc == null)
        {
            MessageBox.Show("Bill not found.", "Duplicate print", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var status = doc.GetValue("status", "posted").AsString;
        if (status != "posted")
        {
            MessageBox.Show($"Only posted bills can be reprinted (status: {status}).", "Duplicate print",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var user = _services.UserSession?.LoggedInUser.Name ?? "Unknown";
        var input = _services.BillDocuments.MapToThermalInput(doc, isDuplicate: true, printedBy: user);
        var printed = await InvoicePrintFlow.ShowAsync(_services, input, printInvoiceEnabled: true);
        if (printed)
        {
            await _services.BillDocuments.AppendPrintAuditAsync(SelectedBill.BillNo, "duplicate", user);
            StatusMessage = $"Duplicate printed for {SelectedBill.BillNo}.";
        }
    }

    private bool CanPrintDuplicate() => SelectedBill != null;
}
