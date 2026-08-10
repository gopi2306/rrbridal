using FlaUI.Core.AutomationElements;

namespace RRBridal.StoreBilling.UiTests;

internal sealed class ShellPage
{
    private readonly Window _window;

    public ShellPage(Window window)
    {
        _window = window;
    }

    public bool IsDisplayed()
    {
        return string.Equals(_window.AutomationId, "main-window", StringComparison.Ordinal)
               || _window.FindFirstDescendant(cf => cf.ByAutomationId("main-shell-grid")) is not null
               || string.Equals(_window.AutomationId, "shell-root", StringComparison.Ordinal)
               || _window.FindFirstDescendant(cf => cf.ByAutomationId("shell-root")) is not null
               || string.Equals(_window.AutomationId, "ShellWindow", StringComparison.Ordinal)
               || _window.FindFirstDescendant(cf => cf.ByText("Ctrl+G Menu")) is not null
               || _window.FindFirstDescendant(cf => cf.ByText("F12 Exit")) is not null;
    }

    public AutomationElement WaitForPage(string automationId, string visibleLabel)
    {
        return Wait.UntilNotNull(
            () =>
            {
                var page = _window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                if (page is not null && !page.IsOffscreen)
                    return page;

                var label = _window.FindFirstDescendant(cf => cf.ByText(visibleLabel));
                return label is not null && !label.IsOffscreen ? label : null;
            },
            UiTestEnvironment.ActionTimeout,
            $"shell page '{visibleLabel}'");
    }

    public AutomationElement WaitForNavigationItem(string label)
    {
        return Wait.UntilNotNull(
            () => _window.FindFirstDescendant(cf => cf.ByText(label)),
            UiTestEnvironment.ActionTimeout,
            $"shell navigation item '{label}'");
    }
}
