using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RRBridal.StoreBilling.App.Services.Salesmen;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views;

public partial class SalesmanView
{
    public SalesmanView()
    {
        InitializeComponent();
        AttachInputFilter(SearchBox, SalesmanInputRules.IsAcceptableSearchInput);
        AttachInputFilter(NameBox, SalesmanInputRules.IsAcceptableNameInput);
        AttachInputFilter(PhoneBox, SalesmanInputRules.IsAcceptablePhoneInput);
    }

    private void SearchBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not SalesmanViewModel vm)
            return;
        e.Handled = true;
        if (vm.RefreshCommand.CanExecute(null))
            vm.RefreshCommand.Execute(null);
    }

    private static void AttachInputFilter(TextBox box, Func<string, bool> isAcceptable)
    {
        box.PreviewTextInput += (_, e) => e.Handled = !isAcceptable(ProposedText(box, e.Text));
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Space && !isAcceptable(ProposedText(box, " ")))
                e.Handled = true;
        };
        DataObject.AddPastingHandler(box, (_, e) =>
        {
            var pasted = e.DataObject.GetDataPresent(DataFormats.UnicodeText)
                ? e.DataObject.GetData(DataFormats.UnicodeText) as string
                : null;
            if (pasted == null || !isAcceptable(ProposedText(box, pasted.Trim())))
                e.CancelCommand();
        });
    }

    private static string ProposedText(TextBox box, string input)
    {
        var text = box.Text ?? "";
        var start = Math.Min(box.SelectionStart, text.Length);
        var length = Math.Min(box.SelectionLength, text.Length - start);
        return text.Remove(start, length).Insert(start, input);
    }
}
