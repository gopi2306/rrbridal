using System.Windows;
using System.Windows.Input;

namespace RRBridal.StoreBilling.App.Views;

public enum CreditNoteVoucherPath
{
    BillReturn,
    DirectCustomer,
}

public partial class CreditNoteChooserDialog
{
    public CreditNoteVoucherPath? SelectedPath { get; private set; }

    public CreditNoteChooserDialog()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => BillReturnButton.Focus();
    }

    public static bool TryShow(Window owner, out CreditNoteVoucherPath path)
    {
        path = CreditNoteVoucherPath.BillReturn;
        var dialog = new CreditNoteChooserDialog { Owner = owner };
        if (dialog.ShowDialog() != true || dialog.SelectedPath == null)
            return false;

        path = dialog.SelectedPath.Value;
        return true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.D1 or Key.NumPad1)
        {
            Select(CreditNoteVoucherPath.BillReturn);
            e.Handled = true;
        }
        else if (key is Key.D2 or Key.NumPad2)
        {
            Select(CreditNoteVoucherPath.DirectCustomer);
            e.Handled = true;
        }
    }

    private void BillReturn_OnClick(object sender, RoutedEventArgs e) =>
        Select(CreditNoteVoucherPath.BillReturn);

    private void Direct_OnClick(object sender, RoutedEventArgs e) =>
        Select(CreditNoteVoucherPath.DirectCustomer);

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Select(CreditNoteVoucherPath path)
    {
        SelectedPath = path;
        DialogResult = true;
        Close();
    }
}
