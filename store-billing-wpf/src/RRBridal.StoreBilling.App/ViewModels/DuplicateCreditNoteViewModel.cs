using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Store;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class DuplicateCreditNoteViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly StoreBillListService _billListService;

    [ObservableProperty] private string _searchOriginalBillNo = "";
    [ObservableProperty] private string _searchReturnNo = "";
    [ObservableProperty] private string _searchCustomerName = "";
    [ObservableProperty] private string _searchMobileNo = "";
    [ObservableProperty] private DateTime? _searchDateFrom;
    [ObservableProperty] private DateTime? _searchDateTo;
    [ObservableProperty] private CreditNoteSearchRow? _selectedCreditNote;
    [ObservableProperty] private string _statusMessage = "Search credit notes to reprint a duplicate copy.";
    [ObservableProperty] private string _detailText = "";

    public ObservableCollection<CreditNoteSearchRow> Results { get; } = new();

    public DuplicateCreditNoteViewModel(AppServices services)
    {
        _services = services;
        _billListService = new StoreBillListService(services.LocalDb);
    }

    [RelayCommand]
    private async Task Search()
    {
        StatusMessage = "Searching…";
        try
        {
            var rows = await _services.CustomerCreditNotes.SearchCreditNotesAsync(
                _services.StoreContext.StoreId,
                string.IsNullOrWhiteSpace(SearchOriginalBillNo) ? null : SearchOriginalBillNo.Trim(),
                string.IsNullOrWhiteSpace(SearchReturnNo) ? null : SearchReturnNo.Trim(),
                string.IsNullOrWhiteSpace(SearchCustomerName) ? null : SearchCustomerName.Trim(),
                string.IsNullOrWhiteSpace(SearchMobileNo) ? null : SearchMobileNo.Trim(),
                SearchDateFrom,
                SearchDateTo,
                limit: 100);

            Results.Clear();
            foreach (var r in rows)
                Results.Add(r);

            StatusMessage = rows.Count == 0 ? "No credit notes found." : $"{rows.Count} credit note(s) found.";
            SelectedCreditNote = Results.Count > 0 ? Results[0] : null;
            UpdateDetail();
        }
        catch (Exception ex)
        {
            StatusMessage = "Search failed: " + ex.Message;
        }
    }

    partial void OnSelectedCreditNoteChanged(CreditNoteSearchRow? value)
    {
        UpdateDetail();
        PrintDuplicateCommand.NotifyCanExecuteChanged();
    }

    private void UpdateDetail()
    {
        if (SelectedCreditNote == null)
        {
            DetailText = "";
            return;
        }

        DetailText =
            $"Credit note: {SelectedCreditNote.CreditNoteNo}\n" +
            $"Return no: {SelectedCreditNote.ReturnNo}\n" +
            $"Original bill: {SelectedCreditNote.OriginalBillNo}\n" +
            $"Date: {SelectedCreditNote.CreatedAtDisplay}\n" +
            $"Customer: {SelectedCreditNote.CustomerName}\n" +
            $"Phone: {SelectedCreditNote.CustomerPhone}\n" +
            $"Amount: ₹ {SelectedCreditNote.Amount:N2}\n" +
            $"Remaining: ₹ {SelectedCreditNote.RemainingAmount:N2}\n" +
            $"Status: {SelectedCreditNote.Status}";
    }

    [RelayCommand(CanExecute = nameof(CanPrintDuplicate))]
    private async Task PrintDuplicate()
    {
        if (SelectedCreditNote == null)
            return;

        if (!_services.PosBillingSettings.Current.AllowDuplicatePrint)
        {
            AppDialog.Show("Duplicate printing is disabled in billing settings.", "Duplicate print",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var returnDoc = await _billListService.GetReturnByReturnNoAsync(
            _services.StoreContext.StoreId,
            SelectedCreditNote.ReturnNo);
        if (returnDoc == null)
        {
            AppDialog.Show("Linked return not found.", "Duplicate print", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SaleReturnDocumentMapper.HasExchangeLines(returnDoc))
        {
            AppDialog.Show(
                "Exchange credit-note duplicate reprint is not supported yet. Use the original exchange receipt from the Returns screen.",
                "Duplicate print",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var returnMode = returnDoc.GetValue("returnMode", "").AsString;
        var creditNoteOnReturn = returnDoc.GetValue("creditNoteNo", "").AsString;
        if (!string.Equals(returnMode, "credit_note", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(creditNoteOnReturn))
        {
            AppDialog.Show("This return was not issued as a credit note.", "Duplicate print",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var creditNoteNo = string.IsNullOrWhiteSpace(creditNoteOnReturn)
            ? SelectedCreditNote.CreditNoteNo
            : creditNoteOnReturn;

        var printed = await SaleReturnPrintFlow.ShowFromReturnDocumentAsync(
            _services,
            returnDoc,
            creditNoteNo,
            isDuplicate: true);
        if (printed)
            StatusMessage = $"Duplicate printed for {SelectedCreditNote.CreditNoteNo}.";
    }

    private bool CanPrintDuplicate() => SelectedCreditNote != null;
}
