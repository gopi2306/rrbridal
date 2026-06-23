using System;
using System.Collections.Generic;
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
using RRBridal.StoreBilling.App.Services.Audit;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Billing.Promotions;
using RRBridal.StoreBilling.App.Services.Customers;
using RRBridal.StoreBilling.App.Services.WhatsApp;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Payments;
using RRBridal.StoreBilling.App.Services.Products;
using RRBridal.StoreBilling.App.Services.PurchaseIntents;
using RRBridal.StoreBilling.App.Services.Store;
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
    [ObservableProperty] private bool _onlineCodOrder;
    [ObservableProperty] private bool _stitching;
    [ObservableProperty] private bool _printInvoice = true;
    [ObservableProperty] private DateTime? _deliveryDate;

    partial void OnStitchingChanged(bool value)
    {
        if (!value)
            DeliveryDate = null;
    }

    [ObservableProperty] private string _billNo = "—";
    [ObservableProperty] private string _holdNo = "";
    [ObservableProperty] private string _draftLabel = "DRAFT";
    [ObservableProperty] private string _billDateDisplay = "";

    [ObservableProperty] private string _subTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _originalTaxTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _revisedSubTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _taxTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _grossTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _itemDiscountFormatted = "₹ 0.00";
    [ObservableProperty] private string _cashDiscAmountFormatted = "₹ 0.00";
    [ObservableProperty] private string _schemeDiscountFormatted = "₹ 0.00";
    [ObservableProperty] private string _alterationTotalFormatted = "₹ 0.00";
    [ObservableProperty] private bool _hasAppliedSchemes;
    [ObservableProperty] private string _roundOffFormatted = "₹ 0.00";
    [ObservableProperty] private string _payableTotalFormatted = "₹ 0.00";

    [ObservableProperty] private string _totalLineQtyFormatted = "0";
    [ObservableProperty] private string _payableBeforeCreditFormatted = "₹ 0.00";
    [ObservableProperty] private string _creditAppliedFormatted = "₹ 0.00";
    [ObservableProperty] private bool _hasAvailableCredit;
    [ObservableProperty] private bool _hasAppliedCredit;
    [ObservableProperty] private decimal _appliedCreditAmount;
    [ObservableProperty] private string _selectedCreditNoteLabel = "";
    [ObservableProperty] private string _originalCreditBalanceFormatted = "";
    [ObservableProperty] private string _remainingAfterCreditFormatted = "";

    public ObservableCollection<CustomerCreditNoteOption> AvailableCreditNotes { get; } = new();

    private string? _selectedCreditNoteNo;

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

    /// <summary>Total automatic scheme discount on lines (₹).</summary>
    public decimal SchemeLineDiscount => Lines.Sum(l => l.SchemeDiscountAmount);

    /// <summary>Sum of per-line alteration amounts (₹).</summary>
    public decimal AlterationTotal => Lines.Sum(l => l.AlterationAmount);

    public ObservableCollection<AppliedSchemeDisplayItem> AppliedSchemes { get; } = new();

    private readonly HashSet<string> _excludedSchemeCodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly PromotionEngine _promotionEngine;
    private decimal _schemeBillDiscount;

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
    private bool _alterationGstIncluded;
    private string? _activeHoldNo;
    private BillTotals _lastBillTotals = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private enum ManualDiscountEditSource { None, ItemPercent, CashAmount }

    private ManualDiscountEditSource _lastManualDiscountEdit = ManualDiscountEditSource.None;
    private decimal _lastValidItemDiscountPercent;
    private decimal _lastValidCashDiscTarget;

    /// <summary>From logged-in user (synced from central). 100 = no cap.</summary>
    public decimal MaxDiscountPercent => _services.UserSession?.LoggedInUser.MaxDiscountPercent ?? 100m;

    public bool ShowMaxDiscountHint => MaxDiscountPercent < 100m;

    public string MaxDiscountPercentHint => $"Max manual discount: {MaxDiscountPercent:0.##}%";

    public ObservableCollection<BillingLineItem> Lines { get; } = new();

    public BillingViewModel(AppServices services)
    {
        _services = services;
        _customerLookup = new CustomerLookupService(services.LocalDb, services.CentralApi);
        _customerRegistration = new CustomerRegistrationService(services.LocalDb, services.CentralApi, services.StoreContext);
        _customerCodeGenerator = new CustomerCodeGenerator(services.LocalDb);
        _promotionEngine = new PromotionEngine(new PromotionSchemeRepository(services.LocalDb));
        RefreshAlterationGstIncludedFromSettings();
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
        _activeHoldNo = null;
        HoldNo = "";
        DraftLabel = "DRAFT";
        BillDateDisplay = DateTime.Now.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
        BillNo = "—";
    }

    private async Task AssignBillNumberAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            BillNo = await _services.BillNumberGenerator.NextBillAsync(cts.Token);
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
        if (e.PropertyName is nameof(BillingLineItem.Rate) && sender is BillingLineItem rateLine)
            WarnIfBelowMargin(rateLine);

        if (e.PropertyName is nameof(BillingLineItem.Amount)
            or nameof(BillingLineItem.Qty)
            or nameof(BillingLineItem.Rate)
            or nameof(BillingLineItem.TaxPercent)
            or nameof(BillingLineItem.TaxAmount)
            or nameof(BillingLineItem.DiscountAmount)
            or nameof(BillingLineItem.CashDiscountAmount)
            or nameof(BillingLineItem.SchemeDiscountAmount)
            or nameof(BillingLineItem.AlterationAmount)
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
        line.OriginalInclusiveAmount > 0 ? line.OriginalInclusiveAmount : line.Amount;

    private List<(BillingLineItem Line, decimal OriginalInclusive)> BuildDiscountSnapshots() =>
        Lines
            .Where(l => l.Amount > 0)
            .Select(l => (Line: l, OriginalInclusive: LineOriginalInclusive(l)))
            .ToList();

    private decimal ComputeItemPercentForDiscountAmount(
        IReadOnlyList<(BillingLineItem Line, decimal OriginalInclusive)> snapshots,
        decimal targetItemDiscountRs)
    {
        var totalInclusive = snapshots
            .Where(s => s.Line.Amount > 0)
            .Sum(s => Math.Max(0m, s.OriginalInclusive - s.Line.SchemeDiscountAmount));
        if (totalInclusive <= 0 || targetItemDiscountRs <= 0)
            return 0;
        return Math.Clamp(targetItemDiscountRs / totalInclusive * 100m, 0, 100);
    }

    private void SyncDiscountTextFromState()
    {
        _suppressDiscountTextSync = true;
        ItemDiscountPercentText = ItemDiscountPercent > 0 ? FormatEditableDecimalText(ItemDiscountPercent) : "";
        CashDiscAmountText = _cashDiscTarget > 0 ? FormatEditableDecimalText(_cashDiscTarget) : "";
        _suppressDiscountTextSync = false;
    }

    /// <summary>Clamp manual discounts to user max % (item + cash vs base after schemes).</summary>
    private bool EnforceManualDiscountCap(bool showMessage)
    {
        var snapshots = BuildDiscountSnapshots();
        var manualBase = BillingDiscountCalculator.ComputeManualDiscountBase(
            snapshots.Select(s => (s.OriginalInclusive, s.Line.SchemeDiscountAmount)));
        if (manualBase <= 0)
        {
            _lastValidItemDiscountPercent = ItemDiscountPercent;
            _lastValidCashDiscTarget = _cashDiscTarget;
            return false;
        }

        var maxPct = MaxDiscountPercent;
        var itemDisc = ItemDiscount;
        var cashDisc = CashDiscAmount;
        if (BillingDiscountCalculator.IsWithinMaxManualDiscount(manualBase, itemDisc, cashDisc, maxPct))
        {
            _lastValidItemDiscountPercent = ItemDiscountPercent;
            _lastValidCashDiscTarget = _cashDiscTarget;
            return false;
        }

        var maxRs = MoneyMath.RoundAmount(manualBase * maxPct / 100m);
        var edited = _lastManualDiscountEdit;
        var changed = false;

        if (edited == ManualDiscountEditSource.CashAmount)
        {
            var allowedItem = Math.Max(0m, maxRs - cashDisc);
            var newPct = ComputeItemPercentForDiscountAmount(snapshots, allowedItem);
            if (newPct != ItemDiscountPercent)
            {
                ItemDiscountPercent = newPct;
                changed = true;
            }

            ApplyProportionalItemDiscount(snapshots);
            itemDisc = ItemDiscount;
            var allowedCash = Math.Max(0m, maxRs - itemDisc);
            if (_cashDiscTarget > allowedCash)
            {
                _cashDiscTarget = allowedCash;
                changed = true;
            }
        }
        else
        {
            itemDisc = ItemDiscount;
            var allowedCash = Math.Max(0m, maxRs - itemDisc);
            if (_cashDiscTarget > allowedCash)
            {
                _cashDiscTarget = allowedCash;
                changed = true;
            }

            ApplyProportionalCashDiscount(snapshots);
            cashDisc = CashDiscAmount;
            if (itemDisc + cashDisc > maxRs + 0.01m)
            {
                _cashDiscTarget = 0;
                ApplyProportionalCashDiscount(snapshots);
                var allowedItem = maxRs;
                ItemDiscountPercent = ComputeItemPercentForDiscountAmount(snapshots, allowedItem);
                ApplyProportionalItemDiscount(snapshots);
                changed = true;
            }
        }

        _lastValidItemDiscountPercent = ItemDiscountPercent;
        _lastValidCashDiscTarget = _cashDiscTarget;

        if (changed)
        {
            SyncDiscountTextFromState();
            if (showMessage)
            {
                MessageBox.Show(
                    $"Discount cannot exceed {maxPct:0.##}% for your user account.",
                    "RR Bridal Billing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        return changed;
    }

    private bool IsManualDiscountWithinCap()
    {
        var snapshots = BuildDiscountSnapshots();
        var manualBase = BillingDiscountCalculator.ComputeManualDiscountBase(
            snapshots.Select(s => (s.OriginalInclusive, s.Line.SchemeDiscountAmount)));
        if (manualBase <= 0)
            return true;
        return BillingDiscountCalculator.IsWithinMaxManualDiscount(
            manualBase, ItemDiscount, CashDiscAmount, MaxDiscountPercent);
    }

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

        var totalInclusive = active.Sum(s => Math.Max(0m, s.OriginalInclusive - s.Line.SchemeDiscountAmount));
        if (totalInclusive <= 0)
        {
            foreach (var line in Lines)
                line.DiscountAmount = 0;
            return;
        }

        var totalDisc = MoneyMath.RoundAmount(totalInclusive * ItemDiscountPercent / 100m);
        var allocated = 0m;
        for (var i = 0; i < active.Count; i++)
        {
            var (line, originalInclusive) = active[i];
            var baseInc = Math.Max(0m, originalInclusive - line.SchemeDiscountAmount);
            if (i == active.Count - 1)
                line.DiscountAmount = Math.Max(0m, totalDisc - allocated);
            else
            {
                var share = MoneyMath.RoundAmount(baseInc / totalInclusive * totalDisc);
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
            .Select(s => (s.Line, InclusiveAfterItem: Math.Max(0m, s.OriginalInclusive - s.Line.SchemeDiscountAmount - s.Line.DiscountAmount)))
            .Where(x => x.InclusiveAfterItem > 0)
            .ToList();

        if (active.Count == 0)
        {
            foreach (var line in Lines)
                line.CashDiscountAmount = 0;
            return;
        }

        var totalBase = active.Sum(x => x.InclusiveAfterItem);
        var totalCash = MoneyMath.RoundAmount(_cashDiscTarget);
        var allocated = 0m;
        for (var i = 0; i < active.Count; i++)
        {
            var (line, inclusiveAfterItem) = active[i];
            if (i == active.Count - 1)
                line.CashDiscountAmount = Math.Max(0m, totalCash - allocated);
            else
            {
                var share = MoneyMath.RoundAmount(inclusiveAfterItem / totalBase * totalCash);
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
        MoneyMath.FormatEditableAmount(value);

    private sealed record BillTotals(
        decimal SubTotal,
        decimal OriginalInclusiveTotal,
        decimal SchemeLineDiscount,
        decimal SchemeBillDiscount,
        decimal ItemDiscount,
        decimal CashDiscount,
        decimal OriginalTaxTotal,
        decimal RevisedSubTotal,
        decimal Cgst,
        decimal Sgst,
        decimal Igst,
        decimal TaxTotal,
        decimal GrandBeforeRound,
        decimal AlterationTotal,
        decimal RoundOff,
        decimal PayableBeforeCredit,
        decimal AppliedCredit,
        decimal Payable);

    private void RefreshAlterationGstIncludedFromSettings()
    {
        _services.PosBillingSettings.Load();
        _alterationGstIncluded = _services.PosBillingSettings.Current.AlterationGstIncluded;
    }

    private void ApplyAlterationToTotals(
        bool gstIncluded,
        ref decimal revisedSub,
        ref decimal cgst,
        ref decimal sgst,
        ref decimal igst,
        ref decimal grandBeforeRound)
    {
        var lines = Lines
            .Where(l => l.AlterationAmount > 0)
            .Select(l => new AlterationBillMath.AlterationLine(l.AlterationAmount, l.TaxPercent, l.IsIgst))
            .ToList();
        AlterationBillMath.Apply(gstIncluded, lines, ref revisedSub, ref cgst, ref sgst, ref igst, ref grandBeforeRound);
    }

    private void EvaluatePromotions()
    {
        foreach (var line in Lines)
            line.SchemeDiscountAmount = 0;
        _schemeBillDiscount = 0;
        AppliedSchemes.Clear();

        var billLines = Lines.Where(l => l.Amount > 0 && !l.IsEntryRow).ToList();
        if (billLines.Count == 0)
        {
            HasAppliedSchemes = false;
            return;
        }

        var context = new BillContext
        {
            Lines = billLines.Select(l => new BillLineContext
            {
                LineNo = l.LineNo,
                Sku = l.ProductCode,
                CategoryId = string.IsNullOrWhiteSpace(l.CategoryId) ? null : l.CategoryId,
                BrandId = string.IsNullOrWhiteSpace(l.BrandId) ? null : l.BrandId,
                OfferGroupId = string.IsNullOrWhiteSpace(l.OfferGroupId) ? null : l.OfferGroupId,
                Qty = l.Qty,
                Rate = l.Rate,
                Amount = l.Amount,
                TaxPercent = l.TaxPercent,
                IsIgst = l.IsIgst,
                OriginalInclusive = LineOriginalInclusive(l),
            }).ToList(),
            CustomerCode = CustomerCode,
            StoreId = _services.StoreContext.StoreId,
            BillDateTime = DateTime.Now,
            Subtotal = billLines.Sum(l => l.Amount),
            InclusiveTotal = billLines.Sum(LineOriginalInclusive),
            ExcludedSchemeCodes = _excludedSchemeCodes,
        };

        var result = _promotionEngine.Evaluate(context);
        foreach (var adj in result.LineAdjustments)
        {
            var line = Lines.FirstOrDefault(l => l.LineNo == adj.LineNo);
            if (line != null)
                line.SchemeDiscountAmount = adj.SchemeDiscountAmount;
        }

        _schemeBillDiscount = result.BillAdjustment;
        if (_schemeBillDiscount > 0)
            ApplyProportionalBillSchemeDiscount(billLines, _schemeBillDiscount);

        foreach (var scheme in result.AppliedSchemes)
        {
            AppliedSchemes.Add(new AppliedSchemeDisplayItem
            {
                SchemeCode = scheme.SchemeCode,
                SchemeName = scheme.SchemeName,
                SavedAmount = scheme.SavedAmount,
            });
        }

        HasAppliedSchemes = AppliedSchemes.Count > 0;
    }

    private void ApplyProportionalBillSchemeDiscount(IReadOnlyList<BillingLineItem> lines, decimal totalBillScheme)
    {
        var active = lines
            .Select(l => (Line: l, Base: Math.Max(0m, LineOriginalInclusive(l) - l.SchemeDiscountAmount)))
            .Where(x => x.Base > 0)
            .ToList();
        if (active.Count == 0) return;

        var baseTotal = active.Sum(x => x.Base);
        var allocated = 0m;
        for (var i = 0; i < active.Count; i++)
        {
            var (line, baseInc) = active[i];
            var share = i == active.Count - 1
                ? totalBillScheme - allocated
                : MoneyMath.RoundAmount(totalBillScheme * baseInc / baseTotal);
            line.SchemeDiscountAmount = MoneyMath.RoundAmount(line.SchemeDiscountAmount + share);
            allocated += share;
        }
    }

    [RelayCommand]
    private void RemoveAppliedScheme(AppliedSchemeDisplayItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.SchemeCode)) return;
        _excludedSchemeCodes.Add(item.SchemeCode.Trim());
        RecalculateTotals();
    }

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
        EvaluatePromotions();

        var snapshots = Lines
            .Where(l => l.Amount > 0)
            .Select(l => (Line: l, OriginalInclusive: LineOriginalInclusive(l)))
            .ToList();

        ApplyProportionalItemDiscount(snapshots);
        ApplyProportionalCashDiscount(snapshots);

        if (EnforceManualDiscountCap(showMessage: false))
        {
            snapshots = BuildDiscountSnapshots();
            ApplyProportionalItemDiscount(snapshots);
            ApplyProportionalCashDiscount(snapshots);
        }

        var sub = Lines.Sum(l => l.Amount);
        var originalInclusive = snapshots.Sum(s => s.OriginalInclusive);
        var schemeLine = SchemeLineDiscount;
        var schemeBill = _schemeBillDiscount;
        var itemDisc = ItemDiscount;
        var cashDisc = CashDiscAmount;
        var originalTax = Lines.Sum(l => l.OriginalTaxAmount);
        var revisedSub = Lines.Sum(l => l.RevisedAmount);
        var cgst = Lines.Sum(l => l.CgstAmount);
        var sgst = Lines.Sum(l => l.SgstAmount);
        var igst = Lines.Sum(l => l.IgstAmount);
        var grandBeforeRound = Lines.Sum(l => l.RevisedInclusiveAmount);
        var alterationTotal = AlterationTotal;
        ApplyAlterationToTotals(_alterationGstIncluded, ref revisedSub, ref cgst, ref sgst, ref igst, ref grandBeforeRound);
        var tax = cgst + sgst + igst;

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

        var payableBeforeCredit = grandBeforeRound + roundOff;
        return new BillTotals(
            sub, originalInclusive, schemeLine, schemeBill, itemDisc, cashDisc, originalTax, revisedSub,
            cgst, sgst, igst, tax, grandBeforeRound, alterationTotal, roundOff, payableBeforeCredit, 0, payableBeforeCredit);
    }

    private void RecalculateTotals()
    {
        if (_isComputingTotals) return;

        OnPropertyChanged(nameof(MaxDiscountPercent));
        OnPropertyChanged(nameof(ShowMaxDiscountHint));
        OnPropertyChanged(nameof(MaxDiscountPercentHint));

        var core = ComputeBillTotals();
        var payableBeforeCredit = core.PayableBeforeCredit;
        var appliedCredit = 0m;
        if (!string.IsNullOrEmpty(_selectedCreditNoteNo))
        {
            var selected = AvailableCreditNotes.FirstOrDefault(n => n.CreditNoteNo == _selectedCreditNoteNo);
            if (selected != null)
                appliedCredit = Math.Min(selected.RemainingAmount, payableBeforeCredit);
        }

        var payable = Math.Max(0, payableBeforeCredit - appliedCredit);
        _lastBillTotals = core with { AppliedCredit = appliedCredit, Payable = payable };

        SubTotalFormatted = MoneyMath.FormatRupee(_lastBillTotals.SubTotal);
        OriginalTaxTotalFormatted = MoneyMath.FormatRupee(_lastBillTotals.OriginalTaxTotal);
        RevisedSubTotalFormatted = MoneyMath.FormatRupee(_lastBillTotals.RevisedSubTotal);
        TaxTotalFormatted = MoneyMath.FormatRupee(_lastBillTotals.TaxTotal);
        CgstTotalFormatted = MoneyMath.FormatRupee(_lastBillTotals.Cgst);
        SgstTotalFormatted = MoneyMath.FormatRupee(_lastBillTotals.Sgst);
        IgstTotalFormatted = MoneyMath.FormatRupee(_lastBillTotals.Igst);
        GrossTotalFormatted = MoneyMath.FormatRupee(_lastBillTotals.OriginalInclusiveTotal);
        ItemDiscountFormatted = MoneyMath.FormatRupee(_lastBillTotals.ItemDiscount);
        CashDiscAmountFormatted = MoneyMath.FormatRupee(_lastBillTotals.CashDiscount);
        SchemeDiscountFormatted = MoneyMath.FormatRupee(_lastBillTotals.SchemeLineDiscount + _lastBillTotals.SchemeBillDiscount);
        AlterationTotalFormatted = MoneyMath.FormatRupee(_lastBillTotals.AlterationTotal);
        HasAppliedSchemes = AppliedSchemes.Count > 0;
        RoundOffFormatted = MoneyMath.FormatRupee(_lastBillTotals.RoundOff);
        PayableBeforeCreditFormatted = MoneyMath.FormatRupee(payableBeforeCredit);
        AppliedCreditAmount = appliedCredit;
        CreditAppliedFormatted = MoneyMath.FormatRupee(appliedCredit);
        HasAppliedCredit = appliedCredit > 0;
        PayableTotalFormatted = MoneyMath.FormatPayable(payable);

        var totalQty = Lines.Where(l => l.Amount > 0).Sum(l => l.Qty);
        TotalLineQtyFormatted = totalQty.ToString("0.###", InCulture);

        CustomerCreditNoteOption? selectedOption = null;
        foreach (var note in AvailableCreditNotes)
        {
            if (note.CreditNoteNo == _selectedCreditNoteNo)
            {
                note.ApplyingAmount = appliedCredit;
                note.RemainingAfterApply = note.RemainingAmount - appliedCredit;
                selectedOption = note;
            }
            else
            {
                note.ApplyingAmount = 0;
                note.RemainingAfterApply = note.RemainingAmount;
            }

            note.RefreshDisplayLabel();
        }

        SelectedCreditNoteLabel = selectedOption?.CreditNoteNo ?? "";
        OriginalCreditBalanceFormatted = selectedOption != null ? MoneyMath.FormatRupee(selectedOption.OriginalAmount) : "";
        RemainingAfterCreditFormatted = selectedOption != null ? MoneyMath.FormatRupee(selectedOption.RemainingAfterApply) : "";
    }

    private void ClearCreditSelection()
    {
        _selectedCreditNoteNo = null;
        foreach (var note in AvailableCreditNotes)
        {
            note.IsSelected = false;
            note.ApplyingAmount = 0;
            note.RemainingAfterApply = note.RemainingAmount;
            note.RefreshDisplayLabel();
        }

        SelectedCreditNoteLabel = "";
        OriginalCreditBalanceFormatted = "";
        RemainingAfterCreditFormatted = "";
    }

    private async Task RefreshCustomerCreditAsync()
    {
        ClearCreditSelection();
        AvailableCreditNotes.Clear();
        HasAvailableCredit = false;

        var phone = (CustomerPhone ?? "").Trim();
        var code = (CustomerCode ?? "").Trim();
        if (string.IsNullOrEmpty(phone) && string.IsNullOrEmpty(code))
            return;

        var storeId = _services.StoreContext.StoreId;
        var notes = await _services.CustomerCreditNotes.ListAvailableForCustomerAsync(storeId, code, phone);
        foreach (var note in notes)
            AvailableCreditNotes.Add(CustomerCreditNoteOption.FromRecord(note));

        HasAvailableCredit = AvailableCreditNotes.Count > 0;
    }

    [RelayCommand]
    private void ToggleCreditNote(CustomerCreditNoteOption? option)
    {
        if (option == null)
            return;

        if (_selectedCreditNoteNo == option.CreditNoteNo)
        {
            ClearCreditSelection();
        }
        else
        {
            _selectedCreditNoteNo = option.CreditNoteNo;
            foreach (var note in AvailableCreditNotes)
                note.IsSelected = note.CreditNoteNo == _selectedCreditNoteNo;
        }

        RecalculateTotals();
    }

    partial void OnItemDiscountPercentTextChanged(string value)
    {
        if (_suppressDiscountTextSync) return;

        if (string.IsNullOrWhiteSpace(value))
        {
            _lastManualDiscountEdit = ManualDiscountEditSource.ItemPercent;
            ItemDiscountPercent = 0;
            RecalculateTotals();
            return;
        }

        if (!TryParseDecimalInput(value, out var parsed))
            return;

        _lastManualDiscountEdit = ManualDiscountEditSource.ItemPercent;
        ItemDiscountPercent = Math.Clamp(parsed, 0, 100);
        RecalculateTotals();
        if (EnforceManualDiscountCap(showMessage: true))
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

        if (_lastManualDiscountEdit != ManualDiscountEditSource.ItemPercent)
            _lastManualDiscountEdit = ManualDiscountEditSource.ItemPercent;

        RecalculateTotals();
    }

    partial void OnCashDiscAmountTextChanged(string value)
    {
        if (_suppressDiscountTextSync) return;

        if (string.IsNullOrWhiteSpace(value))
        {
            _lastManualDiscountEdit = ManualDiscountEditSource.CashAmount;
            _cashDiscTarget = 0;
            RecalculateTotals();
            return;
        }

        if (!TryParseDecimalInput(value, out var parsed))
            return;

        _lastManualDiscountEdit = ManualDiscountEditSource.CashAmount;
        _cashDiscTarget = Math.Max(0m, parsed);
        RecalculateTotals();
        if (EnforceManualDiscountCap(showMessage: true))
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
        _ = RefreshCustomerCreditAsync();
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
        _ = RefreshCustomerCreditAsync();
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
        ClearCreditSelection();
        AvailableCreditNotes.Clear();
        HasAvailableCredit = false;
        RecalculateTotals();
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
        RecalculateTotals();
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
        line.CostPrice = p.CostPrice ?? 0;
        line.MarginPercent = p.MarginPercent ?? 0;
        line.Rate = p.SuggestedRate;
        line.Mrp = p.Mrp ?? 0;
        line.TaxPercent = p.SuggestedTaxPercent;
    }

    private void EnrichLineProductMetadata(BillingLineItem line)
    {
        if (line.IsEntryRow || string.IsNullOrWhiteSpace(line.ProductCode)) return;
        var products = _services.LocalDb.GetCollection<BsonDocument>("local_products_cache");
        var filter = !string.IsNullOrWhiteSpace(line.CentralProductId)
            ? Builders<BsonDocument>.Filter.Eq("centralProductId", line.CentralProductId)
            : Builders<BsonDocument>.Filter.Eq("sku", line.ProductCode.Trim());
        var doc = products.Find(filter).FirstOrDefault();
        if (doc == null) return;
        line.CategoryId = ReadProductRef(doc, "categoryId");
        line.BrandId = ReadProductRef(doc, "brandId");
        line.OfferGroupId = ReadProductRef(doc, "offerGroupId");
    }

    private static string ReadProductRef(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull) return "";
        return v.BsonType switch
        {
            BsonType.ObjectId => v.AsObjectId.ToString(),
            BsonType.String => v.AsString,
            _ => v.ToString() ?? "",
        };
    }

    private void WarnIfBelowMargin(BillingLineItem line)
    {
        if (line.IsEntryRow || line.Qty <= 0 || line.Rate <= 0) return;
        if (line.CostPrice <= 0 || line.MarginPercent <= 0) return;

        var label = string.IsNullOrWhiteSpace(line.ProductCode) ? line.Description : line.ProductCode;
        if (!MarginGatekeeper.TryBuildWarning(label, line.Rate, line.CostPrice, line.MarginPercent, out var message))
            return;

        MessageBox.Show(
            message,
            "Below minimum margin",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
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
                var productLabel = string.IsNullOrWhiteSpace(existing.Description)
                    ? sku
                    : $"{existing.Description} ({sku})";
                var confirm = MessageBox.Show(
                    $"\"{productLabel}\" is already on this bill (qty {existing.Qty:0.###}).\n\nAdd 1 more to quantity?",
                    "RR Bridal Billing",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes);
                if (confirm != MessageBoxResult.Yes)
                {
                    ClearEntryRowProductCode();
                    EnsureEntryRow();
                    RequestFocusEntryProductCode?.Invoke();
                    return;
                }

                existing.Qty += 1;
                WarnIfBelowMargin(existing);
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
            EnrichLineProductMetadata(entry);
            entry.IsIgst = IsInterState;
            WarnIfBelowMargin(entry);
            EnsureEntryRow();
            RequestFocusEntryProductCode?.Invoke();
            return;
        }

        var line = new BillingLineItem { IsIgst = IsInterState };
        FillLineFromCatalog(line, p);
        EnrichLineProductMetadata(line);
        WarnIfBelowMargin(line);
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
        HoldBills = false;
        DoorDelivery = false;
        OnlineCodOrder = false;
        Stitching = false;
        DeliveryDate = null;
        SearchText = "";
        ClearCreditSelection();
        AvailableCreditNotes.Clear();
        HasAvailableCredit = false;
        _excludedSchemeCodes.Clear();
        AppliedSchemes.Clear();
        RefreshAlterationGstIncludedFromSettings();
        EnsureEntryRow();
        RecalculateTotals();
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

        var dayGuard = new DaySessionGuard(_services.DaySessions);
        var dayBlock = await dayGuard.ValidatePostingTodayAsync(
            _services.StoreContext.StoreId,
            _services.StoreContext.PosCounter);
        if (dayBlock != null)
        {
            MessageBox.Show(dayBlock, "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RecalculateTotals();
        if (!IsManualDiscountWithinCap())
        {
            EnforceManualDiscountCap(showMessage: true);
            RecalculateTotals();
            if (!IsManualDiscountWithinCap())
            {
                MessageBox.Show(
                    $"Discount cannot exceed {MaxDiscountPercent:0.##}% for your user account.",
                    "RR Bridal Billing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        var stockShortfalls = await BillingStockValidator.FindStockShortfallsAsync(
            _services.ProductCatalog,
            Lines,
            CancellationToken.None);
        if (stockShortfalls.Count > 0)
        {
            var warnDlg = new BillingPostWarningsDialog(stockShortfalls)
            {
                Owner = Application.Current.MainWindow,
            };
            if (warnDlg.ShowDialog() != true || !warnDlg.PostAnyway)
                return;

            foreach (var shortLine in stockShortfalls)
            {
                try
                {
                    await AddIndentRequestAsync(shortLine.Sku, shortLine.Description, "", CancellationToken.None);
                }
                catch { /* best-effort indent at post */ }
            }
        }

        var shortSkus = BillingStockValidator.ShortSkus(stockShortfalls);

        RecalculateTotals();
        var totals = _lastBillTotals;

        if (OnlineCodOrder && totals.AppliedCredit > 0)
        {
            MessageBox.Show(
                "Online COD orders cannot use credit note payment. Clear the credit note selection first.",
                "RR Bridal Billing",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        await AssignBillNumberAsync();

        PaymentOutcome paymentOutcome;
        if (OnlineCodOrder && totals.Payable > 0)
        {
            paymentOutcome = new PaymentOutcome
            {
                Confirmed = true,
                Mode = PaymentMode.OnlineCod,
                Legs = new List<PaymentLegResult>(),
            };
        }
        else if (totals.Payable > 0)
        {
            _services.PosBillingSettings.Load();
            var paymentVm = new PaymentDialogViewModel(
                _services.PaymentRouter,
                _services.CustomerCreditNotes,
                _services.StoreContext.StoreId,
                BillNo,
                totals.Payable,
                CustomerCode,
                CustomerPhone,
                _selectedCreditNoteNo,
                totals.AppliedCredit,
                skipPaymentOutbox: true,
                allowCreditNoteRemainingCashout: _services.PosBillingSettings.Current.AllowCreditNoteRemainingCashout,
                posCounter: _services.StoreContext.PosCounter);
            await paymentVm.InitializeAsync();
            var paymentDlg = new PaymentDialog(paymentVm) { Owner = Application.Current.MainWindow };
            var paymentResult = paymentDlg.ShowDialog();
            if (paymentResult != true || !paymentVm.Outcome.Confirmed)
                return;
            paymentOutcome = paymentVm.Outcome;
        }
        else
        {
            var legs = new List<PaymentLegResult>();
            if (totals.AppliedCredit > 0 && !string.IsNullOrEmpty(_selectedCreditNoteNo))
            {
                legs.Add(new PaymentLegResult
                {
                    Provider = PaymentProviderKind.CreditNote,
                    Amount = totals.AppliedCredit,
                    Reference = _selectedCreditNoteNo,
                    Status = "Success",
                });
            }

            paymentOutcome = new PaymentOutcome
            {
                Confirmed = true,
                Mode = totals.AppliedCredit > 0 ? PaymentMode.CreditNote : PaymentMode.Cash,
                Legs = legs,
            };
        }

        if (totals.AppliedCredit > 0 && !string.IsNullOrEmpty(_selectedCreditNoteNo))
        {
            var consumed = await _services.CustomerCreditNotes.ConsumeAsync(
                _selectedCreditNoteNo,
                BillNo,
                totals.AppliedCredit);
            if (!consumed)
            {
                MessageBox.Show(
                    "Could not apply the selected credit note (it may already be used). Refresh and try again.",
                    "RR Bridal Billing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        try
        {
            var coll = _services.LocalDb.GetCollection<BsonDocument>("store_bills");
            var linesArr = BuildLinesBsonArray();

            var paymentsArr = new BsonArray();
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
                { "stitching", Stitching },
                { "deliveryDate", FormatDeliveryDate() },
                { "printInvoice", PrintInvoice },
                { "isInterState", IsInterState },
                { "itemDiscountPercent", (double)ItemDiscountPercent },
                { "itemDiscount", (double)totals.ItemDiscount },
                { "cashDiscAmount", (double)totals.CashDiscount },
                { "schemeLineDiscount", (double)totals.SchemeLineDiscount },
                { "schemeBillDiscount", (double)totals.SchemeBillDiscount },
                { "appliedSchemes", BuildAppliedSchemesBsonArray() },
                { "roundOff", (double)totals.RoundOff },
                { "subTotal", (double)totals.SubTotal },
                { "originalInclusiveTotal", (double)totals.OriginalInclusiveTotal },
                { "originalTaxTotal", (double)totals.OriginalTaxTotal },
                { "revisedSubTotal", (double)totals.RevisedSubTotal },
                { "cgstTotal", (double)totals.Cgst },
                { "sgstTotal", (double)totals.Sgst },
                { "igstTotal", (double)totals.Igst },
                { "taxTotal", (double)totals.TaxTotal },
                { "alterationTotal", (double)totals.AlterationTotal },
                { "alterationGstIncluded", _alterationGstIncluded },
                { "payable", (double)totals.Payable },
                { "lines", linesArr },
                { "payments", paymentsArr },
                { "paymentMode", paymentOutcome.Mode.ToString() },
                { "status", "posted" },
                { "createdAtUtc", DateTime.UtcNow.ToString("O") },
            };

            if (totals.AppliedCredit > 0 && !string.IsNullOrEmpty(_selectedCreditNoteNo))
            {
                doc.Add("creditNoteNo", _selectedCreditNoteNo);
                doc.Add("creditApplied", (double)totals.AppliedCredit);
                doc.Add("payableBeforeCredit", (double)totals.PayableBeforeCredit);
            }

            if (stockShortfalls.Count > 0)
                doc.Add("stockExceptions", BillingStockValidator.ToStockExceptionsBson(stockShortfalls));

            if (OnlineCodOrder && totals.Payable > 0)
            {
                doc["salesChannel"] = OnlineCodDocumentReader.SalesChannelOnline;
                doc["paymentMode"] = PaymentMode.OnlineCod.ToString();
                doc["payments"] = new BsonArray();
                doc["onlineCod"] = new BsonDocument
                {
                    { "status", OnlineCodDocumentReader.StatusPending },
                    { "amount", (double)totals.Payable },
                };
            }
            else
            {
                doc["salesChannel"] = "store";
            }

            var postWarnings = new List<string>();
            var postedBillNo = BillNo;
            await coll.InsertOneAsync(doc);

            var (actorName, actorEmail) = StoreAuditLogService.ActorFromSession(_services.UserSession);
            await _services.StoreAuditLog.LogEventAsync(new StoreAuditEvent
            {
                EntityType = "bill",
                EntityId = postedBillNo,
                Action = "posted",
                ActorName = actorName,
                ActorEmail = actorEmail,
                Metadata = new BsonDocument
                {
                    { "payable", (double)totals.Payable },
                    { "lineCount", linesArr.Count },
                    { "paymentMode", paymentOutcome.Mode.ToString() },
                    { "customerPhone", CustomerPhone },
                    { "stockExceptionCount", stockShortfalls.Count },
                },
            });

            if (!string.IsNullOrEmpty(_activeHoldNo))
            {
                try
                {
                    await _services.HeldBills.DeleteAsync(_activeHoldNo);
                }
                catch (Exception ex)
                {
                    postWarnings.Add($"Could not delete hold {_activeHoldNo}: {ex.Message}");
                }

                _activeHoldNo = null;
                HoldNo = "";
            }

            try
            {
                await _services.BillingOutbox.PublishInvoiceCreatedAsync(doc);
            }
            catch (Exception ex)
            {
                postWarnings.Add($"Outbox enqueue failed: {ex.Message}");
            }

            foreach (var line in Lines.Where(l => l.Amount > 0 && !string.IsNullOrWhiteSpace(l.CentralProductId)))
            {
                var sku = (line.ProductCode ?? "").Trim();
                if (shortSkus.Contains(sku))
                    continue;

                try
                {
                    await _services.ProductCatalog.DecrementStockAsync(
                        line.CentralProductId!,
                        line.Qty,
                        reason: "bill_post",
                        billNo: postedBillNo,
                        ct: default);
                }
                catch (Exception ex)
                {
                    postWarnings.Add($"Stock decrement failed for {sku}: {ex.Message}");
                }
            }

            if (_services.WhatsAppBills.ShouldAutoSendAfterPost && !string.IsNullOrWhiteSpace(CustomerPhone))
            {
                try
                {
                    var whatsappInput = BuildThermalInput(paymentOutcome);
                    var wa = await _services.WhatsAppBills.TrySendBillAsync(
                        postedBillNo,
                        whatsappInput,
                        CustomerPhone);
                    if (wa.Status == WhatsAppDeliveryStatus.Failed)
                        postWarnings.Add($"WhatsApp: {wa.Error ?? "send failed"}");
                }
                catch (Exception ex)
                {
                    postWarnings.Add($"WhatsApp: {ex.Message}");
                }
            }

            if (postWarnings.Count > 0)
            {
                var warnArr = new BsonArray(postWarnings);
                doc["postWarnings"] = warnArr;
                await coll.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq("storeId", storeId),
                        Builders<BsonDocument>.Filter.Eq("billNo", BillNo)),
                    Builders<BsonDocument>.Update.Set("postWarnings", warnArr));
            }

            ThermalInvoiceInput? printInput = null;
            if (PrintInvoice)
                printInput = BuildThermalInput(paymentOutcome);

            ClearForNewBill();

            if (postWarnings.Count > 0)
            {
                MessageBox.Show(
                    $"Bill {postedBillNo} was saved, but some follow-up steps had issues:\n\n{string.Join("\n", postWarnings)}",
                    "RR Bridal Billing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            if (printInput != null)
                await ShowInvoicePrintDialogAsync(paymentOutcome, printInvoiceEnabled: true, prebuiltInput: printInput, clearBillingAfterPrint: false);
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
                    AlterationAmount = l.AlterationAmount,
                    LineDiscount = lineDisc,
                    TaxableAmount = l.RevisedAmount,
                    TaxAmount = l.RevisedTaxAmount,
                    LineInclusiveAmount = l.RevisedInclusiveAmount,
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
            BillNo = BillNo is "—" or { Length: 0 } ? (HoldNo.Length > 0 ? HoldNo : "PREVIEW") : BillNo,
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
            ItemDiscountPercent = ItemDiscountPercent,
            ItemDiscount = totals.ItemDiscount,
            CashDiscAmount = totals.CashDiscount,
            AlterationTotal = totals.AlterationTotal,
            AlterationGstIncluded = _alterationGstIncluded,
            RoundOff = totals.RoundOff,
            Payable = totals.Payable,
            TotalQty = totalQty,
            ItemCount = active.Count,
            TotalMrp = totalMrp,
            TotalLineAmount = totalLineAmount,
            TotalTaxableAmount = totalTaxable,
            Savings = savings,
            Payments = paySnap,
            Stitching = Stitching,
            DoorDelivery = DoorDelivery,
            DeliveryDate = FormatDeliveryDate(),
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

        try
        {
            if (string.IsNullOrEmpty(_activeHoldNo))
            {
                _activeHoldNo = await _services.BillNumberGenerator.NextHoldAsync();
                HoldNo = _activeHoldNo;
            }

            DraftLabel = "HELD";
            var doc = BuildHeldBillDocument(_activeHoldNo);
            await _services.HeldBills.UpsertAsync(doc);
            MessageBox.Show($"Hold {HoldNo} saved. Use Resume held bills to continue.", "Hold bill",
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
            var rows = await _services.HeldBills.ListAsync();
            var dlg = new HeldBillsDialog(rows) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true)
                return;

            if (dlg.DeleteRequested && dlg.SelectedRow != null)
            {
                await _services.HeldBills.DeleteAsync(dlg.SelectedRow.HoldNo);
                MessageBox.Show("Held bill deleted.", "Hold bill", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!dlg.ResumeRequested || dlg.SelectedRow == null)
                return;

            var doc = await _services.HeldBills.GetByHoldNoAsync(dlg.SelectedRow.HoldNo);
            if (doc == null)
            {
                MessageBox.Show("Held bill not found.", "Hold bill", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadFromHeldDocument(doc);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open held bills: {ex.Message}", "Hold bill", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void LoadFromHeldDocument(BsonDocument doc)
    {
        ClearForNewBill();
        _activeHoldNo = doc.GetValue("holdNo", "").AsString;
        HoldNo = _activeHoldNo;
        DraftLabel = "HELD";
        BillNo = "—";
        BillDateDisplay = doc.GetValue("billDate", BillDateDisplay).AsString;
        CustomerCode = doc.GetValue("customerCode", "").AsString;
        CustomerName = doc.GetValue("customerName", "").AsString;
        CustomerPhone = doc.GetValue("customerPhone", "").AsString;
        Salesman = doc.GetValue("salesman", "").AsString;
        HoldBills = doc.GetValue("holdBills", false).AsBoolean;
        DoorDelivery = doc.GetValue("doorDelivery", false).AsBoolean;
        Stitching = doc.GetValue("stitching", false).AsBoolean;
        DeliveryDate = ParseDeliveryDate(doc.GetValue("deliveryDate", "").AsString);
        PrintInvoice = doc.GetValue("printInvoice", true).AsBoolean;
        IsInterState = doc.Contains("isInterState") && doc["isInterState"].AsBoolean;
        ItemDiscountPercent = (decimal)doc.GetValue("itemDiscountPercent", 0).ToDouble();
        CashDiscAmountText = MoneyMath.FormatEditableAmount((decimal)doc.GetValue("cashDiscAmount", 0).ToDouble());
        _alterationGstIncluded = doc.GetValue("alterationGstIncluded", false).AsBoolean;

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
                    AlterationAmount = (decimal)lineBson.GetValue("alterationAmount", 0).ToDouble(),
                };
                Lines.Add(line);
            }
        }

        EnsureEntryRow();
        RecalculateTotals();
        NotifyPostBillCanExecute();
    }

    private BsonArray BuildAppliedSchemesBsonArray()
    {
        var arr = new BsonArray();
        foreach (var scheme in AppliedSchemes)
        {
            arr.Add(new BsonDocument
            {
                { "schemeCode", scheme.SchemeCode },
                { "schemeName", scheme.SchemeName },
                { "savedAmount", (double)scheme.SavedAmount },
            });
        }
        return arr;
    }

    private BsonDocument BuildHeldBillDocument(string holdNo)
    {
        var doc = BuildBillPayloadCore();
        doc["holdNo"] = holdNo.Trim();
        doc["deviceId"] = _services.StoreContext.DeviceId;
        doc["posCounter"] = _services.StoreContext.PosCounter;
        return doc;
    }

    private BsonDocument BuildBillBsonDocument(string status, BsonArray? payments, string paymentMode)
    {
        var doc = BuildBillPayloadCore();
        doc["billNo"] = BillNo.Trim();
        doc["payments"] = payments ?? new BsonArray();
        doc["paymentMode"] = paymentMode;
        doc["status"] = status;
        doc["createdAtUtc"] = DateTime.UtcNow.ToString("O");
        return doc;
    }

    private BsonDocument BuildBillPayloadCore()
    {
        RecalculateTotals();
        var totals = _lastBillTotals;
        var linesArr = BuildLinesBsonArray();

        return new BsonDocument
        {
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
            { "stitching", Stitching },
            { "deliveryDate", FormatDeliveryDate() },
            { "printInvoice", PrintInvoice },
            { "isInterState", IsInterState },
            { "itemDiscountPercent", (double)ItemDiscountPercent },
            { "itemDiscount", (double)totals.ItemDiscount },
            { "cashDiscAmount", (double)totals.CashDiscount },
            { "schemeLineDiscount", (double)totals.SchemeLineDiscount },
            { "schemeBillDiscount", (double)totals.SchemeBillDiscount },
            { "appliedSchemes", BuildAppliedSchemesBsonArray() },
            { "roundOff", (double)totals.RoundOff },
            { "subTotal", (double)totals.SubTotal },
            { "originalInclusiveTotal", (double)totals.OriginalInclusiveTotal },
            { "originalTaxTotal", (double)totals.OriginalTaxTotal },
            { "revisedSubTotal", (double)totals.RevisedSubTotal },
            { "cgstTotal", (double)totals.Cgst },
            { "sgstTotal", (double)totals.Sgst },
            { "igstTotal", (double)totals.Igst },
            { "taxTotal", (double)totals.TaxTotal },
            { "alterationTotal", (double)totals.AlterationTotal },
            { "alterationGstIncluded", _alterationGstIncluded },
            { "payable", (double)totals.Payable },
            { "lines", linesArr },
        };
    }

    private BsonArray BuildLinesBsonArray()
    {
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
                { "alterationAmount", (double)line.AlterationAmount },
                { "discountAmount", (double)line.DiscountAmount },
                { "cashDiscountAmount", (double)line.CashDiscountAmount },
                { "schemeDiscountAmount", (double)line.SchemeDiscountAmount },
                { "originalTaxAmount", (double)line.OriginalTaxAmount },
                { "revisedAmount", (double)line.RevisedAmount },
                { "revisedInclusiveAmount", (double)line.RevisedInclusiveAmount },
                { "revisedTaxAmount", (double)line.RevisedTaxAmount },
                { "mrp", (double)line.Mrp },
                { "costPrice", (double)line.CostPrice },
                { "marginPercent", (double)line.MarginPercent },
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

        return linesArr;
    }

    private string FormatDeliveryDate() =>
        DeliveryDate?.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture).ToUpperInvariant() ?? "";

    private static DateTime? ParseDeliveryDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return DateTime.TryParseExact(
            value.Trim(),
            "dd-MMM-yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var dt)
            ? dt
            : null;
    }

    [RelayCommand]
    private static void CloseApp()
    {
        Application.Current.Shutdown();
    }
}
