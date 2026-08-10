using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace RRBridal.StoreBilling.UiTests.Pages;

/// <summary>Bounded, visibility-aware primitives shared by the functional page objects.</summary>
internal abstract class UiPage
{
    protected UiPage(BillingAppSession session, AutomationElement root)
    {
        Session = session;
        Root = root;
    }

    protected BillingAppSession Session { get; }
    protected AutomationElement Root { get; }

    public AutomationElement WaitUntilDisplayed(string automationId) =>
        FindVisibleById(automationId, $"'{automationId}'");

    protected AutomationElement FindVisibleById(string automationId, string? description = null) =>
        Wait.UntilNotNull(
            () => Visible(Root.FindFirstDescendant(cf => cf.ByAutomationId(automationId))),
            UiTestEnvironment.ActionTimeout,
            description ?? $"visible element '{automationId}'");

    protected AutomationElement FindVisibleByText(string text, string? description = null) =>
        Wait.UntilNotNull(
            () => Visible(Root.FindFirstDescendant(cf => cf.ByText(text))),
            UiTestEnvironment.ActionTimeout,
            description ?? $"visible element '{text}'");

    protected AutomationElement FindVisibleByName(string name, string? description = null) =>
        Wait.UntilNotNull(
            () => Visible(Root.FindFirstDescendant(cf => cf.ByName(name))),
            UiTestEnvironment.ActionTimeout,
            description ?? $"visible element named '{name}'");

    protected AutomationElement FindVisibleByControlType(ControlType controlType, string description) =>
        Wait.UntilNotNull(
            () => Root.FindAllDescendants(cf => cf.ByControlType(controlType))
                .FirstOrDefault(element => Visible(element) is not null),
            UiTestEnvironment.ActionTimeout,
            description);

    protected AutomationElement? TryFindVisibleById(string automationId) =>
        Visible(Root.FindFirstDescendant(cf => cf.ByAutomationId(automationId)));

    protected Button ButtonById(string automationId) => FindVisibleById(automationId).AsButton();
    protected Button ButtonByText(string text) => FindVisibleByText(text).AsButton();
    protected TextBox TextBoxById(string automationId) => FindVisibleById(automationId).AsTextBox();
    protected CheckBox CheckBoxById(string automationId) => FindVisibleById(automationId).AsCheckBox();
    protected ComboBox ComboBoxById(string automationId) => FindVisibleById(automationId).AsComboBox();

    protected void ClickId(string automationId) => ButtonById(automationId).Click();
    protected void ClickText(string text) => ButtonByText(text).Click();
    protected void ReplaceText(string automationId, string value) =>
        KeyboardHelper.ReplaceText(FindVisibleById(automationId), value);

    protected void ReplaceNamedText(string name, string value) =>
        KeyboardHelper.ReplaceText(FindVisibleByName(name), value);

    protected void SetChecked(string automationId, bool value)
    {
        var checkBox = CheckBoxById(automationId);
        if (checkBox.IsChecked != value)
            checkBox.Click();
    }

    protected AutomationElement SelectRowContaining(string gridAutomationId, string text)
    {
        var grid = FindVisibleById(gridAutomationId);
        var row = Wait.UntilNotNull(
            () => grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                .FirstOrDefault(candidate =>
                    Visible(candidate) is not null
                    && candidate.FindAllDescendants().Any(cell =>
                        cell.Name.Contains(text, StringComparison.OrdinalIgnoreCase))),
            UiTestEnvironment.ActionTimeout,
            $"row containing '{text}' in '{gridAutomationId}'");
        row.Click();
        return row;
    }

    protected void WaitUntilGone(string automationId) =>
        Wait.Until(
            () => TryFindVisibleById(automationId) is null,
            UiTestEnvironment.ActionTimeout,
            $"'{automationId}' to close");

    protected static AutomationElement? Visible(AutomationElement? element)
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

internal abstract class ShellPageObject : UiPage
{
    protected ShellPageObject(BillingAppSession session, string pageAutomationId, string navigationLabel)
        : base(session, session.Window)
    {
        PageAutomationId = pageAutomationId;
        NavigationLabel = navigationLabel;
    }

    protected string PageAutomationId { get; }
    protected string NavigationLabel { get; }

    public void Open()
    {
        var current = TryFindVisibleById(PageAutomationId);
        if (current is null)
        {
            Session.Shell.WaitForNavigationItem(NavigationLabel).Click();
            Session.Shell.WaitForPage(PageAutomationId, NavigationLabel);
        }
    }
}

internal abstract class ModalPageObject : UiPage
{
    protected ModalPageObject(BillingAppSession session, string automationId)
        : base(session, FindModal(session, automationId))
    {
        AutomationId = automationId;
    }

    public string AutomationId { get; }

    private static Window FindModal(BillingAppSession session, string automationId) =>
        Wait.UntilNotNull(
            () => session.Application
                .GetAllTopLevelWindows(session.Automation)
                .Reverse()
                .FirstOrDefault(window =>
                    Visible(window) is not null
                    && (string.Equals(window.AutomationId, automationId, StringComparison.Ordinal)
                        || window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)) is not null)),
            UiTestEnvironment.ActionTimeout,
            $"modal window '{automationId}'");
}
