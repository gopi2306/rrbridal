using System.Globalization;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using RRBridal.StoreBilling.App.Services.Inventory;

namespace RRBridal.StoreBilling.App.Views;

public partial class InventoryAdjustStockDialog
{
    private static readonly CultureInfo ParseCulture = CultureInfo.InvariantCulture;

    public InventoryAdjustmentMode SelectedMode { get; private set; } = InventoryAdjustmentMode.Add;

    public decimal Quantity { get; private set; }

    public string Reason { get; private set; } = "";

    public InventoryAdjustStockDialog(InventoryGridRow row)
    {
        InitializeComponent();
        SkuBox.Text = row.Sku;
        ProductBox.Text = row.Product;
        CurrentQtyBox.Text = row.StoreQty.ToString("N2", ParseCulture);
    }

    public static bool TryShow(Window owner, InventoryGridRow row, out InventoryAdjustmentMode mode, out decimal quantity, out string reason)
    {
        mode = InventoryAdjustmentMode.Add;
        quantity = 0;
        reason = "";

        var dialog = new InventoryAdjustStockDialog(row) { Owner = owner };
        if (dialog.ShowDialog() != true)
            return false;

        mode = dialog.SelectedMode;
        quantity = dialog.Quantity;
        reason = dialog.Reason;
        return true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Apply_OnClick(object sender, RoutedEventArgs e)
    {
        SelectedMode = ModeBox.SelectedIndex switch
        {
            1 => InventoryAdjustmentMode.Remove,
            2 => InventoryAdjustmentMode.SetTo,
            _ => InventoryAdjustmentMode.Add,
        };

        if (!decimal.TryParse(QuantityBox.Text?.Trim(), NumberStyles.Number, ParseCulture, out var qty) || qty < 0)
        {
            AppDialog.Show(this, "Enter a valid quantity.", "Adjust stock", MessageBoxButton.OK, MessageBoxImage.Warning);
            QuantityBox.Focus();
            return;
        }

        if (SelectedMode is InventoryAdjustmentMode.Add or InventoryAdjustmentMode.Remove && qty <= 0)
        {
            AppDialog.Show(this, "Quantity must be greater than zero.", "Adjust stock", MessageBoxButton.OK, MessageBoxImage.Warning);
            QuantityBox.Focus();
            return;
        }

        var trimmedReason = ReasonBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            AppDialog.Show(this, "Reason is required.", "Adjust stock", MessageBoxButton.OK, MessageBoxImage.Warning);
            ReasonBox.Focus();
            return;
        }

        if (!decimal.TryParse(CurrentQtyBox.Text?.Trim(), NumberStyles.Number, ParseCulture, out var currentQty))
            currentQty = 0;

        var delta = SelectedMode switch
        {
            InventoryAdjustmentMode.Add => qty,
            InventoryAdjustmentMode.Remove => -qty,
            InventoryAdjustmentMode.SetTo => qty - currentQty,
            _ => 0m,
        };

        if (delta == 0)
        {
            AppDialog.Show(this, "No change in quantity.", "Adjust stock", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (currentQty + delta < 0)
        {
            AppDialog.Show(this, "Resulting quantity cannot be negative.", "Adjust stock", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Quantity = qty;
        Reason = trimmedReason;
        DialogResult = true;
        Close();
    }
}
