using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Customers;
using RRBridal.StoreBilling.App.Services.Products;
using RRBridal.StoreBilling.App.Services.Store;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.ViewModels;

public enum ReturnMode
{
    CreditNote,
    CashRefund,
}

public enum SaleReturnSourceMode
{
    SystemBill,
    PreSystemInvoice,
}

public partial class SaleReturnViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly AppServices _services;
    private readonly CustomerLookupService _customerLookup;
    private readonly CustomerRegistrationService _customerRegistration;
    private readonly CustomerCodeGenerator _customerCodeGenerator;

    private bool _suppressLegacyPhoneAutoSearch;
    private bool _legacyPhoneCaptureInProgress;
    private bool _legacyPhoneWasComplete;
    private string _lastCommittedLegacyPhoneNorm = "";

    [ObservableProperty] private string _originalBillNo = "";
    [ObservableProperty] private string _searchCustomerName = "";
    [ObservableProperty] private string _searchCustomerPhone = "";
    [ObservableProperty] private string _statusMessage = "Search by bill no, customer name, or mobile.";
    [ObservableProperty] private BillSearchRow? _selectedSearchBill;
    [ObservableProperty] private bool _billLoaded;
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private ReturnMode _returnMode = ReturnMode.CreditNote;
    [ObservableProperty] private SaleReturnSourceMode _sourceMode = SaleReturnSourceMode.SystemBill;
    [ObservableProperty] private DateTime? _originalBillDate;
    [ObservableProperty] private string _legacyCustomerName = "";
    [ObservableProperty] private string _legacyCustomerPhone = "";
    [ObservableProperty] private string _legacyCustomerCode = "";
    [ObservableProperty] private string _preSystemProductSearch = "";

    [ObservableProperty] private string _returnNo = "";
    [ObservableProperty] private bool _isInterState;

    [ObservableProperty] private string _returnDiscountFormatted = "₹ 0.00";
    [ObservableProperty] private string _grossSubTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _taxableSubTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _taxTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _cgstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _sgstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _igstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _returnTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _replacementTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _amountToCollectFormatted = "₹ 0.00";
    [ObservableProperty] private string _creditBalanceFormatted = "₹ 0.00";
    [ObservableProperty] private bool _hasExchangeLines;
    [ObservableProperty] private string _footerLabel = "Return Total";
    [ObservableProperty] private string _footerAmountFormatted = "₹ 0.00";
    [ObservableProperty] private bool _showPriorReturnQty;

    public bool HasReturnableLines => ReturnLines.Any(l => l.CanSelect);

    public string ReturnAlreadyPostedMessage =>
        AllowMultipleReturnsPerBill()
            ? "Some items on this bill were already returned. Only remaining quantity can be returned."
            : "A return is already posted for this bill. View the return summary in View Bill mode.";

    private bool AllowMultipleReturnsPerBill() =>
        _services.PosBillingSettings.Current.AllowMultipleReturnsPerBill;

    public ObservableCollection<SaleReturnLineItem> ReturnLines { get; } = new();
    public ObservableCollection<SaleExchangeLineItem> ExchangeLines { get; } = new();
    public ObservableCollection<LegacyReturnLineItem> LegacyReturnLines { get; } = new();
    public ObservableCollection<BillSearchRow> SearchResults { get; } = new();

    public bool ShowSearchResults => IsSystemBillMode && SearchResults.Count > 0 && !BillLoaded;

    public bool IsSystemBillMode => SourceMode == SaleReturnSourceMode.SystemBill;

    public bool IsPreSystemMode => SourceMode == SaleReturnSourceMode.PreSystemInvoice;

    public bool ShowSystemBillSearch => IsSystemBillMode;

    public bool ShowPreSystemFields => IsPreSystemMode;

    public bool ShowSystemReturnLineColumns => IsSystemBillMode;

    public bool ShowExchangeSection => IsSystemBillMode && BillLoaded;

    public bool ShowLegacyReturnLines => IsPreSystemMode && BillLoaded;

    public bool HasLegacyReturnLines => LegacyReturnLines.Count > 0;

    public bool ShowLegacyEmptyHint => ShowLegacyReturnLines && !HasLegacyReturnLines;

    private BsonDocument? _originalBillDoc;

    public Func<string, Task>? OnPostedSuccessfully { get; set; }

    public SaleReturnViewModel(AppServices services)
    {
        _services = services;
        _customerLookup = new CustomerLookupService(services.LocalDb, services.CentralApi);
        _customerRegistration = new CustomerRegistrationService(services.LocalDb, services.CentralApi, services.StoreContext);
        _customerCodeGenerator = new CustomerCodeGenerator(services.LocalDb);
        _ = AssignReturnNoAsync();
        LegacyReturnLines.CollectionChanged += (_, e) =>
        {
            if (e.OldItems != null)
            {
                foreach (LegacyReturnLineItem line in e.OldItems)
                    line.PropertyChanged -= OnLegacyLinePropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (LegacyReturnLineItem line in e.NewItems)
                    line.PropertyChanged += OnLegacyLinePropertyChanged;
            }

            RecalculateTotals();
        };
        ExchangeLines.CollectionChanged += (_, e) =>
        {
            if (e.OldItems != null)
            {
                foreach (SaleExchangeLineItem line in e.OldItems)
                    line.PropertyChanged -= OnExchangeLinePropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (SaleExchangeLineItem line in e.NewItems)
                    line.PropertyChanged += OnExchangeLinePropertyChanged;
            }

            RecalculateTotals();
        };
    }

    private async Task AssignReturnNoAsync()
    {
        ReturnNo = await _services.BillNumberGenerator.NextReturnAsync();
    }

    partial void OnSourceModeChanged(SaleReturnSourceMode value)
    {
        _ = ClearForm();
        StatusMessage = value == SaleReturnSourceMode.PreSystemInvoice
            ? "Enter pre-system invoice details, search customer by name/mobile, then Start return."
            : "Search by bill no, customer name, or mobile.";
        OnPropertyChanged(nameof(IsSystemBillMode));
        OnPropertyChanged(nameof(IsPreSystemMode));
        OnPropertyChanged(nameof(ShowSystemBillSearch));
        OnPropertyChanged(nameof(ShowPreSystemFields));
        OnPropertyChanged(nameof(ShowSystemReturnLineColumns));
        OnPropertyChanged(nameof(ShowExchangeSection));
        NotifySearchResultsChanged();
    }

    [RelayCommand]
    private void StartPreSystemReturn()
    {
        if (string.IsNullOrWhiteSpace(OriginalBillNo))
        {
            MessageBox.Show("Enter the reference invoice / bill number.", "Pre-system Return",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (OriginalBillDate == null)
        {
            MessageBox.Show("Select the original invoice date.", "Pre-system Return",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(LegacyCustomerName))
        {
            MessageBox.Show("Enter customer name.", "Pre-system Return",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!IsValidLegacyCustomerPhone(LegacyCustomerPhone))
        {
            MessageBox.Show("Enter a valid 10-digit customer mobile (required for credit note).", "Pre-system Return",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        foreach (var line in ReturnLines)
            line.PropertyChanged -= OnReturnLinePropertyChanged;
        foreach (var line in LegacyReturnLines)
            line.PropertyChanged -= OnLegacyLinePropertyChanged;
        ReturnLines.Clear();
        LegacyReturnLines.Clear();
        ExchangeLines.Clear();
        _originalBillDoc = null;
        BillLoaded = true;
        IsInterState = false;
        StatusMessage = $"Pre-system return started for invoice {OriginalBillNo.Trim()}. Add products below (SKU / barcode search).";
        OnPropertyChanged(nameof(ShowLegacyReturnLines));
        OnPropertyChanged(nameof(ShowLegacyEmptyHint));
        OnPropertyChanged(nameof(HasLegacyReturnLines));
        NotifySearchResultsChanged();
        RecalculateTotals();
    }

    private static bool IsValidLegacyCustomerPhone(string? phone)
    {
        var norm = PhoneMatchHelper.NormalizePhone(phone);
        return !string.IsNullOrEmpty(norm) && norm.Length >= 10;
    }

    partial void OnLegacyCustomerPhoneChanged(string value)
    {
        var norm = PhoneMatchHelper.NormalizePhone(value);
        if (norm != _lastCommittedLegacyPhoneNorm)
            _lastCommittedLegacyPhoneNorm = "";

        var isComplete = PhoneMatchHelper.IsPhoneLikeQuery((value ?? "").Trim());
        if (isComplete && !_legacyPhoneWasComplete)
            _ = HandleLegacyPhoneCommittedAsync();

        _legacyPhoneWasComplete = isComplete;
    }

    [RelayCommand]
    private Task SearchLegacyCustomer()
    {
        var name = (LegacyCustomerName ?? "").Trim();
        var phone = (LegacyCustomerPhone ?? "").Trim();
        if (!string.IsNullOrEmpty(name) && string.IsNullOrEmpty(phone))
            return SearchLegacyCustomerByNameAsync();
        return SearchLegacyCustomerCoreAsync(phoneSearchOnly: false);
    }

    public async Task SearchLegacyCustomerByNameAsync()
    {
        if (_suppressLegacyPhoneAutoSearch)
            return;

        var query = (LegacyCustomerName ?? "").Trim();
        if (string.IsNullOrEmpty(query))
        {
            MessageBox.Show("Enter a customer name to search.", "Pre-system Return",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var results = await _customerLookup.SearchAsync(query);
        if (!PhoneMatchHelper.IsPhoneLikeQuery(query))
        {
            var exactName = results
                .Where(r => string.Equals((r.Name ?? "").Trim(), query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (exactName.Count == 1)
            {
                ApplyLegacyCustomerMatch(exactName[0]);
                return;
            }
        }

        await ShowLegacyCustomerSearchDialogAsync(query);
    }

    public async Task HandleLegacyPhoneCommittedAsync()
    {
        if (_suppressLegacyPhoneAutoSearch || _legacyPhoneCaptureInProgress)
            return;

        var phone = (LegacyCustomerPhone ?? "").Trim();
        if (!PhoneMatchHelper.IsPhoneLikeQuery(phone))
            return;

        var norm = PhoneMatchHelper.NormalizePhone(phone);
        if (!string.IsNullOrEmpty(norm) && norm == _lastCommittedLegacyPhoneNorm)
            return;

        if (IsLegacyCustomerAlreadyLoaded(phone))
        {
            _lastCommittedLegacyPhoneNorm = norm;
            return;
        }

        _legacyPhoneCaptureInProgress = true;
        try
        {
            var results = await _customerLookup.SearchAsync(phone);
            var exact = results.Where(r => PhoneMatchHelper.PhoneMatches(r.Phone, phone)).ToList();

            if (exact.Count > 0)
            {
                ApplyLegacyCustomerMatch(exact[0]);
                _lastCommittedLegacyPhoneNorm = norm;
                return;
            }

            await OpenLegacyCustomerQuickCaptureAsync(phone, LegacyCustomerName.Trim(), isNewCustomer: true);
            _lastCommittedLegacyPhoneNorm = PhoneMatchHelper.NormalizePhone(LegacyCustomerPhone);
        }
        finally
        {
            _legacyPhoneCaptureInProgress = false;
        }
    }

    private bool IsLegacyCustomerAlreadyLoaded(string phone) =>
        !string.IsNullOrWhiteSpace(LegacyCustomerName)
        && PhoneMatchHelper.PhoneMatches(LegacyCustomerPhone, phone);

    private async Task SearchLegacyCustomerCoreAsync(bool phoneSearchOnly)
    {
        if (_suppressLegacyPhoneAutoSearch)
            return;

        var query = (LegacyCustomerPhone ?? "").Trim();
        if (!phoneSearchOnly)
        {
            if (string.IsNullOrEmpty(query))
                query = (LegacyCustomerName ?? "").Trim();
            if (string.IsNullOrEmpty(query))
                query = LegacyCustomerCode.Trim();
        }

        if (string.IsNullOrEmpty(query))
        {
            if (!phoneSearchOnly)
            {
                MessageBox.Show("Enter customer mobile, name, or code to search.", "Pre-system Return",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            return;
        }

        var results = await _customerLookup.SearchAsync(query);
        if (PhoneMatchHelper.IsPhoneLikeQuery(query))
        {
            var exact = results.Where(r => PhoneMatchHelper.PhoneMatches(r.Phone, query)).ToList();
            if (exact.Count == 1)
            {
                ApplyLegacyCustomerMatch(exact[0]);
                return;
            }
        }

        await ShowLegacyCustomerSearchDialogAsync(query);
    }

    private async Task ShowLegacyCustomerSearchDialogAsync(string query)
    {
        while (true)
        {
            var dlg = new CustomerSearchDialog(query, _customerLookup)
            {
                Owner = Application.Current.MainWindow
            };
            var result = dlg.ShowDialog();

            if (result == true && dlg.SelectedCustomer != null)
            {
                ApplyLegacyCustomerMatch(dlg.SelectedCustomer);
                return;
            }

            if (!dlg.WantsNewRegistration)
                return;

            var initialPhone = PhoneMatchHelper.IsPhoneLikeQuery(query) ? query : "";
            var initialName = PhoneMatchHelper.IsPhoneLikeQuery(query) ? "" : query;
            var saved = await OpenLegacyCustomerQuickCaptureAsync(initialPhone, initialName, isNewCustomer: true);
            if (saved)
                return;

            query = initialPhone.Length > 0 ? initialPhone : initialName;
        }
    }

    private async Task<bool> OpenLegacyCustomerQuickCaptureAsync(string initialPhone, string initialName, bool isNewCustomer)
    {
        while (true)
        {
            var dlg = new CustomerQuickCaptureDialog(
                initialPhone,
                initialName,
                existingMatch: null,
                isNewCustomer: isNewCustomer,
                exactMatchCount: 0)
            {
                Owner = Application.Current.MainWindow
            };

            _suppressLegacyPhoneAutoSearch = true;
            var dialogResult = dlg.ShowDialog();
            _suppressLegacyPhoneAutoSearch = false;

            if (dialogResult != true || !dlg.Saved)
                return false;

            if (dlg.WantsAdvancedSearch)
            {
                await ShowLegacyCustomerSearchDialogAsync(
                    !string.IsNullOrWhiteSpace(dlg.MobileNo) ? dlg.MobileNo.Trim() : dlg.CustomerName.Trim());
                return true;
            }

            var name = dlg.CustomerName.Trim();
            var mobile = dlg.MobileNo.Trim();

            var code = await _customerCodeGenerator.NextAsync();
            var reg = await _customerRegistration.RegisterAsync(new CustomerRegistrationPayload
            {
                CustomerCode = code,
                CustomerName = name,
                Mobile = mobile,
            });

            ApplyLegacyCustomerRegistration(reg);

            if (!string.IsNullOrWhiteSpace(reg.CentralSyncWarning))
            {
                MessageBox.Show(reg.CentralSyncWarning, "Pre-system Return",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return true;
        }
    }

    private void ApplyLegacyCustomerMatch(CustomerMatch match)
    {
        _suppressLegacyPhoneAutoSearch = true;
        try
        {
            LegacyCustomerCode = !string.IsNullOrWhiteSpace(match.Code) ? match.Code : match.Id;
            LegacyCustomerName = match.Name;
            LegacyCustomerPhone = match.Phone;
        }
        finally
        {
            _suppressLegacyPhoneAutoSearch = false;
        }

        _lastCommittedLegacyPhoneNorm = PhoneMatchHelper.NormalizePhone(match.Phone);
        _legacyPhoneWasComplete = PhoneMatchHelper.IsPhoneLikeQuery(LegacyCustomerPhone);
        StatusMessage = $"Customer loaded: {LegacyCustomerName.Trim()}. Enter invoice details and press Start return.";
    }

    private void ApplyLegacyCustomerRegistration(CustomerRegistrationResult result)
    {
        _suppressLegacyPhoneAutoSearch = true;
        try
        {
            LegacyCustomerCode = result.BillingCustomerCode;
            LegacyCustomerName = result.CustomerName;
            LegacyCustomerPhone = result.CustomerPhone;
        }
        finally
        {
            _suppressLegacyPhoneAutoSearch = false;
        }

        _lastCommittedLegacyPhoneNorm = PhoneMatchHelper.NormalizePhone(result.CustomerPhone);
        _legacyPhoneWasComplete = PhoneMatchHelper.IsPhoneLikeQuery(LegacyCustomerPhone);
        StatusMessage = $"New customer saved: {result.CustomerName.Trim()}. Enter invoice details and press Start return.";
    }

    [RelayCommand]
    private void RemoveLegacyLine(LegacyReturnLineItem? line)
    {
        if (line != null)
            LegacyReturnLines.Remove(line);
        OnPropertyChanged(nameof(HasLegacyReturnLines));
        OnPropertyChanged(nameof(ShowLegacyEmptyHint));
        RecalculateTotals();
    }

    [RelayCommand]
    private async Task SearchBills()
    {
        if (IsPreSystemMode)
            return;

        if (!HasSearchCriteria())
        {
            MessageBox.Show("Enter at least one search field (bill no, customer name, or mobile).", "Sale Return",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        StatusMessage = "Searching…";
        BillLoaded = false;
        SearchResults.Clear();
        foreach (var line in ReturnLines)
            line.PropertyChanged -= OnReturnLinePropertyChanged;
        foreach (var line in ExchangeLines)
            line.PropertyChanged -= OnExchangeLinePropertyChanged;
        ReturnLines.Clear();
        ExchangeLines.Clear();
        _originalBillDoc = null;

        try
        {
            var rows = await _services.BillDocuments.SearchBillsAsync(
                string.IsNullOrWhiteSpace(OriginalBillNo) ? null : OriginalBillNo.Trim(),
                dateFrom: null,
                dateTo: null,
                customerName: string.IsNullOrWhiteSpace(SearchCustomerName) ? null : SearchCustomerName.Trim(),
                customerPhone: string.IsNullOrWhiteSpace(SearchCustomerPhone) ? null : SearchCustomerPhone.Trim(),
                status: "posted",
                limit: 100);

            foreach (var row in rows)
                SearchResults.Add(row);

            SelectedSearchBill = SearchResults.Count > 0 ? SearchResults[0] : null;
            StatusMessage = rows.Count == 0
                ? "No posted bills found."
                : $"{rows.Count} bill(s) found — select one and press Load bill.";
            NotifySearchResultsChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = "Search failed: " + ex.Message;
            NotifySearchResultsChanged();
        }
    }

    private bool HasSearchCriteria() =>
        !string.IsNullOrWhiteSpace(OriginalBillNo)
        || !string.IsNullOrWhiteSpace(SearchCustomerName)
        || !string.IsNullOrWhiteSpace(SearchCustomerPhone);

    partial void OnSelectedSearchBillChanged(BillSearchRow? value) =>
        OpenSelectedBillCommand.NotifyCanExecuteChanged();

    private bool CanOpenSelectedBill() => SelectedSearchBill != null && !BillLoaded;

    [RelayCommand(CanExecute = nameof(CanOpenSelectedBill))]
    private async Task OpenSelectedBill()
    {
        if (SelectedSearchBill == null)
            return;

        var billNo = SelectedSearchBill.BillNo;
        OriginalBillNo = billNo;
        SearchResults.Clear();
        SelectedSearchBill = null;
        NotifySearchResultsChanged();
        var loaded = await LoadBillByNoAsync(billNo);
        StatusMessage = loaded
            ? $"Loaded bill {billNo}."
            : $"Could not load bill {billNo}.";
    }

    private void NotifySearchResultsChanged()
    {
        OnPropertyChanged(nameof(ShowSearchResults));
        OpenSelectedBillCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task LookupBill() => await SearchBills();

    /// <param name="skipDuplicateChecks">When true, loads bill for viewing (e.g. dashboard redirect to existing return).</param>
    public async Task<bool> LoadBillByNoAsync(string billNoInput, bool skipDuplicateChecks = false)
    {
        var input = billNoInput.Trim();
        if (string.IsNullOrEmpty(input))
            return false;

        var coll = _services.LocalDb.GetCollection<BsonDocument>("store_bills");
        var digits = new string(input.Where(char.IsDigit).ToArray());

        BsonDocument? doc = null;

        if (digits.Length is >= 3 and <= 4)
        {
            var regex = new BsonRegularExpression($"{Regex.Escape(digits)}$", "i");
            var filter = Builders<BsonDocument>.Filter.Regex("billNo", regex);
            var sort = Builders<BsonDocument>.Sort.Descending("createdAtUtc");
            var matches = await coll.Find(filter).Sort(sort).Limit(20).ToListAsync();

            if (matches.Count == 0)
            {
                if (!skipDuplicateChecks)
                    MessageBox.Show($"No bill found ending with '{digits}'.", "Sale Return", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (matches.Count == 1)
            {
                doc = matches[0];
            }
            else if (!skipDuplicateChecks)
            {
                var dlg = new BillPickDialog(matches) { Owner = Application.Current.MainWindow };
                if (dlg.ShowDialog() != true || string.IsNullOrEmpty(dlg.SelectedBillNo))
                    return false;

                doc = matches.FirstOrDefault(m => m.GetValue("billNo", "").AsString == dlg.SelectedBillNo)
                    ?? await coll.Find(new BsonDocument("billNo", dlg.SelectedBillNo)).FirstOrDefaultAsync();
            }
            else
            {
                doc = matches.FirstOrDefault(m =>
                    string.Equals(m.GetValue("billNo", "").AsString, input, StringComparison.OrdinalIgnoreCase))
                    ?? matches[0];
            }
        }
        else
        {
            doc = await coll.Find(new BsonDocument("billNo", input)).FirstOrDefaultAsync();
            if (doc == null && digits.Length > 0)
            {
                var normalized = input.Replace(" ", "-", StringComparison.Ordinal);
                if (!string.Equals(normalized, input, StringComparison.Ordinal))
                    doc = await coll.Find(new BsonDocument("billNo", normalized)).FirstOrDefaultAsync();
            }
        }

        if (doc == null)
        {
            if (!skipDuplicateChecks)
                MessageBox.Show($"Bill '{input}' not found.", "Sale Return", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var billNo = doc.GetValue("billNo", "").AsString;
        if (!skipDuplicateChecks)
        {
            var allowMultiple = AllowMultipleReturnsPerBill();
            if (!allowMultiple)
            {
                var existingReturn = await _services.SaleReturnHistory.FindFirstPostedReturnForBillAsync(
                    _services.StoreContext.StoreId, billNo);
                if (existingReturn != null)
                {
                    MessageBox.Show(
                        BuildDuplicateReturnMessage(billNo, existingReturn),
                        "Sale Return",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                var existingCreditNote = await FindCreditNoteForBillAsync(billNo);
                if (existingCreditNote != null)
                {
                    MessageBox.Show(
                        BuildDuplicateCreditNoteMessage(billNo, existingCreditNote),
                        "Sale Return",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }
            }
            else
            {
                var priorByLine = await _services.SaleReturnHistory.GetPreviouslyReturnedQtyByLineAsync(
                    _services.StoreContext.StoreId, billNo);
                if (!SaleReturnHistoryService.HasRemainingReturnableQty(doc, priorByLine))
                {
                    MessageBox.Show(
                        "All items on this bill have already been returned.",
                        "Sale Return",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }
            }
        }

        OriginalBillNo = billNo;
        await LoadBillFromDocumentAsync(doc, skipDuplicateChecks);
        return true;
    }

    private async Task LoadBillFromDocumentAsync(BsonDocument doc, bool skipDuplicateChecks)
    {
        IReadOnlyDictionary<int, decimal> priorByLine = new Dictionary<int, decimal>();
        if (!skipDuplicateChecks && AllowMultipleReturnsPerBill())
        {
            var billNo = doc.GetValue("billNo", "").AsString;
            priorByLine = await _services.SaleReturnHistory.GetPreviouslyReturnedQtyByLineAsync(
                _services.StoreContext.StoreId, billNo);
        }

        LoadBillFromDocument(doc, priorByLine);
    }

    private static string BuildDuplicateReturnMessage(string billNo, BsonDocument existingReturn)
    {
        var returnNo = existingReturn.GetValue("returnNo", "").AsString;
        var returnNoText = string.IsNullOrWhiteSpace(returnNo) ? "" : $" (Return No: {returnNo})";
        return $"A return has already been posted for bill {billNo}{returnNoText}.\n\nOnly one return is allowed per bill.";
    }

    private Task<CustomerCreditNoteRecord?> FindCreditNoteForBillAsync(string billNo) =>
        _services.CustomerCreditNotes.FindByOriginalBillAsync(_services.StoreContext.StoreId, billNo);

    private static string BuildDuplicateCreditNoteMessage(string billNo, CustomerCreditNoteRecord existingCreditNote)
    {
        var cnText = string.IsNullOrWhiteSpace(existingCreditNote.CreditNoteNo)
            ? ""
            : $" ({existingCreditNote.CreditNoteNo})";
        return $"A credit note has already been created for bill {billNo}{cnText}.\n\nOnly one credit note is allowed per bill.";
    }

    private void LoadBillFromDocument(BsonDocument doc, IReadOnlyDictionary<int, decimal>? priorByLine = null)
    {
        foreach (var line in ReturnLines)
            line.PropertyChanged -= OnReturnLinePropertyChanged;

        _originalBillDoc = doc;
        OriginalBillNo = doc.GetValue("billNo", "").AsString;
        IsInterState = doc.Contains("isInterState") && doc["isInterState"].AsBoolean;
        ReturnLines.Clear();
        priorByLine ??= new Dictionary<int, decimal>();

        if (doc.Contains("lines") && doc["lines"].IsBsonArray)
        {
            foreach (BsonDocument lineBson in doc["lines"].AsBsonArray.OfType<BsonDocument>())
            {
                var lineNo = lineBson.GetValue("lineNo", 0).ToInt32();
                var taxPercent = (decimal)lineBson.GetValue("taxPercent", 0).ToDouble();
                priorByLine.TryGetValue(lineNo, out var priorReturned);
                var item = new SaleReturnLineItem
                {
                    LineNo = lineNo,
                    ProductCode = lineBson.GetValue("sku", "").AsString,
                    Description = lineBson.GetValue("description", "").AsString,
                    OriginalQty = (decimal)lineBson.GetValue("qty", 0).ToDouble(),
                    Rate = (decimal)lineBson.GetValue("rate", 0).ToDouble(),
                    TaxPercent = taxPercent,
                    IsIgst = IsInterState,
                    OriginalItemDiscount = (decimal)lineBson.GetValue("discountAmount", 0).ToDouble(),
                    OriginalCashDiscount = (decimal)lineBson.GetValue("cashDiscountAmount", 0).ToDouble(),
                    OriginalPaidInclusive = ResolveOriginalPaidInclusive(lineBson, taxPercent, IsInterState),
                    PreviouslyReturnedQty = priorReturned,
                };
                item.PropertyChanged += OnReturnLinePropertyChanged;
                ReturnLines.Add(item);
            }
        }

        ShowPriorReturnQty = AllowMultipleReturnsPerBill() && ReturnLines.Any(l => l.PreviouslyReturnedQty > 0);
        BillLoaded = true;
        NotifySearchResultsChanged();
        OnPropertyChanged(nameof(HasReturnableLines));
        RecalculateTotals();
    }

    private static decimal ResolveOriginalPaidInclusive(BsonDocument lineBson, decimal taxPercent, bool isIgst)
    {
        if (lineBson.Contains("revisedInclusiveAmount"))
        {
            var revisedInclusive = (decimal)lineBson["revisedInclusiveAmount"].ToDouble();
            if (revisedInclusive > 0)
                return revisedInclusive;
        }

        var revisedAmount = lineBson.Contains("revisedAmount")
            ? (decimal)lineBson["revisedAmount"].ToDouble()
            : 0m;
        var revisedTax = lineBson.Contains("revisedTaxAmount")
            ? (decimal)lineBson["revisedTaxAmount"].ToDouble()
            : 0m;
        if (revisedAmount > 0 || revisedTax > 0)
            return MoneyMath.RoundAmount(revisedAmount + revisedTax);

        var amount = (decimal)lineBson.GetValue("amount", 0).ToDouble();
        var cgst = (decimal)lineBson.GetValue("cgstAmount", 0).ToDouble();
        var sgst = (decimal)lineBson.GetValue("sgstAmount", 0).ToDouble();
        var igst = (decimal)lineBson.GetValue("igstAmount", 0).ToDouble();
        var taxFromLine = cgst + sgst + igst;
        if (amount > 0 || taxFromLine > 0)
            return MoneyMath.RoundAmount(amount + taxFromLine);

        var itemDisc = (decimal)lineBson.GetValue("discountAmount", 0).ToDouble();
        var cashDisc = (decimal)lineBson.GetValue("cashDiscountAmount", 0).ToDouble();
        var originalInclusive = BillingDiscountCalculator.ComputeOriginalInclusive(amount, taxPercent, isIgst);
        return Math.Max(0m, MoneyMath.RoundAmount(originalInclusive - itemDisc - cashDisc));
    }

    private void OnReturnLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SaleReturnLineItem.ReturnQty)
            or nameof(SaleReturnLineItem.IsSelected)
            or nameof(SaleReturnLineItem.ReturnAmount)
            or nameof(SaleReturnLineItem.ReturnInclusive)
            or nameof(SaleReturnLineItem.ReturnDiscountAmount)
            or nameof(SaleReturnLineItem.GrossReturnAmount)
            or nameof(SaleReturnLineItem.TaxableReturnAmount)
            or nameof(SaleReturnLineItem.LineReturnTotal)
            or nameof(SaleReturnLineItem.CgstAmount)
            or nameof(SaleReturnLineItem.SgstAmount)
            or nameof(SaleReturnLineItem.IgstAmount)
            or nameof(SaleReturnLineItem.TaxAmount))
        {
            RecalculateTotals();
        }
    }

    private void OnExchangeLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SaleExchangeLineItem.Qty)
            or nameof(SaleExchangeLineItem.Amount)
            or nameof(SaleExchangeLineItem.CgstAmount)
            or nameof(SaleExchangeLineItem.SgstAmount)
            or nameof(SaleExchangeLineItem.IgstAmount)
            or nameof(SaleExchangeLineItem.TaxAmount)
            or nameof(SaleExchangeLineItem.Total))
        {
            RecalculateTotals();
        }
    }

    private void OnLegacyLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LegacyReturnLineItem.Qty)
            or nameof(LegacyReturnLineItem.GrossReturnAmount)
            or nameof(LegacyReturnLineItem.TaxableReturnAmount)
            or nameof(LegacyReturnLineItem.LineReturnTotal)
            or nameof(LegacyReturnLineItem.CgstAmount)
            or nameof(LegacyReturnLineItem.SgstAmount)
            or nameof(LegacyReturnLineItem.IgstAmount)
            or nameof(LegacyReturnLineItem.TaxAmount))
        {
            RecalculateTotals();
        }
    }

    private void RecalculateTotals()
    {
        if (IsPreSystemMode)
        {
            RecalculatePreSystemTotals();
            return;
        }

        var selected = ReturnLines.Where(l => l.IsSelected && l.ReturnQty > 0).ToList();
        var grossSub = selected.Sum(l => l.GrossReturnAmount);
        var taxableSub = selected.Sum(l => l.TaxableReturnAmount);
        var returnDiscount = selected.Sum(l => l.ReturnDiscountAmount);
        var cgst = selected.Sum(l => l.CgstAmount);
        var sgst = selected.Sum(l => l.SgstAmount);
        var igst = selected.Sum(l => l.IgstAmount);
        var taxTotal = cgst + sgst + igst;
        var total = selected.Sum(l => l.LineReturnTotal);

        GrossSubTotalFormatted = MoneyMath.FormatRupee(grossSub);
        ReturnDiscountFormatted = MoneyMath.FormatRupee(returnDiscount);
        TaxableSubTotalFormatted = MoneyMath.FormatRupee(taxableSub);
        TaxTotalFormatted = MoneyMath.FormatRupee(taxTotal);
        CgstTotalFormatted = MoneyMath.FormatRupee(cgst);
        SgstTotalFormatted = MoneyMath.FormatRupee(sgst);
        IgstTotalFormatted = MoneyMath.FormatRupee(igst);
        ReturnTotalFormatted = MoneyMath.FormatRupee(total);

        var replacementTotal = ExchangeLines.Sum(l => l.Total);
        var amountToCollect = Math.Max(0, replacementTotal - total);
        var creditBalance = Math.Max(0, total - replacementTotal);
        ReplacementTotalFormatted = MoneyMath.FormatRupee(replacementTotal);
        AmountToCollectFormatted = MoneyMath.FormatPayable(amountToCollect);
        CreditBalanceFormatted = MoneyMath.FormatRupee(creditBalance);

        var hasExchange = ExchangeLines.Any(l => l.Qty > 0);
        HasExchangeLines = hasExchange;
        FooterLabel = hasExchange ? "Amount to collect" : "Return Total";
        FooterAmountFormatted = hasExchange ? AmountToCollectFormatted : ReturnTotalFormatted;
    }

    private void RecalculatePreSystemTotals()
    {
        var lines = LegacyReturnLines.Where(l => l.Qty > 0).ToList();
        var grossSub = lines.Sum(l => l.GrossReturnAmount);
        var taxableSub = lines.Sum(l => l.TaxableReturnAmount);
        var cgst = lines.Sum(l => l.CgstAmount);
        var sgst = lines.Sum(l => l.SgstAmount);
        var igst = lines.Sum(l => l.IgstAmount);
        var taxTotal = cgst + sgst + igst;
        var total = lines.Sum(l => l.LineReturnTotal);

        GrossSubTotalFormatted = MoneyMath.FormatRupee(grossSub);
        ReturnDiscountFormatted = MoneyMath.FormatRupee(0m);
        TaxableSubTotalFormatted = MoneyMath.FormatRupee(taxableSub);
        TaxTotalFormatted = MoneyMath.FormatRupee(taxTotal);
        CgstTotalFormatted = MoneyMath.FormatRupee(cgst);
        SgstTotalFormatted = MoneyMath.FormatRupee(sgst);
        IgstTotalFormatted = MoneyMath.FormatRupee(igst);
        ReturnTotalFormatted = MoneyMath.FormatRupee(total);
        ReplacementTotalFormatted = MoneyMath.FormatRupee(0m);
        AmountToCollectFormatted = MoneyMath.FormatPayable(0m);
        CreditBalanceFormatted = MoneyMath.FormatRupee(total);
        HasExchangeLines = false;
        FooterLabel = "Return Total";
        FooterAmountFormatted = ReturnTotalFormatted;
    }

    public async Task AddExchangeProductFromSearchAsync(string query)
    {
        var q = (query ?? "").Trim();
        if (IsPreSystemMode)
        {
            await AddPreSystemProductFromSearchAsync(q);
            return;
        }

        if (!BillLoaded)
        {
            MessageBox.Show("Load the original bill before adding an exchange product.", "Sale Exchange", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (q.Length < 1)
        {
            var picker = new ProductSearchDialog("", _services) { Owner = Application.Current.MainWindow };
            if (picker.ShowDialog() == true && picker.SelectedProduct != null)
                AddExchangeLineFromCatalog(picker.SelectedProduct);
            return;
        }

        var items = await _services.ProductCatalog.SearchAsync(q);
        if (items.Count == 1)
        {
            AddExchangeLineFromCatalog(items[0]);
            return;
        }

        var existing = await _services.ProductCatalog.FindBySkuOrBarcodeAsync(q);
        if (existing != null && existing.StockQty > 0)
        {
            AddExchangeLineFromCatalog(existing);
            return;
        }

        if (existing != null && existing.StockQty <= 0)
        {
            MessageBox.Show($"Product \"{existing.Name}\" (SKU: {existing.Sku}) is out of local stock.", "Sale Exchange", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new ProductSearchDialog(q, _services) { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() == true && dlg.SelectedProduct != null)
            AddExchangeLineFromCatalog(dlg.SelectedProduct);
    }

    public async Task AddPreSystemProductFromSearchAsync(string query)
    {
        if (!BillLoaded)
        {
            MessageBox.Show("Enter invoice details and press Start return before adding products.", "Pre-system Return",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var q = (query ?? "").Trim();
        if (q.Length < 1)
        {
            var picker = new ProductSearchDialog("", _services) { Owner = Application.Current.MainWindow };
            if (picker.ShowDialog() == true && picker.SelectedProduct != null)
                AddLegacyLineFromCatalog(picker.SelectedProduct);
            return;
        }

        var items = await _services.ProductCatalog.SearchAsync(q);
        if (items.Count == 1)
        {
            AddLegacyLineFromCatalog(items[0]);
            return;
        }

        var existing = await _services.ProductCatalog.FindBySkuOrBarcodeAsync(q);
        if (existing != null)
        {
            AddLegacyLineFromCatalog(existing);
            return;
        }

        var dlg = new ProductSearchDialog(q, _services) { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() == true && dlg.SelectedProduct != null)
            AddLegacyLineFromCatalog(dlg.SelectedProduct);
    }

    private void AddLegacyLineFromCatalog(CatalogProduct product)
    {
        var existing = LegacyReturnLines.FirstOrDefault(l =>
            string.Equals(l.ProductCode, product.Sku, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Qty += 1;
            RecalculateTotals();
            return;
        }

        LegacyReturnLines.Add(new LegacyReturnLineItem
        {
            LineNo = LegacyReturnLines.Count + 1,
            CentralProductId = product.CentralId,
            ProductCode = product.Sku,
            Description = product.Name,
            Qty = 1,
            Rate = product.SuggestedRate,
            TaxPercent = product.SuggestedTaxPercent,
            IsIgst = IsInterState,
        });
        OnPropertyChanged(nameof(HasLegacyReturnLines));
        OnPropertyChanged(nameof(ShowLegacyEmptyHint));
        RecalculateTotals();
    }

    [RelayCommand]
    private async Task AddPreSystemProductAsync()
    {
        await AddPreSystemProductFromSearchAsync(PreSystemProductSearch);
        PreSystemProductSearch = "";
    }

    [RelayCommand]
    private Task BrowsePreSystemProductAsync() => AddPreSystemProductFromSearchAsync("");

    private void AddExchangeLineFromCatalog(CatalogProduct product)
    {
        if (product.StockQty <= 0)
        {
            MessageBox.Show($"Product \"{product.Name}\" (SKU: {product.Sku}) is out of local stock.", "Sale Exchange", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var existing = ExchangeLines.FirstOrDefault(l => string.Equals(l.ProductCode, product.Sku, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            if (existing.Qty + 1 > existing.AvailableQty)
            {
                MessageBox.Show($"Only {existing.AvailableQty:N2} available locally for SKU {existing.ProductCode}.", "Sale Exchange", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            existing.Qty += 1;
            return;
        }

        ExchangeLines.Add(new SaleExchangeLineItem
        {
            CentralProductId = product.CentralId,
            ProductCode = product.Sku,
            Description = product.Name,
            AvailableQty = product.StockQty,
            Qty = 1,
            Rate = product.SuggestedRate,
            Mrp = product.Mrp ?? 0m,
            TaxPercent = product.SuggestedTaxPercent,
            IsIgst = IsInterState,
        });
    }

    [RelayCommand]
    private void RemoveExchangeLine(SaleExchangeLineItem? line)
    {
        if (line != null)
            ExchangeLines.Remove(line);
    }

    [RelayCommand]
    private async Task PostReturn()
    {
        if (IsPreSystemMode)
        {
            await PostPreSystemReturnAsync();
            return;
        }

        var selected = ReturnLines.Where(l => l.IsSelected && l.ReturnQty > 0).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Select at least one item with a return quantity.", "Sale Return", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(OriginalBillNo))
        {
            MessageBox.Show("Load the original bill before posting a return.", "Sale Return", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var allowMultiple = AllowMultipleReturnsPerBill();
        if (!allowMultiple)
        {
            var existingReturn = await _services.SaleReturnHistory.FindFirstPostedReturnForBillAsync(
                _services.StoreContext.StoreId, OriginalBillNo.Trim());
            if (existingReturn != null)
            {
                MessageBox.Show(
                    BuildDuplicateReturnMessage(OriginalBillNo.Trim(), existingReturn),
                    "Sale Return",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }
        else
        {
            var priorByLine = await _services.SaleReturnHistory.GetPreviouslyReturnedQtyByLineAsync(
                _services.StoreContext.StoreId, OriginalBillNo.Trim());
            foreach (var line in selected)
            {
                priorByLine.TryGetValue(line.LineNo, out var prior);
                var maxQty = Math.Max(0, line.OriginalQty - prior);
                if (line.ReturnQty > maxQty)
                {
                    MessageBox.Show(
                        $"Return quantity for line {line.LineNo} ({line.ProductCode}) exceeds remaining quantity ({maxQty:N2}).",
                        "Sale Return",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }
        }

        var dayGuard = new DaySessionGuard(_services.DaySessions);
        var dayBlock = await dayGuard.ValidatePostingTodayAsync(
            _services.StoreContext.StoreId,
            _services.StoreContext.PosCounter);
        if (dayBlock != null)
        {
            MessageBox.Show(dayBlock, "Sale Return", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ReturnMode == ReturnMode.CreditNote && !allowMultiple)
        {
            var existingCreditNote = await FindCreditNoteForBillAsync(OriginalBillNo.Trim());
            if (existingCreditNote != null)
            {
                MessageBox.Show(
                    BuildDuplicateCreditNoteMessage(OriginalBillNo.Trim(), existingCreditNote),
                    "Sale Return",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        try
        {
            var storeId = _services.StoreContext.StoreId;
            var deviceId = _services.StoreContext.DeviceId;
            var posCounter = _services.StoreContext.PosCounter;
            var createdAt = DateTime.UtcNow.ToString("O");
            var eventId = Guid.NewGuid().ToString();

            var linesArr = new BsonArray();
            foreach (var l in selected)
            {
                linesArr.Add(new BsonDocument
                {
                    { "lineNo", l.LineNo },
                    { "sku", l.ProductCode },
                    { "description", l.Description },
                    { "returnQty", (double)l.ReturnQty },
                    { "rate", (double)l.Rate },
                    { "grossAmount", (double)l.GrossReturnAmount },
                    { "discountAmount", (double)l.ReturnItemDiscount },
                    { "cashDiscountAmount", (double)l.ReturnCashDiscount },
                    { "amount", (double)l.TaxableReturnAmount },
                    { "revisedInclusiveAmount", (double)l.ReturnInclusive },
                    { "lineTotal", (double)l.LineReturnTotal },
                    { "taxPercent", (double)l.TaxPercent },
                    { "cgstAmt", (double)l.CgstAmount },
                    { "sgstAmt", (double)l.SgstAmount },
                    { "igstAmt", (double)l.IgstAmount },
                    { "taxAmt", (double)l.TaxAmount },
                });
            }

            var sub = selected.Sum(l => l.TaxableReturnAmount);
            var returnDiscountTotal = selected.Sum(l => l.ReturnDiscountAmount);
            var cgst = selected.Sum(l => l.CgstAmount);
            var sgst = selected.Sum(l => l.SgstAmount);
            var igst = selected.Sum(l => l.IgstAmount);
            var total = selected.Sum(l => l.LineReturnTotal);
            var exchangeLines = ExchangeLines.Where(l => l.Qty > 0).ToList();
            var exchangeLinesArr = new BsonArray();
            foreach (var l in exchangeLines)
            {
                exchangeLinesArr.Add(new BsonDocument
                {
                    { "centralProductId", l.CentralProductId ?? "" },
                    { "sku", l.ProductCode },
                    { "description", l.Description },
                    { "qty", (double)l.Qty },
                    { "rate", (double)l.Rate },
                    { "amount", (double)l.Amount },
                    { "mrp", (double)l.Mrp },
                    { "taxPercent", (double)l.TaxPercent },
                    { "cgstAmt", (double)l.CgstAmount },
                    { "sgstAmt", (double)l.SgstAmount },
                    { "igstAmt", (double)l.IgstAmount },
                    { "taxAmt", (double)l.TaxAmount },
                    { "lineTotal", (double)l.Total },
                });
            }

            var replacementTotal = exchangeLines.Sum(l => l.Total);
            var amountToCollect = Math.Max(0, replacementTotal - total);
            var creditBalance = Math.Max(0, total - replacementTotal);

            decimal cashRefunded = 0m;
            if (ReturnMode == ReturnMode.CashRefund && creditBalance > 0)
            {
                var confirm = MessageBox.Show(
                    $"Cash refund {MoneyMath.FormatRupee(creditBalance)} to customer?",
                    "Sale Return — Cash Refund",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                    return;
                cashRefunded = creditBalance;
            }

            PaymentOutcome? paymentOutcome = null;
            if (amountToCollect > 0)
            {
                var paymentVm = new PaymentDialogViewModel(_services.PaymentRouter, ReturnNo, amountToCollect, _services.RazorpayPosSettings);
                var paymentDlg = new PaymentDialog(paymentVm) { Owner = Application.Current.MainWindow };
                var paymentResult = paymentDlg.ShowDialog();
                if (paymentResult != true || !paymentVm.Outcome.Confirmed)
                    return;
                paymentOutcome = paymentVm.Outcome;
            }

            var paymentsArr = new BsonArray();
            if (paymentOutcome != null)
            {
                foreach (var leg in paymentOutcome.Legs)
                {
                    paymentsArr.Add(new BsonDocument
                    {
                        { "provider", leg.Provider.ToString() },
                        { "amount", (double)leg.Amount },
                        { "reference", leg.Reference },
                        { "status", leg.Status },
                    });
                }
            }

            var refundPaymentsArr = new BsonArray();
            if (cashRefunded > 0)
            {
                refundPaymentsArr.Add(new BsonDocument
                {
                    { "provider", "Cash" },
                    { "amount", (double)cashRefunded },
                    { "reference", ReturnNo },
                    { "status", "posted" },
                });
            }

            var customerCode = _originalBillDoc?.GetValue("customerCode", "").AsString ?? "";
            var customerName = _originalBillDoc?.GetValue("customerName", "").AsString ?? "";
            var customerPhone = _originalBillDoc?.GetValue("customerPhone", "").AsString ?? "";

            var returnDoc = new BsonDocument
            {
                { "returnNo", ReturnNo },
                { "originalBillNo", OriginalBillNo.Trim() },
                { "storeId", storeId },
                { "deviceId", deviceId },
                { "posCounter", posCounter },
                { "customerCode", customerCode },
                { "customerName", customerName },
                { "customerPhone", customerPhone },
                { "transactionType", exchangeLines.Count > 0 ? "exchange" : "return" },
                { "returnMode", ReturnMode == ReturnMode.CreditNote ? "credit_note" : "cash_refund" },
                { "reason", Reason },
                { "isInterState", IsInterState },
                { "lines", linesArr },
                { "returnLines", linesArr },
                { "exchangeLines", exchangeLinesArr },
                { "subTotal", (double)sub },
                { "returnDiscount", (double)returnDiscountTotal },
                { "cgstTotal", (double)cgst },
                { "sgstTotal", (double)sgst },
                { "igstTotal", (double)igst },
                { "returnTotal", (double)total },
                { "replacementTotal", (double)replacementTotal },
                { "amountCollected", (double)amountToCollect },
                { "creditBalance", (double)creditBalance },
                { "cashRefunded", (double)cashRefunded },
                { "payments", paymentsArr },
                { "refundPayments", refundPaymentsArr },
                { "status", "posted" },
                { "createdAtUtc", createdAt },
            };

            var returnsColl = _services.LocalDb.GetCollection<BsonDocument>("store_sale_returns");
            await returnsColl.InsertOneAsync(returnDoc);

            var payload = new BsonDocument
            {
                { "returnNo", ReturnNo },
                { "originalBillNo", OriginalBillNo.Trim() },
                { "createdAtUtc", createdAt },
                { "transactionType", exchangeLines.Count > 0 ? "exchange" : "return" },
                { "returnMode", ReturnMode == ReturnMode.CreditNote ? "credit_note" : "cash_refund" },
                { "reason", Reason },
                { "isInterState", IsInterState },
                { "lines", linesArr },
                { "returnLines", linesArr },
                { "exchangeLines", exchangeLinesArr },
                { "subTotal", (double)sub },
                { "returnDiscount", (double)returnDiscountTotal },
                { "cgstTotal", (double)cgst },
                { "sgstTotal", (double)sgst },
                { "igstTotal", (double)igst },
                { "returnTotal", (double)total },
                { "replacementTotal", (double)replacementTotal },
                { "amountCollected", (double)amountToCollect },
                { "creditBalance", (double)creditBalance },
                { "cashRefunded", (double)cashRefunded },
                { "payments", paymentsArr },
                { "refundPayments", refundPaymentsArr },
            };

            var hash = JsonSerializer.Serialize(new
            {
                returnNo = ReturnNo,
                originalBillNo = OriginalBillNo.Trim(),
                returnMode = ReturnMode == ReturnMode.CreditNote ? "credit_note" : "cash_refund",
                lines = selected.Select(l => new { sku = l.ProductCode, returnQty = l.ReturnQty, rate = l.Rate }),
                exchangeLines = exchangeLines.Select(l => new { sku = l.ProductCode, qty = l.Qty, rate = l.Rate }),
                total,
                replacementTotal,
                amountToCollect,
                creditBalance,
                cashRefunded,
            });

            var outboxEvent = new BsonDocument
            {
                { "eventId", eventId },
                { "storeId", storeId },
                { "deviceId", deviceId },
                { "type", exchangeLines.Count > 0 ? "SaleExchangeCreated" : "SaleReturnCreated" },
                { "createdAt", createdAt },
                { "payload", payload },
                { "hash", hash },
                { "status", "pending" },
            };

            var outbox = _services.LocalDb.GetCollection<BsonDocument>("outbox_events");
            await outbox.InsertOneAsync(outboxEvent);

            var stockOk = 0;
            var stockFailed = new List<string>();
            foreach (var line in selected.Where(l => !string.IsNullOrWhiteSpace(l.ProductCode)))
            {
                var ok = await _services.ProductCatalog.IncrementStockBySkuAsync(
                    line.ProductCode, line.ReturnQty, line.Description);
                if (ok)
                    stockOk++;
                else
                    stockFailed.Add(line.ProductCode);
            }

            foreach (var line in exchangeLines.Where(l => !string.IsNullOrWhiteSpace(l.ProductCode)))
                await _services.ProductCatalog.DecrementStockBySkuAsync(line.ProductCode, line.Qty);

            string? createdCreditNoteNo = null;
            var creditNoteWarning = "";
            if (ReturnMode == ReturnMode.CreditNote && creditBalance > 0)
            {
                var existingCn = await FindCreditNoteForBillAsync(OriginalBillNo.Trim());
                if (existingCn != null && allowMultiple)
                {
                    createdCreditNoteNo = await _services.CustomerCreditNotes.AddCreditFromReturnAsync(
                        existingCn.CreditNoteNo,
                        ReturnNo,
                        creditBalance,
                        storeId);
                }
                else
                {
                    createdCreditNoteNo = await _services.CustomerCreditNotes.CreateFromReturnAsync(
                        ReturnNo,
                        OriginalBillNo.Trim(),
                        customerCode,
                        customerName,
                        customerPhone,
                        creditBalance,
                        storeId);
                }

                if (!string.IsNullOrEmpty(createdCreditNoteNo))
                {
                    await returnsColl.UpdateOneAsync(
                        Builders<BsonDocument>.Filter.Eq("returnNo", ReturnNo),
                        Builders<BsonDocument>.Update.Set("creditNoteNo", createdCreditNoteNo));
                }
                else
                {
                    var duplicateCn = await FindCreditNoteForBillAsync(OriginalBillNo.Trim());
                    creditNoteWarning = duplicateCn != null
                        ? $"\n\n{BuildDuplicateCreditNoteMessage(OriginalBillNo.Trim(), duplicateCn)}"
                        : "\n\nCredit could not be made redeemable on billing: customer needs a valid 10-digit phone on the original bill.";
                }
            }

            var grossAmount = selected.Sum(l => l.GrossReturnAmount);
            if (exchangeLines.Count > 0)
                ShowExchangeReceipt(selected, exchangeLines, total, replacementTotal, amountToCollect, creditBalance, createdCreditNoteNo);
            else
            {
                await SaleReturnPrintFlow.ShowAsync(
                    _services,
                    selected,
                    ReturnNo,
                    OriginalBillNo.Trim(),
                    ReturnMode == ReturnMode.CreditNote ? "Credit Note" : "Cash",
                    grossAmount,
                    total,
                    cgst,
                    sgst,
                    igst,
                    IsInterState,
                    createdCreditNoteNo,
                    cashRefunded);
            }

            var modeText = ReturnMode == ReturnMode.CreditNote ? "Credit Note" : "Cash Refund";
            var successBody = exchangeLines.Count > 0
                ? $"Exchange {ReturnNo} posted."
                : $"Return {ReturnNo} posted. Mode: {modeText}.";
            if (!string.IsNullOrEmpty(createdCreditNoteNo))
                successBody += $"\n\n{createdCreditNoteNo} created for customer (₹ {creditBalance:N2} redeemable on billing).";
            successBody += creditNoteWarning;
            if (stockOk > 0)
                successBody += $"\n\nReturned stock updated for {stockOk} item(s).";
            if (stockFailed.Count > 0)
                successBody += $"\n\nWarning: could not update local stock for: {string.Join(", ", stockFailed)}.";

            MessageBox.Show(successBody, "Sale Return", MessageBoxButton.OK, MessageBoxImage.Information);

            var postedBillNo = OriginalBillNo.Trim();
            if (OnPostedSuccessfully != null)
                await OnPostedSuccessfully(postedBillNo);
            else
                await ClearForm();
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            MessageBox.Show(
                $"A return has already been posted for bill {OriginalBillNo.Trim()}.\n\nOnly one return is allowed per bill.",
                "Sale Return",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not post return: {ex.Message}", "Sale Return", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task PostPreSystemReturnAsync()
    {
        var lines = LegacyReturnLines.Where(l => l.Qty > 0).ToList();
        if (lines.Count == 0)
        {
            MessageBox.Show("Add at least one product with quantity.", "Pre-system Return",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(OriginalBillNo) || OriginalBillDate == null)
        {
            MessageBox.Show("Enter reference invoice number and date.", "Pre-system Return",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!IsValidLegacyCustomerPhone(LegacyCustomerPhone))
        {
            MessageBox.Show("Enter a valid 10-digit customer mobile.", "Pre-system Return",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var storeId = _services.StoreContext.StoreId;
        var phoneNorm = PhoneMatchHelper.NormalizePhone(LegacyCustomerPhone);
        var priorLegacy = await _services.SaleReturnHistory.CountLegacyReturnsForReferenceAsync(
            storeId, OriginalBillNo.Trim(), phoneNorm);
        if (priorLegacy > 0)
        {
            var warn = MessageBox.Show(
                $"There are already {priorLegacy} pre-system return(s) for invoice {OriginalBillNo.Trim()}.\n\nContinue anyway?",
                "Pre-system Return",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (warn != MessageBoxResult.Yes)
                return;
        }

        var dayGuard = new DaySessionGuard(_services.DaySessions);
        var dayBlock = await dayGuard.ValidatePostingTodayAsync(storeId, _services.StoreContext.PosCounter);
        if (dayBlock != null)
        {
            MessageBox.Show(dayBlock, "Pre-system Return", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var sub = lines.Sum(l => l.TaxableReturnAmount);
        var cgst = lines.Sum(l => l.CgstAmount);
        var sgst = lines.Sum(l => l.SgstAmount);
        var igst = lines.Sum(l => l.IgstAmount);
        var total = lines.Sum(l => l.LineReturnTotal);
        var creditBalance = total;

        decimal cashRefunded = 0m;
        if (ReturnMode == ReturnMode.CashRefund && creditBalance > 0)
        {
            var confirm = MessageBox.Show(
                $"Cash refund {MoneyMath.FormatRupee(creditBalance)} to customer?",
                "Pre-system Return — Cash Refund",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;
            cashRefunded = creditBalance;
        }

        try
        {
            var deviceId = _services.StoreContext.DeviceId;
            var posCounter = _services.StoreContext.PosCounter;
            var createdAt = DateTime.UtcNow.ToString("O");
            var eventId = Guid.NewGuid().ToString();
            var billDateIso = OriginalBillDate.Value.ToString("yyyy-MM-dd");
            var billDateDisplay = OriginalBillDate.Value.ToString("dd-MMM-yyyy", InCulture);

            var linesArr = BuildReturnLinesBsonArrayFromLegacy(lines);

            var refundPaymentsArr = new BsonArray();
            if (cashRefunded > 0)
            {
                refundPaymentsArr.Add(new BsonDocument
                {
                    { "provider", "Cash" },
                    { "amount", (double)cashRefunded },
                    { "reference", ReturnNo },
                    { "status", "posted" },
                });
            }

            var customerName = LegacyCustomerName.Trim();
            var customerPhone = LegacyCustomerPhone.Trim();
            var customerCode = LegacyCustomerCode.Trim();

            var returnDoc = new BsonDocument
            {
                { "returnNo", ReturnNo },
                { "originalBillNo", OriginalBillNo.Trim() },
                { "originalBillDate", billDateIso },
                { "isLegacy", true },
                { "source", "pre_system" },
                { "storeId", storeId },
                { "deviceId", deviceId },
                { "posCounter", posCounter },
                { "customerCode", customerCode },
                { "customerName", customerName },
                { "customerPhone", customerPhone },
                { "transactionType", "return" },
                { "returnMode", ReturnMode == ReturnMode.CreditNote ? "credit_note" : "cash_refund" },
                { "reason", Reason },
                { "isInterState", IsInterState },
                { "lines", linesArr },
                { "returnLines", linesArr },
                { "exchangeLines", new BsonArray() },
                { "subTotal", (double)sub },
                { "returnDiscount", 0 },
                { "cgstTotal", (double)cgst },
                { "sgstTotal", (double)sgst },
                { "igstTotal", (double)igst },
                { "returnTotal", (double)total },
                { "replacementTotal", 0 },
                { "amountCollected", 0 },
                { "creditBalance", (double)creditBalance },
                { "cashRefunded", (double)cashRefunded },
                { "payments", new BsonArray() },
                { "refundPayments", refundPaymentsArr },
                { "status", "posted" },
                { "createdAtUtc", createdAt },
            };

            var returnsColl = _services.LocalDb.GetCollection<BsonDocument>("store_sale_returns");
            await returnsColl.InsertOneAsync(returnDoc);

            var payload = (BsonDocument)returnDoc.DeepClone();
            payload.Remove("_id");
            payload["createdAtUtc"] = createdAt;

            var hash = JsonSerializer.Serialize(new
            {
                returnNo = ReturnNo,
                originalBillNo = OriginalBillNo.Trim(),
                originalBillDate = billDateIso,
                isLegacy = true,
                returnMode = ReturnMode == ReturnMode.CreditNote ? "credit_note" : "cash_refund",
                lines = lines.Select(l => new { sku = l.ProductCode, returnQty = l.Qty, rate = l.Rate }),
                total,
                creditBalance,
                cashRefunded,
            });

            var outbox = _services.LocalDb.GetCollection<BsonDocument>("outbox_events");
            await outbox.InsertOneAsync(new BsonDocument
            {
                { "eventId", eventId },
                { "storeId", storeId },
                { "deviceId", deviceId },
                { "type", "SaleReturnCreated" },
                { "createdAt", createdAt },
                { "payload", payload },
                { "hash", hash },
                { "status", "pending" },
            });

            var stockFailed = new List<string>();
            var stockOk = 0;
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l.ProductCode)))
            {
                if (await _services.ProductCatalog.IncrementStockBySkuAsync(line.ProductCode, line.Qty, line.Description))
                    stockOk++;
                else
                    stockFailed.Add(line.ProductCode);
            }

            string? createdCreditNoteNo = null;
            var creditNoteWarning = "";
            if (ReturnMode == ReturnMode.CreditNote && creditBalance > 0)
            {
                createdCreditNoteNo = await _services.CustomerCreditNotes.CreateFromReturnAsync(
                    ReturnNo,
                    OriginalBillNo.Trim(),
                    customerCode,
                    customerName,
                    customerPhone,
                    creditBalance,
                    storeId,
                    isLegacy: true,
                    originalBillDate: billDateIso);

                if (!string.IsNullOrEmpty(createdCreditNoteNo))
                {
                    await returnsColl.UpdateOneAsync(
                        Builders<BsonDocument>.Filter.Eq("returnNo", ReturnNo),
                        Builders<BsonDocument>.Update.Set("creditNoteNo", createdCreditNoteNo));
                }
                else
                {
                    creditNoteWarning = "\n\nCredit could not be created: check customer mobile is valid.";
                }
            }

            var grossAmount = lines.Sum(l => l.GrossReturnAmount);
            await SaleReturnPrintFlow.ShowLegacyAsync(
                _services,
                lines,
                ReturnNo,
                OriginalBillNo.Trim(),
                billDateDisplay,
                ReturnMode == ReturnMode.CreditNote ? "Credit Note" : "Cash",
                grossAmount,
                total,
                cgst,
                sgst,
                igst,
                IsInterState,
                createdCreditNoteNo,
                cashRefunded);

            var modeText = ReturnMode == ReturnMode.CreditNote ? "Credit Note" : "Cash Refund";
            var successBody = $"Pre-system return {ReturnNo} posted. Mode: {modeText}.";
            if (!string.IsNullOrEmpty(createdCreditNoteNo))
                successBody += $"\n\n{createdCreditNoteNo} created for customer (₹ {creditBalance:N2} redeemable on billing).";
            successBody += creditNoteWarning;
            if (stockOk > 0)
                successBody += $"\n\nReturned stock updated for {stockOk} item(s).";
            if (stockFailed.Count > 0)
                successBody += $"\n\nWarning: could not update local stock for: {string.Join(", ", stockFailed)}.";

            MessageBox.Show(successBody, "Pre-system Return", MessageBoxButton.OK, MessageBoxImage.Information);

            var postedBillNo = OriginalBillNo.Trim();
            if (OnPostedSuccessfully != null)
                await OnPostedSuccessfully(postedBillNo);
            else
                await ClearForm();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not post pre-system return: {ex.Message}", "Pre-system Return",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static BsonArray BuildReturnLinesBsonArrayFromLegacy(IReadOnlyList<LegacyReturnLineItem> lines)
    {
        var linesArr = new BsonArray();
        foreach (var l in lines)
        {
            linesArr.Add(new BsonDocument
            {
                { "lineNo", l.LineNo },
                { "sku", l.ProductCode },
                { "description", l.Description },
                { "returnQty", (double)l.Qty },
                { "rate", (double)l.Rate },
                { "grossAmount", (double)l.GrossReturnAmount },
                { "discountAmount", 0 },
                { "cashDiscountAmount", 0 },
                { "amount", (double)l.TaxableReturnAmount },
                { "revisedInclusiveAmount", (double)l.LineReturnTotal },
                { "lineTotal", (double)l.LineReturnTotal },
                { "taxPercent", (double)l.TaxPercent },
                { "cgstAmt", (double)l.CgstAmount },
                { "sgstAmt", (double)l.SgstAmount },
                { "igstAmt", (double)l.IgstAmount },
                { "taxAmt", (double)l.TaxAmount },
            });
        }

        return linesArr;
    }

    [RelayCommand]
    private async Task ClearForm()
    {
        foreach (var line in ReturnLines)
            line.PropertyChanged -= OnReturnLinePropertyChanged;
        foreach (var line in ExchangeLines)
            line.PropertyChanged -= OnExchangeLinePropertyChanged;
        foreach (var line in LegacyReturnLines)
            line.PropertyChanged -= OnLegacyLinePropertyChanged;

        OriginalBillNo = "";
        OriginalBillDate = null;
        LegacyCustomerName = "";
        LegacyCustomerPhone = "";
        LegacyCustomerCode = "";
        PreSystemProductSearch = "";
        _lastCommittedLegacyPhoneNorm = "";
        _legacyPhoneWasComplete = false;
        SearchCustomerName = "";
        SearchCustomerPhone = "";
        Reason = "";
        ReturnMode = ReturnMode.CreditNote;
        IsInterState = false;
        ReturnLines.Clear();
        ExchangeLines.Clear();
        LegacyReturnLines.Clear();
        BillLoaded = false;
        ShowPriorReturnQty = false;
        _originalBillDoc = null;
        SearchResults.Clear();
        NotifySearchResultsChanged();
        StatusMessage = SourceMode == SaleReturnSourceMode.PreSystemInvoice
            ? "Enter pre-system invoice details, search customer by name/mobile, then Start return."
            : "Search by bill no, customer name, or mobile.";
        await AssignReturnNoAsync();
        RecalculateTotals();
    }

    private async void ShowExchangeReceipt(
        System.Collections.Generic.IReadOnlyList<SaleReturnLineItem> returnLines,
        System.Collections.Generic.IReadOnlyList<SaleExchangeLineItem> exchangeLines,
        decimal returnTotal,
        decimal replacementTotal,
        decimal amountCollected,
        decimal creditBalance,
        string? creditNoteNo = null)
    {
        try
        {
            _services.CentralAuthSession.ApplyTo(_services.CentralApi);
            var (profileOk, profileMsg) = await _services.ReceiptConfigSync.EnsureProfileReadyForPrintAsync();
            if (!profileOk)
            {
                MessageBox.Show(profileMsg, "Receipt settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var text = BuildExchangeReceiptText(returnLines, exchangeLines, returnTotal, replacementTotal, amountCollected, creditBalance, creditNoteNo);
            var doc = BillPrintService.CreateFlowDocument(text);
            var dlg = new InvoicePrintPreviewWindow(_services, doc, text, printInvoiceEnabled: true, forceThermalPrinter: true)
            {
                Owner = Application.Current.MainWindow,
            };
            dlg.ShowDialog();
        }
        catch { }
    }

    private string BuildExchangeReceiptText(
        System.Collections.Generic.IReadOnlyList<SaleReturnLineItem> returnLines,
        System.Collections.Generic.IReadOnlyList<SaleExchangeLineItem> exchangeLines,
        decimal returnTotal,
        decimal replacementTotal,
        decimal amountCollected,
        decimal creditBalance,
        string? creditNoteNo = null)
    {
        var store = _services.ReceiptConfig.Current.Store;
        var sb = new StringBuilder();
        sb.AppendLine(store.StoreName);
        if (!string.IsNullOrWhiteSpace(store.Address)) sb.AppendLine(store.Address);
        if (!string.IsNullOrWhiteSpace(store.Gstin)) sb.AppendLine($"GSTIN: {store.Gstin}");
        sb.AppendLine("------------------------------------------");
        sb.AppendLine("SALE EXCHANGE RECEIPT");
        sb.AppendLine($"Return No: {ReturnNo}");
        sb.AppendLine($"Original Bill: {OriginalBillNo.Trim()}");
        sb.AppendLine($"Date: {DateTime.Now:dd-MMM-yyyy HH:mm}");
        sb.AppendLine("------------------------------------------");
        sb.AppendLine("OLD ITEM RETURNED");
        foreach (var line in returnLines)
            sb.AppendLine($"{line.ProductCode} {line.ReturnQty:0.###} x {line.Rate:0.00} = {line.LineReturnTotal:0.00}");
        sb.AppendLine("------------------------------------------");
        sb.AppendLine("NEW ITEM ISSUED");
        foreach (var line in exchangeLines)
            sb.AppendLine($"{line.ProductCode} {line.Qty:0.###} x {line.Rate:0.00} = {line.Total:0.00}");
        sb.AppendLine("------------------------------------------");
        sb.AppendLine($"Old return value : {returnTotal:0.00}");
        sb.AppendLine($"New product value: {replacementTotal:0.00}");
        sb.AppendLine($"Amount collected : {amountCollected:0.00}");
        sb.AppendLine($"Credit balance   : {creditBalance:0.00}");
        if (!string.IsNullOrWhiteSpace(creditNoteNo))
            sb.AppendLine($"Credit note      : {creditNoteNo}");
        sb.AppendLine("------------------------------------------");
        AppendStoreFooter(sb, store);
        return sb.ToString();
    }

    private static void AppendStoreFooter(StringBuilder sb, StoreProfile store)
    {
        if (!string.IsNullOrWhiteSpace(store.TermsAndConditions))
            sb.AppendLine(store.TermsAndConditions);
        foreach (var line in store.PolicyLines ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(line))
                sb.AppendLine(line);
        }
        if (!string.IsNullOrWhiteSpace(store.Website))
            sb.AppendLine(store.Website);
        sb.AppendLine(string.IsNullOrWhiteSpace(store.ThankYouLine) ? "Thank you" : store.ThankYouLine);
    }
}
