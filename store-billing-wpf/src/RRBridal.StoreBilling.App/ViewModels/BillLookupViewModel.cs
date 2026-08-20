using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Store;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class BillLookupViewModel : ObservableObject
{
    private readonly AppServices _services;

    [ObservableProperty] private string _billNoInput = "";
    [ObservableProperty] private string _searchCustomerName = "";
    [ObservableProperty] private string _searchCustomerPhone = "";
    [ObservableProperty] private string _statusMessage = "Search by bill no, customer name, or mobile.";
    [ObservableProperty] private BillLookupMode _activeMode = BillLookupMode.View;
    [ObservableProperty] private bool _hasRemainingReturnableQty;
    [ObservableProperty] private BillSearchRow? _selectedSearchBill;
    [ObservableProperty] private string _dispatchSummary = "No active dispatch";

    public ObservableCollection<BillSearchRow> SearchResults { get; } = new();

    public bool ShowSearchResults => SearchResults.Count > 0 && !Detail.IsLoaded;

    public BillDetailDialogViewModel Detail { get; }
    public BillDetailDialogViewModel OriginalDetail { get; }
    public SaleReturnViewModel Return { get; }
    public AdjustmentBillViewModel Adjustment { get; }
    public Action<string>? OpenDispatchForBill { get; set; }

    public bool IsViewMode => ActiveMode == BillLookupMode.View;
    public bool IsReturnMode => ActiveMode == BillLookupMode.Return;
    public bool IsAdjustmentMode => ActiveMode == BillLookupMode.Adjustment;
    public bool ShowOriginalBillPanel => IsReturnMode || IsAdjustmentMode;
    public bool CanPostReturn =>
        IsReturnMode && Return.BillLoaded && (
            _services.PosBillingSettings.Current.AllowMultipleReturnsPerBill
                ? Return.HasReturnableLines
                : !Detail.HasReturn);
    public bool ShowReturnAlreadyPostedMessage => IsReturnMode && Detail.HasReturn && !CanPostReturn;
    public bool CanPostAdjustment => IsAdjustmentMode && Adjustment.BillLoaded && !Detail.HasAdjustment;

    public bool ShowDeleteBill => _services.StoreContext.IsPrimaryCounter;
    public bool CanDeleteBill =>
        ShowDeleteBill && Detail.IsLoaded && !Detail.HasReturn && !Detail.HasAdjustment;

    public BillLookupViewModel(AppServices services)
    {
        _services = services;
        Detail = new BillDetailDialogViewModel(services, services.StoreBillList);
        OriginalDetail = new BillDetailDialogViewModel(services, services.StoreBillList);
        Return = new SaleReturnViewModel(services);
        Adjustment = new AdjustmentBillViewModel(services);

        Detail.NavigateToReturnForBill = billNo => _ = EnterReturnViewAsync(billNo, viewOnly: true);
        Detail.NavigateToAdjustmentForBill = billNo => _ = EnterAdjustmentViewAsync(billNo, viewOnly: true);

        Return.OnPostedSuccessfully = OnReturnOrAdjustmentPostedAsync;
        Adjustment.OnPostedSuccessfully = OnReturnOrAdjustmentPostedAsync;

        Return.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SaleReturnViewModel.BillLoaded) or nameof(SaleReturnViewModel.ShowPriorReturnQty))
                NotifyReturnPostState();
        };
        Adjustment.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AdjustmentBillViewModel.BillLoaded))
                OnPropertyChanged(nameof(CanPostAdjustment));
        };
    }

    partial void OnActiveModeChanged(BillLookupMode value)
    {
        OnPropertyChanged(nameof(IsViewMode));
        OnPropertyChanged(nameof(IsReturnMode));
        OnPropertyChanged(nameof(IsAdjustmentMode));
        OnPropertyChanged(nameof(ShowOriginalBillPanel));
        NotifyReturnPostState();
        OnPropertyChanged(nameof(CanPostAdjustment));
    }

    partial void OnHasRemainingReturnableQtyChanged(bool value) =>
        StartReturnCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void ShowViewMode() => ActiveMode = BillLookupMode.View;

    [RelayCommand]
    private void BackToView() => ActiveMode = BillLookupMode.View;

    public async Task ResetForNewAsync()
    {
        BillNoInput = "";
        SearchCustomerName = "";
        SearchCustomerPhone = "";
        StatusMessage = "Search by bill no, customer name, or mobile.";
        ActiveMode = BillLookupMode.View;
        SelectedSearchBill = null;
        DispatchSummary = "No active dispatch";
        Detail.Clear();
        OriginalDetail.Clear();
        SearchResults.Clear();
        OnPropertyChanged(nameof(ShowSearchResults));
        await Return.ClearFormCommand.ExecuteAsync(null);
        await Adjustment.ClearFormCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task SearchBills()
    {
        if (!HasSearchCriteria())
        {
            AppDialog.Show("Enter at least one search field (bill no, customer name, or mobile).", "Bill Lookup",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        StatusMessage = "Searching…";
        ActiveMode = BillLookupMode.View;
        DispatchSummary = "No active dispatch";
        Detail.Clear();
        OriginalDetail.Clear();
        SearchResults.Clear();
        await Return.ClearFormCommand.ExecuteAsync(null);
        await Adjustment.ClearFormCommand.ExecuteAsync(null);

        try
        {
            var rows = await _services.BillDocuments.SearchBillsAsync(
                string.IsNullOrWhiteSpace(BillNoInput) ? null : BillNoInput.Trim(),
                dateFrom: null,
                dateTo: null,
                customerName: string.IsNullOrWhiteSpace(SearchCustomerName) ? null : SearchCustomerName.Trim(),
                customerPhone: string.IsNullOrWhiteSpace(SearchCustomerPhone) ? null : SearchCustomerPhone.Trim(),
                status: null,
                limit: 100);

            foreach (var row in rows)
                SearchResults.Add(row);

            SelectedSearchBill = SearchResults.Count > 0 ? SearchResults[0] : null;
            StatusMessage = rows.Count == 0
                ? "No bills found."
                : $"{rows.Count} bill(s) found — select one and press Open bill.";
            NotifySearchResultsChanged();
            NotifyActionCommands();
        }
        catch (Exception ex)
        {
            StatusMessage = "Search failed: " + ex.Message;
            NotifySearchResultsChanged();
        }
    }

    private bool HasSearchCriteria() =>
        !string.IsNullOrWhiteSpace(BillNoInput)
        || !string.IsNullOrWhiteSpace(SearchCustomerName)
        || !string.IsNullOrWhiteSpace(SearchCustomerPhone);

    partial void OnSelectedSearchBillChanged(BillSearchRow? value) =>
        OpenSelectedBillCommand.NotifyCanExecuteChanged();

    private bool CanOpenSelectedBill() => SelectedSearchBill != null && !Detail.IsLoaded;

    [RelayCommand(CanExecute = nameof(CanOpenSelectedBill))]
    private async Task OpenSelectedBill()
    {
        if (SelectedSearchBill == null)
            return;

        var billNo = SelectedSearchBill.BillNo;
        BillNoInput = billNo;
        SearchResults.Clear();
        SelectedSearchBill = null;
        NotifySearchResultsChanged();
        await LoadBillDetailsAsync(billNo);
        StatusMessage = Detail.IsNotFound
            ? $"Bill '{billNo}' not found."
            : $"Loaded bill {billNo}.";
        NotifyActionCommands();
    }

    private void NotifySearchResultsChanged()
    {
        OnPropertyChanged(nameof(ShowSearchResults));
        OpenSelectedBillCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task LookupBill() => await SearchBills();

    private async Task LoadBillDetailsAsync(string billNo)
    {
        await Detail.LoadAsync(billNo);
        await OriginalDetail.LoadAsync(billNo);
        await RefreshReturnEligibilityAsync(billNo);
        await RefreshDispatchSummaryAsync(billNo);
        NotifySearchResultsChanged();
    }

    private async Task RefreshDispatchSummaryAsync(string billNo)
    {
        if (!Detail.IsLoaded)
        {
            DispatchSummary = "No active dispatch";
            return;
        }

        var dispatch = await _services.OutboundDispatches.GetActiveByBillNoAsync(billNo);
        if (dispatch == null)
        {
            DispatchSummary = "No active dispatch";
            return;
        }

        var dispatchNo = dispatch.GetValue("dispatchNo", "").ToString();
        var status = dispatch.GetValue("status", "").ToString();
        DispatchSummary = string.IsNullOrWhiteSpace(dispatchNo)
            ? $"Active dispatch: {status}"
            : $"Dispatch {dispatchNo} · {status}";
    }

    private async Task RefreshReturnEligibilityAsync(string billNo)
    {
        if (!Detail.IsLoaded)
        {
            HasRemainingReturnableQty = false;
            return;
        }

        if (!_services.PosBillingSettings.Current.AllowMultipleReturnsPerBill)
        {
            HasRemainingReturnableQty = !Detail.HasReturn;
            return;
        }

        var billDoc = await _services.BillDocuments.GetByBillNoAsync(billNo);
        if (billDoc == null)
        {
            HasRemainingReturnableQty = false;
            return;
        }

        HasRemainingReturnableQty = await _services.SaleReturnHistory.HasRemainingReturnableQtyAsync(
            _services.StoreContext.StoreId, billDoc);
    }

    private async Task OnReturnOrAdjustmentPostedAsync(string billNo)
    {
        ActiveMode = BillLookupMode.View;
        BillNoInput = billNo;
        await LoadBillDetailsAsync(billNo);
        StatusMessage = $"Updated bill {billNo} after posting.";
        await Return.ClearFormCommand.ExecuteAsync(null);
        await Adjustment.ClearFormCommand.ExecuteAsync(null);
        NotifyActionCommands();
    }

    private async Task EnterReturnViewAsync(string billNo, bool viewOnly = false)
    {
        ActiveMode = BillLookupMode.Return;
        NotifyReturnPostState();
        await Return.LoadBillByNoAsync(billNo, skipDuplicateChecks: viewOnly);
        NotifyReturnPostState();
    }

    private async Task EnterAdjustmentViewAsync(string billNo, bool viewOnly = false)
    {
        ActiveMode = BillLookupMode.Adjustment;
        OnPropertyChanged(nameof(CanPostAdjustment));
        if (!await Adjustment.LoadBillByNoAsync(billNo) && viewOnly)
            ActiveMode = BillLookupMode.View;
    }

    private async Task<string?> ResolveBillNoAsync(string input)
    {
        var digits = new string(input.Where(char.IsDigit).ToArray());

        if (digits.Length is >= 3 and <= 4)
        {
            return _services.CentralMode.IsOnlineMode
                ? await ResolveBillNoBySuffixOnlineAsync(digits)
                : await ResolveBillNoBySuffixLocalAsync(digits);
        }

        var doc = await _services.BillDocuments.GetByBillNoAsync(input);
        if (doc == null && digits.Length > 0)
        {
            var normalized = input.Replace(" ", "-", StringComparison.Ordinal);
            if (!string.Equals(normalized, input, StringComparison.Ordinal))
                doc = await _services.BillDocuments.GetByBillNoAsync(normalized);
        }

        return doc?.GetValue("billNo", "").AsString;
    }

    private async Task<string?> ResolveBillNoBySuffixLocalAsync(string digits)
    {
        var coll = _services.LocalDb.GetCollection<BsonDocument>("store_bills");
        var storeId = _services.StoreContext.StoreId;

        var regex = new BsonRegularExpression($"{Regex.Escape(digits)}$", "i");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", storeId),
            Builders<BsonDocument>.Filter.Regex("billNo", regex));
        var sort = Builders<BsonDocument>.Sort.Descending("createdAtUtc");
        var matches = await coll.Find(filter).Sort(sort).Limit(20).ToListAsync();

        if (matches.Count == 0)
            return null;

        if (matches.Count == 1)
            return matches[0].GetValue("billNo", "").AsString;

        var dlg = new BillPickDialog(matches) { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() != true || string.IsNullOrEmpty(dlg.SelectedBillNo))
            return null;

        return dlg.SelectedBillNo;
    }

    private async Task<string?> ResolveBillNoBySuffixOnlineAsync(string digits)
    {
        // Online mode: never hit the local store_bills collection — search central via
        // BillDocuments.SearchBillsAsync (which itself talks to CentralStorePosClient).
        var rows = await _services.BillDocuments.SearchBillsAsync(
            invoiceNo: digits,
            dateFrom: null,
            dateTo: null,
            customerName: null,
            customerPhone: null,
            status: null,
            limit: 100);

        var matches = rows
            .Where(r => r.BillNo.EndsWith(digits, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.SortUtc)
            .Take(20)
            .ToList();

        if (matches.Count == 0)
            return null;

        if (matches.Count == 1)
            return matches[0].BillNo;

        var pickDocs = matches.Select(r => new BsonDocument
        {
            { "billNo", r.BillNo },
            { "billDate", r.BillDate },
            { "customerName", r.CustomerName },
            { "payable", r.Payable },
        });

        var dlg = new BillPickDialog(pickDocs) { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() != true || string.IsNullOrEmpty(dlg.SelectedBillNo))
            return null;

        return dlg.SelectedBillNo;
    }

    private bool CanStartReturn() => Detail.IsLoaded && HasRemainingReturnableQty;

    [RelayCommand(CanExecute = nameof(CanStartReturn))]
    private async Task StartReturn()
    {
        if (!CanStartReturn())
            return;
        await EnterReturnViewAsync(Detail.LoadedBillNo);
    }

    private bool CanStartAdjustment() => Detail.IsLoaded && !Detail.HasAdjustment;

    [RelayCommand(CanExecute = nameof(CanStartAdjustment))]
    private async Task StartAdjustment()
    {
        if (!CanStartAdjustment())
            return;
        await EnterAdjustmentViewAsync(Detail.LoadedBillNo);
    }

    private bool CanOpenDispatch() => Detail.IsLoaded;

    [RelayCommand(CanExecute = nameof(CanOpenDispatch))]
    private async Task OpenDispatch()
    {
        if (!Detail.IsLoaded || OpenDispatchForBill == null)
            return;
        var bill = await _services.BillDocuments.GetByBillNoAsync(Detail.LoadedBillNo);
        if (bill == null)
        {
            AppDialog.Show("Bill not found.", "Outbound Dispatch", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var status = bill.GetValue("status", "posted").ToString();
        if (!string.Equals(status, "posted", StringComparison.OrdinalIgnoreCase))
        {
            AppDialog.Show("Only posted bills can be dispatched.", "Outbound Dispatch",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        OpenDispatchForBill(Detail.LoadedBillNo);
    }

    [RelayCommand(CanExecute = nameof(CanDeleteBill))]
    private async Task DeleteBill()
    {
        if (!CanDeleteBill)
            return;

        var billNo = Detail.LoadedBillNo;
        var confirm = AppDialog.Show(
            $"Permanently delete bill {billNo}?\n\nStock will be restored and the deletion will sync to central. This cannot be undone.",
            "Delete bill",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        var result = await _services.BillDelete.DeleteAsync(billNo);
        if (!result.Success)
        {
            AppDialog.Show(result.Message, "Delete bill", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusMessage = result.Message;
            return;
        }

        Detail.Clear();
        OriginalDetail.Clear();
        DispatchSummary = "No active dispatch";
        SearchResults.Clear();
        SelectedSearchBill = null;
        await Return.ClearFormCommand.ExecuteAsync(null);
        await Adjustment.ClearFormCommand.ExecuteAsync(null);
        NotifySearchResultsChanged();
        NotifyActionCommands();
        StatusMessage = result.Message;
        AppDialog.Show(result.Message, "Delete bill", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private bool CanPrintDuplicate() => Detail.IsLoaded;

    [RelayCommand(CanExecute = nameof(CanPrintDuplicate))]
    private async Task PrintDuplicate()
    {
        if (!Detail.IsLoaded)
            return;

        if (!_services.PosBillingSettings.Current.AllowDuplicatePrint)
        {
            AppDialog.Show("Duplicate bill printing is disabled in billing settings.", "Duplicate print",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var billNo = Detail.LoadedBillNo;
        var doc = await _services.BillDocuments.GetByBillNoAsync(billNo);
        if (doc == null)
        {
            AppDialog.Show("Bill not found.", "Duplicate print", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var status = doc.GetValue("status", "posted").AsString;
        if (status != "posted")
        {
            AppDialog.Show($"Only posted bills can be reprinted (status: {status}).", "Duplicate print",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var user = _services.UserSession?.LoggedInUser.Name ?? "Unknown";
        var input = _services.BillDocuments.MapToThermalInput(doc, isDuplicate: true, printedBy: user);
        var printed = await InvoicePrintFlow.ShowAsync(_services, input, printInvoiceEnabled: true);
        if (printed)
        {
            await _services.BillDocuments.AppendPrintAuditAsync(billNo, "duplicate", user);
            StatusMessage = $"Duplicate printed for {billNo}.";
        }
    }

    private bool CanPrintCreditNote() => Detail.IsLoaded && Detail.HasCreditNoteReturn;

    [RelayCommand(CanExecute = nameof(CanPrintCreditNote))]
    private async Task PrintCreditNoteDuplicate()
    {
        if (!Detail.HasCreditNoteReturn)
            return;

        if (!_services.PosBillingSettings.Current.AllowDuplicatePrint)
        {
            AppDialog.Show("Duplicate printing is disabled in billing settings.", "Credit note print",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var storeId = _services.StoreContext.StoreId;
        var billNo = Detail.LoadedBillNo;
        var returnDoc = await _services.StoreBillList.GetReturnByBillNoAsync(storeId, billNo);
        if (returnDoc == null)
        {
            AppDialog.Show("Return record not found.", "Credit note print", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SaleReturnDocumentMapper.HasExchangeLines(returnDoc))
        {
            AppDialog.Show(
                "Exchange credit-note duplicate reprint is not supported from bill lookup.",
                "Credit note print",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var creditNoteNo = Detail.ReturnCreditNoteNo;
        if (string.IsNullOrWhiteSpace(creditNoteNo))
        {
            var cn = await _services.CustomerCreditNotes.FindByOriginalBillAsync(storeId, billNo);
            creditNoteNo = cn?.CreditNoteNo ?? "";
        }

        var printed = await SaleReturnPrintFlow.ShowFromReturnDocumentAsync(
            _services,
            returnDoc,
            creditNoteNo,
            isDuplicate: true);
        if (printed)
            StatusMessage = $"Credit note duplicate printed for {billNo}.";
    }

    private void NotifyActionCommands()
    {
        StartReturnCommand.NotifyCanExecuteChanged();
        StartAdjustmentCommand.NotifyCanExecuteChanged();
        DeleteBillCommand.NotifyCanExecuteChanged();
        PrintDuplicateCommand.NotifyCanExecuteChanged();
        PrintCreditNoteDuplicateCommand.NotifyCanExecuteChanged();
        OpenDispatchCommand.NotifyCanExecuteChanged();
        NotifyReturnPostState();
        OnPropertyChanged(nameof(CanPostAdjustment));
        OnPropertyChanged(nameof(CanDeleteBill));
    }

    private void NotifyReturnPostState()
    {
        OnPropertyChanged(nameof(CanPostReturn));
        OnPropertyChanged(nameof(ShowReturnAlreadyPostedMessage));
    }
}
