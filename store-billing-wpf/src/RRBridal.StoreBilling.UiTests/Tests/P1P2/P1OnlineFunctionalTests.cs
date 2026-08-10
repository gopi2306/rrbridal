using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using RRBridal.StoreBilling.UiTests.Pages;
using Xunit;

namespace RRBridal.StoreBilling.UiTests.Tests.P1P2;

[Collection(UiTestCollection.Name)]
[Trait("Category", "UiOnline")]
[Trait("Priority", "P1")]
[Trait("Area", "Expenses")]
public sealed class P1OnlineFunctionalTests : UiTestBase
{
    public P1OnlineFunctionalTests(BillingAppFixture fixture) : base(fixture) { }

    [SkippableFact]
    [Trait("Category", "UiTransactional")]
    public void DailyExpense_EmptySaveShowsVisibleValidationWithoutWrite()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(nameof(DailyExpense_EmptySaveShowsVisibleValidationWithoutWrite), session =>
        {
            SignIn(session);
            Navigate(session, VirtualKeyShort.KEY_E, "daily-expense-page", "Daily Expenses");
            var page = new DailyExpensesPage(session);
            page.Open();

            page.Save();
            var validation = new AppMessagePage(session);
            Assert.Equal("Daily Expense", validation.Title);
            Assert.Contains("valid amount", validation.Body, StringComparison.OrdinalIgnoreCase);
            validation.Close();

            AssertVisibleText(session, "New expense");
            Assert.Empty(session.CentralState.Expenses);
            Assert.DoesNotContain(
                session.CentralState.Requests,
                request => request.Method == "POST"
                           && request.PathAndQuery.StartsWith("/api/store-pos/daily-expenses", StringComparison.Ordinal));
        });
    }

    [SkippableFact]
    [Trait("Area", "Masters")]
    public void CustomerSalesmanAndQuotation_ActionsShowSeededOrEmptyResults()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(nameof(CustomerSalesmanAndQuotation_ActionsShowSeededOrEmptyResults), session =>
        {
            SignIn(session);

            Navigate(session, VirtualKeyShort.KEY_U, "customers-page", "Customers");
            var customers = new CustomersPage(session);
            customers.Open();
            customers.Search();
            AssertVisibleText(session, "UI Test Customer");

            Navigate(session, VirtualKeyShort.KEY_M, "salesmen-page", "Salesman");
            var salesmen = new SalesmenPage(session);
            salesmen.Open();
            salesmen.Search();
            AssertVisibleText(session, "UI Test Salesman");
            salesmen.NewSalesman();
            AssertVisibleText(session, "Name *");
            AssertVisibleText(session, "Cancel");

            Navigate(session, VirtualKeyShort.KEY_Q, "quotations-page", "Quotations");
            var quotations = new QuotationsPage(session);
            quotations.Open();
            quotations.Search();
            AssertVisibleText(session, "No quotations found.");
        });
    }

    [SkippableFact]
    [Trait("Area", "Returns")]
    public void SaleReturn_SourceToggleShowsVisibleLegacyValidationFields()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(nameof(SaleReturn_SourceToggleShowsVisibleLegacyValidationFields), session =>
        {
            SignIn(session);
            Navigate(session, VirtualKeyShort.KEY_R, "sale-return-page", "Returns");
            var page = new SaleReturnsPage(session);
            page.Open();
            page.UseLegacyInvoice();

            AssertVisibleText(session, "Reference invoice no");
            AssertVisibleText(session, "Start return");
            AssertVisibleText(session, "Enter pre-system invoice details");
        });
    }

    internal static void SignIn(BillingAppSession session)
    {
        session.Login.SignIn(FakeCentralServer.ValidEmail, FakeCentralServer.ValidPassword);
        Assert.True(session.WaitForShell().IsDisplayed());
    }

    internal static void Navigate(
        BillingAppSession session,
        VirtualKeyShort key,
        string automationId,
        string label)
    {
        KeyboardHelper.PressControlShortcut(key);
        session.Shell.WaitForPage(automationId, label);
    }

    internal static AutomationElement AssertVisibleText(BillingAppSession session, string text) =>
        Wait.UntilNotNull(
            () => session.Window
                .FindAllDescendants()
                .FirstOrDefault(element =>
                    !element.IsOffscreen
                    && (element.Name ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)),
            UiTestEnvironment.ActionTimeout,
            $"visible UI text containing '{text}'");
}
