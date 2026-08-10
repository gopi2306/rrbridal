using Xunit;
using FlaUI.Core.WindowsAPI;

namespace RRBridal.StoreBilling.UiTests;

[Collection(UiTestCollection.Name)]
[Trait("Category", "UiSmoke")]
[Trait("Category", "UiOnline")]
public sealed class LoginSmokeTests : UiTestBase
{
    public LoginSmokeTests(BillingAppFixture fixture)
        : base(fixture)
    {
    }

    [SkippableFact]
    public void Launch_ShowsLoginScreen()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(nameof(Launch_ShowsLoginScreen), session =>
        {
            Assert.True(session.Login.IsDisplayed(), "Expected the billing login screen to be visible.");
            Assert.True(session.Login.Email.IsEnabled);
            Assert.True(session.Login.Password.IsEnabled);
            Assert.True(session.Login.SignInButton.IsEnabled);
        });
    }

    [SkippableFact]
    public void InvalidLogin_ShowsValidationAndStaysOnLoginScreen()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(nameof(InvalidLogin_ShowsValidationAndStaysOnLoginScreen), session =>
        {
            session.Login.SignIn(FakeCentralServer.ValidEmail, "not-the-password");

            Assert.NotNull(session.Login.WaitForError("Invalid email or password."));
            Assert.True(session.Login.IsDisplayed(), "Invalid credentials must not leave the login screen.");
        });
    }

    [SkippableFact]
    public void ValidLogin_OpensShell()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(nameof(ValidLogin_OpensShell), session =>
        {
            session.Login.SignIn(FakeCentralServer.ValidEmail, FakeCentralServer.ValidPassword);

            var shell = session.WaitForShell();
            Assert.True(shell.IsDisplayed(), "Expected a successful login to open the billing shell.");
        });
    }

    [SkippableFact]
    public void ShellKeyboardShortcuts_NavigateBillingAndDashboard()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(nameof(ShellKeyboardShortcuts_NavigateBillingAndDashboard), session =>
        {
            session.Login.SignIn(FakeCentralServer.ValidEmail, FakeCentralServer.ValidPassword);
            var shell = session.WaitForShell();

            KeyboardHelper.PressControlShortcut(VirtualKeyShort.KEY_A);
            Assert.NotNull(shell.WaitForPage("billing-view", "Billing"));

            KeyboardHelper.PressControlShortcut(VirtualKeyShort.KEY_D);
            Assert.NotNull(shell.WaitForPage("dashboard-page", "Dashboard"));
        });
    }

    [SkippableFact]
    public void ShellKeyboardShortcuts_NavigateEveryConfiguredPage()
    {
        UiTestEnvironment.SkipUnlessRunnable();

        RunWithDiagnostics(nameof(ShellKeyboardShortcuts_NavigateEveryConfiguredPage), session =>
        {
            session.Login.SignIn(FakeCentralServer.ValidEmail, FakeCentralServer.ValidPassword);
            var shell = session.WaitForShell();
            var routes = new (VirtualKeyShort Key, string AutomationId, string Label)[]
            {
                (VirtualKeyShort.KEY_A, "billing-view", "Billing"),
                (VirtualKeyShort.KEY_Q, "quotations-page", "Quotations"),
                (VirtualKeyShort.KEY_B, "barcodes-page", "Barcodes"),
                (VirtualKeyShort.KEY_D, "dashboard-page", "Dashboard"),
                (VirtualKeyShort.KEY_Y, "analytics-page", "Analytics"),
                (VirtualKeyShort.KEY_O, "online-sales-page", "Online Sales"),
                (VirtualKeyShort.KEY_I, "credit-bills-page", "Credit Bills"),
                (VirtualKeyShort.KEY_U, "customers-page", "Customers"),
                (VirtualKeyShort.KEY_M, "salesmen-page", "Salesman"),
                (VirtualKeyShort.KEY_L, "ledger-page", "Ledger"),
                (VirtualKeyShort.KEY_R, "sale-return-page", "Returns"),
                (VirtualKeyShort.KEY_K, "bill-lookup-page", "Bill Lookup"),
                (VirtualKeyShort.KEY_W, "day-close-view", "Day Close"),
                (VirtualKeyShort.KEY_J, "duplicate-bill-page", "Duplicate"),
                (VirtualKeyShort.KEY_T, "adjustments-page", "Adjustments"),
                (VirtualKeyShort.KEY_E, "daily-expense-page", "Daily Expenses"),
                (VirtualKeyShort.KEY_V, "vouchers-page", "Vouchers"),
                (VirtualKeyShort.OEM_COMMA, "settings-page", "Settings"),
            };

            foreach (var route in routes)
            {
                KeyboardHelper.PressControlShortcut(route.Key);
                Assert.NotNull(shell.WaitForPage(route.AutomationId, route.Label));
            }
        });
    }
}
