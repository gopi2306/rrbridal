using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RRBridal.StoreBilling.App.Views;

public partial class AppMessageDialog : Window
{
    public string TitleText { get; }
    public string MessageText { get; }
    public string IconGlyph { get; }
    public Brush IconBackground { get; }
    public Brush IconForeground { get; }

    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public AppMessageDialog(
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage image)
    {
        TitleText = string.IsNullOrWhiteSpace(title) ? "TruBilling" : title;
        MessageText = message ?? "";
        (IconGlyph, IconBackground, IconForeground) = ResolveIcon(image);

        DataContext = this;
        InitializeComponent();
        BuildButtons(buttons);

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Result = buttons is MessageBoxButton.YesNo or MessageBoxButton.YesNoCancel or MessageBoxButton.OKCancel
                    ? MessageBoxResult.Cancel
                    : MessageBoxResult.None;
                if (Result == MessageBoxResult.Cancel || buttons == MessageBoxButton.OK)
                {
                    if (buttons == MessageBoxButton.OK)
                        Result = MessageBoxResult.Cancel;
                    DialogResult = false;
                    e.Handled = true;
                }
            }
        };
    }

    private static (string Glyph, Brush Bg, Brush Fg) ResolveIcon(MessageBoxImage image)
    {
        var accent = (Brush)Application.Current.FindResource("BrushAccent");
        var soft = (Brush)Application.Current.FindResource("BrushAccentSoft");
        var warnBg = (Brush)Application.Current.FindResource("BrushWarningBg");
        var warnFg = (Brush)Application.Current.FindResource("BrushWarning");
        var errBg = (Brush)Application.Current.FindResource("BrushErrorBg");
        var errFg = (Brush)Application.Current.FindResource("BrushNegative");
        var okBg = (Brush)Application.Current.FindResource("BrushSuccessBg");
        var okFg = (Brush)Application.Current.FindResource("BrushSuccess");
        var infoBg = (Brush)Application.Current.FindResource("BrushInfoBg");
        var infoFg = (Brush)Application.Current.FindResource("BrushInfo");

        return image switch
        {
            MessageBoxImage.Error or MessageBoxImage.Hand or MessageBoxImage.Stop
                => ("!", errBg, errFg),
            MessageBoxImage.Warning or MessageBoxImage.Exclamation
                => ("!", warnBg, warnFg),
            MessageBoxImage.Information or MessageBoxImage.Asterisk
                => ("i", infoBg, infoFg),
            MessageBoxImage.Question
                => ("?", soft, accent),
            _ => ("i", soft, accent),
        };
    }

    private void BuildButtons(MessageBoxButton buttons)
    {
        ButtonHost.Children.Clear();

        void Add(string content, Style style, MessageBoxResult result, bool isDefault = false, bool isCancel = false)
        {
            var btn = new Button
            {
                Content = content,
                Style = style,
                MinWidth = 80,
                Margin = new Thickness(8, 0, 0, 0),
                IsDefault = isDefault,
                IsCancel = isCancel,
            };
            btn.Click += (_, _) =>
            {
                Result = result;
                DialogResult = result is MessageBoxResult.Yes or MessageBoxResult.OK;
            };
            ButtonHost.Children.Add(btn);
        }

        var primary = (Style)FindResource("PrimaryButton");
        var outline = (Style)FindResource("OutlineButton");

        switch (buttons)
        {
            case MessageBoxButton.YesNo:
                Add("No", outline, MessageBoxResult.No, isCancel: true);
                Add("Yes", primary, MessageBoxResult.Yes, isDefault: true);
                break;
            case MessageBoxButton.YesNoCancel:
                Add("Cancel", outline, MessageBoxResult.Cancel, isCancel: true);
                Add("No", outline, MessageBoxResult.No);
                Add("Yes", primary, MessageBoxResult.Yes, isDefault: true);
                break;
            case MessageBoxButton.OKCancel:
                Add("Cancel", outline, MessageBoxResult.Cancel, isCancel: true);
                Add("OK", primary, MessageBoxResult.OK, isDefault: true);
                break;
            default:
                Add("OK", primary, MessageBoxResult.OK, isDefault: true, isCancel: true);
                break;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Cancel;
        DialogResult = false;
    }
}
