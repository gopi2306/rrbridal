using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class QuotationManagementViewModel : ObservableObject
{
    private readonly AppServices _services;

    [ObservableProperty] private string _searchQuotationNo = "";
    [ObservableProperty] private string _searchCustomerName = "";
    [ObservableProperty] private string _searchCustomerPhone = "";
    [ObservableProperty] private QuotationSearchRow? _selectedQuotation;
    [ObservableProperty] private string _statusMessage = "Search quotations by quotation no, customer name, or mobile.";

    public ObservableCollection<QuotationSearchRow> Results { get; } = new();

    public Action<string>? OpenQuotation { get; set; }
    public Action<string>? ConvertQuotationToBilling { get; set; }
    public Action? CreateQuotation { get; set; }

    public QuotationManagementViewModel(AppServices services)
    {
        _services = services;
    }

    [RelayCommand]
    private void NewQuotation() => CreateQuotation?.Invoke();

    public void ClearFilters()
    {
        SearchQuotationNo = "";
        SearchCustomerName = "";
        SearchCustomerPhone = "";
        SelectedQuotation = null;
        StatusMessage = "Search quotations by quotation no, customer name, or mobile.";
    }

    [RelayCommand]
    private async Task Refresh() => await Search();

    [RelayCommand]
    private async Task ClearFiltersAndSearch()
    {
        ClearFilters();
        await Search();
    }

    [RelayCommand]
    private async Task Search()
    {
        StatusMessage = "Searching…";
        try
        {
            var rows = await _services.Quotations.SearchAsync(
                string.IsNullOrWhiteSpace(SearchQuotationNo) ? null : SearchQuotationNo.Trim(),
                string.IsNullOrWhiteSpace(SearchCustomerName) ? null : SearchCustomerName.Trim(),
                string.IsNullOrWhiteSpace(SearchCustomerPhone) ? null : SearchCustomerPhone.Trim());

            Results.Clear();
            foreach (var r in rows)
                Results.Add(r);

            StatusMessage = rows.Count == 0 ? "No quotations found." : $"{rows.Count} quotation(s) found.";
            SelectedQuotation = Results.Count > 0 ? Results[0] : null;
        }
        catch (Exception ex)
        {
            StatusMessage = "Search failed: " + ex.Message;
        }
    }

    partial void OnSelectedQuotationChanged(QuotationSearchRow? value)
    {
        OpenSelectedCommand.NotifyCanExecuteChanged();
        ConvertToBillingCommand.NotifyCanExecuteChanged();
        CancelSelectedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanOpenSelected))]
    private void OpenSelected()
    {
        if (SelectedQuotation == null)
            return;
        OpenQuotation?.Invoke(SelectedQuotation.QuotationNo);
    }

    private bool CanOpenSelected() => SelectedQuotation != null;

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private void ConvertToBilling()
    {
        if (SelectedQuotation == null)
            return;
        ConvertQuotationToBilling?.Invoke(SelectedQuotation.QuotationNo);
    }

    private bool CanConvert() =>
        SelectedQuotation != null
        && string.Equals(SelectedQuotation.Status, QuotationService.StatusOpen, StringComparison.OrdinalIgnoreCase);

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private async Task CancelSelected()
    {
        if (SelectedQuotation == null)
            return;

        var confirm = MessageBox.Show(
            $"Cancel quotation {SelectedQuotation.QuotationNo}?",
            "Quotations",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        var ok = await _services.Quotations.CancelAsync(SelectedQuotation.QuotationNo);
        if (!ok)
        {
            MessageBox.Show("Could not cancel (it may already be converted).", "Quotations",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await Search();
    }

    private bool CanCancel() =>
        SelectedQuotation != null
        && string.Equals(SelectedQuotation.Status, QuotationService.StatusOpen, StringComparison.OrdinalIgnoreCase);
}
