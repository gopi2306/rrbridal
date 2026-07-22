using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Views;

public partial class RecordCreditPaymentDialog : Window
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly decimal _balanceDue;
    private readonly bool _allowPartial;
    private readonly CustomerCreditNoteService? _creditNotes;
    private readonly string _storeId;
    private readonly string _billNo;

    public bool Confirmed { get; private set; }
    public CreditReceivedPaymentMode SelectedPaymentMode { get; private set; } = CreditReceivedPaymentMode.Cash;
    public string TransactionNo { get; private set; } = "";
    public decimal PaidAmount { get; private set; }
    public IReadOnlyList<CreditPaymentLeg> PaymentLegs { get; private set; } = [];

    public RecordCreditPaymentDialog(
        decimal balanceDue,
        bool allowPartial,
        string storeId,
        string billNo,
        string? customerPhone,
        string? customerCode,
        CustomerCreditNoteService? creditNotes = null)
    {
        InitializeComponent();
        _balanceDue = balanceDue;
        _allowPartial = allowPartial;
        _storeId = storeId?.Trim() ?? "";
        _billNo = billNo?.Trim() ?? "";
        _creditNotes = creditNotes;

        BalanceHintText.Text = allowPartial
            ? $"Balance due {MoneyMath.FormatRupee(balanceDue)}. Enter full or partial amount."
            : $"Balance due {MoneyMath.FormatRupee(balanceDue)}. Partial collection is disabled — enter the full balance.";
        AmountBox.Text = MoneyMath.FormatEditableAmount(balanceDue);
        if (!allowPartial)
            AmountBox.IsReadOnly = true;

        AmountBox.TextChanged += (_, _) =>
        {
            UpdateRemainingPreview();
            UpdateSplitRemaining();
        };

        CashRadio.Checked += (_, _) => UpdateModePanels();
        UpiRadio.Checked += (_, _) => UpdateModePanels();
        CardRadio.Checked += (_, _) => UpdateModePanels();
        CreditNoteRadio.Checked += (_, _) => UpdateModePanels();
        SplitRadio.Checked += (_, _) => UpdateModePanels();

        SplitCashBox.TextChanged += (_, _) => UpdateSplitRemaining();
        SplitCardBox.TextChanged += (_, _) => UpdateSplitRemaining();
        SplitUpiBox.TextChanged += (_, _) => UpdateSplitRemaining();
        SplitCreditNoteAmountBox.TextChanged += (_, _) => UpdateSplitRemaining();

        CreditNoteCombo.SelectionChanged += (_, _) =>
        {
            if (CreditNoteCombo.SelectedValue is string noteNo && !string.IsNullOrWhiteSpace(noteNo))
                CreditNoteRefBox.Text = noteNo;
        };

        Loaded += async (_, _) =>
        {
            await LoadCreditNotesAsync(customerPhone, customerCode);
            UpdateModePanels();
            UpdateRemainingPreview();
            UpdateSplitRemaining();
        };
    }

    private async Task LoadCreditNotesAsync(string? customerPhone, string? customerCode)
    {
        if (_creditNotes == null)
            return;

        var notes = await _creditNotes.ListAvailableForCustomerAsync(_storeId, customerCode, customerPhone);
        CreditNoteCombo.ItemsSource = notes;
        if (notes.Count > 0)
            CreditNoteCombo.SelectedIndex = 0;
    }

    private void UpdateModePanels()
    {
        var isCreditNote = CreditNoteRadio.IsChecked == true;
        var isSplit = SplitRadio.IsChecked == true;
        var isSingle = !isCreditNote && !isSplit;

        ReferencePanel.Visibility = isSingle ? Visibility.Visible : Visibility.Collapsed;
        CreditNotePanel.Visibility = isCreditNote ? Visibility.Visible : Visibility.Collapsed;
        SplitPanel.Visibility = isSplit ? Visibility.Visible : Visibility.Collapsed;

        if (CashRadio.IsChecked == true)
            ReferenceLabelText.Text = "Transaction / reference no (optional)";
        else if (UpiRadio.IsChecked == true || CardRadio.IsChecked == true)
            ReferenceLabelText.Text = "Transaction / reference no";
    }

    private void UpdateRemainingPreview()
    {
        if (!TryParseAmount(AmountBox.Text, out var amt))
        {
            RemainingPreviewText.Text = "";
            return;
        }

        var remaining = MoneyMath.RoundDisplayAmount(_balanceDue - amt);
        RemainingPreviewText.Text = remaining <= 0
            ? "This payment will settle the bill."
            : $"Remaining after payment: {MoneyMath.FormatRupee(remaining)}";
    }

    private void UpdateSplitRemaining()
    {
        if (SplitRadio.IsChecked != true)
            return;

        if (!TryParseAmount(AmountBox.Text, out var target))
        {
            SplitRemainingText.Text = "";
            return;
        }

        var allocated = ParseSplitAmount(SplitCashBox.Text)
            + ParseSplitAmount(SplitCardBox.Text)
            + ParseSplitAmount(SplitUpiBox.Text)
            + ParseSplitAmount(SplitCreditNoteAmountBox.Text);
        var remaining = MoneyMath.RoundDisplayAmount(target - allocated);
        SplitRemainingText.Text = MoneyMath.FormatRupee(Math.Max(0m, remaining));
        SplitRemainingText.Foreground = remaining == 0
            ? System.Windows.Media.Brushes.ForestGreen
            : System.Windows.Media.Brushes.OrangeRed;
    }

    private static decimal ParseSplitAmount(string? text) =>
        TryParseAmount(text, out var amt) ? MoneyMath.RoundDisplayAmount(amt) : 0m;

    private static bool TryParseAmount(string? text, out decimal amount)
    {
        amount = 0;
        var trimmed = text?.Trim() ?? "";
        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
            || decimal.TryParse(trimmed, NumberStyles.Number, InCulture, out amount);
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseAmount(AmountBox.Text, out var amt) || amt <= 0)
        {
            MessageBox.Show("Enter a valid payment amount.", "Receipt",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        amt = MoneyMath.RoundDisplayAmount(amt);
        if (amt > _balanceDue + 0.009m)
        {
            MessageBox.Show("Amount cannot exceed balance due.", "Receipt",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_allowPartial && amt + 0.009m < _balanceDue)
        {
            MessageBox.Show("Partial collection is disabled. Enter the full balance.", "Receipt",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IReadOnlyList<CreditPaymentLeg> legs = [];
        string reference = "";

        if (CashRadio.IsChecked == true)
        {
            reference = TransactionNoBox.Text?.Trim() ?? "";
            SelectedPaymentMode = CreditReceivedPaymentMode.Cash;
        }
        else if (UpiRadio.IsChecked == true)
        {
            reference = TransactionNoBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(reference))
            {
                MessageBox.Show("Enter a transaction / reference number.", "Receipt",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedPaymentMode = CreditReceivedPaymentMode.UPI;
        }
        else if (CardRadio.IsChecked == true)
        {
            reference = TransactionNoBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(reference))
            {
                MessageBox.Show("Enter a transaction / reference number.", "Receipt",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedPaymentMode = CreditReceivedPaymentMode.Card;
        }
        else if (CreditNoteRadio.IsChecked == true)
        {
            reference = !string.IsNullOrWhiteSpace(CreditNoteRefBox.Text)
                ? CreditNoteRefBox.Text.Trim()
                : CreditNoteCombo.SelectedValue as string ?? "";
            if (string.IsNullOrWhiteSpace(reference))
            {
                MessageBox.Show("Select or enter a credit note number.", "Receipt",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedPaymentMode = CreditReceivedPaymentMode.CreditNote;
        }
        else
        {
            var cash = ParseSplitAmount(SplitCashBox.Text);
            var card = ParseSplitAmount(SplitCardBox.Text);
            var upi = ParseSplitAmount(SplitUpiBox.Text);
            var cnAmt = ParseSplitAmount(SplitCreditNoteAmountBox.Text);
            var cnRef = SplitCreditNoteRefBox.Text?.Trim() ?? "";
            var total = cash + card + upi + cnAmt;

            if (total != amt)
            {
                MessageBox.Show("Split amounts must exactly equal the amount to collect.", "Receipt",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (cnAmt > 0 && string.IsNullOrWhiteSpace(cnRef))
            {
                MessageBox.Show("Enter a credit note reference for the credit note split amount.", "Receipt",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (cash <= 0 && card <= 0 && upi <= 0 && cnAmt <= 0)
            {
                MessageBox.Show("Enter at least one split amount.", "Receipt",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var splitLegs = new List<CreditPaymentLeg>();
            if (cash > 0)
                splitLegs.Add(new CreditPaymentLeg { Mode = CreditReceivedPaymentMode.Cash, Amount = cash });
            if (card > 0)
                splitLegs.Add(new CreditPaymentLeg { Mode = CreditReceivedPaymentMode.Card, Amount = card });
            if (upi > 0)
                splitLegs.Add(new CreditPaymentLeg { Mode = CreditReceivedPaymentMode.UPI, Amount = upi });
            if (cnAmt > 0)
                splitLegs.Add(new CreditPaymentLeg { Mode = CreditReceivedPaymentMode.CreditNote, Amount = cnAmt, Reference = cnRef });

            legs = splitLegs;
            reference = string.Join(", ", splitLegs.Select(l =>
                string.IsNullOrWhiteSpace(l.Reference) ? l.Mode.ToString() : $"{l.Mode}:{l.Reference}"));
            SelectedPaymentMode = CreditReceivedPaymentMode.Split;
        }

        TransactionNo = reference;
        PaidAmount = amt;
        PaymentLegs = legs;
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
