using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views.Controls;

public partial class NavDrawerItem : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(NavDrawerItem),
            new PropertyMetadata(string.Empty, OnLabelOrShortcutChanged));

    public static readonly DependencyProperty ShortcutProperty =
        DependencyProperty.Register(nameof(Shortcut), typeof(string), typeof(NavDrawerItem),
            new PropertyMetadata(string.Empty, OnLabelOrShortcutChanged));

    public static readonly DependencyProperty PageProperty =
        DependencyProperty.Register(nameof(Page), typeof(ShellPage), typeof(NavDrawerItem), new PropertyMetadata(ShellPage.Billing));

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(NavDrawerItem), new PropertyMetadata(false));

    public static readonly DependencyProperty NavigateCommandProperty =
        DependencyProperty.Register(nameof(NavigateCommand), typeof(ICommand), typeof(NavDrawerItem));

    private static readonly DependencyPropertyKey HasShortcutPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(HasShortcut), typeof(bool), typeof(NavDrawerItem),
            new PropertyMetadata(false));

    public static readonly DependencyProperty HasShortcutProperty = HasShortcutPropertyKey.DependencyProperty;

    public NavDrawerItem()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Shortcut
    {
        get => (string)GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    public bool HasShortcut => (bool)GetValue(HasShortcutProperty);

    public string ToolTipText =>
        string.IsNullOrWhiteSpace(Shortcut) ? Label : $"{Label}  ({Shortcut})";

    public ShellPage Page
    {
        get => (ShellPage)GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public ICommand? NavigateCommand
    {
        get => (ICommand?)GetValue(NavigateCommandProperty);
        set => SetValue(NavigateCommandProperty, value);
    }

    private static void OnLabelOrShortcutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavDrawerItem item)
            return;
        item.SetValue(HasShortcutPropertyKey, !string.IsNullOrWhiteSpace(item.Shortcut));
        item.ToolTip = item.ToolTipText;
    }
}
