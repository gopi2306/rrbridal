using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Services.Ui;

/// <summary>
/// Stacks a three-column settings layout (left | spacer | right) into rows on compact widths.
/// </summary>
public static class SettingsColumnsLayoutBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(SettingsColumnsLayoutBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty HookedProperty = DependencyProperty.RegisterAttached(
        "Hooked",
        typeof(bool),
        typeof(SettingsColumnsLayoutBehavior),
        new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Grid grid || e.NewValue is not true)
            return;

        if (grid.GetValue(HookedProperty) is true)
            return;

        grid.SetValue(HookedProperty, true);
        grid.Loaded += (_, _) => Attach(grid);
    }

    private static void Attach(Grid grid)
    {
        ShellViewModel? shell = null;
        PropertyChangedEventHandler? handler = null;

        void Apply()
        {
            if (shell is null)
                return;

            var children = grid.Children.OfType<FrameworkElement>().ToList();
            var left = children.Where(c => Grid.GetColumn(c) == 0).ToList();
            var spacer = children.Where(c => Grid.GetColumn(c) == 1).ToList();
            var right = children.Where(c => Grid.GetColumn(c) == 2).ToList();
            var compact = shell.IsCompactLayout;

            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();

            if (compact)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                foreach (var child in spacer)
                    child.Visibility = Visibility.Collapsed;

                for (var i = 0; i < left.Count; i++)
                {
                    Grid.SetColumn(left[i], 0);
                    Grid.SetRow(left[i], 0);
                    left[i].Margin = new Thickness(0, 0, 0, 12);
                }

                for (var i = 0; i < right.Count; i++)
                {
                    Grid.SetColumn(right[i], 0);
                    Grid.SetRow(right[i], 1);
                    right[i].Margin = new Thickness(0);
                }
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 280 });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 280 });

                foreach (var child in spacer)
                    child.Visibility = Visibility.Visible;

                foreach (var child in left)
                {
                    Grid.SetColumn(child, 0);
                    Grid.SetRow(child, 0);
                    child.Margin = new Thickness(0);
                }

                foreach (var child in spacer)
                {
                    Grid.SetColumn(child, 1);
                    Grid.SetRow(child, 0);
                }

                foreach (var child in right)
                {
                    Grid.SetColumn(child, 2);
                    Grid.SetRow(child, 0);
                    child.Margin = new Thickness(0);
                }
            }
        }

        void TryHook()
        {
            if (Window.GetWindow(grid)?.DataContext is ShellViewModel vm)
            {
                shell = vm;
                handler ??= (_, args) =>
                {
                    if (args.PropertyName is nameof(ShellViewModel.LayoutBreakpoint)
                        or nameof(ShellViewModel.IsCompactLayout))
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
