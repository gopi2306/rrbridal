using FlaUI.Core.AutomationElements;

namespace RRBridal.StoreBilling.UiTests.Pages;

internal sealed class DayClosePage : ShellPageObject
{
    public DayClosePage(BillingAppSession session) : base(session, "day-close-view", "Day Close") { }

    public string SessionStatus => FindVisibleById("day-close-session-status-text").Name;
    public string Status => FindVisibleById("day-close-status-text").Name;
    public void SetBusinessDate(string value) => ReplaceText("day-close-business-date-input", value);
    public void SetOpeningCash(string value) => ReplaceText("day-close-opening-cash-input", value);
    public void OpenDay() => ClickId("day-close-open-day-button");
    public void Refresh() => ClickId("day-close-refresh-button");
    public void AddDeposit(string description, string amount) => AddMovement(description, amount, "day-close-deposit-button");
    public void AddWithdrawal(string description, string amount) => AddMovement(description, amount, "day-close-withdraw-button");
    public void OpenCashHandOver() => ClickId("day-close-cash-hand-over-button");

    public void WaitForSessionStatus(string expected) =>
        Wait.Until(
            () => FindVisibleById("day-close-session-status-text").Name.Contains(expected, StringComparison.OrdinalIgnoreCase),
            UiTestEnvironment.ActionTimeout,
            $"day session status containing '{expected}'");

    private void AddMovement(string description, string amount, string buttonId)
    {
        ReplaceText("day-close-movement-description-input", description);
        ReplaceText("day-close-movement-amount-input", amount);
        ClickId(buttonId);
    }
}

internal sealed class CashHandOverPage : ModalPageObject
{
    public CashHandOverPage(BillingAppSession session) : base(session, "cash-hand-over-window") { }

    public AutomationElement DenominationsGrid => FindVisibleById("cash-hand-over-denominations-grid");
    public void SetCashTaken(string amount) => ReplaceText("cash-hand-over-cash-taken-input", amount);
    public void CloseDay() => ClickId("cash-hand-over-close-day-button");
    public void Print() => ClickId("cash-hand-over-f3-print-button");
    public void Exit() => ClickId("cash-hand-over-f12-exit-button");
}

internal sealed class DailyExpensesPage : ShellPageObject
{
    public DailyExpensesPage(BillingAppSession session) : base(session, "daily-expense-page", "Daily Expenses") { }

    public AutomationElement Grid => FindVisibleById("daily-expense-grid");
    public string Status => FindVisibleById("daily-expense-status").Name;

    public void Enter(string supplier, string category, string amount, string description)
    {
        ReplaceText("daily-expense-supplier", supplier);
        ReplaceText("daily-expense-category", category);
        ReplaceText("daily-expense-amount", amount);
        ReplaceText("daily-expense-description", description);
    }

    public void Save() => ClickId("daily-expense-save");
    public void Clear() => ClickId("daily-expense-clear");
    public void Search(string query)
    {
        ReplaceText("daily-expense-search", query);
        KeyboardHelper.PressEnter();
    }
    public void Select(string expenseNumberOrSupplier) => SelectRowContaining("daily-expense-grid", expenseNumberOrSupplier);
    public void EditSelected() => ClickText("Edit");
    public void PrintSelected() => ClickText("Print receipt");
    public void VoidSelected() => ClickText("Void");
}
