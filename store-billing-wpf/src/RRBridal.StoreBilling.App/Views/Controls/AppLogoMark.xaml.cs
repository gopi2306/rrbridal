using System.Windows;
using System.Windows.Controls;

namespace RRBridal.StoreBilling.App.Views.Controls;

public partial class AppLogoMark : UserControl
{
    public static readonly DependencyProperty LogoHeightProperty =
        DependencyProperty.Register(
            nameof(LogoHeight),
            typeof(double),
            typeof(AppLogoMark),
            new PropertyMetadata(40.0, OnLogoLayoutChanged));

    public static readonly DependencyProperty LogoMaxWidthProperty =
        DependencyProperty.Register(
            nameof(LogoMaxWidth),
            typeof(double),
            typeof(AppLogoMark),
            new PropertyMetadata(double.PositiveInfinity, OnLogoLayoutChanged));

    public static readonly DependencyProperty ShowTruPrefixProperty =
        DependencyProperty.Register(
            nameof(ShowTruPrefix),
            typeof(bool),
            typeof(AppLogoMark),
            new PropertyMetadata(true, OnLogoLayoutChanged));

    public AppLogoMark()
    {
        InitializeComponent();
        ApplyLogoLayout();
    }

    public double LogoHeight
    {
        get => (double)GetValue(LogoHeightProperty);
        set => SetValue(LogoHeightProperty, value);
    }

    public double LogoMaxWidth
    {
        get => (double)GetValue(LogoMaxWidthProperty);
        set => SetValue(LogoMaxWidthProperty, value);
    }

    public bool ShowTruPrefix
    {
        get => (bool)GetValue(ShowTruPrefixProperty);
        set => SetValue(ShowTruPrefixProperty, value);
    }

    private static void OnLogoLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AppLogoMark mark)
            mark.ApplyLogoLayout();
    }

    private void ApplyLogoLayout()
    {
        if (LogoViewbox == null || TruText == null)
            return;

        TruText.Visibility = ShowTruPrefix ? Visibility.Visible : Visibility.Collapsed;
        TruText.FontSize = LogoHeight * 0.52;

        LogoViewbox.Height = LogoHeight;
        LogoViewbox.MaxWidth = ShowTruPrefix
            ? Math.Max(0, LogoMaxWidth - TruText.ActualWidth - 4)
            : LogoMaxWidth;
        LogoViewbox.Width = double.NaN;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ApplyLogoLayout();
    }
}
