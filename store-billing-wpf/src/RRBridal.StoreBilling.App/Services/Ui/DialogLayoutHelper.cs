using System.Windows;

namespace RRBridal.StoreBilling.App.Services.Ui;

public static class DialogLayoutHelper
{
    public static void CenterAndClamp(Window dialog, Window? owner = null)
    {
        var reference = owner ?? Application.Current.MainWindow;
        Rect bounds;
        if (reference is { IsVisible: true })
        {
            bounds = new Rect(reference.Left, reference.Top, reference.ActualWidth, reference.ActualHeight);
        }
        else
        {
            var workArea = SystemParameters.WorkArea;
            bounds = new Rect(workArea.Left, workArea.Top, workArea.Width, workArea.Height);
        }

        var maxWidth = Math.Max(WindowLayoutHelper.MinShellWidth * 0.5, bounds.Width * 0.9);
        var maxHeight = Math.Max(WindowLayoutHelper.MinShellHeight * 0.5, bounds.Height * 0.9);

        if (double.IsNaN(dialog.MaxWidth) || dialog.MaxWidth == 0 || dialog.MaxWidth > maxWidth)
            dialog.MaxWidth = maxWidth;
        if (double.IsNaN(dialog.MaxHeight) || dialog.MaxHeight == 0 || dialog.MaxHeight > maxHeight)
            dialog.MaxHeight = maxHeight;

        if (dialog.Width > dialog.MaxWidth)
            dialog.Width = dialog.MaxWidth;
        if (dialog.Height > dialog.MaxHeight)
            dialog.Height = dialog.MaxHeight;

        dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        dialog.Left = bounds.Left + (bounds.Width - dialog.Width) / 2;
        dialog.Top = bounds.Top + (bounds.Height - dialog.Height) / 2;
    }
}
