using System.Windows;

namespace RRBridal.StoreBilling.App.Services.Ui;

public static class WindowLayoutHelper
{
    public const double MinShellWidth = 900;
    public const double MinShellHeight = 600;
    public const double CompactMaxWidth = 1199;
    public const double MediumMaxWidth = 1599;

    public static LayoutBreakpoint GetBreakpoint(double width) =>
        width switch
        {
            < 1200 => LayoutBreakpoint.Compact,
            < 1600 => LayoutBreakpoint.Medium,
            _ => LayoutBreakpoint.Wide,
        };

    public static void ApplyStartupBounds(Window window)
    {
        var workArea = SystemParameters.WorkArea;
        var targetWidth = workArea.Width < 1024
            ? Math.Max(MinShellWidth, workArea.Width * 0.96)
            : Math.Max(MinShellWidth, Math.Min(workArea.Width * 0.92, workArea.Width));
        var targetHeight = Math.Clamp(workArea.Height * 0.92, MinShellHeight, workArea.Height);

        window.Width = targetWidth;
        window.Height = targetHeight;
        window.Left = workArea.Left + (workArea.Width - targetWidth) / 2;
        window.Top = workArea.Top + (workArea.Height - targetHeight) / 2;
    }

    public static void CenterOnScreen(Window window)
    {
        var workArea = SystemParameters.WorkArea;
        window.Left = workArea.Left + (workArea.Width - window.Width) / 2;
        window.Top = workArea.Top + (workArea.Height - window.Height) / 2;
    }
}
