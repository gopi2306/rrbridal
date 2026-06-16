using System.Windows;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Views;

public partial class RecordCodPaymentDialog : Window
{
    public bool Confirmed { get; private set; }
    public CodReceivedPaymentMode SelectedPaymentMode { get; private set; } = CodReceivedPaymentMode.Cash;
    public string TransactionNo { get; private set; } = "";

    public RecordCodPaymentDialog(decimal amount)
    {
        InitializeComponent();
        AmountText.Text = MoneyMath.FormatRupee(amount);
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var txn = TransactionNoBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(txn))
        {
            MessageBox.Show("Enter a transaction / reference number.", "Record payment",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedPaymentMode = CashRadio.IsChecked == true
            ? CodReceivedPaymentMode.Cash
            : UpiRadio.IsChecked == true
                ? CodReceivedPaymentMode.UPI
                : CodReceivedPaymentMode.Card;
        TransactionNo = txn;
        Confirmed = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
