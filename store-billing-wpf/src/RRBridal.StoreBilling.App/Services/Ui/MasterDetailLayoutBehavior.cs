using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Services.Ui;

public static class MasterDetailLayoutBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(MasterDetailLayoutBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty HookedProperty = DependencyProperty.RegisterAttached(
        "Hooked",
        typeof(bool),
        typeof(MasterDetailLayoutBehavior),
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
            if (children.Count == 0)
                return;

            var compact = shell.IsCompactLayout;
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();

            var mainChildren = children.Where(c => Grid.GetColumn(c) == 0).ToList();
            var detailChildren = children.Where(c => Grid.GetColumn(c) == 1).ToList();
            if (detailChildren.Count == 0 && children.Count >= 2)
            {
                mainChildren = children.Take(children.Count - 1).ToList();
                detailChildren = children.Skip(children.Count - 1).ToList();
            }

            if (compact)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                foreach (var child in mainChildren)
                {
                    Grid.SetColumn(child, 0);
                    Grid.SetRow(child, 0);
                    Grid.SetColumnSpan(child, 1);
                    Grid.SetRowSpan(child, 1);
                    if (child == mainChildren[0])
                        child.Margin = new Thickness(0, 0, 0, 8);
                }

                foreach (var child in detailChildren)
                {
                    Grid.SetColumn(child, 0);
                    Grid.SetRow(child, 1);
                    Grid.SetColumnSpan(child, 1);
                    Grid.SetRowSpan(child, 1);
                    child.MaxHeight = 360;
                    child.Margin = new Thickness(0);
                }
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star),
                    MinWidth = 280,
                    MaxWidth = 440,
                });
                foreach (var child in mainChildren)
                {
                    Grid.SetColumn(child, 0);
                    Grid.SetRow(child, 0);
                    Grid.SetColumnSpan(child, 1);
                    Grid.SetRowSpan(child, 1);
                    if (child == mainChildren[0])
                        child.Margin = new Thickness(0, 0, 12, 0);
                }

                foreach (var child in detailChildren)
                {
                    Grid.SetColumn(child, 1);
                    Grid.SetRow(child, 0);
                    Grid.SetColumnSpan(child, 1);
                    Grid.SetRowSpan(child, 1);
                    child.ClearValue(FrameworkElement.MaxHeightProperty);
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
