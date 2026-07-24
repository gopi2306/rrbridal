using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class OnlineSalesViewModel : ObservableObject
{
    private readonly AppServices _services;

    [ObservableProperty] private string _searchBillNo = "";
    [ObservableProperty] private string _searchCustomerName = "";
    [ObservableProperty] private string _selectedStatusFilter = "all";
    [ObservableProperty] private DateTime? _searchDateFrom;
    [ObservableProperty] private DateTime? _searchDateTo;
    [ObservableProperty] private OnlineCodSearchRow? _selectedOrder;
    [ObservableProperty] private string _statusMessage = "Search online COD orders.";
    [ObservableProperty] private string _balanceTillSummary = "—";
    [ObservableProperty] private string _pendingCountSummary = "—";
    [ObservableProperty] private string _receivedTodaySummary = "—";

    public ObservableCollection<string> StatusFilterOptions { get; } = new() { "all", "pending", "received" };
    public ObservableCollection<OnlineCodSearchRow> Results { get; } = new();

    public OnlineSalesViewModel(AppServices services)
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
            var rows = await _services.OnlineCodBills.SearchAsync(
                _services.StoreContext.StoreId,
                string.IsNullOrWhiteSpace(SearchBillNo) ? null : SearchBillNo.Trim(),
                string.IsNullOrWhiteSpace(SearchCustomerName) ? null : SearchCustomerName.Trim(),
                SelectedStatusFilter,
                SearchDateFrom,
                SearchDateTo);

            Results.Clear();
            foreach (var r in rows)
                Results.Add(r);

            StatusMessage = rows.Count == 0 ? "No online COD orders found." : $"{rows.Count} order(s) found.";
            SelectedOrder = Results.Count > 0 ? Results[0] : null;
        }
        catch (Exception ex)
        {
            StatusMessage = "Search failed: " + ex.Message;
        }
    }

    partial void OnSelectedOrderChanged(OnlineCodSearchRow? value) =>
        RecordPaymentCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanRecordPayment))]
    private async Task RecordPayment()
    {
        if (SelectedOrder == null)
            return;

        var dlg = new RecordCodPaymentDialog(SelectedOrder.Amount)
        {
            Owner = Application.Current.MainWindow,
        };
        if (dlg.ShowDialog() != true || !dlg.Confirmed)
            return;

        var user = _services.UserSession?.LoggedInUser.Name ?? "Unknown";
        var ok = await _services.OnlineCodBills.RecordPaymentReceivedAsync(
            _services.StoreContext.StoreId,
            SelectedOrder.BillNo,
            dlg.SelectedPaymentMode,
            dlg.TransactionNo,
            user);

        if (!ok)
        {
            AppDialog.Show("Could not record payment. The order may already be received.", "Online sales",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StatusMessage = $"Payment recorded for {SelectedOrder.BillNo}.";
        await Refresh();
    }

    private bool CanRecordPayment() =>
        SelectedOrder != null
        && string.Equals(SelectedOrder.Status, OnlineCodDocumentReader.StatusPending, StringComparison.OrdinalIgnoreCase);

    private async Task LoadBalanceAsync()
    {
        try
        {
            var bal = await _services.OnlineCodBills.GetPendingBalanceAsync(_services.StoreContext.StoreId);
            BalanceTillSummary = MoneyMath.FormatRupee(bal.BalanceTill);
            PendingCountSummary = bal.PendingCount.ToString();
            ReceivedTodaySummary = FormatReceivedTodaySummary(bal);
        }
        catch
        {
            BalanceTillSummary = "—";
        }
    }

    private static string FormatReceivedTodaySummary(OnlineCodPendingBalance bal)
    {
        if (bal.ReceivedTodayCount <= 0)
            return "0 · ₹ 0.00";

        var parts = new List<string>
        {
            $"{bal.ReceivedTodayCount} · {MoneyMath.FormatRupee(bal.ReceivedTodayAmount)}",
        };

        if (bal.ReceivedTodayCash > 0)
            parts.Add($"Cash {MoneyMath.FormatRupee(bal.ReceivedTodayCash)}");
        if (bal.ReceivedTodayUpi > 0)
            parts.Add($"UPI {MoneyMath.FormatRupee(bal.ReceivedTodayUpi)}");
        if (bal.ReceivedTodayCard > 0)
            parts.Add($"Card {MoneyMath.FormatRupee(bal.ReceivedTodayCard)}");

        return string.Join(" · ", parts);
    }
}
