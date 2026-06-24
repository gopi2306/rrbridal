using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Audit;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.WhatsApp;
using RRBridal.StoreBilling.App.Services.Payments;
using RRBridal.StoreBilling.App.Services.Customers;
using RRBridal.StoreBilling.App.Services.PurchaseIntents;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppServices _services;

    [ObservableProperty] private string _loginEmail = "";
    [ObservableProperty] private string _loginPassword = "";
    [ObservableProperty] private string _authStatusText = "";
    [ObservableProperty] private string _pendingOutboxText = "";
    [ObservableProperty] private string _cursorText = "";
    [ObservableProperty] private string _lastErrorText = "";

    [ObservableProperty] private string _syncDiagnosticsText = "";
    [ObservableProperty] private string _autoSyncStatusText = "";
    [ObservableProperty] private string _lastActionText = "";

    [ObservableProperty] private string _receiptStoreName = "";

    [ObservableProperty] private string _receiptAddress = "";

    [ObservableProperty] private string _receiptCustomerCarePhone = "";

    [ObservableProperty] private string _receiptGstin = "";

    [ObservableProperty] private string _receiptFssaiNo = "";

    [ObservableProperty] private string _receiptBranchCode = "";

    [ObservableProperty] private string _receiptWebsite = "";

    [ObservableProperty] private string _receiptTerms = "";

    [ObservableProperty] private string _receiptPolicyLinesText = "";

    [ObservableProperty] private string _receiptThankYouLine = "";

    [ObservableProperty] private bool _receiptAlwaysUsePrintDialog;

    [ObservableProperty] private string? _selectedThermalPrinterFullName;

    [ObservableProperty] private string? _selectedOfficePrinterFullName;

    [ObservableProperty] private int _receiptCharWidth = 48;

    [ObservableProperty] private InvoicePrintFormat _receiptPrintFormat = InvoicePrintFormat.Thermal;

    [ObservableProperty] private bool _a5PrePrintedEnabled;

    [ObservableProperty] private bool _alsoPrintThermalFirst;

    [ObservableProperty] private string _receiptCentralSyncText = "(not synced from central yet)";

    [ObservableProperty] private string _receiptPrinterHintText = "";

    [ObservableProperty] private string _receiptPrinterWarningText = "";

    [ObservableProperty] private bool _billingAllowDuplicatePrint = true;
    [ObservableProperty] private bool _billingAllowCreditNoteRemainingCashout;
    [ObservableProperty] private bool _billingAllowMultipleReturnsPerBill;
    [ObservableProperty] private bool _billingAlterationGstIncluded;
    [ObservableProperty] private BillingLineItemDetailLevel _billingLineItemDetailLevel = BillingLineItemDetailLevel.Full;

    [ObservableProperty] private bool _razorpayPosEnabled;
    [ObservableProperty] private string _razorpayPosUsername = "";
    [ObservableProperty] private string _razorpayPosAppKey = "";
    [ObservableProperty] private string _razorpayPosApiBaseUrl = "https://www.ezetap.com/api/3.0/p2padapter/";
    [ObservableProperty] private string _razorpayPosDeviceId = "";
    [ObservableProperty] private int _razorpayPosStatusPollIntervalMs = 2000;
    [ObservableProperty] private int _razorpayPosStatusTimeoutSeconds = 120;
    [ObservableProperty] private string _razorpayPosStatusText = "";

    public IReadOnlyList<BillingLineItemDetailOption> BillingLineItemDetailOptions { get; } =
    [
        new(BillingLineItemDetailLevel.Minimal, "Minimal", "Code, description, qty, rate, amount"),
        new(BillingLineItemDetailLevel.Standard, "Standard", "Minimal + HSN, discount, MRP, tax %, tax amount"),
        new(BillingLineItemDetailLevel.Full, "Full", "All columns including scheme, revised tax, CGST/SGST/IGST"),
    ];

    public string BillingLineItemDetailDescription =>
        BillingLineItemDetailOptions.First(o => o.Level == BillingLineItemDetailLevel).Description;

    partial void OnBillingLineItemDetailLevelChanged(BillingLineItemDetailLevel value) =>
        OnPropertyChanged(nameof(BillingLineItemDetailDescription));

    [ObservableProperty] private bool _whatsAppAutoSendAfterPost = true;

    [ObservableProperty] private string _whatsAppConnectionStatus = "—";

    [ObservableProperty] private string _whatsAppTemplateSummary = "—";

    [ObservableProperty] private string _whatsAppTestPhone = "";

    public ObservableCollection<PrinterOption> PrinterOptions { get; } = new();

    public A5PrePrintedLayoutSettingsViewModel A5Layout { get; } = new();

    public IReadOnlyList<string> A5FontFamilyOptions { get; } =
    [
        "Arial",
        "Calibri",
        "Times New Roman",
        "Courier New",
        "Segoe UI",
    ];

    public bool IsA5PrePrintedSettingsVisible =>
        IsA5ReceiptFormat && A5PrePrintedEnabled;

    public bool IsThermalReceiptFormat
    {
        get => ReceiptPrintFormat == InvoicePrintFormat.Thermal;
        set
        {
            if (value)
                ReceiptPrintFormat = InvoicePrintFormat.Thermal;
        }
    }

    public bool IsA4ReceiptFormat
    {
        get => ReceiptPrintFormat == InvoicePrintFormat.A4;
        set
        {
            if (value)
                ReceiptPrintFormat = InvoicePrintFormat.A4;
        }
    }

    public bool IsA5ReceiptFormat
    {
        get => ReceiptPrintFormat == InvoicePrintFormat.A5;
        set
        {
            if (value)
                ReceiptPrintFormat = InvoicePrintFormat.A5;
        }
    }

    public bool IsOfficeInvoiceFormat =>
        ReceiptPrintFormat is InvoicePrintFormat.A4 or InvoicePrintFormat.A5;

    partial void OnReceiptPrintFormatChanged(InvoicePrintFormat value)
    {
        OnPropertyChanged(nameof(IsThermalReceiptFormat));
        OnPropertyChanged(nameof(IsA4ReceiptFormat));
        OnPropertyChanged(nameof(IsA5ReceiptFormat));
        OnPropertyChanged(nameof(IsOfficeInvoiceFormat));
        OnPropertyChanged(nameof(IsA5PrePrintedSettingsVisible));
    }

    partial void OnA5PrePrintedEnabledChanged(bool value) =>
        OnPropertyChanged(nameof(IsA5PrePrintedSettingsVisible));

    public SettingsViewModel(AppServices services)
    {
        _services = services;
        AuthStatusText = string.IsNullOrEmpty(_services.CentralAuthSession.AccessToken)
            ? "Central auth: not logged in"
            : "Central auth: token loaded";
        _services.PeriodicSync.StatusChanged += OnPeriodicSyncStatusChanged;
        UpdateAutoSyncStatusText();
        _ = RefreshStatusAsync();
    }

    private void OnPeriodicSyncStatusChanged()
    {
        OnPropertyChanged(nameof(AutoSyncStatusText));
        UpdateAutoSyncStatusText();
    }

    private void UpdateAutoSyncStatusText()
    {
        AutoSyncStatusText = _services.PeriodicSync.StatusDescription;
        if (!string.IsNullOrWhiteSpace(_services.PeriodicSync.LastRunMessage)
            && _services.PeriodicSync.LastRunUtc.HasValue)
        {
            AutoSyncStatusText += Environment.NewLine + _services.PeriodicSync.LastRunMessage;
        }
    }

    public Task LoadReceiptSettingsAsync(bool tryPullIfLoggedIn = false, bool forcePullFromCentral = false)
    {
        _services.CentralAuthSession.ApplyTo(_services.CentralApi);
        AuthStatusText = string.IsNullOrEmpty(_services.CentralAuthSession.AccessToken)
            ? "Central auth: not logged in"
            : "Central auth: logged in";

        _services.ReceiptConfig.Reload();
        ApplyReceiptFieldsFromConfig();
        LoadBillingSettingsFromStore();
        LoadRazorpayPosSettingsFromStore();
        LoadWhatsAppSettingsFromStore();
        _ = LoadWhatsAppCentralStatusAsync();
        return Task.CompletedTask;
    }

    public void LoadReceiptSettings() => _ = LoadReceiptSettingsAsync();

    private void ApplyReceiptFieldsFromConfig()
    {
        var c = _services.ReceiptConfig.Current;
        var s = c.Store;
        ReceiptStoreName = s.StoreName;
        ReceiptAddress = s.Address;
        ReceiptCustomerCarePhone = s.CustomerCarePhone;
        ReceiptGstin = s.Gstin;
        ReceiptFssaiNo = s.FssaiNo;
        ReceiptBranchCode = s.BranchCode;
        ReceiptWebsite = s.Website;
        ReceiptTerms = s.TermsAndConditions;
        ReceiptPolicyLinesText = string.Join(Environment.NewLine, s.PolicyLines ?? new List<string>());
        ReceiptThankYouLine = s.ThankYouLine;
        ReceiptAlwaysUsePrintDialog = c.Print.AlwaysUsePrintDialog;
        ApplyPrinterFieldsFromConfig(c.Print);
        ReceiptCharWidth = c.Print.ReceiptCharWidth is >= 32 and <= 56 ? c.Print.ReceiptCharWidth : 48;
        ReceiptPrintFormat = c.Print.PrintFormat;
        A5PrePrintedEnabled = c.Print.A5PrePrintedEnabled;
        AlsoPrintThermalFirst = c.Print.AlsoPrintThermalFirst;
        A5Layout.ApplyFrom(c.Print.A5PrePrintedLayout ?? A5PrePrintedLayoutSettings.CreateDefault());
        OnPropertyChanged(nameof(IsThermalReceiptFormat));
        OnPropertyChanged(nameof(IsA4ReceiptFormat));
        OnPropertyChanged(nameof(IsA5ReceiptFormat));
        OnPropertyChanged(nameof(IsOfficeInvoiceFormat));
        ReceiptCentralSyncText = c.LastReceiptSettingsSyncUtc.HasValue
            ? $"Last synced: {s.StoreName} at {c.LastReceiptSettingsSyncUtc.Value.ToLocalTime():g}"
            : "(not synced from central yet)";
        var hint = c.Print.CentralPrinterModel ?? c.Print.CentralPrinterHint;
        ReceiptPrinterHintText = string.IsNullOrWhiteSpace(hint)
            ? "Central printer hint: (none — pick a local queue below)"
            : $"Central printer hint: {hint}";
        UpdatePrinterWarning();
        RefreshPrinters();
    }

    private void ApplyPrinterFieldsFromConfig(ReceiptPrintSettings print)
    {
        var thermal = print.ThermalPrinterFullName;
        var office = print.OfficeInvoicePrinterFullName;
        if (string.IsNullOrWhiteSpace(thermal) && string.IsNullOrWhiteSpace(office)
            && !string.IsNullOrWhiteSpace(print.BillPrinterFullName))
        {
            thermal = print.BillPrinterFullName;
            office = print.BillPrinterFullName;
        }

        SelectedThermalPrinterFullName = thermal;
        SelectedOfficePrinterFullName = office;
    }

    private void UpdatePrinterWarning()
    {
        var print = _services.ReceiptConfig.Current.Print;
        if (string.IsNullOrWhiteSpace(print.CentralPrinterHint) && string.IsNullOrWhiteSpace(print.CentralPrinterModel))
        {
            ReceiptPrinterWarningText = "";
            return;
        }

        if (!string.IsNullOrWhiteSpace(SelectedThermalPrinterFullName)
            || !string.IsNullOrWhiteSpace(SelectedOfficePrinterFullName))
        {
            ReceiptPrinterWarningText = "";
            return;
        }

        ReceiptPrinterWarningText =
            "Central printer name did not match any queue on this PC — select thermal and A4/A5 printers manually.";
    }

    [RelayCommand]
    public void RefreshPrinters()
    {
        PrinterOptions.Clear();
        foreach (var option in InstalledPrinterDiscovery.ListAll())
            PrinterOptions.Add(option);
    }

    [RelayCommand]
    public void PullReceiptFromCentral()
    {
        _services.ReceiptConfig.Reload();
        ApplyReceiptFieldsFromConfig();
        LastActionText = "Loaded saved receipt settings. Company master updates only via Run sync once.";
        ReceiptCentralSyncText = _services.ReceiptConfig.Current.LastReceiptSettingsSyncUtc.HasValue
            ? $"Last synced via store sync: {_services.ReceiptConfig.Current.Store.StoreName} at {_services.ReceiptConfig.Current.LastReceiptSettingsSyncUtc!.Value.ToLocalTime():g}"
            : "Not synced yet — use Run sync once (after Central login).";
    }

    [RelayCommand]
    public async Task SaveReceiptSettingsAsync()
    {
        var c = _services.ReceiptConfig.Current;
        c.Store.StoreName = ReceiptStoreName.Trim();
        c.Store.Address = ReceiptAddress.Trim();
        c.Store.CustomerCarePhone = ReceiptCustomerCarePhone.Trim();
        c.Store.Gstin = ReceiptGstin.Trim();
        c.Store.FssaiNo = ReceiptFssaiNo.Trim();
        c.Store.BranchCode = ReceiptBranchCode.Trim();
        c.Store.Website = ReceiptWebsite.Trim();
        c.Store.TermsAndConditions = ReceiptTerms.Trim();
        c.Store.ThankYouLine = ReceiptThankYouLine.Trim();
        c.Store.PolicyLines = ReceiptPolicyLinesText
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();
        c.Print.AlwaysUsePrintDialog = ReceiptAlwaysUsePrintDialog;
        c.Print.ThermalPrinterFullName = string.IsNullOrWhiteSpace(SelectedThermalPrinterFullName)
            ? null
            : SelectedThermalPrinterFullName.Trim();
        c.Print.OfficeInvoicePrinterFullName = string.IsNullOrWhiteSpace(SelectedOfficePrinterFullName)
            ? null
            : SelectedOfficePrinterFullName.Trim();
        c.Print.BillPrinterFullName = ReceiptPrintFormat == InvoicePrintFormat.Thermal
            ? c.Print.ThermalPrinterFullName
            : c.Print.OfficeInvoicePrinterFullName;
        var w = ReceiptCharWidth;
        if (w < 32) w = 32;
        if (w > 56) w = 56;
        ReceiptCharWidth = w;
        c.Print.ReceiptCharWidth = w;
        c.Print.PrintFormat = ReceiptPrintFormat;
        c.Print.A5PrePrintedEnabled = A5PrePrintedEnabled;
        c.Print.AlsoPrintThermalFirst = ReceiptPrintFormat is InvoicePrintFormat.A4 or InvoicePrintFormat.A5
            && AlsoPrintThermalFirst;
        c.Print.A5PrePrintedLayout = A5Layout.ToSettings();
        await _services.ReceiptConfig.SaveAsync(CancellationToken.None);
        var (actorName, actorEmail) = StoreAuditLogService.ActorFromSession(_services.UserSession);
        await _services.StoreAuditLog.LogEventAsync(new StoreAuditEvent
        {
            EntityType = "settings",
            EntityId = "receipt_print",
            Action = "saved",
            ActorName = actorName,
            ActorEmail = actorEmail,
            Metadata = new BsonDocument
            {
                { "printFormat", ReceiptPrintFormat.ToString() },
                { "a5PrePrintedEnabled", A5PrePrintedEnabled },
                { "thermalPrinter", SelectedThermalPrinterFullName ?? "" },
                { "officePrinter", SelectedOfficePrinterFullName ?? "" },
            },
        });
        UpdatePrinterWarning();
        LastActionText = "Receipt and printer settings saved.";
    }

    partial void OnSelectedThermalPrinterFullNameChanged(string? value) => UpdatePrinterWarning();

    partial void OnSelectedOfficePrinterFullNameChanged(string? value) => UpdatePrinterWarning();

    [RelayCommand]
    public void ResetA5PrePrintedLayoutToDefaults()
    {
        A5Layout.ApplyFrom(A5PrePrintedLayoutSettings.CreateDefault());
        LastActionText = "A5 pre-printed alignment reset to defaults (save to persist).";
    }

    [RelayCommand]
    public void PreviewA5PrePrintedAlignment() =>
        ShowA5PrePrintedPreview(isDuplicate: false);

    [RelayCommand]
    public void PreviewA5PrePrintedDuplicateAlignment() =>
        ShowA5PrePrintedPreview(isDuplicate: true);

    private void ShowA5PrePrintedPreview(bool isDuplicate)
    {
        var layout = A5Layout.ToSettings();
        var store = _services.ReceiptConfig.Current.Store;
        var input = new ThermalInvoiceInput
        {
            Store = store,
            CharWidth = ReceiptCharWidth,
            BillNo = "TEST-001",
            BillDate = DateTime.Now.ToString("dd-MMM-yyyy").ToUpperInvariant(),
            UserName = "Test",
            Time = DateTime.Now.ToString("HH:mm"),
            Counter = "01",
            CustomerName = "Sample Customer Name Long",
            CustomerPhone = "9876543210",
            Stitching = true,
            DeliveryDate = DateTime.Now.AddDays(7).ToString("dd-MMM-yyyy").ToUpperInvariant(),
            IsDuplicateCopy = isDuplicate,
            Lines =
            [
                new InvoiceLineSnap { LineNo = 1, Description = "Bridal Lehenga", Qty = 1, Rate = 15000, Amount = 15000, TaxableAmount = 15000 },
                new InvoiceLineSnap { LineNo = 2, Description = "Dupatta", Qty = 2, Rate = 500, Amount = 1000, TaxableAmount = 1000 },
                new InvoiceLineSnap { LineNo = 3, Description = "Blouse piece", Qty = 1, Rate = 800, Amount = 800, TaxableAmount = 800 },
                new InvoiceLineSnap { LineNo = 4, Description = "Embroidery work", Qty = 1, Rate = 2500, Amount = 2500, TaxableAmount = 2500 },
                new InvoiceLineSnap { LineNo = 5, Description = "Border lace", Qty = 3, Rate = 150, Amount = 450, TaxableAmount = 450 },
                new InvoiceLineSnap { LineNo = 6, Description = "Stone work", Qty = 1, Rate = 1200, Amount = 1200, TaxableAmount = 1200 },
                new InvoiceLineSnap { LineNo = 7, Description = "Petticoat", Qty = 1, Rate = 600, Amount = 600, TaxableAmount = 600 },
                new InvoiceLineSnap { LineNo = 8, Description = "Chunni", Qty = 1, Rate = 900, Amount = 900, TaxableAmount = 900 },
                new InvoiceLineSnap { LineNo = 9, Description = "Alteration", Qty = 1, Rate = 400, Amount = 400, AlterationAmount = 400, TaxableAmount = 400 },
                new InvoiceLineSnap { LineNo = 10, Description = "Dry clean", Qty = 1, Rate = 350, Amount = 350, TaxableAmount = 350 },
                new InvoiceLineSnap { LineNo = 11, Description = "Gift box", Qty = 1, Rate = 200, Amount = 200, TaxableAmount = 200 },
                new InvoiceLineSnap { LineNo = 12, Description = "Accessories", Qty = 2, Rate = 250, Amount = 500, TaxableAmount = 500 },
                new InvoiceLineSnap { LineNo = 13, Description = "Matching bangles", Qty = 1, Rate = 750, Amount = 750, TaxableAmount = 750 },
                new InvoiceLineSnap { LineNo = 14, Description = "Hair accessory", Qty = 1, Rate = 450, Amount = 450, TaxableAmount = 450 },
                new InvoiceLineSnap { LineNo = 15, Description = "Carry bag", Qty = 1, Rate = 100, Amount = 100, TaxableAmount = 100 },
            ],
            Payable = 24200,
            SubTotal = 24200,
            RevisedSubTotal = 24200,
            ItemDiscountPercent = 10,
            ItemDiscount = 1500,
            CashDiscAmount = 500,
            AlterationTotal = 400,
            ItemCount = 15,
            TotalQty = 18,
        };

        var doc = A5PrePrintedInvoiceDocumentBuilder.Create(input, layout);
        var dlg = new InvoicePrintPreviewWindow(_services, doc, "", printInvoiceEnabled: true)
        {
            Owner = Application.Current.MainWindow,
            Width = 508,
            Height = 720,
            Title = isDuplicate ? "A5 pre-printed duplicate layout" : "A5 pre-printed test layout",
        };
        dlg.ShowDialog();
    }

    [RelayCommand]
    public async Task LoginCentralAsync()
    {
        try
        {
            LastActionText = "Logging in...";
            var (ok, err) = await _services.CentralAuthClient.LoginAsync(LoginEmail, LoginPassword, CancellationToken.None);
            if (!ok)
            {
                AuthStatusText = "Login failed";
                LastActionText = err ?? "Unknown error";
                return;
            }

            AuthStatusText = "Logged in";
            LoginPassword = "";
            LastActionText = "Logged in. Use Run sync once to save company master and printer from central.";
            _services.ReceiptConfig.Reload();
            ApplyReceiptFieldsFromConfig();
            await LoadWhatsAppCentralStatusAsync();
        }
        catch (Exception ex)
        {
            AuthStatusText = "Error";
            LastActionText = ex.Message;
        }
    }

    [RelayCommand]
    public void LogoutCentral()
    {
        _services.CentralAuthClient.Logout();
        AuthStatusText = "Logged out";
        LastActionText = "Session cleared.";
    }

    [RelayCommand]
    public async Task RefreshStatusAsync()
    {
        var status = await _services.SyncEngine.GetStatusAsync(CancellationToken.None);
        PendingOutboxText = $"Pending outbox: {status.PendingOutbox}";
        CursorText = $"Product cursor: {status.LastCursor} | Transfer cursor: {status.LastTransferCursor}";
        LastErrorText = status.LastError ?? "(none)";
        SyncDiagnosticsText = string.IsNullOrWhiteSpace(status.DiagnosticsSummary)
            ? "(run sync once to load diagnostics)"
            : status.DiagnosticsSummary;
        LastActionText = "Status refreshed.";
        UpdateAutoSyncStatusText();
    }

    [RelayCommand]
    public async Task RunSyncOnceAsync()
    {
        try
        {
            LastActionText = "Running sync...";
            var result = await _services.StoreSyncRunner.RunFullStoreSyncAsync(CancellationToken.None);

            if (!result.SkippedBecauseBusy)
            {
                _services.ReceiptConfig.Reload();
                ApplyReceiptFieldsFromConfig();
                _ = _services.ShellBranding.RefreshAsync();
            }

            LastActionText = result.SkippedBecauseBusy ? result.Message : result.Message;
        }
        catch (Exception ex)
        {
            LastActionText = $"Sync failed: {ex.Message}";
        }

        await RefreshStatusAsync();
    }

    [RelayCommand]
    public async Task TestPineLabsPaymentAsync()
    {
        var inv = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
        var result = await _services.PaymentRouter.PayAndRecordAsync(
            PaymentProviderKind.PineLabs,
            new PaymentRequest(inv, 1.00m, "INR"),
            CancellationToken.None);
        LastActionText = $"PineLabs: {result.Status}";
        await RefreshStatusAsync();
    }

    [RelayCommand]
    public async Task TestRazorpayPaymentAsync()
    {
        try
        {
            var inv = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
            var result = await _services.PaymentRouter.PayAndRecordAsync(
                PaymentProviderKind.Razorpay,
                new PaymentRequest(inv, 1.00m, "INR", PosMode: RazorpayPosPayMode.All),
                CancellationToken.None);
            LastActionText = $"Razorpay POS: {result.Status} ({result.ProviderReference})";
        }
        catch (Exception ex)
        {
            LastActionText = $"Razorpay POS test failed: {ex.Message}";
        }

        await RefreshStatusAsync();
    }

    private void LoadRazorpayPosSettingsFromStore()
    {
        _services.RazorpayPosSettings.Load();
        var s = _services.RazorpayPosSettings.Current;
        RazorpayPosEnabled = s.Enabled;
        RazorpayPosUsername = s.Username;
        RazorpayPosAppKey = s.AppKey;
        RazorpayPosApiBaseUrl = s.ApiBaseUrl;
        RazorpayPosDeviceId = s.DeviceId;
        RazorpayPosStatusPollIntervalMs = s.StatusPollIntervalMs;
        RazorpayPosStatusTimeoutSeconds = s.StatusTimeoutSeconds;
        RefreshRazorpayPosStatusText();
    }

    private void RefreshRazorpayPosStatusText()
    {
        var draft = new RazorpayPosSettingsDocument
        {
            Enabled = RazorpayPosEnabled,
            Username = RazorpayPosUsername?.Trim() ?? "",
            AppKey = RazorpayPosAppKey?.Trim() ?? "",
            ApiBaseUrl = string.IsNullOrWhiteSpace(RazorpayPosApiBaseUrl)
                ? "https://www.ezetap.com/api/3.0/p2padapter/"
                : RazorpayPosApiBaseUrl.Trim(),
            DeviceId = RazorpayPosSettingsDocument.NormalizeDeviceId(RazorpayPosDeviceId),
        };
        RazorpayPosStatusText = RazorpayPosSettingsDocument.GetConfigurationStatusMessage(draft);
    }

    partial void OnRazorpayPosEnabledChanged(bool value) => RefreshRazorpayPosStatusText();
    partial void OnRazorpayPosUsernameChanged(string value) => RefreshRazorpayPosStatusText();
    partial void OnRazorpayPosAppKeyChanged(string value) => RefreshRazorpayPosStatusText();
    partial void OnRazorpayPosApiBaseUrlChanged(string value) => RefreshRazorpayPosStatusText();
    partial void OnRazorpayPosDeviceIdChanged(string value) => RefreshRazorpayPosStatusText();

    [RelayCommand]
    public async Task SaveRazorpayPosSettingsAsync()
    {
        _services.RazorpayPosSettings.Update(s =>
        {
            s.Enabled = RazorpayPosEnabled;
            s.Username = RazorpayPosUsername?.Trim() ?? "";
            s.AppKey = RazorpayPosAppKey?.Trim() ?? "";
            s.ApiBaseUrl = string.IsNullOrWhiteSpace(RazorpayPosApiBaseUrl)
                ? "https://www.ezetap.com/api/3.0/p2padapter/"
                : RazorpayPosApiBaseUrl.Trim();
            s.DeviceId = RazorpayPosSettingsDocument.NormalizeDeviceId(RazorpayPosDeviceId);
            s.StatusPollIntervalMs = Math.Clamp(RazorpayPosStatusPollIntervalMs, 500, 10000);
            s.StatusTimeoutSeconds = Math.Clamp(RazorpayPosStatusTimeoutSeconds, 30, 600);
        });
        await _services.RazorpayPosSettings.SaveAsync();
        RazorpayPosDeviceId = _services.RazorpayPosSettings.Current.DeviceId;
        RefreshRazorpayPosStatusText();
        LastActionText = "Razorpay POS settings saved.";
    }

    private void LoadBillingSettingsFromStore()
    {
        _services.PosBillingSettings.Load();
        BillingAllowDuplicatePrint = _services.PosBillingSettings.Current.AllowDuplicatePrint;
        BillingAllowCreditNoteRemainingCashout = _services.PosBillingSettings.Current.AllowCreditNoteRemainingCashout;
        BillingAllowMultipleReturnsPerBill = _services.PosBillingSettings.Current.AllowMultipleReturnsPerBill;
        BillingAlterationGstIncluded = _services.PosBillingSettings.Current.AlterationGstIncluded;
        BillingLineItemDetailLevel = _services.PosBillingSettings.Current.LineItemDetailLevel;
    }

    [RelayCommand]
    public async Task SaveBillingSettingsAsync()
    {
        _services.PosBillingSettings.Update(s =>
        {
            s.AllowDuplicatePrint = BillingAllowDuplicatePrint;
            s.AllowCreditNoteRemainingCashout = BillingAllowCreditNoteRemainingCashout;
            s.AllowMultipleReturnsPerBill = BillingAllowMultipleReturnsPerBill;
            s.AlterationGstIncluded = BillingAlterationGstIncluded;
            s.LineItemDetailLevel = BillingLineItemDetailLevel;
        });
        await _services.PosBillingSettings.SaveAsync();
        var (actorName, actorEmail) = StoreAuditLogService.ActorFromSession(_services.UserSession);
        await _services.StoreAuditLog.LogEventAsync(new StoreAuditEvent
        {
            EntityType = "settings",
            EntityId = "billing",
            Action = "saved",
            ActorName = actorName,
            ActorEmail = actorEmail,
            Metadata = new BsonDocument
            {
                { "allowDuplicatePrint", BillingAllowDuplicatePrint },
                { "allowCreditNoteRemainingCashout", BillingAllowCreditNoteRemainingCashout },
                { "allowMultipleReturnsPerBill", BillingAllowMultipleReturnsPerBill },
                { "alterationGstIncluded", BillingAlterationGstIncluded },
                { "lineItemDetailLevel", BillingLineItemDetailLevel.ToString() },
            },
        });
        LastActionText = "Billing settings saved.";
    }

    [RelayCommand]
    public async Task SaveWhatsAppSettingsAsync()
    {
        _services.WhatsAppPreferences.Update(s => s.AutoSendAfterPost = WhatsAppAutoSendAfterPost);
        await _services.WhatsAppPreferences.SaveAsync();
        LastActionText = "WhatsApp settings saved.";
    }

    [RelayCommand]
    public async Task RefreshWhatsAppStatusAsync()
    {
        await LoadWhatsAppCentralStatusAsync();
        LastActionText = "WhatsApp status refreshed.";
    }

    [RelayCommand]
    public async Task TestWhatsAppSendAsync()
    {
        if (!PhoneE164Helper.CanSendWhatsApp(WhatsAppTestPhone))
        {
            MessageBox.Show("Enter a valid 10-digit test mobile number.", "WhatsApp test", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            LastActionText = "Sending WhatsApp test...";
            _services.CentralAuthSession.ApplyTo(_services.CentralApi);

            var (settings, settingsErr) = await _services.WhatsAppBills.LoadCentralSettingsAsync();
            if (settings == null)
            {
                var msg = settingsErr ?? "Could not load WhatsApp settings.";
                LastActionText = msg;
                MessageBox.Show(msg, "WhatsApp test", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!settings.Configured)
            {
                const string msg = "WhatsApp is not configured on central. Set phone number id, access token, and template name via Central admin, then click Refresh status.";
                LastActionText = msg;
                MessageBox.Show(msg, "WhatsApp test", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!settings.Enabled)
            {
                const string msg = "WhatsApp is disabled for this store on central. Enable it in Central admin, then click Refresh status.";
                LastActionText = msg;
                MessageBox.Show(msg, "WhatsApp test", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var input = BuildWhatsAppSampleInput();
            var (png, _, fileName) = await InvoiceAttachmentExporter.ExportThermalPngAsync(_services, input);
            var (result, err) = await _services.WhatsAppClient.SendTestAsync(
                _services.StoreContext.StoreId,
                WhatsAppTestPhone.Trim(),
                input.CustomerName,
                png,
                fileName);

            if (result == null)
            {
                LastActionText = err ?? "WhatsApp test failed.";
                MessageBox.Show(err ?? "WhatsApp test failed.", "WhatsApp test", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LastActionText = $"WhatsApp test sent (message id: {result.MessageId}).";
            MessageBox.Show("Test bill sent on WhatsApp.", "WhatsApp test", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LastActionText = ex.Message;
            MessageBox.Show(ex.Message, "WhatsApp test", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadWhatsAppSettingsFromStore()
    {
        _services.WhatsAppPreferences.Load();
        WhatsAppAutoSendAfterPost = _services.WhatsAppPreferences.Current.AutoSendAfterPost;
    }

    private async Task LoadWhatsAppCentralStatusAsync()
    {
        if (string.IsNullOrEmpty(_services.CentralAuthSession.AccessToken))
        {
            WhatsAppConnectionStatus = "Log in to Central on Connection & sync tab first.";
            WhatsAppTemplateSummary = "—";
            return;
        }

        _services.CentralAuthSession.ApplyTo(_services.CentralApi);
        var (settings, error) = await _services.WhatsAppBills.LoadCentralSettingsAsync();
        if (settings == null)
        {
            var detail = string.IsNullOrWhiteSpace(error) ? "Unknown error." : error.Trim();
            if (detail.Contains("login required", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("expired", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
            {
                WhatsAppConnectionStatus = "Central session expired or invalid — log in again on Connection & sync.";
            }
            else
            {
                WhatsAppConnectionStatus = $"Could not load WhatsApp settings: {detail}";
            }

            WhatsAppTemplateSummary = "—";
            return;
        }

        WhatsAppConnectionStatus = !settings.Configured
            ? $"Not configured — set phone number id, access token, and template in Central admin for store {_services.StoreContext.StoreId}."
            : settings.Enabled
                ? "Configured and enabled on central."
                : "Configured on central but disabled — enable in Central admin.";

        WhatsAppTemplateSummary = string.IsNullOrWhiteSpace(settings.TemplateName)
            ? "—"
            : $"{settings.TemplateName} ({settings.TemplateLanguage})";
    }

    private ThermalInvoiceInput BuildWhatsAppSampleInput()
    {
        var store = _services.ReceiptConfig.Current.Store;
        return new ThermalInvoiceInput
        {
            Store = store,
            CharWidth = ReceiptCharWidth,
            BillNo = "TEST-WA",
            BillDate = DateTime.Now.ToString("dd-MMM-yyyy").ToUpperInvariant(),
            UserName = "Test",
            Time = DateTime.Now.ToString("HH:mm"),
            Counter = _services.StoreContext.PosCounter,
            CustomerName = "Sample Customer",
            CustomerPhone = WhatsAppTestPhone.Trim(),
            Lines =
            [
                new InvoiceLineSnap { LineNo = 1, Description = "Sample item", Qty = 1, Rate = 1000, Amount = 1000, TaxableAmount = 1000 },
            ],
            Payable = 1000,
            TotalQty = 1,
            ItemCount = 1,
            Payments = PaymentReceiptSnap.Preview(),
        };
    }

    [RelayCommand]
    public async Task TestPurchaseIntentAsync()
    {
        try
        {
            var sku = $"TEST-SKU-{DateTime.Now:HHmmss}";
            var eventId = await _services.PurchaseIntentPublisher.SubmitAsync(
                new[] { new PurchaseIntentLineInput(sku, 1, "QA") },
                remarks: "Test intent",
                CancellationToken.None);
            LastActionText = $"Intent queued: {eventId}";
        }
        catch (Exception ex)
        {
            LastActionText = ex.Message;
        }

        await RefreshStatusAsync();
    }
}

public sealed record BillingLineItemDetailOption(BillingLineItemDetailLevel Level, string Label, string Description);
