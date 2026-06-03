using System.Printing;
using System.Windows;
using RRBridal.StoreBilling.App.Services.BarcodePrinting;

namespace RRBridal.StoreBilling.App.Views;

public partial class BarcodePrinterPickerDialog
{
    public string? SelectedPrinterFullName { get; private set; }

    public BarcodePrinterPickerDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadPrinters();
    }

    private void LoadPrinters()
    {
        PrinterCombo.Items.Clear();
        var physical = new List<string>();

        try
        {
            using var server = new LocalPrintServer();
            foreach (PrintQueue pq in server.GetPrintQueues())
            {
                var name = pq.FullName;
                if (BarcodePrinterFilter.IsVirtualOrPdfQueue(name))
                    continue;
                physical.Add(name);
            }
        }
        catch
        {
            // leave empty
        }

        foreach (var name in physical.OrderByDescending(BarcodePrinterPreferences.ScoreQueue))
            PrinterCombo.Items.Add(name);

        var preferred = BarcodePrinterPreferences.PickPreferredQueue(physical);
        if (preferred != null)
            PrinterCombo.SelectedItem = preferred;
    }

    private void Print_OnClick(object sender, RoutedEventArgs e)
    {
        if (PrinterCombo.SelectedItem is not string name || string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Select a printer.", "Barcode printing", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedPrinterFullName = name;
        DialogResult = true;
        Close();
    }
}
