using System.Linq;
using System.Windows;
using System.Windows.Threading;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.Services.Ui;

/// <summary>
/// Modern branded replacement for <see cref="MessageBox"/>. Same return type for drop-in use.
/// </summary>
public static class AppDialog
{
    public static MessageBoxResult Show(string messageBoxText) =>
        Show(messageBoxText, "TruBilling", MessageBoxButton.OK, MessageBoxImage.None);

    public static MessageBoxResult Show(string messageBoxText, string caption) =>
        Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button) =>
        Show(messageBoxText, caption, button, MessageBoxImage.None);

    public static MessageBoxResult Show(
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon)
    {
        if (Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            return dispatcher.Invoke(
                () => ShowOnUiThread(messageBoxText, caption, button, icon, null),
                DispatcherPriority.Normal);
        }

        return ShowOnUiThread(messageBoxText, caption, button, icon, null);
    }

    /// <summary>Owner-aware overload (matches MessageBox.Show(Window, …)).</summary>
    public static MessageBoxResult Show(
        Window? owner,
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon)
    {
        if (Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            return dispatcher.Invoke(
                () => ShowOnUiThread(messageBoxText, caption, button, icon, owner),
                DispatcherPriority.Normal);
        }

        return ShowOnUiThread(messageBoxText, caption, button, icon, owner);
    }

    /// <summary>Default-result overload (matches MessageBox.Show(…, MessageBoxResult)).</summary>
    public static MessageBoxResult Show(
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageBoxResult defaultResult)
    {
        var result = Show(messageBoxText, caption, button, icon);
        return result == MessageBoxResult.None ? defaultResult : result;
    }

    public static bool Confirm(string message, string caption = "Confirm") =>
        Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private static MessageBoxResult ShowOnUiThread(
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        Window? preferredOwner)
    {
        var dialog = new AppMessageDialog(messageBoxText, caption, button, icon);
        var owner = preferredOwner
                    ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current?.MainWindow;
        if (owner is { IsVisible: true })
            dialog.Owner = owner;

        dialog.ShowDialog();
        return dialog.Result;
    }
}
