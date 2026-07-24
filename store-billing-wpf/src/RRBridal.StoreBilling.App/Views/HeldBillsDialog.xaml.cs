using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Views;

public partial class HeldBillsDialog
{
    public HeldBillRow? SelectedRow { get; private set; }

    public bool ResumeRequested { get; private set; }

    public bool DeleteRequested { get; private set; }

    public HeldBillsDialog(IReadOnlyList<HeldBillRow> rows)
    {
        InitializeComponent();
        BillsGrid.ItemsSource = rows;
        StatusText.Text = rows.Count == 0 ? "No held bills." : $"{rows.Count} held bill(s).";
        if (rows.Count > 0)
            BillsGrid.SelectedIndex = 0;
    }

    private void BillsGrid_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SelectedRow = BillsGrid.SelectedItem as HeldBillRow;
    }

    private void Resume_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow == null)
        {
            AppDialog.Show("Select a held bill first.", "Held bills", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ResumeRequested = true;
        DialogResult = true;
        Close();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow == null)
        {
            AppDialog.Show("Select a held bill first.", "Held bills", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (AppDialog.Show($"Delete held bill {SelectedRow.HoldNo}?", "Held bills", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        DeleteRequested = true;
        DialogResult = true;
        Close();
    }
}
