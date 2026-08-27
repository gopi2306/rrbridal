using System.ComponentModel;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Customers;
using RRBridal.StoreBilling.App.Services.Ui;

namespace RRBridal.StoreBilling.App.Views;

public partial class CustomerQuickCaptureDialog : Window, INotifyPropertyChanged
{
    private string _customerName = "";
    private string _mobileNo = "";
    private string _telephone = "";
    private string _email = "";
    private string _gstin = "";
    private string _doorNo = "";
    private string _street = "";
    private string _fullAddress = "";
    private string _city = "";
    private string _pincode = "";
    private string _state = "";
    private bool _isCreditCustomer;
    private string _statusText = "";

    public CustomerMatch? ExistingMatch { get; }

    public bool IsNewCustomer { get; }

    public int ExactMatchCount { get; }

    public bool Saved { get; private set; }

    public bool WantsAdvancedSearch { get; private set; }

    public string CustomerName
    {
        get => _customerName;
        set { _customerName = value; OnPropChanged(); }
    }

    public string MobileNo
    {
        get => _mobileNo;
        set { _mobileNo = value; OnPropChanged(); }
    }

    public string Telephone
    {
        get => _telephone;
        set { _telephone = value; OnPropChanged(); }
    }

    public string Email
    {
        get => _email;
        set { _email = value; OnPropChanged(); }
    }

    public string Gstin
    {
        get => _gstin;
        set { _gstin = value; OnPropChanged(); }
    }

    public string DoorNo
    {
        get => _doorNo;
        set { _doorNo = value; OnPropChanged(); }
    }

    public string Street
    {
        get => _street;
        set { _street = value; OnPropChanged(); }
    }

    public string FullAddress
    {
        get => _fullAddress;
        set { _fullAddress = value; OnPropChanged(); }
    }

    public string City
    {
        get => _city;
        set { _city = value; OnPropChanged(); }
    }

    public string Pincode
    {
        get => _pincode;
        set { _pincode = value; OnPropChanged(); }
    }

    public string State
    {
        get => _state;
        set { _state = value; OnPropChanged(); }
    }

    public bool IsCreditCustomer
    {
        get => _isCreditCustomer;
        set { _isCreditCustomer = value; OnPropChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropChanged(); }
    }

    public Visibility ShowAdvancedSearch => ExactMatchCount > 1 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ShowExtendedFields => IsNewCustomer ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public CustomerQuickCaptureDialog(
        string initialPhone,
        string initialName,
        CustomerMatch? existingMatch,
        bool isNewCustomer,
        int exactMatchCount)
    {
        ExistingMatch = existingMatch;
        IsNewCustomer = isNewCustomer;
        ExactMatchCount = exactMatchCount;

        InitializeComponent();
        DataContext = this;

        MobileNo = initialPhone;
        CustomerName = initialName;

        StatusText = isNewCustomer
            ? "New customer — enter details and save to add to the store."
            : exactMatchCount > 1
                ? $"{exactMatchCount} customers match this mobile. Showing the first — use Advanced search to pick another."
                : "Existing customer found — confirm or edit name, then save.";

        OnPropChanged(nameof(ShowAdvancedSearch));
        OnPropChanged(nameof(ShowExtendedFields));

        Loaded += (_, _) =>
        {
            DialogLayoutHelper.CenterAndClamp(this, Owner);
            if (string.IsNullOrWhiteSpace(CustomerName))
                NameBox.Focus();
            else
            {
                NameBox.Focus();
                NameBox.SelectAll();
            }
        };
    }

    public CustomerRegistrationPayload ToPayload(string customerCode) => new()
    {
        CustomerCode = customerCode,
        CustomerName = CustomerName.Trim(),
        Telephone = Telephone.Trim(),
        Mobile = MobileNo.Trim(),
        Email = Email.Trim(),
        Gstin = Gstin.Trim(),
        DoorNo = DoorNo.Trim(),
        Street = Street.Trim(),
        FullAddress = FullAddress.Trim(),
        City = City.Trim(),
        Pincode = Pincode.Trim(),
        State = State.Trim(),
        IsCreditCustomer = IsCreditCustomer,
    };

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!Validate())
            return;

        Saved = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void AdvancedSearch_OnClick(object sender, RoutedEventArgs e)
    {
        WantsAdvancedSearch = true;
        DialogResult = false;
        Close();
    }

    private bool Validate()
    {
        var phone = MobileNo.Trim();
        if (!PhoneMatchHelper.IsPhoneLikeQuery(phone))
        {
            ShowValidation("Enter a valid 10-digit mobile number.");
            MobileBox.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(CustomerName))
        {
            ShowValidation("Customer name is required.");
            NameBox.Focus();
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Email) && !IsValidEmail(Email.Trim()))
        {
            ShowValidation("Email address does not look valid.");
            return false;
        }

        HideValidation();
        return true;
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

    private void ShowValidation(string message)
    {
        ValidationBlock.Text = message;
        ValidationBlock.Visibility = Visibility.Visible;
    }

    private void HideValidation()
    {
        ValidationBlock.Visibility = Visibility.Collapsed;
    }

    private void OnPropChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
