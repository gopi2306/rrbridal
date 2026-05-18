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

    public Action? NavigateToCustomerRegistration { get; set; }

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

    [ObservableProperty] private string _itemDiscountPercentText = "0";

    [ObservableProperty] private decimal _itemDiscountPercent;

    /// <summary>Total item discount (₹), sum of proportional line discounts.</summary>
    public decimal ItemDiscount => Lines.Sum(l => l.DiscountAmount);

    [ObservableProperty] private string _cashDiscAmountText = "0";

    private decimal _cashDiscTarget;

    /// <summary>Total cash discount (₹), sum of proportional line cash discounts.</summary>
    public decimal CashDiscAmount => Lines.Sum(l => l.CashDiscountAmount);

    [ObservableProperty] private string _roundOffText = "0";

    [ObservableProperty] private decimal _roundOff;

    private bool _suppressDiscountTextSync;
    private bool _suppressPhoneAutoSearch;
    private bool _roundOffUserEdited;
    private BillTotals _lastBillTotals = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public ObservableCollection<BillingLineItem> Lines { get; } = new();

    public BillingViewModel(AppServices services)
    {
        _services = services;
        _customerLookup = new CustomerLookupService(services.LocalDb, services.CentralApi);
        AssignNewBillIdentity();
        ApplyLoggedInSalesman();
        Lines.CollectionChanged += OnLinesCollectionChanged;
        RecalculateTotals();
    }

    private void ApplyLoggedInSalesman()
    {
        Salesman = _services.UserSession?.LoggedInUser.Name?.Trim() ?? "";
    }

    private void AssignNewBillIdentity()
    {
        BillNo = $"{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(100, 999)}";
        BillDateDisplay = DateTime.Now.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
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
            or nameof(BillingLineItem.TaxAmount))
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

    private void ApplyProportionalItemDiscount()
    {
        var sub = Lines.Sum(l => l.Amount);
        if (sub <= 0 || ItemDiscountPercent <= 0)
        {
            foreach (var line in Lines)
                line.DiscountAmount = 0;
            return;
        }

        var totalDisc = Math.Round(sub * ItemDiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
        var active = Lines.Where(l => l.Amount > 0).ToList();
        if (active.Count == 0)
        {
            foreach (var line in Lines)
                line.DiscountAmount = 0;
            return;
        }

        var allocated = 0m;
        for (var i = 0; i < active.Count; i++)
        {
            var line = active[i];
            if (i == active.Count - 1)
                line.DiscountAmount = Math.Max(0m, totalDisc - allocated);
            else
            {
                var share = Math.Round(line.Amount / sub * totalDisc, 2, MidpointRounding.AwayFromZero);
                line.DiscountAmount = share;
                allocated += share;
            }
        }

        foreach (var line in Lines.Where(l => l.Amount <= 0))
            line.DiscountAmount = 0;
    }

    private void ApplyProportionalCashDiscount()
    {
        var sub = Lines.Sum(l => l.Amount);
        if (sub <= 0 || _cashDiscTarget <= 0)
        {
            foreach (var line in Lines)
                line.CashDiscountAmount = 0;
            return;
        }

        var totalCash = Math.Round(_cashDiscTarget, 2, MidpointRounding.AwayFromZero);
        var active = Lines.Where(l => l.Amount > 0).ToList();
        if (active.Count == 0)
        {
            foreach (var line in Lines)
                line.CashDiscountAmount = 0;
            return;
        }

        var allocated = 0m;
        for (var i = 0; i < active.Count; i++)
        {
            var line = active[i];
            if (i == active.Count - 1)
                line.CashDiscountAmount = Math.Max(0m, totalCash - allocated);
            else
            {
                var share = Math.Round(line.Amount / sub * totalCash, 2, MidpointRounding.AwayFromZero);
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

    private sealed record BillTotals(
        decimal SubTotal,
        decimal ItemDiscount,
        decimal CashDiscount,
        decimal Cgst,
        decimal Sgst,
        decimal Igst,
        decimal TaxTotal,
        decimal GrandBeforeRound,
        decimal RoundOff,
        decimal Payable);

    private BillTotals ComputeBillTotals()
    {
        ApplyProportionalItemDiscount();
        ApplyProportionalCashDiscount();

        var sub = Lines.Sum(l => l.Amount);
        var itemDisc = ItemDiscount;
        var cashDisc = CashDiscAmount;
        var cgst = Lines.Sum(l => l.CgstAmount);
        var sgst = Lines.Sum(l => l.SgstAmount);
        var igst = Lines.Sum(l => l.IgstAmount);
        var tax = cgst + sgst + igst;
        var grandBeforeRound = sub - itemDisc - cashDisc + tax;

        decimal roundOff;
        if (!_roundOffUserEdited)
        {
            var payableRounded = Math.Round(grandBeforeRound, 0, MidpointRounding.AwayFromZero);
            roundOff = payableRounded - grandBeforeRound;
            if (RoundOff != roundOff)
            {
                _suppressDiscountTextSync = true;
                RoundOff = roundOff;
                RoundOffText = roundOff.ToString("0.##", CultureInfo.InvariantCulture);
                _suppressDiscountTextSync = false;
            }
        }
        else
        {
            roundOff = RoundOff;
        }

        var payable = grandBeforeRound + roundOff;
        return new BillTotals(sub, itemDisc, cashDisc, cgst, sgst, igst, tax, grandBeforeRound, roundOff, payable);
    }

    private void RecalculateTotals()
    {
        var totals = ComputeBillTotals();
        _lastBillTotals = totals;

        SubTotalFormatted = FormatRupee(totals.SubTotal);
        TaxTotalFormatted = FormatRupee(totals.TaxTotal);
        CgstTotalFormatted = FormatRupee(totals.Cgst);
        SgstTotalFormatted = FormatRupee(totals.Sgst);
        IgstTotalFormatted = FormatRupee(totals.Igst);
        GrossTotalFormatted = FormatRupee(totals.SubTotal);
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
            _suppressDiscountTextSync = true;
            ItemDiscountPercentText = "0";
            ItemDiscountPercent = 0;
            _suppressDiscountTextSync = false;
            RecalculateTotals();
            return;
        }

        if (!TryParseDecimalInput(value, out var parsed))
        {
            _suppressDiscountTextSync = true;
            ItemDiscountPercentText = "0";
            ItemDiscountPercent = 0;
            _suppressDiscountTextSync = false;
            RecalculateTotals();
            return;
        }

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

        var text = value.ToString("0.##", CultureInfo.InvariantCulture);
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
            _suppressDiscountTextSync = true;
            CashDiscAmountText = "0";
            _cashDiscTarget = 0;
            _suppressDiscountTextSync = false;
            RecalculateTotals();
            return;
        }

        if (!TryParseDecimalInput(value, out var parsed))
        {
            _suppressDiscountTextSync = true;
            CashDiscAmountText = "0";
            _cashDiscTarget = 0;
            _suppressDiscountTextSync = false;
            RecalculateTotals();
            return;
        }

        _cashDiscTarget = Math.Max(0m, parsed);
        RecalculateTotals();
    }

    partial void OnRoundOffTextChanged(string value)
    {
        if (_suppressDiscountTextSync) return;

        _roundOffUserEdited = true;

        if (string.IsNullOrWhiteSpace(value))
        {
            _suppressDiscountTextSync = true;
            RoundOffText = "0";
            RoundOff = 0;
            _suppressDiscountTextSync = false;
            RecalculateTotals();
            return;
        }

        if (!TryParseDecimalInput(value, out var parsed))
        {
            _suppressDiscountTextSync = true;
            RoundOffText = "0";
            RoundOff = 0;
            _suppressDiscountTextSync = false;
            RecalculateTotals();
            return;
        }

        RoundOff = parsed;
        RecalculateTotals();
    }

    partial void OnRoundOffChanged(decimal value)
    {
        var text = value.ToString("0.##", CultureInfo.InvariantCulture);
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
    }

    [RelayCommand]
    private Task SearchCustomer() => SearchCustomerCoreAsync(phoneSearchOnly: false);

    [RelayCommand]
    private Task SearchCustomerByPhone() => SearchCustomerCoreAsync(phoneSearchOnly: true);

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

        var dlg = new CustomerSearchDialog(query, _customerLookup)
        {
            Owner = Application.Current.MainWindow
        };
        var result = dlg.ShowDialog();

        if (result == true && dlg.SelectedCustomer != null)
        {
            ApplyCustomerMatch(dlg.SelectedCustomer);
        }
        else if (dlg.WantsNewRegistration)
        {
            NavigateToCustomerRegistration?.Invoke();
        }
    }

    public async Task OpenProductSearchAsync(CancellationToken ct = default)
    {
        var q = (SearchText ?? "").Trim();
        if (q.Length >= 1)
        {
            var items = await _services.ProductCatalog.SearchAsync(q, ct);
            if (items.Count == 1)
            {
                AddLineFromCatalog(items[0]);
                SearchText = "";
                return;
            }

            if (items.Count == 0)
            {
                var existing = await _services.ProductCatalog.FindBySkuOrBarcodeAsync(q, ct);
                if (existing != null && existing.StockQty <= 0)
                {
                    MessageBox.Show(
                        $"Product \"{existing.Name}\" (SKU: {existing.Sku}) is out of stock.\nA reference indent request has been created.",
                        "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await AddIndentRequestAsync(existing.Sku, existing.Name, existing.CentralId, ct);
                    SearchText = "";
                    return;
                }

                MessageBox.Show(
                    $"No product found in local inventory for \"{q}\".\nA reference indent request has been created.",
                    "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Warning);
                await AddIndentRequestAsync(q, q, "", ct);
                SearchText = "";
                return;
            }
        }

        var dlg = new ProductSearchDialog(SearchText ?? "", _services) { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() != true || dlg.SelectedProduct == null)
            return;

        AddLineFromCatalog(dlg.SelectedProduct);
        SearchText = "";
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

    private void AddLineFromCatalog(CatalogProduct p)
    {
        var sku = (p.Sku ?? "").Trim();
        if (!string.IsNullOrEmpty(sku))
        {
            var existing = Lines.FirstOrDefault(l =>
                string.Equals((l.ProductCode ?? "").Trim(), sku, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Qty += 1;
                return;
            }
        }

        Lines.Add(new BillingLineItem
        {
            CentralProductId = p.CentralId ?? "",
            ProductCode = p.Sku,
            Description = p.Name,
            HsnCode = string.IsNullOrWhiteSpace(p.HsnSac) ? "" : p.HsnSac.Trim(),
            Qty = 1,
            Rate = p.SuggestedRate,
            Mrp = p.Mrp ?? 0,
            TaxPercent = p.SuggestedTaxPercent,
            IsIgst = IsInterState,
        });
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

        await TryAddProductByCodeAsync(dlg.ProductCode, ct);
    }

    private async Task TryAddProductByCodeAsync(string code, CancellationToken ct = default)
    {
        var q = (code ?? "").Trim();
        if (string.IsNullOrEmpty(q))
            return;

        var existing = await _services.ProductCatalog.FindBySkuOrBarcodeAsync(q, ct);
        if (existing != null)
        {
            if (existing.StockQty <= 0)
            {
                MessageBox.Show(
                    $"Product \"{existing.Name}\" (SKU: {existing.Sku}) is out of stock.\nA reference indent request has been created.",
                    "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Warning);
                await AddIndentRequestAsync(existing.Sku, existing.Name, existing.CentralId, ct);
                return;
            }

            AddLineFromCatalog(existing);
            return;
        }

        var items = await _services.ProductCatalog.SearchAsync(q, ct);
        if (items.Count == 1)
        {
            AddLineFromCatalog(items[0]);
            return;
        }

        if (items.Count > 1)
        {
            var pickDlg = new ProductSearchDialog(q, _services) { Owner = Application.Current.MainWindow };
            if (pickDlg.ShowDialog() == true && pickDlg.SelectedProduct != null)
                AddLineFromCatalog(pickDlg.SelectedProduct);
            return;
        }

        MessageBox.Show(
            $"No product found in local inventory for \"{q}\".\nA reference indent request has been created.",
            "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Warning);
        await AddIndentRequestAsync(q, q, "", ct);
    }

    [RelayCommand]
    private void ImportCsv()
    {
        MessageBox.Show("CSV import is not implemented yet.", "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void RemoveLine(BillingLineItem? line)
    {
        if (line != null)
            Lines.Remove(line);
    }

    [RelayCommand]
    private void ClearForNewBill()
    {
        _suppressPhoneAutoSearch = true;
        CustomerCode = "";
        CustomerName = "";
        CustomerPhone = "";
        _suppressPhoneAutoSearch = false;
        AssignNewBillIdentity();
        ApplyLoggedInSalesman();
        Lines.Clear();
        _suppressDiscountTextSync = true;
        ItemDiscountPercentText = "0";
        ItemDiscountPercent = 0;
        CashDiscAmountText = "0";
        _cashDiscTarget = 0;
        RoundOffText = "0";
        RoundOff = 0;
        _roundOffUserEdited = false;
        _suppressDiscountTextSync = false;
        IsInterState = false;
        SearchText = "";
    }

    [RelayCommand]
    private async Task PostBill()
    {
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

            var doc = new BsonDocument
            {
                { "billNo", BillNo },
                { "billDate", BillDateDisplay },
                { "storeId", storeId },
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

            await coll.InsertOneAsync(doc);

            foreach (var line in Lines.Where(l => l.Amount > 0 && !string.IsNullOrWhiteSpace(l.CentralProductId)))
                await _services.ProductCatalog.DecrementStockAsync(line.CentralProductId!, line.Qty, ct: default);

            ThermalInvoiceInput? printInput = null;
            if (PrintInvoice)
                printInput = BuildThermalInput(paymentVm.Outcome);

            ClearForNewBill();

            if (printInput != null)
                await ShowInvoicePrintDialogAsync(paymentVm.Outcome, printInvoiceEnabled: true, prebuiltInput: printInput);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save bill: {ex.Message}", "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ShowInvoicePrintDialogAsync(
        PaymentOutcome? paymentOutcome,
        bool printInvoiceEnabled,
        ThermalInvoiceInput? prebuiltInput = null)
    {
        try
        {
            _services.CentralAuthSession.ApplyTo(_services.CentralApi);
            var (profileOk, profileMsg) = await _services.ReceiptConfigSync.EnsureProfileReadyForPrintAsync();
            if (!profileOk)
            {
                MessageBox.Show(
                    profileMsg,
                    "Receipt settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var input = prebuiltInput ?? BuildThermalInput(paymentOutcome);
            var text = ThermalInvoiceTextBuilder.Build(input);
            var assets = await ThermalReceiptDocumentBuilder.BuildAssetsAsync(
                _services.ReceiptConfig.Current,
                input.BillNo,
                _services.ReceiptLogoCache);
            var fontSize = input.CharWidth >= 48 ? 9.0 : 10.0;
            var doc = BillPrintService.CreateReceiptDocument(text, assets, fontSize);
            var dlg = new InvoicePrintPreviewWindow(_services, doc, text, printInvoiceEnabled)
            {
                Owner = Application.Current.MainWindow,
            };
            dlg.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open invoice preview: {ex.Message}", "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private static void ShowHelp()
    {
        MessageBox.Show(
            "Store billing flow:\n" +
            "• Settings (gear): login to central, Run sync once — fills local product cache.\n" +
            "• Barcode labels: store the printed code in central product field barcode (or sku), then sync — scan types into search; one match adds the line automatically.\n" +
            "• F3 — focus search; type SKU/barcode/name and press Enter — pick product (or auto-add if exactly one match).\n" +
            "• F2 — new bill (clears lines).\n" +
            "• Add manual — enter product code (SKU/barcode) to add a line.\n" +
            "• F9 — post bill (saves to local store_bills in Mongo).\n" +
            "• F10 — invoice preview / print (thermal format).\n" +
            "• Set STORE_ID, DEVICE_ID and STORE_MONGO_URI env vars for multi-store.\n" +
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

        await ShowInvoicePrintDialogAsync(paymentOutcome: null, printInvoiceEnabled: PrintInvoice);
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
                var taxable = Math.Max(0m, l.Amount - lineDisc);
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
                    TaxableAmount = taxable,
                    TaxAmount = l.TaxAmount,
                };
            })
            .ToList();

        var totals = _lastBillTotals;
        var totalQty = active.Sum(l => l.Qty);
        var totalMrp = active.Sum(l => l.Mrp * l.Qty);
        var totalLineAmount = active.Sum(l => l.Amount);
        var totalTaxable = snaps.Sum(l => l.TaxableAmount);
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
            Counter = Environment.GetEnvironmentVariable("POS_COUNTER") ?? "1",
            CustomerName = CustomerName ?? "",
            CustomerPhone = CustomerPhone ?? "",
            Lines = snaps,
            SubTotal = totals.SubTotal,
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
    private static void LogStub()
    {
        MessageBox.Show("Activity log is not implemented yet.", "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private static void CloseApp()
    {
        Application.Current.Shutdown();
    }
}
