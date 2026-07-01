using System.Windows;

namespace RRBridal.StoreBilling.App.Views;

public partial class BillPostConfirmDialog
{
    public BillPostConfirmDialog(string customerName, string payableFormatted, int itemCount, bool onlineCodOrder)
    {
        InitializeComponent();
        CustomerText.Text = string.IsNullOrWhiteSpace(customerName) ? "—" : customerName.Trim();
        ItemCountText.Text = itemCount == 1 ? "1 item" : $"{itemCount} items";
        PayableText.Text = payableFormatted;
        if (onlineCodOrder)
            OnlineCodNote.Visibility = Visibility.Visible;
    }

    private void PostBill_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
