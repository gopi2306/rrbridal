using System.Windows;
using System.Windows.Input;

namespace RRBridal.StoreBilling.App.Views;

public partial class AddProductCodeDialog : Window
{
    public string ProductCode { get; private set; } = "";

    public AddProductCodeDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ProductCodeBox.Focus();
        };
    }

    private void ProductCodeBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        TryConfirm();
    }

    private void Add_OnClick(object sender, RoutedEventArgs e) => TryConfirm();

    private void TryConfirm()
    {
        var code = (ProductCodeBox.Text ?? "").Trim();
        if (string.IsNullOrEmpty(code))
        {
            MessageBox.Show("Enter a product code.", "Add product",
                MessageBoxButton.OK, MessageBoxImage.Information);
            ProductCodeBox.Focus();
            return;
        }

        ProductCode = code;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
