using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Services.Ui;

/// <summary>
/// Stacks a three-column settings layout (left | spacer | right) into rows on compact widths.
/// Tracks original column roles so remapping remains stable across re-applies.
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

    private static readonly DependencyProperty RoleProperty = DependencyProperty.RegisterAttached(
        "Role",
        typeof(string),
        typeof(SettingsColumnsLayoutBehavior),
        new PropertyMetadata(null));

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
        if (grid.IsLoaded)
            Attach(grid);
    }

    private static void Attach(Grid grid)
    {
        ShellViewModel? shell = null;
        PropertyChangedEventHandler? handler = null;
        var rolesCaptured = false;

        void CaptureRoles()
        {
            if (rolesCaptured)
                return;
            foreach (var child in grid.Children.OfType<FrameworkElement>())
            {
                if (child.GetValue(RoleProperty) is string)
                    continue;
                var col = Grid.GetColumn(child);
                var role = col switch
                {
                    0 => "left",
                    1 => "spacer",
                    2 => "right",
                    _ => "left",
                };
                child.SetValue(RoleProperty, role);
            }
            rolesCaptured = true;
        }

        void Apply()
        {
            if (shell is null)
                return;

            CaptureRoles();
            var children = grid.Children.OfType<FrameworkElement>().ToList();
            var left = children.Where(c => c.GetValue(RoleProperty) as string == "left").ToList();
            var spacer = children.Where(c => c.GetValue(RoleProperty) as string == "spacer").ToList();
            var right = children.Where(c => c.GetValue(RoleProperty) as string == "right").ToList();
            var compact = shell.IsCompactLayout;

            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();

            if (compact)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                foreach (var child in spacer)
                    child.Visibility = Visibility.Collapsed;

                for (var i = 0; i < left.Count; i++)
                {
                    Grid.SetColumn(left[i], 0);
                    Grid.SetRow(left[i], 0);
                    left[i].Margin = new Thickness(0, 0, 0, 12);
                    left[i].Visibility = Visibility.Visible;
                }

                for (var i = 0; i < right.Count; i++)
                {
                    Grid.SetColumn(right[i], 0);
                    Grid.SetRow(right[i], 1);
                    right[i].Margin = new Thickness(0);
                    right[i].Visibility = Visibility.Visible;
                }
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 280 });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 280 });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                foreach (var child in spacer)
                    child.Visibility = Visibility.Visible;

                foreach (var child in left)
                {
                    Grid.SetColumn(child, 0);
                    Grid.SetRow(child, 0);
                    child.Margin = new Thickness(0);
                    child.Visibility = Visibility.Visible;
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
                    child.Visibility = Visibility.Visible;
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
