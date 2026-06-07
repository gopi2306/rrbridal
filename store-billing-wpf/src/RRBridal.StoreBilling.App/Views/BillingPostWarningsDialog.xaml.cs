using System.Collections.Generic;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Views;

public partial class BillingPostWarningsDialog
{
    public bool PostAnyway { get; private set; }

    public BillingPostWarningsDialog(IReadOnlyList<StockShortLine> shortLines)
    {
        InitializeComponent();
        ShortGrid.ItemsSource = shortLines;
    }

    private void PostAnyway_Click(object sender, RoutedEventArgs e)
    {
        PostAnyway = true;
        DialogResult = true;
        Close();
    }
}
