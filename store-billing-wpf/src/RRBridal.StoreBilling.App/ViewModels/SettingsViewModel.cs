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

    [ObservableProperty] private string _receiptCentralSyncText = "(not synced from central yet)";

    [ObservableProperty] private string _receiptPrinterHintText = "";

    [ObservableProperty] private string _receiptPrinterWarningText = "";

    public ObservableCollection<PrinterOption> PrinterOptions { get; } = new();

    public SettingsViewModel(AppServices services)
    {
        _services = services;
        AuthStatusText = string.IsNullOrEmpty(_services.CentralAuthSession.AccessToken)
            ? "Central auth: not logged in"
            : "Central auth: token loaded";
        _ = RefreshStatusAsync();
    }

    public async Task LoadReceiptSettingsAsync(bool tryPullIfLoggedIn = false, bool forcePullFromCentral = false)
    {
        _services.CentralAuthSession.ApplyTo(_services.CentralApi);
        AuthStatusText = string.IsNullOrEmpty(_services.CentralAuthSession.AccessToken)
            ? "Central auth: not logged in"
            : "Central auth: logged in";

        var shouldPull = forcePullFromCentral
            || (tryPullIfLoggedIn && !string.IsNullOrEmpty(_services.CentralAuthSession.AccessToken)
                && (ShouldPullReceiptProfile()));

        if (shouldPull)
            await PullReceiptFromCentralAsync(refreshUiFromMemory: true);
        else
        {
            _services.ReceiptConfig.Reload();
            ApplyReceiptFieldsFromConfig();
        }
    }

    private bool ShouldPullReceiptProfile()
    {
        if (!_services.ReceiptConfig.Current.LastReceiptSettingsSyncUtc.HasValue)
            return true;

        var s = _services.ReceiptConfig.Current.Store;
        return string.Equals(s.StoreName, "RR Bridal", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(s.Gstin);
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
    public async Task PullReceiptFromCentralAsync() => await PullReceiptFromCentralAsync(refreshUiFromMemory: true);

    private async Task PullReceiptFromCentralAsync(bool refreshUiFromMemory)
    {
        try
        {
            if (string.IsNullOrEmpty(_services.CentralAuthSession.AccessToken))
            {
                LastActionText = "Log in to Central first (email + password above), then pull.";
                ReceiptCentralSyncText = "Not synced — central login required.";
                return;
            }

            _services.CentralAuthSession.ApplyTo(_services.CentralApi);
            LastActionText = "Pulling receipt settings from central...";
            var (ok, message) = await _services.ReceiptConfigSync.SyncFromCentralAsync(CancellationToken.None);

            if (ok && refreshUiFromMemory)
                ApplyReceiptFieldsFromConfig();
            else
            {
                _services.ReceiptConfig.Reload();
                ApplyReceiptFieldsFromConfig();
            }

            LastActionText = message;
            if (!ok)
            {
                ReceiptPrinterWarningText = message;
                ReceiptCentralSyncText = "Pull failed — see message below.";
            }
            else
            {
                _ = _services.ShellBranding.RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            LastActionText = ex.Message;
        }
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
            await PullReceiptFromCentralAsync(refreshUiFromMemory: true);
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
    }

    [RelayCommand]
    public async Task RunSyncOnceAsync()
    {
        try
        {
            LastActionText = "Running sync...";
            _services.CentralAuthSession.ApplyTo(_services.CentralApi);
            await _services.SyncEngine.RunOnceAsync(CancellationToken.None);

            if (!string.IsNullOrEmpty(_services.CentralAuthSession.AccessToken))
            {
                var (ok, msg) = await _services.ReceiptConfigSync.EnsureProfileReadyForPrintAsync(CancellationToken.None);
                if (ok)
                    ApplyReceiptFieldsFromConfig();
                LastActionText = ok ? $"Sync complete. {msg}" : $"Sync finished; receipt pull failed: {msg}";
            }
            else
                LastActionText = "Sync complete. Log in to Central to pull receipt header.";
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
