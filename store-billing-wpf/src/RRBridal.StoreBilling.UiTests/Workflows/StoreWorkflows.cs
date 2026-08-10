using RRBridal.StoreBilling.UiTests.Pages;

namespace RRBridal.StoreBilling.UiTests.Workflows;

internal sealed class DayWorkflow
{
    private readonly BillingAppSession _session;

    public DayWorkflow(BillingAppSession session) => _session = session;

    public DayClosePage OpenDay(string businessDate, string openingCash)
    {
        var page = new DayClosePage(_session);
        page.Open();
        page.SetBusinessDate(businessDate);
        page.SetOpeningCash(openingCash);
        page.OpenDay();
        page.WaitForSessionStatus("open");
        return page;
    }

    public void CloseDay(string cashTaken, bool printBeforeClose = false)
    {
        var page = new DayClosePage(_session);
        page.Open();
        page.OpenCashHandOver();
        var handOver = new CashHandOverPage(_session);
        handOver.SetCashTaken(cashTaken);
        if (printBeforeClose)
            handOver.Print();
        handOver.CloseDay();
        page.WaitForSessionStatus("closed");
    }
}

internal sealed class BillingWorkflow
{
    private readonly BillingAppSession _session;

    public BillingWorkflow(BillingAppSession session) => _session = session;

    public void PostCashBill(
        Action<BillingPage> populateBill,
        string cashReceived,
        bool acceptShortStock = false)
    {
        var billing = new BillingPage(_session);
        billing.Open();
        populateBill(billing);
        billing.Post();

        var dialogId = WaitForEitherDialog("billing-post-warnings-dialog", "bill-post-confirm-dialog");
        if (dialogId == "billing-post-warnings-dialog")
        {
            var warnings = new BillingPostWarningsPage(_session);
            if (!acceptShortStock)
            {
                warnings.Cancel();
                throw new InvalidOperationException(
                    "Bill has a stock shortfall. Set acceptShortStock to true to post it.");
            }
            warnings.PostAnyway();
        }

        new BillPostConfirmPage(_session).Submit();
        var payment = new PaymentPage(_session);
        payment.ChooseCash();
        payment.SetCashReceived(cashReceived);
        payment.Confirm();
        _session.Shell.WaitForPage("billing-view", "Billing");
    }

    public void HoldBill(Action<BillingPage> populateBill)
    {
        var billing = new BillingPage(_session);
        billing.Open();
        populateBill(billing);
        billing.HoldCurrent();
        _session.Shell.WaitForPage("billing-view", "Billing");
    }

    public BillingPage ResumeHeldBill(string billOrCustomer)
    {
        var billing = new BillingPage(_session);
        billing.Open();
        billing.ResumeHeld();
        var held = new HeldBillsPage(_session);
        held.Select(billOrCustomer);
        held.Resume();
        _session.Shell.WaitForPage("billing-view", "Billing");
        return billing;
    }

    private string WaitForEitherDialog(params string[] automationIds) =>
        Wait.UntilNotNull(
            () =>
            {
                var windows = _session.Application.GetAllTopLevelWindows(_session.Automation);
                return automationIds.FirstOrDefault(id => windows.Any(window =>
                    string.Equals(window.AutomationId, id, StringComparison.Ordinal)
                    || window.FindFirstDescendant(cf => cf.ByAutomationId(id)) is not null));
            },
            UiTestEnvironment.ActionTimeout,
            $"one of: {string.Join(", ", automationIds)}");
}

internal sealed class ExpenseWorkflow
{
    private readonly BillingAppSession _session;

    public ExpenseWorkflow(BillingAppSession session) => _session = session;

    public DailyExpensesPage Create(
        string supplier,
        string category,
        string amount,
        string description)
    {
        var page = new DailyExpensesPage(_session);
        page.Open();
        page.Enter(supplier, category, amount, description);
        page.Save();
        Wait.Until(
            () => !string.IsNullOrWhiteSpace(page.Status),
            UiTestEnvironment.ActionTimeout,
            "expense save status");
        return page;
    }
}
