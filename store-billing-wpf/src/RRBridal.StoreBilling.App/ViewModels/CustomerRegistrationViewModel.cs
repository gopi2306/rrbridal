using System;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Customers;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class CustomerRegistrationViewModel : ObservableObject
{
    private readonly CustomerRegistrationService _registrationService;
    private readonly CustomerCodeGenerator _codeGenerator;
    private readonly BillingViewModel _billing;
    private readonly Action _navigateToBilling;

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
    [ObservableProperty] private bool _gpsStub;
    [ObservableProperty] private string _statusMessage = "";

    public CustomerRegistrationViewModel(AppServices services, BillingViewModel billing, Action navigateToBilling)
    {
        _registrationService = new CustomerRegistrationService(services.LocalDb, services.CentralApi, services.StoreContext);
        _codeGenerator = new CustomerCodeGenerator(services.LocalDb);
        _billing = billing;
        _navigateToBilling = navigateToBilling;
    }

    [RelayCommand]
    private void Cancel()
    {
        _navigateToBilling();
    }

    [RelayCommand]
    private async Task Save()
    {
        StatusMessage = "";
        if (string.IsNullOrWhiteSpace(CustomerName))
        {
            AppDialog.Show("Customer name is required.", "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(Mobile))
        {
            AppDialog.Show("Mobile number is required.", "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!string.IsNullOrWhiteSpace(Email) && !IsValidEmail(Email))
        {
            AppDialog.Show("Email address does not look valid.", "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            StatusMessage = "Generating customer code…";
            CustomerCode = await _codeGenerator.NextAsync();

            StatusMessage = "Saving customer…";
            var payload = new CustomerRegistrationPayload
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

            var result = await _registrationService.RegisterAsync(payload);

            var owner = Application.Current.MainWindow;
            var dlg = new CustomerSaveSuccessDialog(result.CentralSyncWarning)
            {
                Owner = owner,
            };
            var goBilling = dlg.ShowDialog() == true;

            if (goBilling)
            {
                _billing.ApplyCustomerRegistration(result);
                _navigateToBilling();
            }
            else
                ClearForm();

            StatusMessage = goBilling
                ? "Customer applied to billing."
                : "Customer saved. You can register another or open Billing when ready.";
        }
        catch (Exception ex)
        {
            StatusMessage = "";
            AppDialog.Show($"Could not save customer: {ex.Message}", "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void ClearForm()
    {
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
        GpsStub = false;
        StatusMessage = "";
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
