using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RRBridal.StoreBilling.App.ViewModels;

namespace RRBridal.StoreBilling.App.Services.Ui;

/// <summary>
/// On Compact screens, shrinks fixed form-label columns (≈80–160px) to Auto
/// so text inputs can use the remaining width. Restores pixel widths on Medium/Wide.
/// </summary>
public static class ResponsiveFormGridBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(ResponsiveFormGridBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty HookedProperty = DependencyProperty.RegisterAttached(
        "Hooked",
        typeof(bool),
        typeof(ResponsiveFormGridBehavior),
        new PropertyMetadata(false));

    private static readonly DependencyProperty OriginalColumnsProperty = DependencyProperty.RegisterAttached(
        "OriginalColumns",
        typeof(GridLength[]),
        typeof(ResponsiveFormGridBehavior));

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

        void CaptureOriginals()
        {
            if (grid.GetValue(OriginalColumnsProperty) is GridLength[])
                return;
            var originals = grid.ColumnDefinitions.Select(c => c.Width).ToArray();
            grid.SetValue(OriginalColumnsProperty, originals);
        }

        void Apply()
        {
            if (shell is null)
                return;
            CaptureOriginals();
            if (grid.GetValue(OriginalColumnsProperty) is not GridLength[] originals)
                return;

            var compact = shell.IsCompactLayout;
            for (var i = 0; i < grid.ColumnDefinitions.Count && i < originals.Length; i++)
            {
                var col = grid.ColumnDefinitions[i];
                var original = originals[i];
                if (original.IsAbsolute && original.Value is >= 80 and <= 180)
                {
                    if (compact)
                    {
                        col.Width = GridLength.Auto;
                        col.MinWidth = 0;
                        col.MaxWidth = 120;
                    }
                    else
                    {
                        col.ClearValue(ColumnDefinition.MaxWidthProperty);
                        col.Width = original;
                        if (Application.Current?.Resources[UiDensityService.FormLabelMinWidthKey] is double labelMin)
                            col.MinWidth = System.Math.Min(labelMin, original.Value);
                    }
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
