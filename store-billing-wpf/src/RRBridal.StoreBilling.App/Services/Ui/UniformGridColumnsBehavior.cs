using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Services.Ui;

public static class UniformGridColumnsBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(UniformGridColumnsBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty CompactColumnsProperty = DependencyProperty.RegisterAttached(
        "CompactColumns",
        typeof(int),
        typeof(UniformGridColumnsBehavior),
        new PropertyMetadata(1));

    public static readonly DependencyProperty MediumColumnsProperty = DependencyProperty.RegisterAttached(
        "MediumColumns",
        typeof(int),
        typeof(UniformGridColumnsBehavior),
        new PropertyMetadata(2));

    public static readonly DependencyProperty WideColumnsProperty = DependencyProperty.RegisterAttached(
        "WideColumns",
        typeof(int),
        typeof(UniformGridColumnsBehavior),
        new PropertyMetadata(3));

    private static readonly DependencyProperty HookedProperty = DependencyProperty.RegisterAttached(
        "Hooked",
        typeof(bool),
        typeof(UniformGridColumnsBehavior),
        new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);
    public static int GetCompactColumns(DependencyObject obj) => (int)obj.GetValue(CompactColumnsProperty);
    public static void SetCompactColumns(DependencyObject obj, int value) => obj.SetValue(CompactColumnsProperty, value);
    public static int GetMediumColumns(DependencyObject obj) => (int)obj.GetValue(MediumColumnsProperty);
    public static void SetMediumColumns(DependencyObject obj, int value) => obj.SetValue(MediumColumnsProperty, value);
    public static int GetWideColumns(DependencyObject obj) => (int)obj.GetValue(WideColumnsProperty);
    public static void SetWideColumns(DependencyObject obj, int value) => obj.SetValue(WideColumnsProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UniformGrid grid || e.NewValue is not true)
            return;

        if (grid.GetValue(HookedProperty) is true)
            return;

        grid.SetValue(HookedProperty, true);
        grid.Loaded += (_, _) => Attach(grid);
    }

    private static void Attach(UniformGrid grid)
    {
        ShellViewModel? shell = null;
        PropertyChangedEventHandler? handler = null;

        void Apply()
        {
            if (shell is null)
                return;

            grid.Columns = shell.LayoutBreakpoint switch
            {
                LayoutBreakpoint.Compact => GetCompactColumns(grid),
                LayoutBreakpoint.Medium => GetMediumColumns(grid),
                _ => GetWideColumns(grid),
            };
        }

        void TryHook()
        {
            if (Window.GetWindow(grid)?.DataContext is ShellViewModel vm)
            {
                shell = vm;
                handler ??= (_, args) =>
                {
                    if (args.PropertyName is nameof(ShellViewModel.LayoutBreakpoint))
                        Apply();
                };
                shell.PropertyChanged += handler;
                Apply();
                return;
            }

            grid.Dispatcher.BeginInvoke(TryHook, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        grid.Unloaded += (_, _) =>
        {
            if (shell is not null && handler is not null)
                shell.PropertyChanged -= handler;
        };

        TryHook();
    }
}
