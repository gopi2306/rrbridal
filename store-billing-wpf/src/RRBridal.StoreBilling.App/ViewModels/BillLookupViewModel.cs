using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Store;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class BillLookupViewModel : ObservableObject
{
    private readonly AppServices _services;

    [ObservableProperty] private string _billNoInput = "";
    [ObservableProperty] private string _statusMessage = "Enter a bill number and press Look up.";
    [ObservableProperty] private BillLookupMode _activeMode = BillLookupMode.View;
    [ObservableProperty] private bool _hasRemainingReturnableQty;

    public BillDetailDialogViewModel Detail { get; }
    public BillDetailDialogViewModel OriginalDetail { get; }
    public SaleReturnViewModel Return { get; }
    public AdjustmentBillViewModel Adjustment { get; }

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

    [RelayCommand]
    private async Task LookupBill()
    {
        var input = (BillNoInput ?? "").Trim();
        if (string.IsNullOrEmpty(input))
        {
            MessageBox.Show("Enter a bill number to look up.", "Bill Lookup", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        StatusMessage = "Looking up bill…";
        var resolved = await ResolveBillNoAsync(input);
        if (resolved == null)
        {
            Detail.Lines.Clear();
            OriginalDetail.Lines.Clear();
            StatusMessage = $"Bill '{input}' not found.";
            return;
        }

        BillNoInput = resolved;
        ActiveMode = BillLookupMode.View;
        await Return.ClearFormCommand.ExecuteAsync(null);
        await Adjustment.ClearFormCommand.ExecuteAsync(null);
        await LoadBillDetailsAsync(resolved);
        StatusMessage = Detail.IsNotFound
            ? $"Bill '{resolved}' not found."
            : $"Loaded bill {resolved}.";
        NotifyActionCommands();
    }

    private async Task LoadBillDetailsAsync(string billNo)
    {
        await Detail.LoadAsync(billNo);
        await OriginalDetail.LoadAsync(billNo);
        await RefreshReturnEligibilityAsync(billNo);
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
        var coll = _services.LocalDb.GetCollection<BsonDocument>("store_bills");
        var storeId = _services.StoreContext.StoreId;
        var digits = new string(input.Where(char.IsDigit).ToArray());

        BsonDocument? doc = null;

        if (digits.Length is >= 3 and <= 4)
        {
            var regex = new BsonRegularExpression($"{Regex.Escape(digits)}$", "i");
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", storeId),
                Builders<BsonDocument>.Filter.Regex("billNo", regex));
            var sort = Builders<BsonDocument>.Sort.Descending("createdAtUtc");
            var matches = await coll.Find(filter).Sort(sort).Limit(20).ToListAsync();

            if (matches.Count == 0)
                return null;

            if (matches.Count == 1)
            {
                doc = matches[0];
            }
            else
            {
                var dlg = new BillPickDialog(matches) { Owner = Application.Current.MainWindow };
                if (dlg.ShowDialog() != true || string.IsNullOrEmpty(dlg.SelectedBillNo))
                    return null;

                doc = matches.FirstOrDefault(m => m.GetValue("billNo", "").AsString == dlg.SelectedBillNo)
                    ?? await coll.Find(Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq("storeId", storeId),
                        Builders<BsonDocument>.Filter.Eq("billNo", dlg.SelectedBillNo))).FirstOrDefaultAsync();
            }
        }
        else
        {
            doc = await _services.BillDocuments.GetByBillNoAsync(input);
            if (doc == null && digits.Length > 0)
            {
                var normalized = input.Replace(" ", "-", StringComparison.Ordinal);
                if (!string.Equals(normalized, input, StringComparison.Ordinal))
                    doc = await _services.BillDocuments.GetByBillNoAsync(normalized);
            }
        }

        return doc?.GetValue("billNo", "").AsString;
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

    private bool CanPrintDuplicate() => Detail.IsLoaded;

    [RelayCommand(CanExecute = nameof(CanPrintDuplicate))]
    private async Task PrintDuplicate()
    {
        if (!Detail.IsLoaded)
            return;

        if (!_services.PosBillingSettings.Current.AllowDuplicatePrint)
        {
            MessageBox.Show("Duplicate bill printing is disabled in billing settings.", "Duplicate print",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var billNo = Detail.LoadedBillNo;
        var doc = await _services.BillDocuments.GetByBillNoAsync(billNo);
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
            MessageBox.Show("Duplicate printing is disabled in billing settings.", "Credit note print",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var storeId = _services.StoreContext.StoreId;
        var billNo = Detail.LoadedBillNo;
        var returnDoc = await _services.StoreBillList.GetReturnByBillNoAsync(storeId, billNo);
        if (returnDoc == null)
        {
            MessageBox.Show("Return record not found.", "Credit note print", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SaleReturnDocumentMapper.HasExchangeLines(returnDoc))
        {
            MessageBox.Show(
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
        PrintDuplicateCommand.NotifyCanExecuteChanged();
        PrintCreditNoteDuplicateCommand.NotifyCanExecuteChanged();
        NotifyReturnPostState();
        OnPropertyChanged(nameof(CanPostAdjustment));
    }

    private void NotifyReturnPostState()
    {
        OnPropertyChanged(nameof(CanPostReturn));
        OnPropertyChanged(nameof(ShowReturnAlreadyPostedMessage));
    }
}
