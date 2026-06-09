using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Views.Controls;

public partial class NavTabButton : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(NavTabButton), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PageProperty =
        DependencyProperty.Register(nameof(Page), typeof(ShellPage), typeof(NavTabButton), new PropertyMetadata(ShellPage.Billing));

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(NavTabButton), new PropertyMetadata(false));

    public static readonly DependencyProperty NavigateCommandProperty =
        DependencyProperty.Register(nameof(NavigateCommand), typeof(ICommand), typeof(NavTabButton));

    public NavTabButton()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

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

}
