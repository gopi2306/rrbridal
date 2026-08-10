using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Expenses;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Store;
using RRBridal.StoreBilling.App.Services.Ui;

namespace RRBridal.StoreBilling.App.ViewModels;

public sealed class DailyExpenseRow
{
    public required BsonDocument Document { get; init; }
    public required string ExpenseNo { get; init; }
    public string Supplier { get; init; } = "";
    public string InvoiceNo { get; init; } = "";
    public string Category { get; init; } = "";
    public required string Description { get; init; }
    public string GstDisplay { get; init; } = "";
    public string PaymentDisplay { get; init; } = "";
    public required string AmountDisplay { get; init; }
    public required string PostedAtLocal { get; init; }
    public required string BusinessDate { get; init; }
    public string Status { get; init; } = "posted";
}

public partial class ExpensePaymentLegInput : ObservableObject
{
    public ExpensePaymentLegInput(string mode) => Mode = mode;
    public string Mode { get; }
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _amountText = "";
    [ObservableProperty] private string _reference = "";
}

public partial class DailyExpenseViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");
    private readonly AppServices _services;
    private readonly IMongoCollection<BsonDocument> _expenses;
    private readonly List<BsonDocument> _loadedDocuments = [];
    private bool _loadingEntry;

    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private DateTime _supplierInvoiceDate = DateTime.Today;
    [ObservableProperty] private string _supplierName = "";
    [ObservableProperty] private string _supplierGstin = "";
    [ObservableProperty] private string _supplierStateCode = "";
    [ObservableProperty] private string _storeStateCode = "";
    [ObservableProperty] private bool _isSupplyTypeAutomatic;
    [ObservableProperty] private bool _canSelectSupplyType = true;
    [ObservableProperty] private string _supplierInvoiceNo = "";
    [ObservableProperty] private string _selectedCategory = "Other";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _amountText = "";
    [ObservableProperty] private string _selectedGstMode = "No GST";
    [ObservableProperty] private string _selectedGstRate = "0";
    [ObservableProperty] private string _selectedSupplyType = "Intra-state (CGST + SGST)";
    [ObservableProperty] private string _taxSummary = "Taxable ₹ 0.00 · Total ₹ 0.00";
    [ObservableProperty] private string _paymentBalanceSummary = "Payment balance ₹ 0.00";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _dayTotalSummary = "₹ 0.00";
    [ObservableProperty] private string _gstTotalSummary = "GST ₹ 0.00";
    [ObservableProperty] private string _cashTotalSummary = "Cash ₹ 0.00";
    [ObservableProperty] private string _storeDisplayName = "";
    [ObservableProperty] private string _tillDisplayLine = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _filterCategory = "All";
    [ObservableProperty] private string _filterPaymentMode = "All";
    [ObservableProperty] private string _filterStatus = "Posted";
    [ObservableProperty] private DailyExpenseRow? _selectedExpense;
    [ObservableProperty] private string _editingExpenseNo = "";
    [ObservableProperty] private string _voidReason = "";

    public ObservableCollection<DailyExpenseRow> Expenses { get; } = [];
    public ObservableCollection<ExpensePaymentLegInput> PaymentLegs { get; } =
    [
        new(ExpensePaymentMode.Cash),
        new(ExpensePaymentMode.Card),
        new(ExpensePaymentMode.Upi),
        new(ExpensePaymentMode.BankTransfer),
    ];

    public IReadOnlyList<string> Categories { get; } =
        ["Rent", "Utilities", "Transport", "Packaging", "Repairs", "Professional Fees", "Office", "Marketing", "Staff Welfare", "Other"];
    public IReadOnlyList<string> CategoryFilters => ["All", .. Categories];
    public IReadOnlyList<string> GstModes { get; } = ["No GST", "Inclusive", "Exclusive"];
    public IReadOnlyList<string> GstRates { get; } = ["0", "5", "12", "18", "28"];
    public IReadOnlyList<string> SupplyTypes { get; } = ["Intra-state (CGST + SGST)", "Inter-state (IGST)"];
    public IReadOnlyList<string> PaymentFilters { get; } = ["All", .. ExpensePaymentMode.All];
    public IReadOnlyList<string> StatusFilters { get; } = ["Posted", "Void", "All"];
    public string FormTitle => string.IsNullOrWhiteSpace(EditingExpenseNo) ? "New expense" : $"Edit {EditingExpenseNo}";
    public string SaveButtonText => string.IsNullOrWhiteSpace(EditingExpenseNo) ? "Post expense" : "Save changes";

    public DailyExpenseViewModel(AppServices services)
    {
        _services = services;
        _expenses = services.LocalDb.GetCollection<BsonDocument>("store_daily_expenses");
        foreach (var leg in PaymentLegs)
            leg.PropertyChanged += (_, args) => OnPaymentLegChanged(leg, args.PropertyName);
        PaymentLegs[0].IsSelected = true;
        RefreshSupplyTypeFromGstin();
        ApplyBrandingFromShell();
        _ = RefreshCommand.ExecuteAsync(null);
    }

    public void ApplyBrandingFromShell()
    {
        var snap = _services.ShellBranding.Current;
        StoreDisplayName = snap.StoreDisplayName;
        TillDisplayLine = snap.TillDisplayLine;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        StatusMessage = "Loading expenses…";
        try
        {
            var businessDate = SelectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            List<BsonDocument> docs;
            if (_services.CentralMode.IsOnlineMode)
            {
                using var json = await _services.StorePos.ListDailyExpensesAsync(businessDate, 500);
                docs = json.RootElement.ValueKind == JsonValueKind.Array
                    ? json.RootElement.EnumerateArray().Select(MapCentralExpenseToDoc).ToList()
                    : [];
            }
            else
            {
                var filter = Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("storeId", _services.StoreContext.StoreId),
                    Builders<BsonDocument>.Filter.Eq("businessDate", businessDate));
                docs = await _expenses.Find(filter).ToListAsync();
            }

            docs.Sort((a, b) => string.Compare(
                DailyExpenseDomain.ReadString(b, "createdAtUtc"),
                DailyExpenseDomain.ReadString(a, "createdAtUtc"),
                StringComparison.Ordinal));
            _loadedDocuments.Clear();
            _loadedDocuments.AddRange(docs);
            ApplyFilters();
        }
        catch (Exception ex)
        {
            StatusMessage = "Load failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveExpense()
    {
        var draft = BuildDraft(out var parseError);
        if (parseError != null)
        {
            AppDialog.Show(parseError, "Daily Expense", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var validationError = DailyExpenseDomain.Validate(draft!);
        if (validationError != null)
        {
            AppDialog.Show(validationError, "Daily Expense", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dayBlock = await new DaySessionGuard(_services.DaySessions).ValidatePostingAsync(
            _services.StoreContext.StoreId,
            draft!.BusinessDate,
            _services.StoreContext.PosCounter);
        if (dayBlock != null)
        {
            AppDialog.Show(dayBlock, "Daily Expense", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            BsonDocument saved;
            var persistLocally = !_services.CentralMode.IsOnlineMode;
            if (string.IsNullOrWhiteSpace(EditingExpenseNo))
            {
                saved = await _services.DailyExpenses.CreateAsync(draft, _services.UserSession, persistLocally);
                StatusMessage = $"Posted {DailyExpenseDomain.ReadString(saved, "expenseNo")} — {MoneyMath.FormatRupee(DailyExpenseDomain.ReadDecimal(saved, "amount"))}.";
            }
            else
            {
                var existing = _loadedDocuments.FirstOrDefault(d =>
                    string.Equals(DailyExpenseDomain.ReadString(d, "expenseNo"), EditingExpenseNo, StringComparison.OrdinalIgnoreCase));
                if (existing == null) throw new InvalidOperationException("The selected expense is no longer available.");
                saved = await _services.DailyExpenses.UpdateAsync(existing, draft, _services.UserSession, persistLocally);
                StatusMessage = $"Updated {EditingExpenseNo}.";
            }
            ClearEntryForm();
            await RefreshCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            AppDialog.Show("Save failed: " + ex.Message, "Daily Expense", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void EditSelected()
    {
        if (SelectedExpense == null)
        {
            AppDialog.Show("Select an expense to edit.", "Daily Expense", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.Equals(SelectedExpense.Status, "void", StringComparison.OrdinalIgnoreCase))
        {
            AppDialog.Show("Voided expenses cannot be edited.", "Daily Expense", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        LoadEntry(SelectedExpense.Document);
    }

    [RelayCommand]
    private async Task VoidSelected()
    {
        if (SelectedExpense == null)
        {
            AppDialog.Show("Select an expense to void.", "Daily Expense", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(VoidReason))
        {
            AppDialog.Show("Enter a void reason first.", "Daily Expense", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (AppDialog.Show(
                $"Void {SelectedExpense.ExpenseNo}? The record will remain in the audit trail.",
                "Void Expense",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            await _services.DailyExpenses.VoidAsync(
                SelectedExpense.Document,
                VoidReason,
                _services.UserSession,
                !_services.CentralMode.IsOnlineMode);
            VoidReason = "";
            await RefreshCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            AppDialog.Show("Void failed: " + ex.Message, "Daily Expense", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task PrintSelected()
    {
        if (SelectedExpense == null)
        {
            AppDialog.Show("Select an expense to print.", "Daily Expense", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        await ExpenseReceiptPrintFlow.ShowAsync(_services, SelectedExpense.Document);
    }

    [RelayCommand]
    public void ClearEntryForm()
    {
        _loadingEntry = true;
        EditingExpenseNo = "";
        SupplierName = "";
        SupplierGstin = "";
        SupplierStateCode = "";
        SupplierInvoiceNo = "";
        SupplierInvoiceDate = SelectedDate;
        SelectedCategory = "Other";
        Description = "";
        AmountText = "";
        SelectedGstMode = "No GST";
        SelectedGstRate = "0";
        SelectedSupplyType = SupplyTypes[0];
        foreach (var leg in PaymentLegs)
        {
            leg.IsSelected = string.Equals(leg.Mode, ExpensePaymentMode.Cash, StringComparison.OrdinalIgnoreCase);
            leg.AmountText = "";
            leg.Reference = "";
        }
        _loadingEntry = false;
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(SaveButtonText));
        Recalculate();
    }

    private void LoadEntry(BsonDocument doc)
    {
        _loadingEntry = true;
        EditingExpenseNo = DailyExpenseDomain.ReadString(doc, "expenseNo") ?? "";
        SupplierName = DailyExpenseDomain.ReadString(doc, "supplierName") ?? "";
        SupplierGstin = DailyExpenseDomain.ReadString(doc, "supplierGstin") ?? "";
        SupplierStateCode = DailyExpenseDomain.ReadString(doc, "supplierStateCode")
                            ?? GstStateCodeResolver.ExtractStateCode(SupplierGstin)
                            ?? "";
        SupplierInvoiceNo = DailyExpenseDomain.ReadString(doc, "supplierInvoiceNo") ?? "";
        if (DateTime.TryParse(DailyExpenseDomain.ReadString(doc, "supplierInvoiceDate"), out var invoiceDate))
            SupplierInvoiceDate = invoiceDate;
        SelectedCategory = DailyExpenseDomain.ReadString(doc, "category") ?? "Other";
        Description = DailyExpenseDomain.ReadString(doc, "description") ?? "";
        AmountText = (DailyExpenseDomain.ReadDecimal(doc, "enteredAmount") > 0
            ? DailyExpenseDomain.ReadDecimal(doc, "enteredAmount")
            : DailyExpenseDomain.ReadDecimal(doc, "amount")).ToString("0.00", InCulture);
        SelectedGstMode = ToGstDisplay(DailyExpenseDomain.ReadString(doc, "gstMode"));
        SelectedGstRate = DailyExpenseDomain.ReadDecimal(doc, "gstRate").ToString("0.##", CultureInfo.InvariantCulture);
        SelectedSupplyType = string.Equals(DailyExpenseDomain.ReadString(doc, "supplyType"), ExpenseSupplyType.InterState, StringComparison.OrdinalIgnoreCase)
            ? SupplyTypes[1]
            : SupplyTypes[0];
        foreach (var leg in PaymentLegs)
        {
            leg.IsSelected = false;
            leg.AmountText = "";
            leg.Reference = "";
        }
        if (doc.TryGetValue("payments", out var payments) && payments.IsBsonArray && payments.AsBsonArray.Count > 0)
        {
            foreach (var payment in payments.AsBsonArray.OfType<BsonDocument>())
            {
                var mode = DailyExpenseDomain.ReadString(payment, "mode") ?? DailyExpenseDomain.ReadString(payment, "provider") ?? "";
                var input = PaymentLegs.FirstOrDefault(p => string.Equals(p.Mode, mode, StringComparison.OrdinalIgnoreCase));
                if (input == null) continue;
                input.IsSelected = true;
                input.AmountText = DailyExpenseDomain.ReadDecimal(payment, "amount").ToString("0.00", InCulture);
                input.Reference = DailyExpenseDomain.ReadString(payment, "reference") ?? "";
            }
        }
        else
        {
            PaymentLegs[0].IsSelected = true;
            PaymentLegs[0].AmountText = DailyExpenseDomain.ReadDecimal(doc, "amount").ToString("0.00", InCulture);
        }
        _loadingEntry = false;
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(SaveButtonText));
        Recalculate();
    }

    private DailyExpenseDraft? BuildDraft(out string? error)
    {
        error = null;
        if (!TryMoney(AmountText, out var amount))
        {
            error = "Enter a valid amount.";
            return null;
        }
        _ = decimal.TryParse(SelectedGstRate, NumberStyles.Number, CultureInfo.InvariantCulture, out var gstRate);
        var legs = new List<DailyExpensePaymentLeg>();
        foreach (var input in PaymentLegs)
        {
            if (!input.IsSelected) continue;
            if (string.IsNullOrWhiteSpace(input.AmountText)) continue;
            if (!TryMoney(input.AmountText, out var legAmount))
            {
                error = $"Enter a valid {input.Mode} payment amount.";
                return null;
            }
            if (legAmount > 0) legs.Add(new DailyExpensePaymentLeg(input.Mode, legAmount, input.Reference));
        }
        return new DailyExpenseDraft
        {
            BusinessDate = SelectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            SupplierName = SupplierName,
            SupplierGstin = SupplierGstin,
            SupplierStateCode = SupplierStateCode,
            StoreStateCode = StoreStateCode,
            SupplierInvoiceNo = SupplierInvoiceNo,
            SupplierInvoiceDate = SupplierInvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Category = SelectedCategory,
            Description = Description,
            EnteredAmount = amount,
            GstMode = ToGstMode(SelectedGstMode),
            GstRate = gstRate,
            SupplyType = SelectedSupplyType.StartsWith("Inter", StringComparison.OrdinalIgnoreCase)
                ? ExpenseSupplyType.InterState
                : ExpenseSupplyType.IntraState,
            Payments = legs,
        };
    }

    private void Recalculate()
    {
        if (_loadingEntry || !TryMoney(AmountText, out var amount) || amount <= 0)
        {
            TaxSummary = "Taxable ₹ 0.00 · Total ₹ 0.00";
            PaymentBalanceSummary = "Payment balance ₹ 0.00";
            return;
        }
        _ = decimal.TryParse(SelectedGstRate, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate);
        var tax = DailyExpenseDomain.CalculateTax(
            amount,
            ToGstMode(SelectedGstMode),
            rate,
            SelectedSupplyType.StartsWith("Inter", StringComparison.OrdinalIgnoreCase) ? ExpenseSupplyType.InterState : ExpenseSupplyType.IntraState);
        var cashLeg = PaymentLegs[0];
        if (cashLeg.IsSelected)
        {
            var nonCashTotal = PaymentLegs
                .Skip(1)
                .Where(p => p.IsSelected)
                .Sum(p => TryMoney(p.AmountText, out var value) ? Math.Max(0m, value) : 0m);
            var balancedCash = Math.Max(0m, tax.TotalAmount - nonCashTotal);
            _loadingEntry = true;
            cashLeg.AmountText = balancedCash.ToString("0.00", InCulture);
            _loadingEntry = false;
        }
        TaxSummary = $"Taxable {MoneyMath.FormatRupee(tax.TaxableAmount)} · CGST {MoneyMath.FormatRupee(tax.CgstAmount)} · SGST {MoneyMath.FormatRupee(tax.SgstAmount)} · IGST {MoneyMath.FormatRupee(tax.IgstAmount)} · Total {MoneyMath.FormatRupee(tax.TotalAmount)}";
        var paid = PaymentLegs
            .Where(p => p.IsSelected)
            .Sum(p => TryMoney(p.AmountText, out var value) ? value : 0m);
        var remaining = tax.TotalAmount - paid;
        PaymentBalanceSummary = Math.Abs(remaining) <= 0.01m
            ? "Remaining to allocate: ₹ 0.00 — Balanced"
            : remaining > 0
                ? $"Remaining to allocate: {MoneyMath.FormatRupee(remaining)}"
                : $"Over-allocated by: {MoneyMath.FormatRupee(Math.Abs(remaining))}";
    }

    private void OnPaymentLegChanged(ExpensePaymentLegInput leg, string? propertyName)
    {
        if (_loadingEntry) return;
        if (propertyName == nameof(ExpensePaymentLegInput.IsSelected) && !leg.IsSelected)
        {
            _loadingEntry = true;
            leg.AmountText = "";
            leg.Reference = "";
            _loadingEntry = false;
        }
        Recalculate();
    }

    private void ApplyFilters()
    {
        var query = SearchText.Trim();
        var filtered = _loadedDocuments.Where(doc =>
        {
            var status = DailyExpenseDomain.ReadString(doc, "status") ?? "posted";
            if (!string.Equals(FilterStatus, "All", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(status, FilterStatus, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.Equals(FilterCategory, "All", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(DailyExpenseDomain.ReadString(doc, "category"), FilterCategory, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.Equals(FilterPaymentMode, "All", StringComparison.OrdinalIgnoreCase) && !HasPaymentMode(doc, FilterPaymentMode)) return false;
            if (query.Length > 0)
            {
                var haystack = string.Join(" ", new[]
                {
                    DailyExpenseDomain.ReadString(doc, "expenseNo"),
                    DailyExpenseDomain.ReadString(doc, "supplierName"),
                    DailyExpenseDomain.ReadString(doc, "supplierInvoiceNo"),
                    DailyExpenseDomain.ReadString(doc, "description"),
                });
                if (!haystack.Contains(query, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }).ToList();

        Expenses.Clear();
        foreach (var doc in filtered) Expenses.Add(ToRow(doc));
        var posted = filtered.Where(d => string.Equals(DailyExpenseDomain.ReadString(d, "status") ?? "posted", "posted", StringComparison.OrdinalIgnoreCase)).ToList();
        var total = posted.Sum(d => DailyExpenseDomain.ReadDecimal(d, "amount"));
        var gst = posted.Sum(d => DailyExpenseDomain.ReadDecimal(d, "cgstAmount") + DailyExpenseDomain.ReadDecimal(d, "sgstAmount") + DailyExpenseDomain.ReadDecimal(d, "igstAmount"));
        DayTotalSummary = MoneyMath.FormatRupee(total);
        GstTotalSummary = $"GST {MoneyMath.FormatRupee(gst)}";
        CashTotalSummary = $"Cash {MoneyMath.FormatRupee(posted.Sum(DailyExpenseDomain.CashAmount))}";
        StatusMessage = Expenses.Count == 0 ? "No matching expenses." : $"{Expenses.Count} expense(s) shown.";
    }

    private static bool HasPaymentMode(BsonDocument doc, string mode)
    {
        if (!doc.TryGetValue("payments", out var payments) || !payments.IsBsonArray || payments.AsBsonArray.Count == 0)
            return string.Equals(mode, ExpensePaymentMode.Cash, StringComparison.OrdinalIgnoreCase);
        return payments.AsBsonArray.OfType<BsonDocument>().Any(p =>
            string.Equals(DailyExpenseDomain.ReadString(p, "mode") ?? DailyExpenseDomain.ReadString(p, "provider"), mode, StringComparison.OrdinalIgnoreCase)
            && DailyExpenseDomain.ReadDecimal(p, "amount") > 0);
    }

    private static DailyExpenseRow ToRow(BsonDocument doc)
    {
        var tax = DailyExpenseDomain.ReadDecimal(doc, "cgstAmount") + DailyExpenseDomain.ReadDecimal(doc, "sgstAmount") + DailyExpenseDomain.ReadDecimal(doc, "igstAmount");
        return new DailyExpenseRow
        {
            Document = doc,
            ExpenseNo = DailyExpenseDomain.ReadString(doc, "expenseNo") ?? "",
            Supplier = DailyExpenseDomain.ReadString(doc, "supplierName") ?? "—",
            InvoiceNo = DailyExpenseDomain.ReadString(doc, "supplierInvoiceNo") ?? "—",
            Category = DailyExpenseDomain.ReadString(doc, "category") ?? "—",
            Description = DailyExpenseDomain.ReadString(doc, "description") ?? "",
            GstDisplay = tax > 0 ? MoneyMath.FormatRupee(tax) : "—",
            PaymentDisplay = FormatPayments(doc),
            AmountDisplay = MoneyMath.FormatRupee(DailyExpenseDomain.ReadDecimal(doc, "amount")),
            PostedAtLocal = FormatPostedLocal(DailyExpenseDomain.ReadString(doc, "createdAtUtc") ?? ""),
            BusinessDate = DailyExpenseDomain.ReadString(doc, "businessDate") ?? "",
            Status = DailyExpenseDomain.ReadString(doc, "status") ?? "posted",
        };
    }

    private static string FormatPayments(BsonDocument doc)
    {
        if (!doc.TryGetValue("payments", out var value) || !value.IsBsonArray || value.AsBsonArray.Count == 0)
            return $"Cash {MoneyMath.FormatRupee(DailyExpenseDomain.ReadDecimal(doc, "amount"))}";
        return string.Join(", ", value.AsBsonArray.OfType<BsonDocument>()
            .Where(p => DailyExpenseDomain.ReadDecimal(p, "amount") > 0)
            .Select(p => $"{DailyExpenseDomain.ReadString(p, "mode") ?? "Other"} {MoneyMath.FormatRupee(DailyExpenseDomain.ReadDecimal(p, "amount"))}"));
    }

    partial void OnSelectedDateChanged(DateTime value) => _ = RefreshCommand.ExecuteAsync(null);
    partial void OnAmountTextChanged(string value) => Recalculate();
    partial void OnSelectedGstModeChanged(string value) => Recalculate();
    partial void OnSelectedGstRateChanged(string value) => Recalculate();
    partial void OnSelectedSupplyTypeChanged(string value) => Recalculate();
    partial void OnSupplierGstinChanged(string value) => RefreshSupplyTypeFromGstin();
    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnFilterCategoryChanged(string value) => ApplyFilters();
    partial void OnFilterPaymentModeChanged(string value) => ApplyFilters();
    partial void OnFilterStatusChanged(string value) => ApplyFilters();
    partial void OnEditingExpenseNoChanged(string value)
    {
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(SaveButtonText));
    }

    private static string ToGstMode(string? display) =>
        display?.StartsWith("Incl", StringComparison.OrdinalIgnoreCase) == true ? ExpenseGstMode.Inclusive :
        display?.StartsWith("Excl", StringComparison.OrdinalIgnoreCase) == true ? ExpenseGstMode.Exclusive :
        ExpenseGstMode.None;

    private void RefreshSupplyTypeFromGstin()
    {
        SupplierStateCode = GstStateCodeResolver.ExtractStateCode(SupplierGstin) ?? "";
        StoreStateCode = GstStateCodeResolver.ExtractStateCode(_services.ReceiptConfig.Current.Store.Gstin) ?? "";
        IsSupplyTypeAutomatic = SupplierStateCode.Length == 2 && StoreStateCode.Length == 2;
        CanSelectSupplyType = !IsSupplyTypeAutomatic;
        if (IsSupplyTypeAutomatic)
        {
            SelectedSupplyType = string.Equals(SupplierStateCode, StoreStateCode, StringComparison.Ordinal)
                ? SupplyTypes[0]
                : SupplyTypes[1];
        }
    }

    private static string ToGstDisplay(string? mode) =>
        string.Equals(mode, ExpenseGstMode.Inclusive, StringComparison.OrdinalIgnoreCase) ? "Inclusive" :
        string.Equals(mode, ExpenseGstMode.Exclusive, StringComparison.OrdinalIgnoreCase) ? "Exclusive" :
        "No GST";

    private static bool TryMoney(string? text, out decimal value) =>
        decimal.TryParse((text ?? "").Trim(), NumberStyles.Number, InCulture, out value)
        || decimal.TryParse((text ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    private static BsonDocument MapCentralExpenseToDoc(JsonElement element)
    {
        var doc = element.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object
            ? BsonDocument.Parse(payload.GetRawText())
            : BsonDocument.Parse(element.GetRawText());
        if (element.TryGetProperty("expenseNo", out var no) && no.ValueKind == JsonValueKind.String) doc["expenseNo"] = no.GetString() ?? "";
        if (!doc.Contains("status")) doc["status"] = "posted";
        return doc;
    }

    private static string FormatPostedLocal(string createdAtUtc)
    {
        if (!DateTime.TryParse(createdAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var utc)) return "—";
        return utc.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture);
    }
}
