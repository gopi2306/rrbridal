using FlaUI.Core.AutomationElements;

namespace RRBridal.StoreBilling.UiTests.Pages;

internal sealed class BillingPage : ShellPageObject
{
    public BillingPage(BillingAppSession session) : base(session, "billing-view", "Billing") { }

    public AutomationElement LinesGrid => FindVisibleById("billing-lines-grid");
    public string PayableText => FindVisibleById("billing-payable-total-text").Name;

    public void SetCustomer(string name, string phone)
    {
        ReplaceText("billing-customer-phone-input", phone);
        ReplaceText("billing-customer-name-input", name);
    }

    public void SetSalesman(string value) => ReplaceText("billing-salesman-input", value);
    public void SetItemDiscount(string value) => ReplaceText("billing-item-discount-input", value);
    public void SetCashDiscount(string value) => ReplaceText("billing-cash-discount-input", value);
    public void SetRoundOff(string value) => ReplaceText("billing-round-off-input", value);
    public void SetHold(bool value = true) => SetChecked("billing-hold-bill-checkbox", value);
    public void SetPrintInvoice(bool value) => SetChecked("billing-print-invoice-checkbox", value);
    public void SetOnlineCod(bool value = true) => SetChecked("billing-online-cod-checkbox", value);
    public void FindCustomer() => ClickId("billing-find-customer-button");
    public void ResumeHeld() => ClickId("billing-resume-held-button");
    public void AddManualLine() => ClickId("billing-add-manual-line-button");
    public void HoldCurrent() => ClickText("F8 / Ctrl+H Hold");
    public void Post() => ClickId("main-post-bill-button");
}

internal sealed class PaymentPage : ModalPageObject
{
    public PaymentPage(BillingAppSession session) : base(session, "payment-dialog") { }

    public string InvoiceTotal => FindVisibleById("payment-invoice-total-text").Name;
    public string ErrorText => TryFindVisibleById("payment-error-text")?.Name ?? "";
    public void ChooseCash() => ClickId("payment-method-cash");
    public void ChooseCard() => ClickId("payment-method-card");
    public void ChooseUpi() => ClickId("payment-method-upi");
    public void ChooseCreditNote() => ClickId("payment-method-credit-note");
    public void ChooseSplit() => ClickId("payment-method-split");
    public void ChooseBillOnCredit() => ClickId("payment-method-bill-on-credit");
    public void SetCashReceived(string amount) => ReplaceText("payment-cash-received-input", amount);
    public void SetCreditNote(string reference, string amount)
    {
        ReplaceText("payment-credit-note-reference-input", reference);
        ReplaceText("payment-credit-note-amount-input", amount);
    }
    public void SetSplit(string cash, string card, string upi)
    {
        ReplaceText("payment-split-cash-input", cash);
        ReplaceText("payment-split-card-input", card);
        ReplaceText("payment-split-upi-input", upi);
    }
    public void Confirm() => ClickId("payment-confirm-button");
    public void Cancel() => ClickId("payment-cancel-button");
}

internal sealed class BillPostConfirmPage : ModalPageObject
{
    public BillPostConfirmPage(BillingAppSession session) : base(session, "bill-post-confirm-dialog") { }

    public string Customer => FindVisibleById("bill-post-confirm-customer-text").Name;
    public string ItemCount => FindVisibleById("bill-post-confirm-item-count-text").Name;
    public string Payable => FindVisibleById("bill-post-confirm-payable-text").Name;
    public void Submit() => ClickId("bill-post-confirm-submit-button");
    public void Cancel() => ClickId("bill-post-confirm-cancel-button");
}

internal sealed class HeldBillsPage : ModalPageObject
{
    public HeldBillsPage(BillingAppSession session) : base(session, "held-bills-dialog") { }

    public AutomationElement Grid => FindVisibleById("held-bills-grid");
    public void Select(string billOrCustomer) => SelectRowContaining("held-bills-grid", billOrCustomer);
    public void Resume() => ClickId("held-bills-resume-button");
    public void Delete() => ClickId("held-bills-delete-button");
    public void Cancel() => ClickId("held-bills-cancel-button");
}

internal sealed class AddProductCodePage : ModalPageObject
{
    public AddProductCodePage(BillingAppSession session) : base(session, "add-product-code-dialog") { }

    public void Add(string productCode)
    {
        ReplaceText("add-product-code-product-code-input", productCode);
        ClickId("add-product-code-add-button");
    }

    public void Cancel() => ClickId("add-product-code-cancel-button");
}
