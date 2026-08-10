using FlaUI.Core.AutomationElements;

namespace RRBridal.StoreBilling.UiTests;

internal sealed class LoginPage
{
    private readonly Window _window;

    public LoginPage(Window window)
    {
        _window = window;
    }

    public TextBox Email =>
        FindByAutomationIds(
                ["login-email", "login-email-input", "EmailBox"],
                "email field")
            .AsTextBox();

    public TextBox Password =>
        FindByAutomationIds(
                ["login-password", "login-password-input", "PasswordBox"],
                "password field")
            .AsTextBox();

    public Button SignInButton =>
        Wait.UntilNotNull(
                () => TryFindByAutomationIds(
                          "login-submit",
                          "login-submit-button")
                      ?? _window.FindFirstDescendant(cf => cf.ByText("Sign in")),
                UiTestEnvironment.ActionTimeout,
                "the Sign in button")
            .AsButton();

    public bool IsDisplayed()
    {
        return _window.FindFirstDescendant(cf => cf.ByText("Welcome back")) is not null
               && TryFindByAutomationIds("login-email", "login-email-input", "EmailBox") is not null
               && TryFindByAutomationIds("login-password", "login-password-input", "PasswordBox") is not null;
    }

    public void SignIn(string email, string password)
    {
        KeyboardHelper.ReplaceText(Email, email);
        KeyboardHelper.ReplaceText(Password, password);
        SignInButton.Click();
    }

    public AutomationElement WaitForError(string expectedText)
    {
        return Wait.UntilNotNull(
            () =>
            {
                var explicitError = TryFindByAutomationIds(
                    "login-error",
                    "login-error-text");
                if (explicitError is not null
                    && explicitError.Name.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
                {
                    return explicitError;
                }

                return _window.FindFirstDescendant(cf => cf.ByText(expectedText));
            },
            UiTestEnvironment.ActionTimeout,
            $"login error '{expectedText}'");
    }

    private AutomationElement FindByAutomationIds(
        IReadOnlyCollection<string> automationIds,
        string description)
    {
        return Wait.UntilNotNull(
            () => TryFindByAutomationIds([.. automationIds]),
            UiTestEnvironment.ActionTimeout,
            description);
    }

    private AutomationElement? TryFindByAutomationIds(params string[] automationIds)
    {
        foreach (var automationId in automationIds)
        {
            var element = _window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (element is not null)
                return element;
        }

        return null;
    }
}
