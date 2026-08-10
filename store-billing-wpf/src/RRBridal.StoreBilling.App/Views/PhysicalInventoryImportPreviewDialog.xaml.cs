using System.Linq;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Inventory;

namespace RRBridal.StoreBilling.App.Views;

public partial class PhysicalInventoryImportPreviewDialog : Window
{
    private readonly PhysicalInventoryImportPreview _preview;

    private PhysicalInventoryImportPreviewDialog(PhysicalInventoryImportPreview preview)
    {
        InitializeComponent();
        _preview = preview;
        PreviewGrid.ItemsSource = preview.Lines;
        SummaryText.Text =
            $"{preview.Adjusted} correction(s), {preview.Skipped} unchanged row(s), " +
            $"{preview.Errors.Count} error(s).";
        ErrorsText.Text = preview.Errors.Count == 0
            ? ""
            : string.Join(
                "\n",
                preview.Errors.Take(5).Select(error =>
                    error.RowNumber > 0
                        ? $"Row {error.RowNumber} {error.Sku}: {error.Message}"
                        : error.Message));
        ApplyButton.IsEnabled = preview.CanCommit;
    }

    public string Reason => ReasonBox.Text.Trim();

    public static bool TryShow(
        Window owner,
        PhysicalInventoryImportPreview preview,
        out string reason)
    {
        var dialog = new PhysicalInventoryImportPreviewDialog(preview) { Owner = owner };
        var accepted = dialog.ShowDialog() == true;
        reason = accepted ? dialog.Reason : "";
        return accepted;
    }

    private void Apply_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_preview.CanCommit)
            return;
        if (string.IsNullOrWhiteSpace(ReasonBox.Text))
        {
            MessageBox.Show(this, "Reason is required.", "Physical inventory", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
