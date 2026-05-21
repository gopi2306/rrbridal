using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using RRBridal.StoreBilling.App.Services.Customers;

namespace RRBridal.StoreBilling.App.Views;

public partial class CustomerQuickCaptureDialog : Window, INotifyPropertyChanged
{
    private string _customerName = "";
    private string _mobileNo = "";
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

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropChanged(); }
    }

    public Visibility ShowAdvancedSearch => ExactMatchCount > 1 ? Visibility.Visible : Visibility.Collapsed;

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
            ? "New customer — enter name and save to add to the store."
            : exactMatchCount > 1
                ? $"{exactMatchCount} customers match this mobile. Showing the first — use Advanced search to pick another."
                : "Existing customer found — confirm or edit name, then save.";

        OnPropChanged(nameof(ShowAdvancedSearch));

        Loaded += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(CustomerName))
                NameBox.Focus();
            else
            {
                NameBox.Focus();
                NameBox.SelectAll();
            }
        };
    }

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

        HideValidation();
        return true;
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
