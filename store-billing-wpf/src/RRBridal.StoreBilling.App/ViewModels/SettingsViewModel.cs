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
    [ObservableProperty] private string _mongoHealthStatusText = "";
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

    [ObservableProperty] private CreditPrintFormat _creditPrintFormat = CreditPrintFormat.Thermal;

    [ObservableProperty] private bool _a4PrePrintedEnabled;

    [ObservableProperty] private bool _a5PrePrintedEnabled;

    [ObservableProperty] private bool _alsoPrintThermalFirst;

    [ObservableProperty] private string _receiptCentralSyncText = "(not synced from central yet)";

    [ObservableProperty] private string _receiptPrinterHintText = "";

    [ObservableProperty] private string _receiptPrinterWarningText = "";

    [ObservableProperty] private bool _billingAllowDuplicatePrint = true;
    [ObservableProperty] private bool _billingConfirmDuplicateProductAdd = true;
    [ObservableProperty] private bool _billingAllowCreditNoteRemainingCashout;
    [ObservableProperty] private bool _billingAllowMultipleReturnsPerBill;
    [ObservableProperty] private bool _billingAlterationGstIncluded;
    [ObservableProperty] private BillingLineItemDetailLevel _billingLineItemDetailLevel = BillingLineItemDetailLevel.Full;
    [ObservableProperty] private bool _billingEnableCreditBilling = true;
    [ObservableProperty] private bool _billingCreditRequireCreditCustomer = true;
    [ObservableProperty] private string _billingCreditMinAdvancePercentText = "0";
    [ObservableProperty] private string _billingCreditMinAdvanceAmountText = "0";
    [ObservableProperty] private bool _billingCreditAllowZeroAdvance = true;
    [ObservableProperty] private bool _billingCreditAllowPartialCollection = true;
    [ObservableProperty] private string _billingCreditMaxBalancePerBillText = "0";

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

    public A4PrePrintedLayoutSettingsViewModel A4Layout { get; } = new();

    public A5PrePrintedLayoutSettingsViewModel A5Layout { get; } = new();

    public IReadOnlyList<string> A4FontFamilyOptions { get; } =
    [
        "Arial",
        "Calibri",
        "Times New Roman",
        "Courier New",
        "Segoe UI",
    ];

    public IReadOnlyList<string> A5FontFamilyOptions { get; } =
    [
        "Arial",
        "Calibri",
        "Times New Roman",
        "Courier New",
        "Segoe UI",
    ];

    public bool IsA4PrePrintedSettingsVisible =>
        IsA4ReceiptFormat && A4PrePrintedEnabled;

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

    public bool IsA4CommercialReceiptFormat
    {
        get => ReceiptPrintFormat == InvoicePrintFormat.A4Commercial;
        set
        {
            if (value)
                ReceiptPrintFormat = InvoicePrintFormat.A4Commercial;
        }
    }

    public bool IsCreditThermalPrintFormat
    {
        get => CreditPrintFormat == CreditPrintFormat.Thermal;
        set
        {
            if (value)
                CreditPrintFormat = CreditPrintFormat.Thermal;
        }
    }

    public bool IsCreditA4PrintFormat
    {
        get => CreditPrintFormat == CreditPrintFormat.A4;
        set
        {
            if (value)
                CreditPrintFormat = CreditPrintFormat.A4;
        }
    }

    public bool IsOfficeInvoiceFormat =>
        ReceiptPrintFormat is InvoicePrintFormat.A4 or InvoicePrintFormat.A5 or InvoicePrintFormat.A4Commercial;

    partial void OnReceiptPrintFormatChanged(InvoicePrintFormat value)
    {
        OnPropertyChanged(nameof(IsThermalReceiptFormat));
        OnPropertyChanged(nameof(IsA4ReceiptFormat));
        OnPropertyChanged(nameof(IsA4CommercialReceiptFormat));
        OnPropertyChanged(nameof(IsA5ReceiptFormat));
        OnPropertyChanged(nameof(IsOfficeInvoiceFormat));
        OnPropertyChanged(nameof(IsA4PrePrintedSettingsVisible));
        OnPropertyChanged(nameof(IsA5PrePrintedSettingsVisible));
    }

    partial void OnCreditPrintFormatChanged(CreditPrintFormat value)
    {
        OnPropertyChanged(nameof(IsCreditThermalPrintFormat));
        OnPropertyChanged(nameof(IsCreditA4PrintFormat));
    }

    partial void OnA4PrePrintedEnabledChanged(bool value) =>
        OnPropertyChanged(nameof(IsA4PrePrintedSettingsVisible));

    partial void OnA5PrePrintedEnabledChanged(bool value) =>
        OnPropertyChanged(nameof(IsA5PrePrintedSettingsVisible));

    public SettingsViewModel(AppServices services)
    {
        _services = services;
        AuthStatusText = string.IsNullOrEmpty(_services.CentralAuthSession.AccessToken)
            ? "Central auth: not logged in"
            : "Central auth: token loaded";
        _services.PeriodicSync.StatusChanged += OnPeriodicSyncStatusChanged;
        _services.MongoHealth.StatusChanged += OnMongoHealthStatusChanged;
        UpdateAutoSyncStatusText();
        UpdateMongoHealthStatusText();
        _ = RefreshStatusAsync();
    }

    private void OnPeriodicSyncStatusChanged()
    {
        OnPropertyChanged(nameof(AutoSyncStatusText));
        UpdateAutoSyncStatusText();
    }

    private void OnMongoHealthStatusChanged()
    {
        UpdateMongoHealthStatusText();
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

    private void UpdateMongoHealthStatusText()
    {
        var health = _services.MongoHealth;
        MongoHealthStatusText = health.StatusDescription;
        if (!string.IsNullOrWhiteSpace(health.LastError) && health.State != MongoHealthState.Connected)
            MongoHealthStatusText += Environment.NewLine + health.LastError;
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
        CreditPrintFormat = c.Print.CreditPrintFormat;
        A4PrePrintedEnabled = c.Print.A4PrePrintedEnabled;
        A5PrePrintedEnabled = c.Print.A5PrePrintedEnabled;
        AlsoPrintThermalFirst = c.Print.AlsoPrintThermalFirst;
        A4Layout.ApplyFrom(c.Print.A4PrePrintedLayout ?? A4PrePrintedLayoutSettings.CreateDefault());
        A5Layout.ApplyFrom(c.Print.A5PrePrintedLayout ?? A5PrePrintedLayoutSettings.CreateDefault());
        OnPropertyChanged(nameof(IsThermalReceiptFormat));
        OnPropertyChanged(nameof(IsA4ReceiptFormat));
        OnPropertyChanged(nameof(IsA4CommercialReceiptFormat));
        OnPropertyChanged(nameof(IsA5ReceiptFormat));
        OnPropertyChanged(nameof(IsOfficeInvoiceFormat));
        OnPropertyChanged(nameof(IsCreditThermalPrintFormat));
        OnPropertyChanged(nameof(IsCreditA4PrintFormat));
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
        c.Print.CreditPrintFormat = CreditPrintFormat;
        c.Print.A4PrePrintedEnabled = A4PrePrintedEnabled;
        c.Print.A5PrePrintedEnabled = A5PrePrintedEnabled;
        c.Print.AlsoPrintThermalFirst = ReceiptPrintFormat is InvoicePrintFormat.A4 or InvoicePrintFormat.A5 or InvoicePrintFormat.A4Commercial
            && AlsoPrintThermalFirst;
        c.Print.A4PrePrintedLayout = A4Layout.ToSettings();
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
                { "creditPrintFormat", CreditPrintFormat.ToString() },
                { "a4PrePrintedEnabled", A4PrePrintedEnabled },
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
    public void ResetA4PrePrintedLayoutToDefaults()
    {
        A4Layout.ApplyFrom(A4PrePrintedLayoutSettings.CreateDefault());
        LastActionText = "A4 pre-printed alignment reset to defaults (save to persist).";
    }

    [RelayCommand]
    public void ResetA5PrePrintedLayoutToDefaults()
    {
        A5Layout.ApplyFrom(A5PrePrintedLayoutSettings.CreateDefault());
        LastActionText = "A5 pre-printed alignment reset to defaults (save to persist).";
    }

    [RelayCommand]
    public async Task PreviewReceiptSettingsAsync() =>
        await ShowReceiptSettingsPreviewAsync(isDuplicate: false);

    [RelayCommand]
    public async Task PreviewCreditReceiptSettingsAsync()
    {
        try
        {
            var c = _services.ReceiptConfig.Current;
            c.Print.CreditPrintFormat = CreditPrintFormat;
            c.Print.ReceiptCharWidth = ReceiptCharWidth is >= 32 and <= 56 ? ReceiptCharWidth : 48;
            var sample = CreditReceiptMapper.CreateSample(
                c.Store,
                CreditPrintFormat,
                c.Print.ReceiptCharWidth);
            await CreditReceiptPrintFlow.ShowAsync(_services, sample);
            LastActionText = $"Credit receipt preview ({CreditPrintFormat}).";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Credit receipt preview failed: {ex.Message}", "Settings",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    public void PreviewA4PrePrintedAlignment() =>
        _ = ShowReceiptSettingsPreviewAsync(isDuplicate: false);

    [RelayCommand]
    public void PreviewA4PrePrintedDuplicateAlignment() =>
        _ = ShowReceiptSettingsPreviewAsync(isDuplicate: true);

    [RelayCommand]
    public void PreviewA5PrePrintedAlignment() =>
        _ = ShowReceiptSettingsPreviewAsync(isDuplicate: false);

    [RelayCommand]
    public void PreviewA5PrePrintedDuplicateAlignment() =>
        _ = ShowReceiptSettingsPreviewAsync(isDuplicate: true);

    private async Task ShowReceiptSettingsPreviewAsync(bool isDuplicate)
    {
        try
        {
            var store = BuildStoreProfileFromUi();
            var input = CreateSampleInvoiceInput(store, isDuplicate);
            var printFormat = ReceiptPrintFormat;
            var isA4PrePrinted = printFormat == InvoicePrintFormat.A4 && A4PrePrintedEnabled;
            var isA5PrePrinted = printFormat == InvoicePrintFormat.A5 && A5PrePrintedEnabled;
            var isOfficeFormat = printFormat is InvoicePrintFormat.A4 or InvoicePrintFormat.A5 or InvoicePrintFormat.A4Commercial;
            var dualPrint = isOfficeFormat && AlsoPrintThermalFirst;
            var a4Layout = A4Layout.ToSettings();
            var a5Layout = A5Layout.ToSettings();
            var previewConfig = new ReceiptConfigDocument
            {
                Store = store,
                Print = _services.ReceiptConfig.Current.Print,
            };

            var assets = await ThermalReceiptDocumentBuilder.BuildAssetsAsync(
                previewConfig,
                input.BillNo,
                _services.ReceiptLogoCache);
            var text = ThermalInvoiceTextBuilder.Build(input);

            FlowDocument doc;
            FlowDocument? thermalDoc = null;

            if (dualPrint)
            {
                var fontSize = input.CharWidth >= 48 ? 9.0 : 10.0;
                thermalDoc = BillPrintService.CreateReceiptDocument(text, assets, fontSize);
                doc = BuildSettingsPreviewInvoiceDocument(input, assets, printFormat, isA4PrePrinted, isA5PrePrinted, a4Layout, a5Layout);
            }
            else if (isA4PrePrinted)
            {
                doc = A4PrePrintedInvoiceDocumentBuilder.Create(input, a4Layout);
            }
            else if (isA5PrePrinted)
            {
                doc = A5PrePrintedInvoiceDocumentBuilder.Create(input, a5Layout);
            }
            else if (isOfficeFormat)
            {
                doc = BuildSettingsPreviewInvoiceDocument(input, assets, printFormat, isA4PrePrinted, isA5PrePrinted, a4Layout, a5Layout);
            }
            else
            {
                var fontSize = input.CharWidth >= 48 ? 9.0 : 10.0;
                doc = BillPrintService.CreateReceiptDocument(text, assets, fontSize);
            }

            var isA5 = printFormat == InvoicePrintFormat.A5;
            var isTaxInvoice = isOfficeFormat;
            var title = isA4PrePrinted
                ? isDuplicate ? "A4 pre-printed duplicate layout" : "A4 pre-printed test layout"
                : isA5PrePrinted
                ? isDuplicate ? "A5 pre-printed duplicate layout" : "A5 pre-printed test layout"
                : printFormat == InvoicePrintFormat.A4Commercial
                    ? "A4 commercial invoice preview"
                : isA5
                    ? "A5 invoice preview"
                    : printFormat == InvoicePrintFormat.A4
                        ? "A4 invoice preview"
                        : "Thermal receipt preview";
            var dlg = new InvoicePrintPreviewWindow(
                _services,
                doc,
                text,
                printInvoiceEnabled: true,
                thermalDoc,
                dualPrint)
            {
                Owner = Application.Current.MainWindow,
                Width = isA5 ? 508 : isTaxInvoice ? 720 : 420,
                Height = isA5 ? 720 : isTaxInvoice ? 820 : 560,
                Title = title,
            };
            dlg.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not open invoice preview: {ex.Message}",
                "Preview",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static FlowDocument BuildSettingsPreviewInvoiceDocument(
        ThermalInvoiceInput input,
        ThermalReceiptAssets assets,
        InvoicePrintFormat printFormat,
        bool isA4PrePrinted,
        bool isA5PrePrinted,
        A4PrePrintedLayoutSettings? a4Layout,
        A5PrePrintedLayoutSettings? a5Layout)
    {
        if (isA4PrePrinted)
            return A4PrePrintedInvoiceDocumentBuilder.Create(input, a4Layout);

        if (isA5PrePrinted)
            return A5PrePrintedInvoiceDocumentBuilder.Create(input, a5Layout);

        if (printFormat == InvoicePrintFormat.A4Commercial)
            return CommercialA4InvoiceDocumentBuilder.Create(input);

        var (pageW, pageH) = printFormat == InvoicePrintFormat.A5
            ? (148.0, 210.0)
            : (210.0, 297.0);
        var linesPerPage = a5Layout?.MaxLineRows ?? 10;
        if (linesPerPage <= 0) linesPerPage = 10;
        return A4InvoiceDocumentBuilder.Create(input, assets, pageW, pageH, linesPerPage, a5Layout);
    }

    private StoreProfile BuildStoreProfileFromUi()
    {
        var saved = _services.ReceiptConfig.Current.Store;
        return new StoreProfile
        {
            StoreName = string.IsNullOrWhiteSpace(ReceiptStoreName) ? saved.StoreName : ReceiptStoreName.Trim(),
            Address = ReceiptAddress.Trim(),
            CustomerCarePhone = ReceiptCustomerCarePhone.Trim(),
            Gstin = ReceiptGstin.Trim(),
            StateName = saved.StateName,
            FssaiNo = ReceiptFssaiNo.Trim(),
            BranchCode = ReceiptBranchCode.Trim(),
            Website = ReceiptWebsite.Trim(),
            TermsAndConditions = ReceiptTerms.Trim(),
            ThankYouLine = ReceiptThankYouLine.Trim(),
            PolicyLines = ReceiptPolicyLinesText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList(),
            LogoUrl = saved.LogoUrl,
            LogoFilePath = saved.LogoFilePath,
            QrSlots = saved.QrSlots,
            ShowBillBarcode = saved.ShowBillBarcode,
        };
    }

    private ThermalInvoiceInput CreateSampleInvoiceInput(StoreProfile store, bool isDuplicate) =>
        new()
        {
            Store = store,
            CharWidth = ReceiptCharWidth,
            BillNo = "1916",
            BillDate = DateTime.Now.ToString("dd-MMM-yy").ToUpperInvariant(),
            OrderNo = "ORD-1024",
            UserName = "MUBEEN",
            Time = DateTime.Now.ToString("HH:mm"),
            Counter = "01",
            CustomerName = "Sample Customer Name Long",
            CustomerPhone = "9876543210",
            Stitching = true,
            DeliveryDate = DateTime.Now.AddDays(7).ToString("dd-MMM-yyyy").ToUpperInvariant(),
            IsDuplicateCopy = isDuplicate,
            Lines =
            [
                new InvoiceLineSnap { LineNo = 1, Description = "DNO-3139/AA", Hsn = "6204", Qty = 8, Rate = 311, Amount = 2488, TaxableAmount = 2488, LineInclusiveAmount = 2488 },
                new InvoiceLineSnap { LineNo = 2, Description = "DNO-5100/AA", Hsn = "6204", Qty = 8, Rate = 351, Amount = 2808, TaxableAmount = 2808, LineInclusiveAmount = 2808 },
                new InvoiceLineSnap { LineNo = 3, Description = "SUITS", Hsn = "6203", Qty = 4, Rate = 495, Amount = 1980, TaxableAmount = 1980, LineInclusiveAmount = 1980 },
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
            Payable = 7270,
            SubTotal = 7276,
            RevisedSubTotal = 7270,
            ItemDiscountPercent = 0,
            ItemDiscount = 0,
            CashDiscAmount = 6,
            AlterationTotal = 400,
            ItemCount = 15,
            TotalQty = 20,
            Payments = PaymentReceiptSnap.Preview(),
        };

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
        await _services.MongoHealth.PingAsync().ConfigureAwait(true);
        UpdateMongoHealthStatusText();
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
    public async Task ForceProductResyncAsync()
    {
        try
        {
            LastActionText = "Re-syncing full product catalog…";
            await _services.StoreSyncRunner.SyncLock.WaitAsync(CancellationToken.None);
            int productCount;
            try
            {
                productCount = await _services.SyncEngine.ResyncAllProductsAsync(CancellationToken.None);
            }
            finally
            {
                _services.StoreSyncRunner.SyncLock.Release();
            }

            if (productCount > 0)
            {
                _services.ReceiptConfig.Reload();
                ApplyReceiptFieldsFromConfig();
                _ = _services.ShellBranding.RefreshAsync();
            }

            LastActionText = $"Product re-sync complete. Updated {productCount:N0} product rows locally.";
        }
        catch (Exception ex)
        {
            LastActionText = $"Product re-sync failed: {ex.Message}";
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
        BillingConfirmDuplicateProductAdd = _services.PosBillingSettings.Current.ConfirmDuplicateProductAdd;
        BillingAllowCreditNoteRemainingCashout = _services.PosBillingSettings.Current.AllowCreditNoteRemainingCashout;
        BillingAllowMultipleReturnsPerBill = _services.PosBillingSettings.Current.AllowMultipleReturnsPerBill;
        BillingAlterationGstIncluded = _services.PosBillingSettings.Current.AlterationGstIncluded;
        BillingLineItemDetailLevel = _services.PosBillingSettings.Current.LineItemDetailLevel;
        BillingEnableCreditBilling = _services.PosBillingSettings.Current.EnableCreditBilling;
        BillingCreditRequireCreditCustomer = _services.PosBillingSettings.Current.CreditBillingRequireCreditCustomer;
        BillingCreditMinAdvancePercentText = _services.PosBillingSettings.Current.CreditBillingMinimumAdvancePercent.ToString("0.##");
        BillingCreditMinAdvanceAmountText = _services.PosBillingSettings.Current.CreditBillingMinimumAdvanceAmount.ToString("0.##");
        BillingCreditAllowZeroAdvance = _services.PosBillingSettings.Current.CreditBillingAllowZeroAdvance;
        BillingCreditAllowPartialCollection = _services.PosBillingSettings.Current.CreditBillingAllowPartialCollection;
        BillingCreditMaxBalancePerBillText = _services.PosBillingSettings.Current.CreditBillingMaxBalancePerBill.ToString("0.##");
    }

    [RelayCommand]
    public async Task SaveBillingSettingsAsync()
    {
        decimal.TryParse(BillingCreditMinAdvancePercentText, out var minPct);
        decimal.TryParse(BillingCreditMinAdvanceAmountText, out var minAmt);
        decimal.TryParse(BillingCreditMaxBalancePerBillText, out var maxBal);
        _services.PosBillingSettings.Update(s =>
        {
            s.AllowDuplicatePrint = BillingAllowDuplicatePrint;
            s.ConfirmDuplicateProductAdd = BillingConfirmDuplicateProductAdd;
            s.AllowCreditNoteRemainingCashout = BillingAllowCreditNoteRemainingCashout;
            s.AllowMultipleReturnsPerBill = BillingAllowMultipleReturnsPerBill;
            s.AlterationGstIncluded = BillingAlterationGstIncluded;
            s.LineItemDetailLevel = BillingLineItemDetailLevel;
            s.EnableCreditBilling = BillingEnableCreditBilling;
            s.CreditBillingRequireCreditCustomer = BillingCreditRequireCreditCustomer;
            s.CreditBillingMinimumAdvancePercent = Math.Max(0m, minPct);
            s.CreditBillingMinimumAdvanceAmount = Math.Max(0m, minAmt);
            s.CreditBillingAllowZeroAdvance = BillingCreditAllowZeroAdvance;
            s.CreditBillingAllowPartialCollection = BillingCreditAllowPartialCollection;
            s.CreditBillingMaxBalancePerBill = Math.Max(0m, maxBal);
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
                { "confirmDuplicateProductAdd", BillingConfirmDuplicateProductAdd },
                { "allowCreditNoteRemainingCashout", BillingAllowCreditNoteRemainingCashout },
                { "allowMultipleReturnsPerBill", BillingAllowMultipleReturnsPerBill },
                { "alterationGstIncluded", BillingAlterationGstIncluded },
                { "lineItemDetailLevel", BillingLineItemDetailLevel.ToString() },
                { "enableCreditBilling", BillingEnableCreditBilling },
                { "creditBillingRequireCreditCustomer", BillingCreditRequireCreditCustomer },
                { "creditBillingMinimumAdvancePercent", (double)Math.Max(0m, minPct) },
                { "creditBillingMinimumAdvanceAmount", (double)Math.Max(0m, minAmt) },
                { "creditBillingAllowZeroAdvance", BillingCreditAllowZeroAdvance },
                { "creditBillingAllowPartialCollection", BillingCreditAllowPartialCollection },
                { "creditBillingMaxBalancePerBill", (double)Math.Max(0m, maxBal) },
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
