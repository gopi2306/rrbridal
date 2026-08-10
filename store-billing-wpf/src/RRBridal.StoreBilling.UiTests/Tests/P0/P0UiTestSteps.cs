using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using RRBridal.StoreBilling.UiTests.Pages;
using Xunit;

namespace RRBridal.StoreBilling.UiTests.Tests.P0;

internal static class P0UiTestSteps
{
    internal static void SignIn(BillingAppSession session)
    {
        session.Login.SignIn(FakeCentralServer.ValidEmail, FakeCentralServer.ValidPassword);
        Assert.True(session.WaitForShell().IsDisplayed(), "Expected valid credentials to open the billing shell.");
    }

    internal static BillingPage PrepareCashBill(BillingAppSession session)
    {
        var billing = new BillingPage(session);
        billing.Open();
        billing.SetPrintInvoice(false);
        billing.SetCustomer("UI Test Customer", "9000000001");

        var salesman = Wait.UntilNotNull(
                () => Visible(session.Window.FindFirstDescendant(
                    cf => cf.ByAutomationId("billing-salesman-input"))),
                UiTestEnvironment.ActionTimeout,
                "the billing salesman selector")
            .AsComboBox();
        Wait.Until(
            () => salesman.Items.Length > 0,
            UiTestEnvironment.ActionTimeout,
            "at least one seeded salesman");
        salesman.Select(0);

        billing.AddManualLine();
        Thread.Sleep(250);
        if (!session.Application.GetAllTopLevelWindows(session.Automation).Any(window =>
                string.Equals(window.AutomationId, "add-product-code-dialog", StringComparison.Ordinal)
                || window.FindFirstDescendant(
                    cf => cf.ByAutomationId("add-product-code-dialog")) is not null))
        {
            var addManual = WaitForVisibleId(session, "billing-add-manual-line-button");
            addManual.Focus();
            KeyboardHelper.PressEnter();
        }
        new AddProductCodePage(session).Add("UI-SAREE-001");
        WaitForVisibleText(session, "UI-SAREE-001");
        return billing;
    }

    internal static AutomationElement WaitForVisibleText(BillingAppSession session, string text) =>
        Wait.UntilNotNull(
            () => session.Window.FindAllDescendants().FirstOrDefault(element =>
                Visible(element) is not null
                && (element.Name ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)),
            UiTestEnvironment.ActionTimeout,
            $"visible UI text containing '{text}'");

    internal static AutomationElement WaitForVisibleId(BillingAppSession session, string automationId) =>
        Wait.UntilNotNull(
            () => Visible(session.Window.FindFirstDescendant(cf => cf.ByAutomationId(automationId))),
            UiTestEnvironment.ActionTimeout,
            $"visible UI element '{automationId}'");

    internal static void InvokeVisibleButton(BillingAppSession session, string name)
    {
        var button = Wait.UntilNotNull(
                () => session.Window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                    .FirstOrDefault(element =>
                        Visible(element) is not null
                        && (element.Name ?? "").Contains(name, StringComparison.OrdinalIgnoreCase)),
                UiTestEnvironment.ActionTimeout,
                $"visible button containing '{name}'")
            .AsButton();
        button.Invoke();
    }

    internal static void InvokeVisibleButtonById(BillingAppSession session, string automationId) =>
        WaitForVisibleId(session, automationId).AsButton().Invoke();

    internal static void LogoutAndRelogin(BillingAppSession session)
    {
        WaitForVisibleId(session, "main-sign-out-button").AsButton().Click();
        var confirmation = new AppMessagePage(session);
        Assert.Contains("return to the login screen", confirmation.Body, StringComparison.OrdinalIgnoreCase);
        confirmation.Choose("Yes");

        Wait.Until(
            () =>
            {
                session.RefreshWindow();
                return session.Login.IsDisplayed();
            },
            UiTestEnvironment.StartupTimeout,
            "the login screen after logout");

        SignIn(session);
    }

    private static AutomationElement? Visible(AutomationElement? element)
    {
        if (element is null)
            return null;
        try
        {
            return element.IsOffscreen ? null : element;
        }
        catch
        {
            return null;
        }
    }
}
