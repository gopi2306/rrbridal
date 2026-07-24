using System.IO;
using System.Printing;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using System.Windows.Controls;
using Microsoft.Win32;
using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services.BarcodePrinting;
using RRBridal.StoreBilling.App.Views.Controls;

namespace RRBridal.StoreBilling.App.Views;

public partial class BarcodeLabelPrintPreviewWindow
{
    private readonly IReadOnlyList<BarcodePrintLineItem> _lines;
    private readonly string _companyName;
    private readonly BarcodeLabelDesignConfig _design;
    private readonly BarcodePrintService _printService;
    private readonly IReadOnlyList<BarcodeLabelLayout> _layouts;

    public bool PrintSucceeded { get; private set; }

    public string? LastResultMessage { get; private set; }

    public BarcodeLabelPrintPreviewWindow(
        IReadOnlyList<BarcodePrintLineItem> lines,
        string companyName,
        BarcodeLabelDesignStore designStore)
    {
        InitializeComponent();
        _lines = lines;
        _companyName = companyName;
        _design = designStore.ResolveDesign();
        _printService = new BarcodePrintService(designStore);
        _layouts = BarcodeLabelLayout.FromLines(lines, companyName, _design);

        BuildPreviewCards();
        LoadPrinters();
        UpdateSummary();
    }

    private void BuildPreviewCards()
    {
        LabelsPanel.Children.Clear();
        foreach (var layout in _layouts)
        {
            var card = new BarcodeLabelPreviewControl();
            card.ApplyLayout(layout);
            LabelsPanel.Children.Add(card);
        }
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

        UpdatePrinterWarning();
        UpdateSummary();
    }

    private void PrinterCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePrinterWarning();
        UpdateSummary();
    }

    private void UpdatePrinterWarning()
    {
        if (PrinterCombo.SelectedItem is string name && BarcodePrinterFilter.IsVirtualOrPdfQueue(name))
        {
            PrinterWarningText.Text = BarcodePrinterFilter.VirtualPrinterWarning;
            PrinterWarningText.Visibility = Visibility.Visible;
            PrintButton.IsEnabled = false;
        }
        else
        {
            PrinterWarningText.Visibility = Visibility.Collapsed;
            PrintButton.IsEnabled = PrinterCombo.Items.Count > 0;
        }
    }

    private void SaveEpl_OnClick(object sender, RoutedEventArgs e)
    {
        var printer = PrinterCombo.SelectedItem as string;
        var language = BarcodePrinterPreferences.ResolveLanguage(printer);
        var payload = BarcodeLabelCommandBuilder.BuildBatch(_lines, _companyName, language, _design);
        var ext = language == BarcodePrinterLanguage.Tspl ? "tspl" : "epl";
        var dlg = new SaveFileDialog
        {
            Title = "Save label printer commands",
            Filter = $"Label commands (*.{ext})|*.{ext}|Text files (*.txt)|*.txt",
            DefaultExt = ext,
            FileName = $"RR-Bridal-barcode-labels.{ext}",
        };
        if (dlg.ShowDialog() != true)
            return;

        File.WriteAllText(dlg.FileName, payload);
        AppDialog.Show(
            $"Saved {BarcodePrinterPreferences.LanguageHint(language)} commands to:\n{dlg.FileName}",
            "Barcode printing",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void UpdateSummary()
    {
        var total = _layouts.Sum(l => l.CopyCount);
        var printer = PrinterCombo.SelectedItem as string;
        var lang = printer != null
            ? BarcodePrinterPreferences.LanguageHint(BarcodePrinterPreferences.ResolveLanguage(printer))
            : BarcodePrinterPreferences.LanguageHint(BarcodePrinterLanguage.Tspl);
        SummaryText.Text = $"{_layouts.Count} SKU(s) · {total} label(s) · {_design.Name} ({lang})";
    }

    private void Print_OnClick(object sender, RoutedEventArgs e)
    {
        if (PrinterCombo.SelectedItem is not string printer || string.IsNullOrWhiteSpace(printer))
        {
            AppDialog.Show("Select a printer.", "Barcode printing", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (BarcodePrinterFilter.IsVirtualOrPdfQueue(printer))
        {
            AppDialog.Show(
                BarcodePrinterFilter.VirtualPrinterWarning,
                "Barcode printing",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var (ok, message) = _printService.PrintLabels(_lines, _companyName, printer);
        LastResultMessage = message;
        PrintSucceeded = ok;

        ResultText.Text = message;
        ResultBanner.Visibility = Visibility.Visible;
        PrintButton.Content = ok ? "Print again" : "Retry print";

        if (!ok)
        {
            AppDialog.Show(message, "Barcode printing — error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Title = $"Barcode labels printed — {_design.Name}";
    }
}
