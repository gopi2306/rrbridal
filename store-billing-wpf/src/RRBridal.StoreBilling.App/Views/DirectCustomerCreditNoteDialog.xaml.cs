using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Customers;
using RRBridal.StoreBilling.App.Services.Ui;

namespace RRBridal.StoreBilling.App.Views;

public partial class DirectCustomerCreditNoteDialog : INotifyPropertyChanged
{
    private static readonly CultureInfo ParseCulture = CultureInfo.InvariantCulture;

    private readonly AppServices _services;
    private string _customerCode = "";
    private string _customerName = "";
    private string _customerPhone = "";
    private string _amountText = "";
    private string _reason = "";
    private string? _createdCreditNoteNo;
    private bool _isPosting;

    public string CustomerCode
    {
        get => _customerCode;
        set { _customerCode = value; OnPropChanged(); OnPropChanged(nameof(CustomerDisplay)); }
    }

    public string CustomerName
    {
        get => _customerName;
        set { _customerName = value; OnPropChanged(); OnPropChanged(nameof(CustomerDisplay)); }
    }

    public string CustomerPhone
    {
        get => _customerPhone;
        set { _customerPhone = value; OnPropChanged(); OnPropChanged(nameof(CustomerDisplay)); }
    }

    public string AmountText
    {
        get => _amountText;
        set { _amountText = value; OnPropChanged(); }
    }

    public string Reason
    {
        get => _reason;
        set { _reason = value; OnPropChanged(); }
    }

    public string CustomerDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CustomerName) && string.IsNullOrWhiteSpace(CustomerPhone))
                return "Search and select a customer…";
            if (string.IsNullOrWhiteSpace(CustomerPhone))
                return CustomerName;
            return $"{CustomerName}  —  {CustomerPhone}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DirectCustomerCreditNoteDialog(AppServices services)
    {
        _services = services;
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) => AmountBox.Focus();
    }

    /// <summary>Shows dialog and posts; returns created credit note number, or null if cancelled/failed.</summary>
    public static async Task<string?> TryShowAndPostAsync(Window owner, AppServices services)
    {
        var dialog = new DirectCustomerCreditNoteDialog(services) { Owner = owner };
        var result = dialog.ShowDialog();
        if (result != true)
            return null;
        return dialog._createdCreditNoteNo;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SearchCustomer_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new CustomerSearchDialog(
            CustomerPhone.Trim(),
            new CustomerLookupService(_services.LocalDb, _services.CentralApi, _services.CentralMode))
        {
            Owner = this,
        };
        if (dlg.ShowDialog() != true || dlg.SelectedCustomer == null)
            return;

        ApplyCustomer(dlg.SelectedCustomer);
    }

    private void ApplyCustomer(CustomerMatch match)
    {
        CustomerCode = match.Code ?? "";
        CustomerName = match.Name ?? "";
        CustomerPhone = match.Phone ?? "";
    }

    private async void Post_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isPosting)
            return;

        if (!decimal.TryParse(AmountText?.Trim(), NumberStyles.Any, ParseCulture, out var amount)
            && !decimal.TryParse(AmountText?.Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out amount))
        {
            AppDialog.Show("Enter a valid amount.", "Direct Customer Credit Note", MessageBoxButton.OK, MessageBoxImage.Warning);
            AmountBox.Focus();
            return;
        }

        if (amount <= 0)
        {
            AppDialog.Show("Amount must be greater than zero.", "Direct Customer Credit Note", MessageBoxButton.OK, MessageBoxImage.Warning);
            AmountBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(CustomerName))
        {
            AppDialog.Show("Select or enter a customer name.", "Direct Customer Credit Note", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var phoneNorm = PhoneMatchHelper.NormalizePhone(CustomerPhone);
        if (string.IsNullOrEmpty(phoneNorm) || phoneNorm.Length < 10)
        {
            AppDialog.Show("Enter a valid 10-digit customer mobile.", "Direct Customer Credit Note", MessageBoxButton.OK, MessageBoxImage.Warning);
            PhoneBox.Focus();
            return;
        }

        _isPosting = true;
        try
        {
            var (success, message, creditNoteNo) = await _services.CustomerCreditNotes.CreateDirectAsync(
                CustomerCode,
                CustomerName,
                CustomerPhone,
                amount,
                _services.StoreContext.StoreId,
                Reason);

            if (!success || string.IsNullOrWhiteSpace(creditNoteNo))
            {
                AppDialog.Show(message, "Direct Customer Credit Note", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _createdCreditNoteNo = creditNoteNo;
            DialogResult = true;
            Close();
        }
        finally
        {
            _isPosting = false;
        }
    }

    private void OnPropChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
