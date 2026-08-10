using RRBridal.StoreBilling.UiTests.Pages;
using RRBridal.StoreBilling.UiTests.Workflows;
using Xunit;

namespace RRBridal.StoreBilling.UiTests.Tests.P0;

[Collection(UiTestCollection.Name)]
[Trait("Category", "UiOnline")]
[Trait("Priority", "P0")]
public sealed class P0OnlineFunctionalTests : UiTestBase
{
    public P0OnlineFunctionalTests(BillingAppFixture fixture) : base(fixture) { }

    [SkippableFact]
    [Trait("Area", "Authentication")]
    public void ValidLogin_ShowsOnlineOpenDay_AndSupportsLogoutRelogin()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(nameof(ValidLogin_ShowsOnlineOpenDay_AndSupportsLogoutRelogin), session =>
        {
            P0UiTestSteps.SignIn(session);

            P0UiTestSteps.WaitForVisibleText(session, "Central: Online");
            P0UiTestSteps.WaitForVisibleText(session, "Day: Open");
            Assert.Contains(
                session.CentralState.Requests,
                request => request.PathAndQuery.StartsWith(
                    "/api/store-pos/day-sessions/current",
                    StringComparison.Ordinal));

            P0UiTestSteps.LogoutAndRelogin(session);
            P0UiTestSteps.WaitForVisibleText(session, "Central: Online");
        });
    }

    [SkippableFact]
    [Trait("Category", "UiTransactional")]
    [Trait("Area", "Billing")]
    public void HoldResumeAndCashPost_PersistsBillInOnlineFakeCentral()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(nameof(HoldResumeAndCashPost_PersistsBillInOnlineFakeCentral), session =>
        {
            P0UiTestSteps.SignIn(session);
            var billing = P0UiTestSteps.PrepareCashBill(session);

            billing.HoldCurrent();
            Thread.Sleep(250);
            if (!session.Application.GetAllTopLevelWindows(session.Automation).Any(window =>
                    string.Equals(window.AutomationId, "app-message-dialog", StringComparison.Ordinal)
                    || window.FindFirstDescendant(cf => cf.ByAutomationId("app-message-dialog")) is not null))
            {
                P0UiTestSteps.InvokeVisibleButton(session, "F8 / Ctrl+H Hold");
            }
            var heldMessage = new AppMessagePage(session);
            Assert.Contains("saved", heldMessage.Body, StringComparison.OrdinalIgnoreCase);
            heldMessage.Choose("OK");

            billing.ResumeHeld();
            Thread.Sleep(250);
            if (!session.Application.GetAllTopLevelWindows(session.Automation).Any(window =>
                    string.Equals(window.AutomationId, "held-bills-dialog", StringComparison.Ordinal)
                    || window.FindFirstDescendant(cf => cf.ByAutomationId("held-bills-dialog")) is not null))
            {
                P0UiTestSteps.InvokeVisibleButtonById(session, "billing-resume-held-button");
            }
            var held = new HeldBillsPage(session);
            held.Select("UI Test Customer");
            held.Resume();
            session.Shell.WaitForPage("billing-view", "Billing");
            var resumed = new BillingPage(session);
            P0UiTestSteps.WaitForVisibleText(session, "UI-SAREE-001");
            resumed.SetPrintInvoice(false);

            new BillingWorkflow(session).PostCashBill(
                _ => P0UiTestSteps.InvokeVisibleButtonById(session, "main-post-bill-button"),
                "5000");

            Wait.Until(
                () => session.CentralState.Bills.Count == 1,
                UiTestEnvironment.ActionTimeout,
                "the posted bill in fake central state");
            var posted = Assert.Single(session.CentralState.Bills.Values);
            Assert.Contains(
                "UI Test Customer",
                posted.GetProperty("customerName").GetString() ?? "",
                StringComparison.Ordinal);
            Assert.Contains(
                posted.GetProperty("lines").EnumerateArray(),
                line => string.Equals(
                    line.GetProperty("sku").GetString(),
                    "UI-SAREE-001",
                    StringComparison.Ordinal));
            Assert.Contains(
                session.CentralState.Requests,
                request => request.Method == "POST"
                           && request.PathAndQuery == "/api/store-pos/bills");
            Assert.Empty(session.CentralState.HeldBills);
        });
    }
}
