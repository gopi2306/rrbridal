using System.Globalization;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;

namespace RRBridal.StoreBilling.App.Views;

public partial class CashMovementDialog
{
    private static readonly CultureInfo ParseCulture = CultureInfo.InvariantCulture;

    public decimal Amount { get; private set; }

    public string Description { get; private set; } = "";

    public CashMovementDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => AmountBox.Focus();
    }

    public static bool TryShow(Window owner, out decimal amount, out string description)
    {
        amount = 0;
        description = "";

        var dialog = new CashMovementDialog { Owner = owner };
        if (dialog.ShowDialog() != true)
            return false;

        amount = dialog.Amount;
        description = dialog.Description;
        return true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Post_OnClick(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(AmountBox.Text?.Trim(), NumberStyles.Any, ParseCulture, out var amount)
            && !decimal.TryParse(AmountBox.Text?.Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out amount))
        {
            AppDialog.Show("Enter a valid amount.", "Payment voucher", MessageBoxButton.OK, MessageBoxImage.Warning);
            AmountBox.Focus();
            return;
        }

        if (amount <= 0)
        {
            AppDialog.Show("Amount must be greater than zero.", "Payment voucher", MessageBoxButton.OK, MessageBoxImage.Warning);
            AmountBox.Focus();
            return;
        }

        Amount = amount;
        Description = DescriptionBox.Text?.Trim() ?? "";
        DialogResult = true;
        Close();
    }
}
