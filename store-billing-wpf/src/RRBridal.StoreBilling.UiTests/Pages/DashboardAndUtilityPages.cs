using FlaUI.Core.AutomationElements;

namespace RRBridal.StoreBilling.UiTests.Pages;

internal sealed class DashboardPage : StandardPage
{
    public DashboardPage(BillingAppSession session) : base(session, "dashboard-page", "Dashboard") { }
    public void RefreshMetrics() => ClickText("Refresh metrics");
    public void SearchInventory() => ClickText("Search inventory");
    public void OpenOnlineSales() => ClickText("Online sales");
    public void OpenCreditBills() => ClickText("Credit bills");
    public void AdjustSelectedInventory() => ClickText("Adjust");
    public void ImportPhysicalCounts() => ClickText("Import physical counts");
}

internal sealed class InventoryAdjustmentPage : ModalPageObject
{
    public InventoryAdjustmentPage(BillingAppSession session) : base(session, "inventory-adjust-stock-dialog") { }
    public string Sku => TextBoxById("inventory-adjust-stock-sku-input").Text;
    public string CurrentQuantity => TextBoxById("inventory-adjust-stock-current-qty-input").Text;
    public void SelectMode(string mode) => ComboBoxById("inventory-adjust-stock-mode-selector").Select(mode);
    public void SetQuantity(string quantity) => ReplaceText("inventory-adjust-stock-quantity-input", quantity);
    public void SetReason(string reason) => ReplaceText("inventory-adjust-stock-reason-input", reason);
    public void Apply() => ClickId("inventory-adjust-stock-apply-button");
    public void Cancel() => ClickId("inventory-adjust-stock-cancel-button");
}

internal sealed class NotificationsPage : ModalPageObject
{
    public NotificationsPage(BillingAppSession session) : base(session, "notifications-dialog") { }
    public AutomationElement Items => FindVisibleById("notifications-items-grid");
    public void Refresh() => ClickId("notifications-refresh-button");
    public void SyncNow() => ClickId("notifications-sync-now-button");
    public void Close() => ClickId("notifications-close-button");
}

internal sealed class InvoicePrintPreviewPage : ModalPageObject
{
    public InvoicePrintPreviewPage(BillingAppSession session) : base(session, "invoice-print-preview-window") { }
    public void Print() => ClickId("invoice-print-preview-print-button");
    public void Close() => ClickId("invoice-print-preview-close-button");
}

internal sealed class BarcodePrintPreviewPage : ModalPageObject
{
    public BarcodePrintPreviewPage(BillingAppSession session) : base(session, "barcode-label-print-preview-window") { }
    public void SelectPrinter(string printer) => ComboBoxById("barcode-label-print-preview-printer-selector").Select(printer);
    public void Print() => ClickId("barcode-label-print-preview-print-button");
    public void SaveLabelFile() => ClickId("barcode-label-print-preview-save-label-file-button");
    public void Close() => ClickId("barcode-label-print-preview-close-button");
}
