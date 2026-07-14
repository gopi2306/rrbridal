using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Views;

public partial class RecordCreditPaymentDialog : Window
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly decimal _balanceDue;
    private readonly bool _allowPartial;

    public bool Confirmed { get; private set; }
    public CreditReceivedPaymentMode SelectedPaymentMode { get; private set; } = CreditReceivedPaymentMode.Cash;
    public string TransactionNo { get; private set; } = "";
    public decimal PaidAmount { get; private set; }

    public RecordCreditPaymentDialog(decimal balanceDue, bool allowPartial)
    {
        InitializeComponent();
        _balanceDue = balanceDue;
        _allowPartial = allowPartial;
        BalanceHintText.Text = allowPartial
            ? $"Balance due {MoneyMath.FormatRupee(balanceDue)}. Enter full or partial amount."
            : $"Balance due {MoneyMath.FormatRupee(balanceDue)}. Partial collection is disabled — enter the full balance.";
        AmountBox.Text = MoneyMath.FormatEditableAmount(balanceDue);
        if (!allowPartial)
            AmountBox.IsReadOnly = true;
        AmountBox.TextChanged += (_, _) => UpdateRemainingPreview();
        UpdateRemainingPreview();
    }

    private void UpdateRemainingPreview()
    {
        if (!TryParseAmount(out var amt))
        {
            RemainingPreviewText.Text = "";
            return;
        }

        var remaining = MoneyMath.RoundDisplayAmount(_balanceDue - amt);
        RemainingPreviewText.Text = remaining <= 0
            ? "This payment will settle the bill."
            : $"Remaining after payment: {MoneyMath.FormatRupee(remaining)}";
    }

    private bool TryParseAmount(out decimal amount)
    {
        amount = 0;
        var text = AmountBox.Text?.Trim() ?? "";
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
            || decimal.TryParse(text, NumberStyles.Number, InCulture, out amount);
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseAmount(out var amt) || amt <= 0)
        {
            MessageBox.Show("Enter a valid payment amount.", "Record payment",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        amt = MoneyMath.RoundDisplayAmount(amt);
        if (amt > _balanceDue + 0.009m)
        {
            MessageBox.Show("Amount cannot exceed balance due.", "Record payment",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_allowPartial && amt + 0.009m < _balanceDue)
        {
            MessageBox.Show("Partial collection is disabled. Enter the full balance.", "Record payment",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var txn = TransactionNoBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(txn))
        {
            MessageBox.Show("Enter a transaction / reference number.", "Record payment",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedPaymentMode = CashRadio.IsChecked == true
            ? CreditReceivedPaymentMode.Cash
            : UpiRadio.IsChecked == true
                ? CreditReceivedPaymentMode.UPI
                : CreditReceivedPaymentMode.Card;
        TransactionNo = txn;
        PaidAmount = amt;
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
