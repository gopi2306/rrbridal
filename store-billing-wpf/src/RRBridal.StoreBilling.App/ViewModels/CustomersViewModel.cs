using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows;
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

    public ObservableCollection<CustomerListRow> Results { get; } = new();

    public CustomersViewModel(AppServices services, BillingViewModel billing, Action navigateToBilling)
    {
        _services = services;
        _registrationService = new CustomerRegistrationService(services.LocalDb, services.CentralApi, services.StoreContext);
        _directory = new CustomerDirectoryService(
            services.LocalDb,
            new CustomerLookupService(services.LocalDb, services.CentralApi));
        _codeGenerator = new CustomerCodeGenerator(services.LocalDb);
        _billing = billing;
        _navigateToBilling = navigateToBilling;
    }

    public void StartNewRegistration()
    {
        IsNewCustomer = true;
        IsDetailReadOnly = false;
        SelectedCustomer = null;
        ClearDetailForm();
        DetailSourceLabel = "Register a new customer for billing and credit eligibility.";
        DetailPanelTitle = "New customer";
        StatusMessage = "Enter customer details and save (F4).";
        NotifyDetailVisibility();
        SaveCommand.NotifyCanExecuteChanged();
        UseInBillingCommand.NotifyCanExecuteChanged();
    }

    private void NotifyDetailVisibility()
    {
        OnPropertyChanged(nameof(ShowDetailForm));
        OnPropertyChanged(nameof(ShowDetailPlaceholder));
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await Search();
    }

    [RelayCommand]
    private async Task Search()
    {
        StatusMessage = "Loading customers…";
        try
        {
            var rows = await _directory.SearchAsync(
                _services.StoreContext.StoreId,
                string.IsNullOrWhiteSpace(SearchCustomerCode) ? null : SearchCustomerCode.Trim(),
                string.IsNullOrWhiteSpace(SearchCustomerName) ? null : SearchCustomerName.Trim(),
                string.IsNullOrWhiteSpace(SearchCustomerPhone) ? null : SearchCustomerPhone.Trim());

            Results.Clear();
            foreach (var row in rows)
                Results.Add(row);

            StatusMessage = rows.Count == 0
                ? "No customers found."
                : $"{rows.Count} customer(s) found.";
            ResultsCountSummary = rows.Count == 0 ? "No matches" : $"{rows.Count} customer(s)";
            if (!IsNewCustomer)
                SelectedCustomer = Results.Count > 0 ? Results[0] : null;
            else
                NotifyDetailVisibility();
        }
        catch (Exception ex)
        {
            StatusMessage = "Search failed: " + ex.Message;
        }
    }

    partial void OnSelectedCustomerChanged(CustomerListRow? value)
    {
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
            DetailSourceLabel = "Central customer (register locally to edit)";
            IsDetailReadOnly = true;
            StatusMessage = $"{row.Name} — central record only.";
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
        CustomerCode = row.CustomerCode;
        CustomerName = row.Name;
        Mobile = row.Phone;
        Telephone = "";
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

    private void LoadDetailFromDocument(BsonDocument doc)
    {
        LocalMongoId = doc["_id"].ToString() ?? "";
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
            MessageBox.Show("Customer name is required.", "Customers", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(Mobile))
        {
            MessageBox.Show("Mobile number is required.", "Customers", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!string.IsNullOrWhiteSpace(Email) && !IsValidEmail(Email))
        {
            MessageBox.Show("Email address does not look valid.", "Customers", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (string.IsNullOrWhiteSpace(LocalMongoId))
                {
                    MessageBox.Show("Select a local customer to update.", "Customers", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                result = await _registrationService.UpdateAsync(LocalMongoId, payload);
            }

            if (!string.IsNullOrWhiteSpace(result.CentralSyncWarning))
            {
                MessageBox.Show(result.CentralSyncWarning, "Customers", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            IsNewCustomer = false;
            await Search();
            SelectedCustomer = Results.FirstOrDefault(r => r.LocalMongoId == result.LocalMongoId)
                ?? Results.FirstOrDefault(r => r.CustomerCode == result.BillingCustomerCode);
            StatusMessage = $"Customer {CustomerName} saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = "";
            MessageBox.Show($"Could not save customer: {ex.Message}", "Customers", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanSave() => IsNewCustomer || (!IsDetailReadOnly && !string.IsNullOrWhiteSpace(LocalMongoId));

    [RelayCommand(CanExecute = nameof(CanUseInBilling))]
    private void UseInBilling()
    {
        if (SelectedCustomer == null || string.IsNullOrWhiteSpace(CustomerName))
            return;

        _billing.ApplyCustomerRegistration(new CustomerRegistrationResult
        {
            LocalMongoId = LocalMongoId,
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
        && SelectedCustomer != null
        && !string.IsNullOrWhiteSpace(CustomerName)
        && SelectedCustomer.Source == "Local";

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

    private void ClearDetailForm()
    {
        LocalMongoId = "";
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
