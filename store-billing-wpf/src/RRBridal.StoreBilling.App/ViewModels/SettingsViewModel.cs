using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Printing;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Payments;
using RRBridal.StoreBilling.App.Services.PurchaseIntents;

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

    [ObservableProperty] private string? _selectedPrinterFullName;

    [ObservableProperty] private int _receiptCharWidth = 48;

    [ObservableProperty] private InvoicePrintFormat _receiptPrintFormat = InvoicePrintFormat.Thermal;

    [ObservableProperty] private bool _a5PrePrintedEnabled;

    [ObservableProperty] private bool _alsoPrintThermalFirst;

    [ObservableProperty] private string _receiptCentralSyncText = "(not synced from central yet)";

    [ObservableProperty] private string _receiptPrinterHintText = "";

    [ObservableProperty] private string _receiptPrinterWarningText = "";

    [ObservableProperty] private bool _billingAllowDuplicatePrint = true;

    public ObservableCollection<PrinterOption> PrinterOptions { get; } = new();

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
    }

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
        SelectedPrinterFullName = c.Print.BillPrinterFullName;
        ReceiptCharWidth = c.Print.ReceiptCharWidth is >= 32 and <= 56 ? c.Print.ReceiptCharWidth : 48;
        ReceiptPrintFormat = c.Print.PrintFormat;
        A5PrePrintedEnabled = c.Print.A5PrePrintedEnabled;
        AlsoPrintThermalFirst = c.Print.AlsoPrintThermalFirst;
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

    private void UpdatePrinterWarning()
    {
        var print = _services.ReceiptConfig.Current.Print;
        if (string.IsNullOrWhiteSpace(print.CentralPrinterHint) && string.IsNullOrWhiteSpace(print.CentralPrinterModel))
        {
            ReceiptPrinterWarningText = "";
            return;
        }

        if (!string.IsNullOrWhiteSpace(SelectedPrinterFullName))
        {
            ReceiptPrinterWarningText = "";
            return;
        }

        ReceiptPrinterWarningText =
            "Central printer name did not match any queue on this PC — select a printer manually.";
    }

    [RelayCommand]
    public void RefreshPrinters()
    {
        PrinterOptions.Clear();
        try
        {
            using var server = new LocalPrintServer();
            foreach (PrintQueue pq in server.GetPrintQueues())
            {
                PrinterOptions.Add(new PrinterOption
                {
                    Display = pq.Name,
                    FullName = pq.FullName,
                });
            }
        }
        catch
        {
            // ignore — no printers
        }
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
        c.Print.BillPrinterFullName = string.IsNullOrWhiteSpace(SelectedPrinterFullName) ? null : SelectedPrinterFullName.Trim();
        var w = ReceiptCharWidth;
        if (w < 32) w = 32;
        if (w > 56) w = 56;
        ReceiptCharWidth = w;
        c.Print.ReceiptCharWidth = w;
        c.Print.PrintFormat = ReceiptPrintFormat;
        c.Print.A5PrePrintedEnabled = A5PrePrintedEnabled;
        c.Print.AlsoPrintThermalFirst = ReceiptPrintFormat is InvoicePrintFormat.A4 or InvoicePrintFormat.A5
            && AlsoPrintThermalFirst;
        await _services.ReceiptConfig.SaveAsync(CancellationToken.None);
        UpdatePrinterWarning();
        LastActionText = "Receipt and printer settings saved.";
    }

    partial void OnSelectedPrinterFullNameChanged(string? value) => UpdatePrinterWarning();

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
        var inv = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
        var result = await _services.PaymentRouter.PayAndRecordAsync(
            PaymentProviderKind.Razorpay,
            new PaymentRequest(inv, 1.00m, "INR"),
            CancellationToken.None);
        LastActionText = $"Razorpay: {result.Status}";
        await RefreshStatusAsync();
    }

    private void LoadBillingSettingsFromStore()
    {
        _services.PosBillingSettings.Load();
        BillingAllowDuplicatePrint = _services.PosBillingSettings.Current.AllowDuplicatePrint;
    }

    [RelayCommand]
    public async Task SaveBillingSettingsAsync()
    {
        _services.PosBillingSettings.Update(s => s.AllowDuplicatePrint = BillingAllowDuplicatePrint);
        await _services.PosBillingSettings.SaveAsync();
        LastActionText = "Duplicate print setting saved.";
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
