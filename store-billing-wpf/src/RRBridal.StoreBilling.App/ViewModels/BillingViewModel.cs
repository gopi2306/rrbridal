using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
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
    [ObservableProperty] private string _salesman = "John Miller";
    [ObservableProperty] private string _customerPhone = "";
    [ObservableProperty] private string _doorNo = "";
    [ObservableProperty] private string _street = "";
    [ObservableProperty] private string _fullAddress = "";

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
    [ObservableProperty] private string _payableTotalFormatted = "₹ 0.00";

    [ObservableProperty] private string _cgstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _sgstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _igstTotalFormatted = "₹ 0.00";

    [ObservableProperty] private bool _isInterState;

    [ObservableProperty] private decimal _itemDiscount;

    [ObservableProperty] private decimal _cashDiscPercent;
    [ObservableProperty] private decimal _cashDiscAmount;
    [ObservableProperty] private decimal _roundOff;

    public ObservableCollection<BillingLineItem> Lines { get; } = new();

    public BillingViewModel(AppServices services)
    {
        _services = services;
        _customerLookup = new CustomerLookupService(services.LocalDb, services.CentralApi);
        AssignNewBillIdentity();
        Lines.CollectionChanged += OnLinesCollectionChanged;
        RecalculateTotals();
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

    private void RecalculateTotals()
    {
        var sub = Lines.Sum(l => l.Amount);
        var cgst = Lines.Sum(l => l.CgstAmount);
        var sgst = Lines.Sum(l => l.SgstAmount);
        var igst = Lines.Sum(l => l.IgstAmount);
        var tax = cgst + sgst + igst;
        var gross = sub;
        var afterDisc = gross - ItemDiscount - CashDiscAmount + RoundOff;
        var payable = afterDisc + tax;

        SubTotalFormatted = FormatRupee(sub);
        TaxTotalFormatted = FormatRupee(tax);
        CgstTotalFormatted = FormatRupee(cgst);
        SgstTotalFormatted = FormatRupee(sgst);
        IgstTotalFormatted = FormatRupee(igst);
        GrossTotalFormatted = FormatRupee(gross);
        ItemDiscountFormatted = FormatRupee(ItemDiscount);
        PayableTotalFormatted = FormatRupee(payable);
    }

    partial void OnItemDiscountChanged(decimal value) => RecalculateTotals();
    partial void OnCashDiscAmountChanged(decimal value) => RecalculateTotals();
    partial void OnRoundOffChanged(decimal value) => RecalculateTotals();

    partial void OnIsInterStateChanged(bool value)
    {
        foreach (var line in Lines)
            line.IsIgst = value;
        RecalculateTotals();
    }

    private static string FormatRupee(decimal value) => "₹ " + value.ToString("N2", InCulture);

    public void ApplyCustomerRegistration(CustomerRegistrationResult result)
    {
        CustomerCode = result.BillingCustomerCode;
        CustomerName = result.CustomerName;
        CustomerPhone = result.CustomerPhone;
        DoorNo = result.DoorNo;
        Street = result.Street;
        FullAddress = result.FullAddress;
    }

    public void ApplyCustomerMatch(CustomerMatch match)
    {
        CustomerCode = !string.IsNullOrWhiteSpace(match.Code) ? match.Code : match.Id;
        CustomerName = match.Name;
        CustomerPhone = match.Phone;
        DoorNo = match.DoorNo;
        Street = match.Street;
        FullAddress = match.FullAddress;
    }

    [RelayCommand]
    private async Task SearchCustomer()
    {
        var query = (CustomerPhone ?? "").Trim();
        if (string.IsNullOrEmpty(query))
            query = (CustomerName ?? "").Trim();
        if (string.IsNullOrEmpty(query))
            query = (CustomerCode ?? "").Trim();

        if (string.IsNullOrEmpty(query))
        {
            MessageBox.Show("Enter a mobile number, customer name, or code to search.", "RR Bridal Billing",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
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
    private void AddManualLine()
    {
        Lines.Add(new BillingLineItem
        {
            ProductCode = "",
            Description = "",
            Qty = 1,
            Rate = 0,
            Mrp = 0,
            TaxPercent = 18,
            IsIgst = IsInterState,
        });
    }

    [RelayCommand]
    private void ImportCsv()
    {
        MessageBox.Show("CSV import is not implemented yet.", "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ClearForNewBill()
    {
        CustomerCode = "";
        CustomerName = "";
        CustomerPhone = "";
        DoorNo = "";
        Street = "";
        FullAddress = "";
        AssignNewBillIdentity();
        Lines.Clear();
        ItemDiscount = 0;
        CashDiscAmount = 0;
        RoundOff = 0;
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

        var sub = Lines.Sum(l => l.Amount);
        var cgst = Lines.Sum(l => l.CgstAmount);
        var sgst = Lines.Sum(l => l.SgstAmount);
        var igst = Lines.Sum(l => l.IgstAmount);
        var tax = cgst + sgst + igst;
        var payable = sub - ItemDiscount - CashDiscAmount + RoundOff + tax;

        var paymentVm = new PaymentDialogViewModel(_services.PaymentRouter, BillNo, payable);
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
                { "doorNo", DoorNo },
                { "street", Street },
                { "fullAddress", FullAddress },
                { "holdBills", HoldBills },
                { "doorDelivery", DoorDelivery },
                { "printInvoice", PrintInvoice },
                { "isInterState", IsInterState },
                { "itemDiscount", (double)ItemDiscount },
                { "cashDiscAmount", (double)CashDiscAmount },
                { "roundOff", (double)RoundOff },
                { "subTotal", (double)sub },
                { "cgstTotal", (double)cgst },
                { "sgstTotal", (double)sgst },
                { "igstTotal", (double)igst },
                { "taxTotal", (double)tax },
                { "payable", (double)payable },
                { "lines", linesArr },
                { "payments", paymentsArr },
                { "paymentMode", paymentVm.SelectedMode.ToString() },
                { "status", "posted" },
                { "createdAtUtc", DateTime.UtcNow.ToString("O") },
            };

            await coll.InsertOneAsync(doc);

            foreach (var line in Lines.Where(l => l.Amount > 0 && !string.IsNullOrWhiteSpace(l.CentralProductId)))
                await _services.ProductCatalog.DecrementStockAsync(line.CentralProductId!, line.Qty, ct: default);

            if (PrintInvoice)
                PrintReceiptSilently();

            ClearForNewBill();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save bill: {ex.Message}", "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PrintReceiptSilently()
    {
        try
        {
            _services.ReceiptConfig.Reload();
            var text = ThermalInvoiceTextBuilder.Build(BuildThermalInput());
            var dlg = new InvoicePrintPreviewWindow(_services, text, printInvoiceEnabled: true)
            {
                Owner = Application.Current.MainWindow,
            };
            dlg.ShowDialog();
        }
        catch
        {
            // best-effort: don't block the reset if printing fails
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
            "• Add manual — empty line for ad-hoc items.\n" +
            "• F9 — post bill (saves to local store_bills in Mongo).\n" +
            "• F10 — invoice preview / print (thermal format).\n" +
            "• Set STORE_ID, DEVICE_ID and STORE_MONGO_URI env vars for multi-store.\n" +
            "• F12 — exit.",
            "RR Bridal Billing",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    [RelayCommand]
    private void PrintStub()
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

        _services.ReceiptConfig.Reload();
        var text = ThermalInvoiceTextBuilder.Build(BuildThermalInput());
        var dlg = new InvoicePrintPreviewWindow(_services, text, PrintInvoice)
        {
            Owner = Application.Current.MainWindow,
        };
        dlg.ShowDialog();
    }

    private ThermalInvoiceInput BuildThermalInput()
    {
        var store = _services.ReceiptConfig.Current.Store;
        var print = _services.ReceiptConfig.Current.Print;
        var active = Lines.Where(l => l.Amount > 0).ToList();
        var snaps = active
            .Select(l => new InvoiceLineSnap
            {
                LineNo = l.LineNo,
                Description = l.Description,
                Hsn = l.HsnCode ?? "",
                TaxPercent = l.TaxPercent,
                Qty = l.Qty,
                Rate = l.Rate,
                Mrp = l.Mrp,
                Amount = l.Amount,
            })
            .ToList();

        var sub = Lines.Sum(l => l.Amount);
        var cgst = Lines.Sum(l => l.CgstAmount);
        var sgst = Lines.Sum(l => l.SgstAmount);
        var igst = Lines.Sum(l => l.IgstAmount);
        var tax = cgst + sgst + igst;
        var payable = sub - ItemDiscount - CashDiscAmount + RoundOff + tax;
        var totalQty = active.Sum(l => l.Qty);
        var totalMrp = active.Sum(l => l.Mrp * l.Qty);
        var totalLineAmount = active.Sum(l => l.Amount);
        var savings = active.Sum(l => Math.Max(0m, l.Mrp * l.Qty - l.Amount));

        return new ThermalInvoiceInput
        {
            Store = store,
            CharWidth = print.ReceiptCharWidth,
            BillNo = BillNo,
            BillDate = BillDateDisplay,
            UserName = Salesman,
            Time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            Counter = Environment.GetEnvironmentVariable("POS_COUNTER") ?? "1",
            Lines = snaps,
            SubTotal = sub,
            TaxTotal = tax,
            IsInterState = IsInterState,
            CgstTotal = cgst,
            SgstTotal = sgst,
            IgstTotal = igst,
            ItemDiscount = ItemDiscount,
            CashDiscAmount = CashDiscAmount,
            RoundOff = RoundOff,
            Payable = payable,
            TotalQty = totalQty,
            ItemCount = active.Count,
            TotalMrp = totalMrp,
            TotalLineAmount = totalLineAmount,
            Savings = savings,
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
