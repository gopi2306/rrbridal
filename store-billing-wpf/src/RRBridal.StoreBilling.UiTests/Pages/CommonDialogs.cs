using FlaUI.Core.AutomationElements;

namespace RRBridal.StoreBilling.UiTests.Pages;

internal sealed class AppMessagePage : ModalPageObject
{
    public AppMessagePage(BillingAppSession session) : base(session, "app-message-dialog") { }
    public string Title => FindVisibleById("app-message-title-text").Name;
    public string Body => FindVisibleById("app-message-body-text").Name;
    public void Choose(string buttonText) => ClickText(buttonText);
    public void Close() => ClickId("app-message-close-button");
}

internal sealed class CustomerSearchPage : ModalPageObject
{
    public CustomerSearchPage(BillingAppSession session) : base(session, "customer-search-dialog") { }
    public void Search(string query)
    {
        ReplaceText("customer-search-search-input", query);
        ClickId("customer-search-search-button");
    }
    public void SelectResult(string text)
    {
        SelectRowContaining("customer-search-results-list", text);
        ClickId("customer-search-select-button");
    }
    public void RegisterNew() => ClickId("customer-search-register-new-customer-button");
    public void Cancel() => ClickId("customer-search-cancel-button");
}

internal sealed class ProductSearchPage : ModalPageObject
{
    public ProductSearchPage(BillingAppSession session) : base(session, "product-search-dialog") { }
    public void Search(string query)
    {
        ReplaceText("product-search-search-query-input", query);
        ClickId("product-search-search-button");
    }
    public void AddResult(string text)
    {
        SelectRowContaining("product-search-results-list", text);
        ClickId("product-search-add-to-bill-button");
    }
    public void Cancel() => ClickId("product-search-cancel-button");
}

internal sealed class BillPickPage : ModalPageObject
{
    public BillPickPage(BillingAppSession session) : base(session, "bill-pick-dialog") { }
    public void SelectBill(string billNumber)
    {
        SelectRowContaining("bill-pick-results-list", billNumber);
        ClickId("bill-pick-select-button");
    }
    public void Cancel() => ClickId("bill-pick-cancel-button");
}

internal sealed class BillingPostWarningsPage : ModalPageObject
{
    public BillingPostWarningsPage(BillingAppSession session) : base(session, "billing-post-warnings-dialog") { }
    public AutomationElement ShortStockGrid => FindVisibleById("billing-post-warnings-short-grid");
    public void PostAnyway() => ClickId("billing-post-warnings-post-anyway-button");
    public void Cancel() => ClickId("billing-post-warnings-cancel-button");
}

internal sealed class BillDetailPage : ModalPageObject
{
    public BillDetailPage(BillingAppSession session) : base(session, "bill-detail-dialog") { }
    public AutomationElement Lines => FindVisibleById("bill-detail-lines-grid");
    public void OpenInReturns() => ClickId("bill-detail-open-in-returns-button");
    public void OpenInAdjustments() => ClickId("bill-detail-open-in-adjustments-button");
    public void Close() => ClickId("bill-detail-close-button");
}

internal sealed class RecordCodPaymentPage : ModalPageObject
{
    public RecordCodPaymentPage(BillingAppSession session) : base(session, "record-cod-payment-dialog") { }
    public void Confirm(string transactionNumber)
    {
        ReplaceText("record-cod-payment-transaction-no-input", transactionNumber);
        ClickId("record-cod-payment-confirm-button");
    }
    public void Cancel() => ClickId("record-cod-payment-cancel-button");
}
