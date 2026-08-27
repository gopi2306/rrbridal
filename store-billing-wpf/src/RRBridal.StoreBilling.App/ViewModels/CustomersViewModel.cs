using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Customers;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class CustomersViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly CustomerRegistrationService _registrationService;
    private readonly CustomerDirectoryService _directory;
    private readonly CustomerLookupService _lookup;
    private readonly CustomerCodeGenerator _codeGenerator;
    private readonly BillingViewModel _billing;
    private readonly Action _navigateToBilling;

    [ObservableProperty] private string _searchCustomerCode = "";
    [ObservableProperty] private string _searchCustomerName = "";
    [ObservableProperty] private string _searchCustomerPhone = "";
    [ObservableProperty] private CustomerListRow? _selectedCustomer;
    [ObservableProperty] private string _statusMessage = "Search or select a customer to view details.";
    [ObservableProperty] private bool _isNewCustomer;
    [ObservableProperty] private bool _isDetailReadOnly = true;
    [ObservableProperty] private string _detailSourceLabel = "";

    [ObservableProperty] private string _localMongoId = "";
    [ObservableProperty] private string _centralCustomerId = "";
    [ObservableProperty] private string _customerCode = "";
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _telephone = "";
    [ObservableProperty] private string _mobile = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _gstin = "";
    [ObservableProperty] private string _doorNo = "";
    [ObservableProperty] private string _street = "";
    [ObservableProperty] private string _fullAddress = "";
    [ObservableProperty] private string _place = "";
    [ObservableProperty] private string _city = "";
    [ObservableProperty] private string _pincode = "";
    [ObservableProperty] private string _state = "";
    [ObservableProperty] private string _landmark = "";
    [ObservableProperty] private bool _isCreditCustomer;
    [ObservableProperty] private string _syncStatus = "";
    [ObservableProperty] private string _resultsCountSummary = "0 customers";
    [ObservableProperty] private string _detailPanelTitle = "Customer details";

    public bool ShowDetailForm => IsNewCustomer || SelectedCustomer != null;
    public bool ShowDetailPlaceholder => !ShowDetailForm;
    public string CustomerCodeHint => string.IsNullOrWhiteSpace(CustomerCode)
        ? "Assigned automatically when you save."
        : "Customer code cannot be edited after it is assigned.";

    public ObservableCollection<CustomerListRow> Results { get; } = new();

    private bool _suppressSelection;

    public CustomersViewModel(AppServices services, BillingViewModel billing, Action navigateToBilling)
    {
        _services = services;
        _registrationService = new CustomerRegistrationService(services.LocalDb, services.CentralApi, services.StoreContext, services.CentralMode);
        _lookup = new CustomerLookupService(services.LocalDb, services.CentralApi, services.CentralMode);
        _directory = new CustomerDirectoryService(services.LocalDb, _lookup, services.CentralMode);
        _codeGenerator = new CustomerCodeGenerator(
            services.LocalDb,
            () => services.CentralMode.IsOnlineMode);
        _billing = billing;
        _navigateToBilling = navigateToBilling;
    }

    public void StartNewRegistration()
    {
        _suppressSelection = true;
        try
        {
            IsNewCustomer = true;
            SelectedCustomer = null;
            ClearDetailForm();
            ApplyNewCustomerFormState();
        }
        finally
        {
            _suppressSelection = false;
            if (SelectedCustomer != null)
            {
                _suppressSelection = true;
                SelectedCustomer = null;
                _suppressSelection = false;
                ClearDetailForm();
                ApplyNewCustomerFormState();
            }
        }

        NotifyDetailVisibility();
        SaveCommand.NotifyCanExecuteChanged();
        UseInBillingCommand.NotifyCanExecuteChanged();
    }

    private void ApplyNewCustomerFormState()
    {
        IsDetailReadOnly = false;
        DetailSourceLabel = "Register a new customer for billing and credit eligibility.";
        DetailPanelTitle = "New customer";
        StatusMessage = "Enter name and mobile, then save (F4). Customer code is assigned automatically.";
        OnPropertyChanged(nameof(CustomerCodeHint));
    }

    private void NotifyDetailVisibility()
    {
        OnPropertyChanged(nameof(ShowDetailForm));
        OnPropertyChanged(nameof(ShowDetailPlaceholder));
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await SearchCustomersAsync(autoSelectFirst: !IsNewCustomer);
    }

    [RelayCommand]
    private async Task Search()
    {
        await SearchCustomersAsync(autoSelectFirst: !IsNewCustomer);
    }

    private async Task SearchCustomersAsync(bool autoSelectFirst)
    {
        StatusMessage = "Loading customers…";
        try
        {
            var rows = await _directory.SearchAsync(
                _services.StoreContext.StoreId,
                string.IsNullOrWhiteSpace(SearchCustomerCode) ? null : SearchCustomerCode.Trim(),
                string.IsNullOrWhiteSpace(SearchCustomerName) ? null : SearchCustomerName.Trim(),
                string.IsNullOrWhiteSpace(SearchCustomerPhone) ? null : SearchCustomerPhone.Trim());

            _suppressSelection = true;
            try
            {
                Results.Clear();
                foreach (var row in rows)
                    Results.Add(row);
            }
            finally
            {
                _suppressSelection = false;
            }

            ResultsCountSummary = rows.Count == 0 ? "No matches" : $"{rows.Count} customer(s)";
            if (IsNewCustomer)
            {
                SelectedCustomer = null;
                ApplyNewCustomerFormState();
                NotifyDetailVisibility();
            }
            else if (autoSelectFirst)
            {
                SelectedCustomer = Results.Count > 0 ? Results[0] : null;
                StatusMessage = rows.Count == 0
                    ? "No customers found."
                    : $"{rows.Count} customer(s) found.";
            }
            else
            {
                StatusMessage = rows.Count == 0
                    ? "No customers found."
                    : $"{rows.Count} customer(s) found.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Search failed: " + ex.Message;
        }
    }

    partial void OnSelectedCustomerChanged(CustomerListRow? value)
    {
        if (_suppressSelection)
            return;

        if (value != null)
            IsNewCustomer = false;
        _ = LoadSelectedDetailAsync(value);
        UseInBillingCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        NotifyDetailVisibility();
    }

    partial void OnIsNewCustomerChanged(bool value) => NotifyDetailVisibility();

    private async Task LoadSelectedDetailAsync(CustomerListRow? row)
    {
        if (row == null)
        {
            if (IsNewCustomer)
                return;

            ClearDetailForm();
            DetailSourceLabel = "";
            DetailPanelTitle = "Customer details";
            IsDetailReadOnly = true;
            StatusMessage = "Select a customer from the list or click New customer.";
            return;
        }

        DetailPanelTitle = row.Name;

        if (row.Source == "Central" || string.IsNullOrWhiteSpace(row.LocalMongoId))
        {
            LoadDetailFromRow(row);

            if (_services.CentralMode.IsOnlineMode && !string.IsNullOrWhiteSpace(row.CentralCustomerId))
            {
                try
                {
                    StatusMessage = $"Loading {row.Name}…";
                    var central = await _lookup.GetCentralByIdAsync(row.CentralCustomerId);
                    if (central != null
                        && SelectedCustomer?.CentralCustomerId == row.CentralCustomerId)
                    {
                        LoadDetailFromCentralMatch(central);
                        DetailPanelTitle = CustomerName;
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Loaded list fields only — full detail failed: {ex.Message}";
                    DetailSourceLabel = "Central customer — editable (Online mode).";
                    IsDetailReadOnly = false;
                    return;
                }

                DetailSourceLabel = "Central customer — editable (Online mode).";
                IsDetailReadOnly = false;
                StatusMessage = $"Viewing {CustomerName} (central).";
            }
            else
            {
                DetailSourceLabel = "Central customer (register locally to edit)";
                IsDetailReadOnly = true;
                StatusMessage = $"{row.Name} — central record only.";
            }
            return;
        }

        var doc = await _directory.GetLocalByIdAsync(row.LocalMongoId);
        if (doc == null)
        {
            LoadDetailFromRow(row);
            DetailSourceLabel = "Local";
            IsDetailReadOnly = false;
            return;
        }

        LoadDetailFromDocument(doc);
        DetailPanelTitle = CustomerName;
        DetailSourceLabel = "Local customer — edits sync to central when linked.";
        IsDetailReadOnly = false;
        StatusMessage = $"Viewing {CustomerName}.";
    }

    private void LoadDetailFromRow(CustomerListRow row)
    {
        LocalMongoId = row.LocalMongoId ?? "";
        CentralCustomerId = row.CentralCustomerId ?? "";
        CustomerCode = row.CustomerCode;
        CustomerName = row.Name;
        SplitPhoneIntoFields(row.Phone);
        Email = row.Email;
        IsCreditCustomer = row.IsCreditCustomer;
        SyncStatus = row.SyncStatus;
        Gstin = "";
        DoorNo = "";
        Street = "";
        FullAddress = "";
        Place = "";
        City = "";
        Pincode = "";
        State = "";
        Landmark = "";
    }

    private void LoadDetailFromCentralMatch(CustomerMatch match)
    {
        LocalMongoId = "";
        CentralCustomerId = match.Id;
        CustomerCode = match.Code;
        CustomerName = match.Name;
        SplitPhoneIntoFields(match.Phone);
        Email = match.Email ?? "";
        Gstin = match.Gstin ?? "";
        DoorNo = match.DoorNo ?? "";
        Street = match.Street ?? "";
        FullAddress = match.FullAddress ?? "";
        Place = match.Place ?? "";
        City = match.City ?? "";
        Pincode = match.Pincode ?? "";
        State = match.State ?? "";
        Landmark = "";
        IsCreditCustomer = match.IsCreditCustomer;
        SyncStatus = "central";
    }

    private void SplitPhoneIntoFields(string? phoneCombined)
    {
        var phone = (phoneCombined ?? "").Trim();
        if (string.IsNullOrEmpty(phone))
        {
            Telephone = "";
            Mobile = "";
            return;
        }

        var sep = phone.IndexOf(" / ", StringComparison.Ordinal);
        if (sep >= 0)
        {
            Telephone = phone[..sep].Trim();
            Mobile = phone[(sep + 3)..].Trim();
            return;
        }

        Telephone = "";
        Mobile = phone;
    }

    private void LoadDetailFromDocument(BsonDocument doc)
    {
        LocalMongoId = doc["_id"].ToString() ?? "";
        var centralIdValue = doc.GetValue("centralCustomerId", BsonNull.Value);
        CentralCustomerId = centralIdValue.IsBsonNull ? "" : centralIdValue.AsString;
        CustomerCode = doc.GetValue("customerCode", "").AsString;
        CustomerName = doc.GetValue("name", "").AsString;
        Telephone = doc.GetValue("telephone", "").AsString;
        Mobile = doc.GetValue("mobile", "").AsString;
        if (string.IsNullOrWhiteSpace(Mobile))
            Mobile = doc.GetValue("phone", "").AsString;
        Email = doc.GetValue("email", "").AsString;
        Gstin = doc.GetValue("gstin", "").AsString;
        DoorNo = doc.GetValue("doorNo", "").AsString;
        Street = doc.GetValue("street", "").AsString;
        FullAddress = doc.GetValue("fullAddress", "").AsString;
        Place = doc.GetValue("place", "").AsString;
        City = doc.GetValue("city", "").AsString;
        Pincode = doc.GetValue("pincode", "").AsString;
        State = doc.GetValue("state", "").AsString;
        Landmark = doc.GetValue("landmark", "").AsString;
        IsCreditCustomer = doc.Contains("isCreditCustomer") && doc["isCreditCustomer"].ToBoolean();
        SyncStatus = doc.GetValue("centralSyncStatus", "").AsString;
    }

    [RelayCommand]
    private void NewCustomer() => StartNewRegistration();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(CustomerName))
        {
            AppDialog.Show("Customer name is required.", "Customers", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(Mobile))
        {
            AppDialog.Show("Mobile number is required.", "Customers", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!string.IsNullOrWhiteSpace(Email) && !IsValidEmail(Email))
        {
            AppDialog.Show("Email address does not look valid.", "Customers", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            StatusMessage = IsNewCustomer ? "Saving new customer…" : "Updating customer…";
            var payload = BuildPayload();

            CustomerRegistrationResult result;
            if (IsNewCustomer)
            {
                CustomerCode = await _codeGenerator.NextAsync();
                payload = BuildPayload();
                result = await _registrationService.RegisterAsync(payload);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(LocalMongoId) && string.IsNullOrWhiteSpace(CentralCustomerId))
                {
                    AppDialog.Show("Select a customer to update.", "Customers", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                result = await _registrationService.UpdateAsync(LocalMongoId, payload, CentralCustomerId);
            }

            if (!string.IsNullOrWhiteSpace(result.CentralSyncWarning))
            {
                AppDialog.Show(result.CentralSyncWarning, "Customers", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            IsNewCustomer = false;
            LocalMongoId = result.LocalMongoId ?? "";
            CentralCustomerId = result.CentralCustomerId ?? "";
            if (!string.IsNullOrWhiteSpace(result.BillingCustomerCode))
                CustomerCode = result.BillingCustomerCode;
            SyncStatus = result.CentralSyncStatus ?? SyncStatus;

            await SearchCustomersAsync(autoSelectFirst: false);
            var savedRow = FindSavedRow(result);
            if (savedRow != null)
                SelectedCustomer = savedRow;
            UseInBillingCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            StatusMessage = $"Customer {CustomerName} saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = "";
            AppDialog.Show($"Could not save customer: {ex.Message}", "Customers", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanSave() => IsNewCustomer
        || (!IsDetailReadOnly && (!string.IsNullOrWhiteSpace(LocalMongoId) || !string.IsNullOrWhiteSpace(CentralCustomerId)));

    [RelayCommand(CanExecute = nameof(CanUseInBilling))]
    private void UseInBilling()
    {
        if (string.IsNullOrWhiteSpace(CustomerName))
            return;

        _billing.ApplyCustomerRegistration(new CustomerRegistrationResult
        {
            LocalMongoId = LocalMongoId,
            CentralCustomerId = CentralCustomerId,
            CentralSyncStatus = SyncStatus,
            CustomerName = CustomerName,
            CustomerPhone = string.IsNullOrWhiteSpace(Mobile) ? Telephone : Mobile,
            DoorNo = DoorNo,
            Street = Street,
            FullAddress = FullAddress,
            BillingCustomerCode = CustomerCode,
        });
        _navigateToBilling();
    }

    private bool CanUseInBilling() =>
        !IsNewCustomer
        && !string.IsNullOrWhiteSpace(CustomerName)
        && (SelectedCustomer != null
            || !string.IsNullOrWhiteSpace(LocalMongoId)
            || !string.IsNullOrWhiteSpace(CentralCustomerId)
            || !string.IsNullOrWhiteSpace(CustomerCode));

    private CustomerRegistrationPayload BuildPayload() => new()
    {
        CustomerCode = CustomerCode,
        CustomerName = CustomerName,
        Telephone = Telephone,
        Mobile = Mobile,
        Email = Email,
        Gstin = Gstin,
        DoorNo = DoorNo,
        Street = Street,
        FullAddress = FullAddress,
        Place = Place,
        City = City,
        Pincode = Pincode,
        State = State,
        Landmark = Landmark,
        IsCreditCustomer = IsCreditCustomer,
    };

    [RelayCommand]
    private void SelectAllCustomers()
    {
        foreach (var row in Results)
            row.IsSelected = true;
        StatusMessage = $"{Results.Count} customer(s) selected.";
    }

    [RelayCommand]
    private void ClearCustomerSelection()
    {
        foreach (var row in Results)
            row.IsSelected = false;
        StatusMessage = "Selection cleared.";
    }

    [RelayCommand]
    private async Task BroadcastWhatsApp()
    {
        var selected = Results.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0)
        {
            AppDialog.Show("Select one or more customers first (or Select all).", "Broadcast", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrEmpty(_services.CentralAuthSession.AccessToken))
        {
            AppDialog.Show("Log in to Central on Settings → Connection & sync first.", "Broadcast", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _services.CentralAuthSession.ApplyTo(_services.CentralApi);
        var (settings, settingsErr) = await _services.WhatsAppClient.GetSettingsAsync(_services.StoreContext.StoreId);
        if (settings == null)
        {
            AppDialog.Show(settingsErr ?? "Could not load WhatsApp settings.", "Broadcast", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!settings.Enabled)
        {
            AppDialog.Show("WhatsApp is disabled for this store on Central.", "Broadcast", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var recipients = new List<(string Name, string Phone)>();
        foreach (var row in selected)
        {
            if (!PhoneE164Helper.CanSendWhatsApp(row.Phone, settings.DefaultCountryCode))
                continue;
            recipients.Add((row.Name, row.Phone));
        }

        if (recipients.Count == 0)
        {
            AppDialog.Show("None of the selected customers have a valid mobile number.", "Broadcast", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var owner = Application.Current?.MainWindow;
        var dlg = new WhatsAppBroadcastDialog(_services, settings, recipients)
        {
            Owner = owner,
        };
        dlg.ShowDialog();
    }

    private void ClearDetailForm()
    {
        LocalMongoId = "";
        CentralCustomerId = "";
        CustomerCode = "";
        CustomerName = "";
        Telephone = "";
        Mobile = "";
        Email = "";
        Gstin = "";
        DoorNo = "";
        Street = "";
        FullAddress = "";
        Place = "";
        City = "";
        Pincode = "";
        State = "";
        Landmark = "";
        IsCreditCustomer = false;
        SyncStatus = "";
        OnPropertyChanged(nameof(CustomerCodeHint));
    }

    partial void OnCustomerCodeChanged(string value) => OnPropertyChanged(nameof(CustomerCodeHint));

    private CustomerListRow? FindSavedRow(CustomerRegistrationResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.CentralCustomerId))
        {
            var byCentralId = Results.FirstOrDefault(r =>
                string.Equals(r.CentralCustomerId, result.CentralCustomerId, StringComparison.Ordinal));
            if (byCentralId != null)
                return byCentralId;
        }

        if (!string.IsNullOrWhiteSpace(result.LocalMongoId))
        {
            var byLocalId = Results.FirstOrDefault(r =>
                string.Equals(r.LocalMongoId, result.LocalMongoId, StringComparison.Ordinal));
            if (byLocalId != null)
                return byLocalId;
        }

        if (!string.IsNullOrWhiteSpace(result.BillingCustomerCode))
        {
            var byCode = Results.FirstOrDefault(r =>
                string.Equals(r.CustomerCode, result.BillingCustomerCode, StringComparison.OrdinalIgnoreCase));
            if (byCode != null)
                return byCode;
        }

        var phone = string.IsNullOrWhiteSpace(Mobile) ? Telephone : Mobile;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            return Results.FirstOrDefault(r =>
                string.Equals(r.Phone, phone, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.Name, result.CustomerName, StringComparison.OrdinalIgnoreCase));
        }

        return Results.FirstOrDefault(r =>
            string.Equals(r.Name, result.CustomerName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
