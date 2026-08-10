using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace RRBridal.StoreBilling.UiTests.Pages;

internal abstract class StandardPage : ShellPageObject
{
    protected StandardPage(BillingAppSession session, string pageId, string navigationLabel)
        : base(session, pageId, navigationLabel) { }

    public AutomationElement FirstGrid => FindVisibleByControlType(ControlType.DataGrid, "page data grid");
    public void ClickButton(string label) => ClickText(label);
    public void SetField(string automationName, string value) => ReplaceNamedText(automationName, value);
}

internal sealed class CustomersPage : StandardPage
{
    public CustomersPage(BillingAppSession session) : base(session, "customers-page", "Customers") { }
    public void NewCustomer() => ClickText("New customer");
    public void Save() => ClickText("Save (F4)");
    public void UseInBilling() => ClickText("Use in billing");
    public void Search() => ClickText("Search");
}

internal sealed class SalesmenPage : StandardPage
{
    public SalesmenPage(BillingAppSession session) : base(session, "salesmen-page", "Salesman") { }
    public void NewSalesman() => ClickText("New salesman");
    public void Save() => ClickText("Save");
    public void Search() => ClickText("Search");
}

internal sealed class QuotationsPage : StandardPage
{
    public QuotationsPage(BillingAppSession session) : base(session, "quotations-page", "Quotations") { }
    public void Create() => ClickText("+ Create quotation");
    public void Search() => ClickText("Search");
    public void OpenSelected() => ClickText("Open");
    public void ConvertSelectedToBilling() => ClickText("Convert to billing");
    public void CancelSelected() => ClickText("Cancel");
}

internal sealed class SaleReturnsPage : StandardPage
{
    public SaleReturnsPage(BillingAppSession session) : base(session, "sale-return-page", "Returns") { }
    public void UseSystemBill() => FindVisibleById("sale-return-source-system").Click();
    public void UseLegacyInvoice() => FindVisibleById("sale-return-source-legacy").Click();
    public void SearchBill(string billNumber)
    {
        ReplaceText("sale-return-bill-number", billNumber);
        ClickId("sale-return-search");
    }
    public void SelectResult(string billNumber) => SelectRowContaining("sale-return-search-results", billNumber);
    public void LoadBill() => ClickId("sale-return-load-bill");
    public void Post() => ClickText("Post Return / Exchange");
}

internal sealed class CreditBillsPage : StandardPage
{
    public CreditBillsPage(BillingAppSession session) : base(session, "credit-bills-page", "Credit Bills") { }
    public void Search() => ClickText("Search");
    public void RecordPayment() => ClickText("Record payment");
}

internal sealed class OnlineSalesPage : StandardPage
{
    public OnlineSalesPage(BillingAppSession session) : base(session, "online-sales-page", "Online Sales") { }
    public void Search() => ClickText("Search");
    public void Refresh() => ClickText("Refresh");
    public void RecordPayment() => ClickText("Record payment");
}

internal sealed class BillLookupPage : StandardPage
{
    public BillLookupPage(BillingAppSession session) : base(session, "bill-lookup-page", "Bill Lookup") { }
    public void Search() => ClickText("Search");
    public void OpenSelected() => ClickText("Open bill");
    public void ViewBill() => ClickText("View Bill");
    public void AddReturn() => ClickText("Add Return");
    public void AddAdjustment() => ClickText("Add Adjustment");
    public void DuplicatePrint() => ClickText("Duplicate Print");
}

internal sealed class SettingsPage : StandardPage
{
    public SettingsPage(BillingAppSession session) : base(session, "settings-page", "Settings") { }
    public void SelectSection(string header) => FindVisibleByText(header).Click();
    public void SaveBillingSettings() => ClickText("Save billing settings");
    public void RunSync() => ClickText("Run sync");
}
