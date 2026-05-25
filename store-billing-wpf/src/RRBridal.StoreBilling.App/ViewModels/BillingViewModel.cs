using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Customers;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Payments;
using RRBridal.StoreBilling.App.Services.Products;
using RRBridal.StoreBilling.App.Services.PurchaseIntents;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class BillingViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly AppServices _services;
    private readonly CustomerLookupService _customerLookup;
    private readonly CustomerRegistrationService _customerRegistration;
    private readonly CustomerCodeGenerator _customerCodeGenerator;

    public Action? NavigateToCustomerRegistration { get; set; }

    /// <summary>Raised when customer fields change so shell can refresh F9 Post bill.</summary>
    public event Action? PostBillCanExecuteChanged;

    public bool IsCustomerReadyForPost =>
        !string.IsNullOrWhiteSpace(CustomerName?.Trim())
        && PhoneMatchHelper.IsPhoneLikeQuery((CustomerPhone ?? "").Trim());

    private bool CanPostBill() => IsCustomerReadyForPost;

    private void NotifyPostBillCanExecute()
    {
        PostBillCommand.NotifyCanExecuteChanged();
        PostBillCanExecuteChanged?.Invoke();
    }

    [ObservableProperty] private string _searchText = "";

    [ObservableProperty] private string _customerCode = "";
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _salesman = "";
    [ObservableProperty] private string _customerPhone = "";

    [ObservableProperty] private bool _holdBills;
    [ObservableProperty] private bool _doorDelivery;
    [ObservableProperty] private bool _printInvoice = true;

    [ObservableProperty] private string _billNo = "";
    [ObservableProperty] private string _draftLabel = "DRAFT";
    [ObservableProperty] private string _billDateDisplay = "";

    [ObservableProperty] private string _subTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _originalTaxTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _revisedSubTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _taxTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _grossTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _itemDiscountFormatted = "₹ 0.00";
    [ObservableProperty] private string _cashDiscAmountFormatted = "₹ 0.00";
    [ObservableProperty] private string _roundOffFormatted = "₹ 0.00";
    [ObservableProperty] private string _payableTotalFormatted = "₹ 0.00";

    [ObservableProperty] private string _cgstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _sgstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _igstTotalFormatted = "₹ 0.00";

    [ObservableProperty] private bool _isInterState;

    [ObservableProperty] private string _itemDiscountPercentText = "";

    [ObservableProperty] private decimal _itemDiscountPercent;

    /// <summary>Total item discount (₹), sum of proportional line discounts.</summary>
    public decimal ItemDiscount => Lines.Sum(l => l.DiscountAmount);

    [ObservableProperty] private string _cashDiscAmountText = "";

    private decimal _cashDiscTarget;

    /// <summary>Total cash discount (₹), sum of proportional line cash discounts.</summary>
    public decimal CashDiscAmount => Lines.Sum(l => l.CashDiscountAmount);

    [ObservableProperty] private string _roundOffText = "";

    [ObservableProperty] private decimal _roundOff;

    private bool _suppressDiscountTextSync;
    private bool _suppressPhoneAutoSearch;
    private bool _suppressCustomerFieldSync;
    private bool _phoneCaptureInProgress;
    private string _lastCommittedPhoneNorm = "";
    private bool _phoneWasComplete;

    [ObservableProperty] private bool _isPhoneComplete;

    /// <summary>Orange blinking border while fewer than 10 digits are entered.</summary>
    [ObservableProperty] private bool _isPhoneIncompleteHighlight;

    private bool _roundOffUserEdited;
    private bool _isComputingTotals;
    private string? _resumingDraftBillNo;
    private BillTotals _lastBillTotals = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public ObservableCollection<BillingLineItem> Lines { get; } = new();

    public BillingViewModel(AppServices services)
    {
        _services = services;
        _customerLookup = new CustomerLookupService(services.LocalDb, services.CentralApi);
        _customerRegistration = new CustomerRegistrationService(services.LocalDb, services.CentralApi, services.StoreContext);
        _customerCodeGenerator = new CustomerCodeGenerator(services.LocalDb);
        AssignNewBillIdentity();
        ApplyLoggedInSalesman();
        Lines.CollectionChanged += OnLinesCollectionChanged;
        EnsureEntryRow();
        RecalculateTotals();
        NotifyPostBillCanExecute();
    }

    /// <summary>View focuses the trailing entry row Product code cell after add.</summary>
    public Action? RequestFocusEntryProductCode { get; set; }

    private void ApplyLoggedInSalesman()
    {
        Salesman = _services.UserSession?.LoggedInUser.Name?.Trim() ?? "";
    }

    private void AssignNewBillIdentity()
    {
        _resumingDraftBillNo = null;
        BillDateDisplay = DateTime.Now.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
        BillNo = $"{DateTime.Now:yyyyMMdd}-{_services.StoreContext.PosCounter}-draft";
        _ = AssignBillNumberAsync();
    }

    private async Task AssignBillNumberAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            BillNo = await _services.BillNumberGenerator.NextAsync(cts.Token);
        }
        catch
        {
            BillNo = $"{DateTime.Now:yyyyMMdd}-{_services.StoreContext.PosCounter}-0000";
        }
    }

    private void OnLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (BillingLineItem line in e.OldItems)
                line.PropertyChanged -= OnLinePropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (BillingLineItem line in e.NewItems)
                line.PropertyChanged += OnLinePropertyChanged;
        }

        RenumberLines();
        _roundOffUserEdited = false;
        RecalculateTotals();
    }

    private void OnLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BillingLineItem.Amount)
            or nameof(BillingLineItem.Qty)
            or nameof(BillingLineItem.Rate)
            or nameof(BillingLineItem.TaxPercent)
            or nameof(BillingLineItem.TaxAmount)
            or nameof(BillingLineItem.DiscountAmount)
            or nameof(BillingLineItem.CashDiscountAmount)
            or nameof(BillingLineItem.OriginalTaxAmount)
            or nameof(BillingLineItem.RevisedAmount)
            or nameof(BillingLineItem.RevisedInclusiveAmount))
        {
            RecalculateTotals();
        }
    }

    private void RenumberLines()
    {
        var i = 1;
        foreach (var line in Lines)
            line.LineNo = i++;
    }

    private static decimal LineOriginalInclusive(BillingLineItem line) =>
        BillingDiscountCalculator.ComputeOriginalInclusive(line.Amount, line.TaxPercent, line.IsIgst);

    private void ApplyProportionalItemDiscount(IReadOnlyList<(BillingLineItem Line, decimal OriginalInclusive)> snapshots)
    {
        if (ItemDiscountPercent <= 0)
        {
            foreach (var line in Lines)
                line.DiscountAmount = 0;
            return;
        }

        var active = snapshots.Where(s => s.Line.Amount > 0).ToList();
        if (active.Count == 0)
        {
            foreach (var line in Lines)
                line.DiscountAmount = 0;
            return;
        }

        var totalInclusive = active.Sum(s => s.OriginalInclusive);
        if (totalInclusive <= 0)
        {
            foreach (var line in Lines)
                line.DiscountAmount = 0;
            return;
        }

        var totalDisc = Math.Round(totalInclusive * ItemDiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
        var allocated = 0m;
        for (var i = 0; i < active.Count; i++)
        {
            var (line, originalInclusive) = active[i];
            if (i == active.Count - 1)
                line.DiscountAmount = Math.Max(0m, totalDisc - allocated);
            else
            {
                var share = Math.Round(originalInclusive / totalInclusive * totalDisc, 2, MidpointRounding.AwayFromZero);
                line.DiscountAmount = share;
                allocated += share;
            }
        }

        foreach (var line in Lines.Where(l => l.Amount <= 0))
            line.DiscountAmount = 0;
    }

    private void ApplyProportionalCashDiscount(IReadOnlyList<(BillingLineItem Line, decimal OriginalInclusive)> snapshots)
    {
        if (_cashDiscTarget <= 0)
        {
            foreach (var line in Lines)
                line.CashDiscountAmount = 0;
            return;
        }

        var active = snapshots
            .Where(s => s.Line.Amount > 0)
            .Select(s => (s.Line, InclusiveAfterItem: Math.Max(0m, s.OriginalInclusive - s.Line.DiscountAmount)))
            .Where(x => x.InclusiveAfterItem > 0)
            .ToList();

        if (active.Count == 0)
        {
            foreach (var line in Lines)
                line.CashDiscountAmount = 0;
            return;
        }

        var totalBase = active.Sum(x => x.InclusiveAfterItem);
        var totalCash = Math.Round(_cashDiscTarget, 2, MidpointRounding.AwayFromZero);
        var allocated = 0m;
        for (var i = 0; i < active.Count; i++)
        {
            var (line, inclusiveAfterItem) = active[i];
            if (i == active.Count - 1)
                line.CashDiscountAmount = Math.Max(0m, totalCash - allocated);
            else
            {
                var share = Math.Round(inclusiveAfterItem / totalBase * totalCash, 2, MidpointRounding.AwayFromZero);
                line.CashDiscountAmount = share;
                allocated += share;
            }
        }

        foreach (var line in Lines.Where(l => l.Amount <= 0))
            line.CashDiscountAmount = 0;
    }

    private static bool TryParseDecimalInput(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(text.Trim(), NumberStyles.Number, InCulture, out value);
    }

    /// <summary>Empty display when numeric value is zero (placeholder-style inputs).</summary>
    private static string FormatEditableDecimalText(decimal value) =>
        value == 0 ? "" : value.ToString("0.##", CultureInfo.InvariantCulture);

    private sealed record BillTotals(
        decimal SubTotal,
        decimal OriginalInclusiveTotal,
        decimal ItemDiscount,
        decimal CashDiscount,
        decimal OriginalTaxTotal,
        decimal RevisedSubTotal,
        decimal Cgst,
        decimal Sgst,
        decimal Igst,
        decimal TaxTotal,
        decimal GrandBeforeRound,
        decimal RoundOff,
        decimal Payable);

    private BillTotals ComputeBillTotals()
    {
        _isComputingTotals = true;
        try
        {
        return ComputeBillTotalsCore();
        }
        finally
        {
            _isComputingTotals = false;
        }
    }

    private BillTotals ComputeBillTotalsCore()
    {
        var snapshots = Lines
            .Where(l => l.Amount > 0)
            .Select(l => (Line: l, OriginalInclusive: LineOriginalInclusive(l)))
            .ToList();

        ApplyProportionalItemDiscount(snapshots);
        ApplyProportionalCashDiscount(snapshots);

        var sub = Lines.Sum(l => l.Amount);
        var originalInclusive = snapshots.Sum(s => s.OriginalInclusive);
        var itemDisc = ItemDiscount;
        var cashDisc = CashDiscAmount;
        var originalTax = Lines.Sum(l => l.OriginalTaxAmount);
        var revisedSub = Lines.Sum(l => l.RevisedAmount);
        var cgst = Lines.Sum(l => l.CgstAmount);
        var sgst = Lines.Sum(l => l.SgstAmount);
        var igst = Lines.Sum(l => l.IgstAmount);
        var tax = cgst + sgst + igst;
        var grandBeforeRound = Lines.Sum(l => l.RevisedInclusiveAmount);

        decimal roundOff;
        if (!_roundOffUserEdited)
        {
            var payableRounded = Math.Round(grandBeforeRound, 0, MidpointRounding.AwayFromZero);
            roundOff = payableRounded - grandBeforeRound;
            if (RoundOff != roundOff)
            {
                _suppressDiscountTextSync = true;
                RoundOff = roundOff;
                RoundOffText = FormatEditableDecimalText(roundOff);
                _suppressDiscountTextSync = false;
            }
        }
        else
        {
            roundOff = RoundOff;
        }

        var payable = grandBeforeRound + roundOff;
        return new BillTotals(
            sub, originalInclusive, itemDisc, cashDisc, originalTax, revisedSub,
            cgst, sgst, igst, tax, grandBeforeRound, roundOff, payable);
    }

    private void RecalculateTotals()
    {
        if (_isComputingTotals) return;

        var totals = ComputeBillTotals();
        _lastBillTotals = totals;

        SubTotalFormatted = FormatRupee(totals.SubTotal);
        OriginalTaxTotalFormatted = FormatRupee(totals.OriginalTaxTotal);
        RevisedSubTotalFormatted = FormatRupee(totals.RevisedSubTotal);
        TaxTotalFormatted = FormatRupee(totals.TaxTotal);
        CgstTotalFormatted = FormatRupee(totals.Cgst);
        SgstTotalFormatted = FormatRupee(totals.Sgst);
        IgstTotalFormatted = FormatRupee(totals.Igst);
        GrossTotalFormatted = FormatRupee(totals.OriginalInclusiveTotal);
        ItemDiscountFormatted = FormatRupee(totals.ItemDiscount);
        CashDiscAmountFormatted = FormatRupee(totals.CashDiscount);
        RoundOffFormatted = FormatRupee(totals.RoundOff);
        PayableTotalFormatted = FormatRupee(totals.Payable);
    }

    partial void OnItemDiscountPercentTextChanged(string value)
    {
        if (_suppressDiscountTextSync) return;

        if (string.IsNullOrWhiteSpace(value))
        {
            ItemDiscountPercent = 0;
            RecalculateTotals();
            return;
        }

        if (!TryParseDecimalInput(value, out var parsed))
            return;

        ItemDiscountPercent = Math.Clamp(parsed, 0, 100);
        RecalculateTotals();
    }

    partial void OnItemDiscountPercentChanged(decimal value)
    {
        if (value is < 0 or > 100)
        {
            ItemDiscountPercent = Math.Clamp(value, 0, 100);
            return;
        }

        var text = FormatEditableDecimalText(value);
        if (ItemDiscountPercentText != text)
        {
            _suppressDiscountTextSync = true;
            ItemDiscountPercentText = text;
            _suppressDiscountTextSync = false;
        }

        RecalculateTotals();
    }

    partial void OnCashDiscAmountTextChanged(string value)
    {
        if (_suppressDiscountTextSync) return;

        if (string.IsNullOrWhiteSpace(value))
        {
            _cashDiscTarget = 0;
            RecalculateTotals();
            return;
        }

        if (!TryParseDecimalInput(value, out var parsed))
            return;

        _cashDiscTarget = Math.Max(0m, parsed);
        RecalculateTotals();
    }

    partial void OnRoundOffTextChanged(string value)
    {
        if (_suppressDiscountTextSync) return;

        _roundOffUserEdited = true;

        if (string.IsNullOrWhiteSpace(value))
        {
            RoundOff = 0;
            RecalculateTotals();
            return;
        }

        if (!TryParseDecimalInput(value, out var parsed))
            return;

        RoundOff = parsed;
        RecalculateTotals();
    }

    partial void OnRoundOffChanged(decimal value)
    {
        if (_roundOffUserEdited) return;

        var text = FormatEditableDecimalText(value);
        if (RoundOffText != text)
        {
            _suppressDiscountTextSync = true;
            RoundOffText = text;
            _suppressDiscountTextSync = false;
        }
    }

    partial void OnIsInterStateChanged(bool value)
    {
        foreach (var line in Lines)
            line.IsIgst = value;
        RecalculateTotals();
    }

    private static string FormatRupee(decimal value) => "₹ " + value.ToString("N2", InCulture);

    public void ApplyCustomerRegistration(CustomerRegistrationResult result)
    {
        _suppressPhoneAutoSearch = true;
        try
        {
            CustomerCode = result.BillingCustomerCode;
            CustomerName = result.CustomerName;
            CustomerPhone = result.CustomerPhone;
        }
        finally
        {
            _suppressPhoneAutoSearch = false;
        }
        NotifyPostBillCanExecute();
    }

    public void ApplyCustomerMatch(CustomerMatch match)
    {
        _suppressPhoneAutoSearch = true;
        try
        {
            CustomerCode = !string.IsNullOrWhiteSpace(match.Code) ? match.Code : match.Id;
            CustomerName = match.Name;
            CustomerPhone = match.Phone;
        }
        finally
        {
            _suppressPhoneAutoSearch = false;
        }
        NotifyPostBillCanExecute();
    }

    /** Clears customer code, name, and phone from the bill (e.g. user deleted phone or name). */
    private void ClearCustomerProfile()
    {
        _suppressCustomerFieldSync = true;
        _suppressPhoneAutoSearch = true;
        try
        {
            CustomerCode = "";
            CustomerName = "";
            CustomerPhone = "";
            _lastCommittedPhoneNorm = "";
            _phoneWasComplete = false;
            IsPhoneComplete = false;
            IsPhoneIncompleteHighlight = false;
        }
        finally
        {
            _suppressPhoneAutoSearch = false;
            _suppressCustomerFieldSync = false;
        }
        NotifyPostBillCanExecute();
    }

    private bool HasLoadedCustomerOnBill() =>
        !string.IsNullOrWhiteSpace(CustomerCode)
        || !string.IsNullOrWhiteSpace(CustomerName)
        || !string.IsNullOrWhiteSpace(CustomerPhone);

    partial void OnCustomerNameChanged(string value)
    {
        if (!_suppressCustomerFieldSync && string.IsNullOrWhiteSpace(value) && HasLoadedCustomerOnBill())
            ClearCustomerProfile();
        else
            NotifyPostBillCanExecute();
    }

    partial void OnCustomerCodeChanged(string value)
    {
        if (!_suppressCustomerFieldSync && string.IsNullOrWhiteSpace(value) && HasLoadedCustomerOnBill())
            ClearCustomerProfile();
        else
            NotifyPostBillCanExecute();
    }

    [RelayCommand]
    private Task SearchCustomer()
    {
        var name = (CustomerName ?? "").Trim();
        var phone = (CustomerPhone ?? "").Trim();
        if (!string.IsNullOrEmpty(name) && string.IsNullOrEmpty(phone))
            return SearchCustomerByNameAsync();
        return SearchCustomerCoreAsync(phoneSearchOnly: false);
    }

    [RelayCommand]
    private Task SearchCustomerByPhone() => HandlePhoneCommittedAsync();

    public async Task SearchCustomerByNameAsync()
    {
        if (_suppressPhoneAutoSearch)
            return;

        var query = (CustomerName ?? "").Trim();
        if (string.IsNullOrEmpty(query))
        {
            MessageBox.Show("Enter a customer name to search.", "RR Bridal Billing",
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
                ApplyCustomerMatch(exactName[0]);
                return;
            }
        }

        await ShowCustomerSearchDialogAsync(query);
    }

    partial void OnCustomerPhoneChanged(string value)
    {
        if (!_suppressCustomerFieldSync && string.IsNullOrWhiteSpace(value))
        {
            if (HasLoadedCustomerOnBill())
            {
                ClearCustomerProfile();
                return;
            }
        }

        var norm = PhoneMatchHelper.NormalizePhone(value);
        if (norm != _lastCommittedPhoneNorm)
            _lastCommittedPhoneNorm = "";

        var isComplete = PhoneMatchHelper.IsPhoneLikeQuery((value ?? "").Trim());
        if (IsPhoneComplete != isComplete)
            IsPhoneComplete = isComplete;

        if (isComplete && !_phoneWasComplete)
            _ = HandlePhoneCommittedAsync();

        _phoneWasComplete = isComplete;
        UpdatePhoneHighlightState();
        NotifyPostBillCanExecute();
    }

    partial void OnIsPhoneCompleteChanged(bool value) => UpdatePhoneHighlightState();

    private void UpdatePhoneHighlightState()
    {
        var hasInput = !string.IsNullOrWhiteSpace(CustomerPhone);
        var incomplete = hasInput && !IsPhoneComplete;
        if (IsPhoneIncompleteHighlight != incomplete)
            IsPhoneIncompleteHighlight = incomplete;
    }

    private bool IsCustomerAlreadyOnBill(string phone) =>
        !string.IsNullOrWhiteSpace(CustomerName)
        && PhoneMatchHelper.PhoneMatches(CustomerPhone, phone);

    public async Task HandlePhoneCommittedAsync()
    {
        if (_suppressPhoneAutoSearch || _phoneCaptureInProgress)
            return;

        var phone = (CustomerPhone ?? "").Trim();
        if (!PhoneMatchHelper.IsPhoneLikeQuery(phone))
            return;

        var norm = PhoneMatchHelper.NormalizePhone(phone);
        if (!string.IsNullOrEmpty(norm) && norm == _lastCommittedPhoneNorm)
            return;

        if (IsCustomerAlreadyOnBill(phone))
        {
            _lastCommittedPhoneNorm = norm;
            return;
        }

        _phoneCaptureInProgress = true;
        try
        {
            var results = await _customerLookup.SearchAsync(phone);
            var exact = results.Where(r => PhoneMatchHelper.PhoneMatches(r.Phone, phone)).ToList();

            if (exact.Count > 0)
            {
                ApplyCustomerMatch(exact[0]);
                _lastCommittedPhoneNorm = norm;
                return;
            }

            var dlg = new CustomerQuickCaptureDialog(
                phone,
                "",
                existingMatch: null,
                isNewCustomer: true,
                exactMatchCount: 0)
            {
                Owner = Application.Current.MainWindow
            };

            _suppressPhoneAutoSearch = true;
            var dialogResult = dlg.ShowDialog();
            _suppressPhoneAutoSearch = false;

            if (dialogResult != true || !dlg.Saved)
                return;

            var name = dlg.CustomerName.Trim();
            var mobile = dlg.MobileNo.Trim();

            var code = await _customerCodeGenerator.NextAsync();
            var reg = await _customerRegistration.RegisterAsync(new CustomerRegistrationPayload
            {
                CustomerCode = code,
                CustomerName = name,
                Mobile = mobile,
            });

            ApplyCustomerRegistration(reg);

            if (!string.IsNullOrWhiteSpace(reg.CentralSyncWarning))
            {
                MessageBox.Show(reg.CentralSyncWarning, "RR Bridal Billing",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            _lastCommittedPhoneNorm = PhoneMatchHelper.NormalizePhone(mobile);
        }
        finally
        {
            _phoneCaptureInProgress = false;
        }
    }

    private async Task SearchCustomerCoreAsync(bool phoneSearchOnly)
    {
        if (_suppressPhoneAutoSearch)
            return;

        var query = (CustomerPhone ?? "").Trim();
        if (!phoneSearchOnly)
        {
            if (string.IsNullOrEmpty(query))
                query = (CustomerName ?? "").Trim();
            if (string.IsNullOrEmpty(query))
                query = (CustomerCode ?? "").Trim();
        }

        if (string.IsNullOrEmpty(query))
        {
            if (!phoneSearchOnly)
            {
                MessageBox.Show("Enter a mobile number, customer name, or code to search.", "RR Bridal Billing",
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
                ApplyCustomerMatch(exact[0]);
                return;
            }
        }

        await ShowCustomerSearchDialogAsync(query);
    }

    private Task ShowCustomerSearchDialogAsync(string query)
    {
        var dlg = new CustomerSearchDialog(query, _customerLookup)
        {
            Owner = Application.Current.MainWindow
        };
        var result = dlg.ShowDialog();

        if (result == true && dlg.SelectedCustomer != null)
        {
            ApplyCustomerMatch(dlg.SelectedCustomer);
            _lastCommittedPhoneNorm = PhoneMatchHelper.NormalizePhone(dlg.SelectedCustomer.Phone);
        }
        else if (dlg.WantsNewRegistration)
        {
            NavigateToCustomerRegistration?.Invoke();
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task SearchProduct() => CommitProductCodeInputAsync(SearchText);

    public Task OpenProductSearchAsync(CancellationToken ct = default) =>
        CommitProductCodeInputAsync(SearchText, ct);

    public async Task CommitProductCodeInputAsync(string? input, CancellationToken ct = default)
    {
        var q = (input ?? "").Trim();
        if (q.Length < 1)
        {
            MessageBox.Show(
                "Enter a product code.",
                "RR Bridal Billing",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var exact = await _services.ProductCatalog.FindBySkuOrBarcodeAsync(q, ct);
        if (exact != null)
        {
            if (exact.StockQty <= 0)
            {
                MessageBox.Show(
                    $"Product \"{exact.Name}\" (SKU: {exact.Sku}) is out of stock.\nA reference indent request has been created.",
                    "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Warning);
                await AddIndentRequestAsync(exact.Sku, exact.Name, exact.CentralId, ct);
                ClearEntryRowProductCode();
                SearchText = "";
                RequestFocusEntryProductCode?.Invoke();
                return;
            }

            AddLineFromCatalog(exact);
            SearchText = "";
            return;
        }

        var codeItems = await _services.ProductCatalog.SearchByProductCodeAsync(q, ct);
        if (codeItems.Count == 1)
        {
            AddLineFromCatalog(codeItems[0]);
            SearchText = "";
            return;
        }

        if (codeItems.Count > 1)
        {
            var codeDlg = new ProductSearchDialog(q, _services, codeOnly: true)
            {
                Owner = Application.Current.MainWindow
            };
            if (codeDlg.ShowDialog() == true && codeDlg.SelectedProduct != null)
                AddLineFromCatalog(codeDlg.SelectedProduct);
            SearchText = "";
            return;
        }

        var nameItems = await _services.ProductCatalog.SearchAsync(q, ct);
        if (nameItems.Count > 0)
        {
            var nameDlg = new ProductSearchDialog(q, _services, codeOnly: false)
            {
                Owner = Application.Current.MainWindow
            };
            if (nameDlg.ShowDialog() == true && nameDlg.SelectedProduct != null)
                AddLineFromCatalog(nameDlg.SelectedProduct);
            SearchText = "";
            return;
        }

        MessageBox.Show(
            $"No product found in local inventory for \"{q}\".\nA reference indent request has been created.",
            "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Warning);
        await AddIndentRequestAsync(q, q, "", ct);
        ClearEntryRowProductCode();
        SearchText = "";
        RequestFocusEntryProductCode?.Invoke();
    }

    private async Task AddIndentRequestAsync(string sku, string description, string centralProductId, CancellationToken ct)
    {
        try
        {
            var eventId = await _services.PurchaseIntentPublisher.SubmitAsync(
                new[] { new PurchaseIntentLineInput(sku, 1, "Reference intent from local stock shortage") },
                "Reference intent created from WPF billing because local stock was unavailable.",
                ct);

            var coll = _services.LocalDb.GetCollection<BsonDocument>("indent_requests");
            var doc = new BsonDocument
            {
                { "sku", sku },
                { "description", description },
                { "centralProductId", centralProductId ?? "" },
                { "storeId", _services.StoreContext.StoreId },
                { "requestedQty", 1 },
                { "sourceEventId", eventId },
                { "status", "pending" },
                { "createdAtUtc", DateTime.UtcNow.ToString("O") },
            };
            await coll.InsertOneAsync(doc, cancellationToken: ct);
        }
        catch { }
    }

    public void EnsureEntryRow()
    {
        foreach (var row in Lines.Where(l => l.IsEntryRow).ToList())
            Lines.Remove(row);
        Lines.Add(new BillingLineItem { IsEntryRow = true });
    }

    private BillingLineItem? GetEntryRow() =>
        Lines.FirstOrDefault(l => l.IsEntryRow);

    private void ClearEntryRowProductCode()
    {
        var entry = GetEntryRow();
        if (entry != null)
            entry.ProductCode = "";
    }

    private static void FillLineFromCatalog(BillingLineItem line, CatalogProduct p)
    {
        line.IsEntryRow = false;
        line.CentralProductId = p.CentralId ?? "";
        line.ProductCode = p.Sku;
        line.Description = p.Name;
        line.HsnCode = string.IsNullOrWhiteSpace(p.HsnSac) ? "" : p.HsnSac.Trim();
        line.Qty = 1;
        line.Rate = p.SuggestedRate;
        line.Mrp = p.Mrp ?? 0;
        line.TaxPercent = p.SuggestedTaxPercent;
    }

    private void AddLineFromCatalog(CatalogProduct p)
    {
        var sku = (p.Sku ?? "").Trim();
        if (!string.IsNullOrEmpty(sku))
        {
            var existing = Lines.FirstOrDefault(l =>
                !l.IsEntryRow
                && string.Equals((l.ProductCode ?? "").Trim(), sku, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Qty += 1;
                ClearEntryRowProductCode();
                EnsureEntryRow();
                RequestFocusEntryProductCode?.Invoke();
                return;
            }
        }

        var entry = GetEntryRow();
        if (entry != null)
        {
            FillLineFromCatalog(entry, p);
            entry.IsIgst = IsInterState;
            EnsureEntryRow();
            RequestFocusEntryProductCode?.Invoke();
            return;
        }

        var line = new BillingLineItem { IsIgst = IsInterState };
        FillLineFromCatalog(line, p);
        Lines.Add(line);
        EnsureEntryRow();
        RequestFocusEntryProductCode?.Invoke();
    }

    [RelayCommand]
    private async Task AddManualLineAsync(CancellationToken ct = default)
    {
        var dlg = new AddProductCodeDialog
        {
            Owner = Application.Current.MainWindow
        };
        if (dlg.ShowDialog() != true)
            return;

        await CommitProductCodeInputAsync(dlg.ProductCode, ct);
    }

    [RelayCommand]
    private void ImportCsv()
    {
        MessageBox.Show("CSV import is not implemented yet.", "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void RemoveLine(BillingLineItem? line)
    {
        if (line == null || line.IsEntryRow)
            return;
        Lines.Remove(line);
        EnsureEntryRow();
    }

    [RelayCommand]
    private void ClearForNewBill()
    {
        ClearCustomerProfile();
        AssignNewBillIdentity();
        ApplyLoggedInSalesman();
        Lines.Clear();
        _suppressDiscountTextSync = true;
        ItemDiscountPercentText = "";
        ItemDiscountPercent = 0;
        CashDiscAmountText = "";
        _cashDiscTarget = 0;
        RoundOffText = "";
        RoundOff = 0;
        _roundOffUserEdited = false;
        _suppressDiscountTextSync = false;
        IsInterState = false;
        SearchText = "";
        EnsureEntryRow();
        NotifyPostBillCanExecute();
        RequestFocusEntryProductCode?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanPostBill))]
    private async Task PostBill()
    {
        if (!CanPostBill())
        {
            MessageBox.Show(
                "Enter customer name and a valid 10-digit mobile number before posting the bill.",
                "RR Bridal Billing",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!Lines.Any(l => l.Amount > 0))
        {
            MessageBox.Show(
                "Add at least one line with quantity × rate (use product search or Add manual).",
                "RR Bridal Billing",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        foreach (var line in Lines.Where(l => l.Amount > 0 && !string.IsNullOrWhiteSpace(l.ProductCode)))
        {
            var available = await _services.ProductCatalog.GetAvailableStockAsync(line.CentralProductId, line.ProductCode);
            if (available < line.Qty || available < 1)
            {
                await AddIndentRequestAsync(line.ProductCode, line.Description, line.CentralProductId, ct: default);
                MessageBox.Show(
                    $"Product \"{line.Description}\" (SKU: {line.ProductCode}) has only {available:N2} available locally.\nA reference indent request has been created.",
                    "RR Bridal Billing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        RecalculateTotals();
        var totals = _lastBillTotals;

        var paymentVm = new PaymentDialogViewModel(_services.PaymentRouter, BillNo, totals.Payable);
        var paymentDlg = new PaymentDialog(paymentVm) { Owner = Application.Current.MainWindow };
        var paymentResult = paymentDlg.ShowDialog();

        if (paymentResult != true || !paymentVm.Outcome.Confirmed)
            return;

        try
        {
            var coll = _services.LocalDb.GetCollection<BsonDocument>("store_bills");
            var linesArr = new BsonArray();
            foreach (var line in Lines.Where(l => l.Amount > 0))
            {
                linesArr.Add(new BsonDocument
                {
                    { "lineNo", line.LineNo },
                    { "centralProductId", line.CentralProductId ?? "" },
                    { "sku", line.ProductCode },
                    { "description", line.Description },
                    { "hsn", line.HsnCode ?? "" },
                    { "qty", (double)line.Qty },
                    { "rate", (double)line.Rate },
                    { "amount", (double)line.Amount },
                    { "discountAmount", (double)line.DiscountAmount },
                    { "cashDiscountAmount", (double)line.CashDiscountAmount },
                    { "originalTaxAmount", (double)line.OriginalTaxAmount },
                    { "revisedAmount", (double)line.RevisedAmount },
                    { "revisedInclusiveAmount", (double)line.RevisedInclusiveAmount },
                    { "revisedTaxAmount", (double)line.RevisedTaxAmount },
                    { "mrp", (double)line.Mrp },
                    { "taxPercent", (double)line.TaxPercent },
                    { "cgstPercent", (double)line.CgstPercent },
                    { "sgstPercent", (double)line.SgstPercent },
                    { "igstPercent", (double)line.IgstPercent },
                    { "cgstAmount", (double)line.CgstAmount },
                    { "sgstAmount", (double)line.SgstAmount },
                    { "igstAmount", (double)line.IgstAmount },
                    { "taxAmount", (double)line.TaxAmount },
                });
            }

            var paymentsArr = new BsonArray();
            foreach (var leg in paymentVm.Outcome.Legs)
            {
                paymentsArr.Add(new BsonDocument
                {
                    { "provider", leg.Provider.ToString() },
                    { "amount", (double)leg.Amount },
                    { "reference", leg.Reference },
                    { "status", leg.Status },
                });
            }

            var storeId = _services.StoreContext.StoreId;
            var deviceId = _services.StoreContext.DeviceId;
            var posCounter = _services.StoreContext.PosCounter;

            var doc = new BsonDocument
            {
                { "billNo", BillNo },
                { "billDate", BillDateDisplay },
                { "storeId", storeId },
                { "deviceId", deviceId },
                { "posCounter", posCounter },
                { "customerCode", CustomerCode },
                { "customerName", CustomerName },
                { "customerPhone", CustomerPhone },
                { "salesman", Salesman },
                { "doorNo", "" },
                { "street", "" },
                { "fullAddress", "" },
                { "holdBills", HoldBills },
                { "doorDelivery", DoorDelivery },
                { "printInvoice", PrintInvoice },
                { "isInterState", IsInterState },
                { "itemDiscountPercent", (double)ItemDiscountPercent },
                { "itemDiscount", (double)totals.ItemDiscount },
                { "cashDiscAmount", (double)totals.CashDiscount },
                { "roundOff", (double)totals.RoundOff },
                { "subTotal", (double)totals.SubTotal },
                { "originalInclusiveTotal", (double)totals.OriginalInclusiveTotal },
                { "originalTaxTotal", (double)totals.OriginalTaxTotal },
                { "revisedSubTotal", (double)totals.RevisedSubTotal },
                { "cgstTotal", (double)totals.Cgst },
                { "sgstTotal", (double)totals.Sgst },
                { "igstTotal", (double)totals.Igst },
                { "taxTotal", (double)totals.TaxTotal },
                { "payable", (double)totals.Payable },
                { "lines", linesArr },
                { "payments", paymentsArr },
                { "paymentMode", paymentVm.SelectedMode.ToString() },
                { "status", "posted" },
                { "createdAtUtc", DateTime.UtcNow.ToString("O") },
            };

            if (!string.IsNullOrEmpty(_resumingDraftBillNo))
            {
                await coll.DeleteOneAsync(Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("storeId", storeId),
                    Builders<BsonDocument>.Filter.Eq("billNo", _resumingDraftBillNo),
                    Builders<BsonDocument>.Filter.Eq("status", "draft")));
            }

            await coll.InsertOneAsync(doc);
            _resumingDraftBillNo = null;

            foreach (var line in Lines.Where(l => l.Amount > 0 && !string.IsNullOrWhiteSpace(l.CentralProductId)))
                await _services.ProductCatalog.DecrementStockAsync(line.CentralProductId!, line.Qty, ct: default);

            ThermalInvoiceInput? printInput = null;
            if (PrintInvoice)
                printInput = BuildThermalInput(paymentVm.Outcome);

            ClearForNewBill();

            if (printInput != null)
                await ShowInvoicePrintDialogAsync(paymentVm.Outcome, printInvoiceEnabled: true, prebuiltInput: printInput, clearBillingAfterPrint: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save bill: {ex.Message}", "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ShowInvoicePrintDialogAsync(
        PaymentOutcome? paymentOutcome,
        bool printInvoiceEnabled,
        ThermalInvoiceInput? prebuiltInput = null,
        bool clearBillingAfterPrint = true)
    {
        var input = prebuiltInput ?? BuildThermalInput(paymentOutcome);
        var printed = await InvoicePrintFlow.ShowAsync(_services, input, printInvoiceEnabled);
        if (!printed)
            return;

        if (clearBillingAfterPrint)
            ClearForNewBill();

        RequestFocusEntryProductCode?.Invoke();
        if (RequestFocusEntryProductCode == null)
            _services.FocusSearch?.FocusBillingProductSearch();
    }

    [RelayCommand]
    private static void ShowHelp()
    {
        MessageBox.Show(
            "Store billing flow:\n" +
            "• Settings (gear): login to central, Run sync once — fills local product cache.\n" +
            "• Barcode labels: store the printed code in central product field barcode (or sku), then sync — scan into last row Product code.\n" +
            "• Last row Product code — SKU/barcode adds directly; product name opens pick list. Enter → next row.\n" +
            "• F3 — focus entry row Product code; toolbar search uses same rules.\n" +
            "• F2 — new bill (clears lines).\n" +
            "• Add manual — same as typing product code in the last grid row.\n" +
            "• F8 — hold bill (save draft without payment or stock change).\n" +
            "• Resume held — open held bills from billing screen.\n" +
            "• F9 — post bill (saves to local store_bills in Mongo).\n" +
            "• F10 — invoice preview / print (thermal format).\n" +
            "• F11 — duplicate bill (reprint posted invoice).\n" +
            "• Multi-counter: same STORE_ID + STORE_MONGO_URI on LAN; unique DEVICE_ID and POS_COUNTER per till (see deploy/env.counter-*.example).\n" +
            "• Set STORE_ID, DEVICE_ID, POS_COUNTER and STORE_MONGO_URI in .env.\n" +
            "• F12 — exit.",
            "RR Bridal Billing",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task PrintStub()
    {
        if (!Lines.Any(l => l.Amount > 0))
        {
            MessageBox.Show(
                "Add at least one line with quantity × rate before printing.",
                "RR Bridal Billing",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        await ShowInvoicePrintDialogAsync(paymentOutcome: null, printInvoiceEnabled: PrintInvoice, clearBillingAfterPrint: true);
    }

    private ThermalInvoiceInput BuildThermalInput(PaymentOutcome? paymentOutcome)
    {
        RecalculateTotals();

        var store = _services.ReceiptConfig.Current.Store;
        var print = _services.ReceiptConfig.Current.Print;
        var active = Lines.Where(l => l.Amount > 0).ToList();
        var snaps = active
            .Select(l =>
            {
                var lineDisc = l.DiscountAmount + l.CashDiscountAmount;
                return new InvoiceLineSnap
                {
                    LineNo = l.LineNo,
                    Description = l.Description,
                    Hsn = l.HsnCode ?? "",
                    TaxPercent = l.TaxPercent,
                    Qty = l.Qty,
                    Rate = l.Rate,
                    Mrp = l.Mrp,
                    Amount = l.Amount,
                    LineDiscount = lineDisc,
                    TaxableAmount = l.RevisedAmount,
                    TaxAmount = l.RevisedTaxAmount,
                };
            })
            .ToList();

        var totals = _lastBillTotals;
        var totalQty = active.Sum(l => l.Qty);
        var totalMrp = active.Sum(l => l.Mrp * l.Qty);
        var totalLineAmount = active.Sum(l => l.Amount);
        var totalTaxable = active.Sum(l => l.RevisedAmount);
        var savings = active.Sum(l => Math.Max(0m, l.Mrp * l.Qty - l.Amount));

        PaymentReceiptSnap? paySnap = null;
        if (paymentOutcome is { Confirmed: true })
        {
            var legs = paymentOutcome.Legs.Select(l => (l.Provider, l.Amount)).ToList();
            paySnap = PaymentReceiptSnap.FromLegs(
                legs,
                paymentOutcome.Mode.ToString(),
                paymentOutcome.CashReceived,
                paymentOutcome.ChangeReturned);
        }
        else if (paymentOutcome == null)
            paySnap = PaymentReceiptSnap.Preview();

        return new ThermalInvoiceInput
        {
            Store = store,
            CharWidth = print.ReceiptCharWidth is >= 32 and <= 56 ? print.ReceiptCharWidth : 48,
            BillNo = BillNo,
            BillDate = BillDateDisplay,
            UserName = Salesman,
            Time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            Counter = _services.StoreContext.PosCounter,
            CustomerName = CustomerName ?? "",
            CustomerPhone = CustomerPhone ?? "",
            Lines = snaps,
            SubTotal = totals.SubTotal,
            OriginalTaxTotal = totals.OriginalTaxTotal,
            RevisedSubTotal = totals.RevisedSubTotal,
            TaxTotal = totals.TaxTotal,
            IsInterState = IsInterState,
            CgstTotal = totals.Cgst,
            SgstTotal = totals.Sgst,
            IgstTotal = totals.Igst,
            ItemDiscount = totals.ItemDiscount,
            CashDiscAmount = totals.CashDiscount,
            RoundOff = totals.RoundOff,
            Payable = totals.Payable,
            TotalQty = totalQty,
            ItemCount = active.Count,
            TotalMrp = totalMrp,
            TotalLineAmount = totalLineAmount,
            TotalTaxableAmount = totalTaxable,
            Savings = savings,
            Payments = paySnap,
        };
    }

    [RelayCommand]
    private async Task HoldBill()
    {
        if (!Lines.Any(l => l.Amount > 0))
        {
            MessageBox.Show("Add at least one line before holding the bill.", "Hold bill",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        RecalculateTotals();
        if (string.IsNullOrWhiteSpace(BillNo))
            await AssignBillNumberAsync();

        try
        {
            var coll = _services.LocalDb.GetCollection<BsonDocument>("store_bills");
            var doc = BuildBillBsonDocument("draft", payments: null, paymentMode: "");
            var storeId = _services.StoreContext.StoreId;
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", storeId),
                Builders<BsonDocument>.Filter.Eq("billNo", BillNo),
                Builders<BsonDocument>.Filter.Eq("status", "draft"));

            await coll.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
            _resumingDraftBillNo = BillNo;
            MessageBox.Show($"Bill {BillNo} held. Use Resume held bills to continue.", "Hold bill",
                MessageBoxButton.OK, MessageBoxImage.Information);
            ClearForNewBill();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not hold bill: {ex.Message}", "Hold bill", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ResumeHeldBills()
    {
        try
        {
            var rows = await _services.BillDocuments.ListDraftsAsync();
            var dlg = new HeldBillsDialog(rows) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true)
                return;

            if (dlg.DeleteRequested && dlg.SelectedRow != null)
            {
                await _services.BillDocuments.DeleteDraftAsync(dlg.SelectedRow.BillNo);
                MessageBox.Show("Held bill deleted.", "Hold bill", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!dlg.ResumeRequested || dlg.SelectedRow == null)
                return;

            var doc = await _services.BillDocuments.GetByBillNoAsync(dlg.SelectedRow.BillNo);
            if (doc == null)
            {
                MessageBox.Show("Held bill not found.", "Hold bill", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadFromDraftDocument(doc);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open held bills: {ex.Message}", "Hold bill", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void LoadFromDraftDocument(BsonDocument doc)
    {
        ClearForNewBill();
        _resumingDraftBillNo = doc.GetValue("billNo", "").AsString;
        BillNo = _resumingDraftBillNo;
        BillDateDisplay = doc.GetValue("billDate", BillDateDisplay).AsString;
        CustomerCode = doc.GetValue("customerCode", "").AsString;
        CustomerName = doc.GetValue("customerName", "").AsString;
        CustomerPhone = doc.GetValue("customerPhone", "").AsString;
        Salesman = doc.GetValue("salesman", "").AsString;
        HoldBills = doc.GetValue("holdBills", false).AsBoolean;
        DoorDelivery = doc.GetValue("doorDelivery", false).AsBoolean;
        PrintInvoice = doc.GetValue("printInvoice", true).AsBoolean;
        IsInterState = doc.Contains("isInterState") && doc["isInterState"].AsBoolean;
        ItemDiscountPercent = (decimal)doc.GetValue("itemDiscountPercent", 0).ToDouble();
        CashDiscAmountText = doc.GetValue("cashDiscAmount", 0).ToDouble().ToString("0.##", InCulture);

        if (doc.TryGetValue("lines", out var linesVal) && linesVal.IsBsonArray)
        {
            foreach (BsonDocument lineBson in linesVal.AsBsonArray.OfType<BsonDocument>())
            {
                var line = new BillingLineItem
                {
                    LineNo = lineBson.GetValue("lineNo", 0).ToInt32(),
                    CentralProductId = lineBson.GetValue("centralProductId", "").AsString,
                    ProductCode = lineBson.GetValue("sku", "").AsString,
                    Description = lineBson.GetValue("description", "").AsString,
                    HsnCode = lineBson.GetValue("hsn", "").AsString,
                    Qty = (decimal)lineBson.GetValue("qty", 0).ToDouble(),
                    Rate = (decimal)lineBson.GetValue("rate", 0).ToDouble(),
                    Mrp = (decimal)lineBson.GetValue("mrp", 0).ToDouble(),
                    TaxPercent = (decimal)lineBson.GetValue("taxPercent", 0).ToDouble(),
                    IsIgst = IsInterState,
                };
                Lines.Add(line);
            }
        }

        EnsureEntryRow();
        RecalculateTotals();
        NotifyPostBillCanExecute();
    }

    private BsonDocument BuildBillBsonDocument(string status, BsonArray? payments, string paymentMode)
    {
        RecalculateTotals();
        var totals = _lastBillTotals;
        var linesArr = new BsonArray();
        foreach (var line in Lines.Where(l => l.Amount > 0))
        {
            linesArr.Add(new BsonDocument
            {
                { "lineNo", line.LineNo },
                { "centralProductId", line.CentralProductId ?? "" },
                { "sku", line.ProductCode },
                { "description", line.Description },
                { "hsn", line.HsnCode ?? "" },
                { "qty", (double)line.Qty },
                { "rate", (double)line.Rate },
                { "amount", (double)line.Amount },
                { "discountAmount", (double)line.DiscountAmount },
                { "cashDiscountAmount", (double)line.CashDiscountAmount },
                { "originalTaxAmount", (double)line.OriginalTaxAmount },
                { "revisedAmount", (double)line.RevisedAmount },
                { "revisedInclusiveAmount", (double)line.RevisedInclusiveAmount },
                { "revisedTaxAmount", (double)line.RevisedTaxAmount },
                { "mrp", (double)line.Mrp },
                { "taxPercent", (double)line.TaxPercent },
                { "cgstPercent", (double)line.CgstPercent },
                { "sgstPercent", (double)line.SgstPercent },
                { "igstPercent", (double)line.IgstPercent },
                { "cgstAmount", (double)line.CgstAmount },
                { "sgstAmount", (double)line.SgstAmount },
                { "igstAmount", (double)line.IgstAmount },
                { "taxAmount", (double)line.TaxAmount },
            });
        }

        return new BsonDocument
        {
            { "billNo", BillNo.Trim() },
            { "billDate", BillDateDisplay },
            { "storeId", _services.StoreContext.StoreId },
            { "deviceId", _services.StoreContext.DeviceId },
            { "posCounter", _services.StoreContext.PosCounter },
            { "customerCode", CustomerCode },
            { "customerName", CustomerName },
            { "customerPhone", CustomerPhone },
            { "salesman", Salesman },
            { "doorNo", "" },
            { "street", "" },
            { "fullAddress", "" },
            { "holdBills", HoldBills },
            { "doorDelivery", DoorDelivery },
            { "printInvoice", PrintInvoice },
            { "isInterState", IsInterState },
            { "itemDiscountPercent", (double)ItemDiscountPercent },
            { "itemDiscount", (double)totals.ItemDiscount },
            { "cashDiscAmount", (double)totals.CashDiscount },
            { "roundOff", (double)totals.RoundOff },
            { "subTotal", (double)totals.SubTotal },
            { "originalInclusiveTotal", (double)totals.OriginalInclusiveTotal },
            { "originalTaxTotal", (double)totals.OriginalTaxTotal },
            { "revisedSubTotal", (double)totals.RevisedSubTotal },
            { "cgstTotal", (double)totals.Cgst },
            { "sgstTotal", (double)totals.Sgst },
            { "igstTotal", (double)totals.Igst },
            { "taxTotal", (double)totals.TaxTotal },
            { "payable", (double)totals.Payable },
            { "lines", linesArr },
            { "payments", payments ?? new BsonArray() },
            { "paymentMode", paymentMode },
            { "status", status },
            { "createdAtUtc", DateTime.UtcNow.ToString("O") },
        };
    }

    [RelayCommand]
    private static void CloseApp()
    {
        Application.Current.Shutdown();
    }
}
