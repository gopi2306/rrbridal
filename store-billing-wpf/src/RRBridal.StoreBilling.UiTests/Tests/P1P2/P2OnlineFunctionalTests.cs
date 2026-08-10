using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using RRBridal.StoreBilling.UiTests.Pages;
using Xunit;

namespace RRBridal.StoreBilling.UiTests.Tests.P1P2;

[Collection(UiTestCollection.Name)]
[Trait("Category", "UiOnline")]
[Trait("Priority", "P2")]
[Trait("Area", "Operations")]
public sealed class P2OnlineFunctionalTests : UiTestBase
{
    public P2OnlineFunctionalTests(BillingAppFixture fixture) : base(fixture) { }

    [SkippableFact]
    [Trait("Area", "CreditAndOnlineSales")]
    public void CreditOnlineSalesAndDashboard_ActionsShowVisibleEmptyState()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(nameof(CreditOnlineSalesAndDashboard_ActionsShowVisibleEmptyState), session =>
        {
            P1OnlineFunctionalTests.SignIn(session);

            P1OnlineFunctionalTests.Navigate(session, VirtualKeyShort.KEY_I, "credit-bills-page", "Credit Bills");
            var credit = new CreditBillsPage(session);
            credit.Open();
            credit.Search();
            P1OnlineFunctionalTests.AssertVisibleText(session, "No credit bills found.");
            P1OnlineFunctionalTests.AssertVisibleText(session, "Pending balance");

            P1OnlineFunctionalTests.Navigate(session, VirtualKeyShort.KEY_O, "online-sales-page", "Online Sales");
            var online = new OnlineSalesPage(session);
            online.Open();
            online.Search();
            P1OnlineFunctionalTests.AssertVisibleText(session, "No online COD orders found.");
            P1OnlineFunctionalTests.AssertVisibleText(session, "Balance till");

            P1OnlineFunctionalTests.Navigate(session, VirtualKeyShort.KEY_D, "dashboard-page", "Dashboard");
            var dashboard = new DashboardPage(session);
            dashboard.Open();
            dashboard.RefreshMetrics();
            P1OnlineFunctionalTests.AssertVisibleText(session, "Dashboard");
            P1OnlineFunctionalTests.AssertVisibleText(session, "Refresh metrics");
        });
    }

    [SkippableFact]
    [Trait("Area", "BillLookup")]
    public void BillLookup_EmptySearchShowsVisibleValidationAndCancelsCleanly()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(nameof(BillLookup_EmptySearchShowsVisibleValidationAndCancelsCleanly), session =>
        {
            P1OnlineFunctionalTests.SignIn(session);
            P1OnlineFunctionalTests.Navigate(session, VirtualKeyShort.KEY_K, "bill-lookup-page", "Bill Lookup");
            var lookup = new BillLookupPage(session);
            lookup.Open();
            lookup.Search();

            var validation = new AppMessagePage(session);
            Assert.Equal("Bill Lookup", validation.Title);
            Assert.Contains("at least one search field", validation.Body, StringComparison.OrdinalIgnoreCase);
            validation.Close();

            P1OnlineFunctionalTests.AssertVisibleText(session, "Find a posted bill");
        });
    }

    [SkippableFact]
    [Trait("Area", "ReportingAndBarcode")]
    public void AnalyticsLedgerAndBarcode_ShowVisibleContentAndBarcodeValidation()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(nameof(AnalyticsLedgerAndBarcode_ShowVisibleContentAndBarcodeValidation), session =>
        {
            P1OnlineFunctionalTests.SignIn(session);

            P1OnlineFunctionalTests.Navigate(session, VirtualKeyShort.KEY_Y, "analytics-page", "Analytics");
            P1OnlineFunctionalTests.AssertVisibleText(session, "Bills in period");
            P1OnlineFunctionalTests.AssertVisibleText(session, "Revenue in period");

            P1OnlineFunctionalTests.Navigate(session, VirtualKeyShort.KEY_L, "ledger-page", "Ledger");
            P1OnlineFunctionalTests.AssertVisibleText(session, "Posted bills");
            P1OnlineFunctionalTests.AssertVisibleText(session, "Payments");

            P1OnlineFunctionalTests.Navigate(session, VirtualKeyShort.KEY_B, "barcodes-page", "Barcodes");
            P1OnlineFunctionalTests.AssertVisibleText(session, "Barcode printing");
            P1OnlineFunctionalTests.AssertVisibleText(session, "Print (F5)").AsButton().Click();

            var validation = new AppMessagePage(session);
            Assert.Equal("Barcode printing", validation.Title);
            Assert.Contains("at least one line", validation.Body, StringComparison.OrdinalIgnoreCase);
            validation.Close();
        });
    }

    [SkippableFact]
    [Trait("Area", "Settings")]
    public void Settings_AccessShowsVisibleSections()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(nameof(Settings_AccessShowsVisibleSections), session =>
        {
            P1OnlineFunctionalTests.SignIn(session);
            P1OnlineFunctionalTests.Navigate(session, VirtualKeyShort.OEM_COMMA, "settings-page", "Settings");
            var settings = new SettingsPage(session);
            settings.Open();
            P1OnlineFunctionalTests.AssertVisibleText(session, "Sync, printing, WhatsApp, appearance");
            P1OnlineFunctionalTests.AssertVisibleText(session, "1. Sync");
            P1OnlineFunctionalTests.AssertVisibleText(session, "4. App & billing");
        });
    }
}
