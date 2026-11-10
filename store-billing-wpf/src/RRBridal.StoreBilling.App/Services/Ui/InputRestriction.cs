using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RRBridal.StoreBilling.App.Services.Customers;

namespace RRBridal.StoreBilling.App.Services.Ui;

public enum InputRestrictionKind
{
    None,
    Mobile,
    Telephone,
    CustomerCode,
    Name,
    Gstin,
}

/// <summary>
/// Attached behavior that blocks typed or pasted text the field does not accept,
/// e.g. <c>ui:InputRestriction.Kind="Mobile"</c>.
/// </summary>
public static class InputRestriction
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.RegisterAttached(
        "Kind",
        typeof(InputRestrictionKind),
        typeof(InputRestriction),
        new PropertyMetadata(InputRestrictionKind.None, OnKindChanged));

    public static InputRestrictionKind GetKind(DependencyObject element) =>
        (InputRestrictionKind)element.GetValue(KindProperty);

    public static void SetKind(DependencyObject element, InputRestrictionKind value) =>
        element.SetValue(KindProperty, value);

    public static Func<string, bool>? RuleFor(InputRestrictionKind kind) => kind switch
    {
        InputRestrictionKind.Mobile => CustomerInputRules.IsAcceptableMobile,
        InputRestrictionKind.Telephone => CustomerInputRules.IsAcceptableTelephone,
        InputRestrictionKind.CustomerCode => CustomerInputRules.IsAcceptableCustomerCode,
        InputRestrictionKind.Name => CustomerInputRules.IsAcceptableName,
        InputRestrictionKind.Gstin => CustomerInputRules.IsAcceptableGstin,
        _ => null,
    };

    private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box)
            return;

        box.PreviewTextInput -= OnPreviewTextInput;
        box.PreviewKeyDown -= OnPreviewKeyDown;
        DataObject.RemovePastingHandler(box, OnPasting);

        if ((InputRestrictionKind)e.NewValue == InputRestrictionKind.None)
            return;

        box.PreviewTextInput += OnPreviewTextInput;
        box.PreviewKeyDown += OnPreviewKeyDown;
        DataObject.AddPastingHandler(box, OnPasting);
    }

    private static bool Accepts(TextBox box, string input)
    {
        var rule = RuleFor(GetKind(box));
        if (rule == null)
            return true;
        var normalized = box.CharacterCasing == CharacterCasing.Upper ? input.ToUpperInvariant() : input;
        return rule(ProposedText(box, normalized));
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox box && !Accepts(box, e.Text))
            e.Handled = true;
    }

    // Space does not raise PreviewTextInput in WPF, so it is filtered here.
    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && sender is TextBox box && !Accepts(box, " "))
            e.Handled = true;
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox box)
            return;
        var pasted = e.DataObject.GetDataPresent(DataFormats.UnicodeText)
            ? e.DataObject.GetData(DataFormats.UnicodeText) as string
            : null;
        if (pasted == null || !Accepts(box, pasted))
            e.CancelCommand();
    }

    private static string ProposedText(TextBox box, string input)
    {
        var text = box.Text ?? "";
        var start = Math.Min(box.SelectionStart, text.Length);
        var length = Math.Min(box.SelectionLength, text.Length - start);
        return text.Remove(start, length).Insert(start, input);
    }
}
