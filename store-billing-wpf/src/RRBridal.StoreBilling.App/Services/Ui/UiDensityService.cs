using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RRBridal.StoreBilling.App.Services.Ui;

/// <summary>
/// Updates application DynamicResource density tokens when the shell layout breakpoint changes.
/// Styles bind these keys so inputs, cards, and typography scale on Compact / Medium / Wide.
/// Defaults are compact POS-friendly (small, even inputs + buttons).
/// </summary>
public static class UiDensityService
{
    public const string InputMinHeightKey = "DensityInputMinHeight";
    public const string InputPaddingKey = "DensityInputPadding";
    public const string InputFontSizeKey = "DensityInputFontSize";
    public const string CompactInputMinHeightKey = "DensityCompactInputMinHeight";
    public const string CompactInputPaddingKey = "DensityCompactInputPadding";
    public const string ComboMinHeightKey = "DensityComboMinHeight";
    public const string ButtonPaddingKey = "DensityButtonPadding";
    public const string ButtonMinHeightKey = "DensityButtonMinHeight";
    public const string CardPaddingKey = "DensityCardPadding";
    public const string FilterBarPaddingKey = "DensityFilterBarPadding";
    public const string PageTitleFontSizeKey = "DensityPageTitleFontSize";
    public const string SectionTitleFontSizeKey = "DensitySectionTitleFontSize";
    public const string MetricValueFontSizeKey = "DensityMetricValueFontSize";
    public const string ContentMarginKey = "DensityContentMargin";
    public const string FormLabelMinWidthKey = "DensityFormLabelMinWidth";
    public const string DataGridRowHeightKey = "DensityDataGridRowHeight";

    private static LayoutBreakpoint _applied = (LayoutBreakpoint)(-1);

    public static void EnsureDefaults()
    {
        var app = Application.Current;
        if (app?.Resources == null)
            return;

        SetIfMissing(app, InputMinHeightKey, 30d);
        SetIfMissing(app, InputPaddingKey, new Thickness(10, 4, 10, 4));
        SetIfMissing(app, InputFontSizeKey, 12.5d);
        SetIfMissing(app, CompactInputMinHeightKey, 26d);
        SetIfMissing(app, CompactInputPaddingKey, new Thickness(6, 2, 6, 2));
        SetIfMissing(app, ComboMinHeightKey, 30d);
        SetIfMissing(app, ButtonPaddingKey, new Thickness(12, 5, 12, 5));
        SetIfMissing(app, ButtonMinHeightKey, 30d);
        SetIfMissing(app, CardPaddingKey, new Thickness(14, 12, 14, 12));
        SetIfMissing(app, FilterBarPaddingKey, new Thickness(12, 10, 12, 10));
        SetIfMissing(app, PageTitleFontSizeKey, 22d);
        SetIfMissing(app, SectionTitleFontSizeKey, 14.5d);
        SetIfMissing(app, MetricValueFontSizeKey, 18d);
        SetIfMissing(app, ContentMarginKey, new Thickness(12, 8, 12, 8));
        SetIfMissing(app, FormLabelMinWidthKey, 110d);
        SetIfMissing(app, DataGridRowHeightKey, 28d);
    }

    public static void Apply(LayoutBreakpoint breakpoint)
    {
        var app = Application.Current;
        if (app?.Resources == null)
            return;

        EnsureDefaults();
        if (_applied == breakpoint)
            return;
        _applied = breakpoint;

        switch (breakpoint)
        {
            case LayoutBreakpoint.Compact:
                Set(app, InputMinHeightKey, 28d);
                Set(app, InputPaddingKey, new Thickness(8, 3, 8, 3));
                Set(app, InputFontSizeKey, 12d);
                Set(app, CompactInputMinHeightKey, 24d);
                Set(app, CompactInputPaddingKey, new Thickness(5, 1, 5, 1));
                Set(app, ComboMinHeightKey, 28d);
                Set(app, ButtonPaddingKey, new Thickness(10, 4, 10, 4));
                Set(app, ButtonMinHeightKey, 28d);
                Set(app, CardPaddingKey, new Thickness(10, 8, 10, 8));
                Set(app, FilterBarPaddingKey, new Thickness(10, 8, 10, 8));
                Set(app, PageTitleFontSizeKey, 18d);
                Set(app, SectionTitleFontSizeKey, 13d);
                Set(app, MetricValueFontSizeKey, 15d);
                Set(app, ContentMarginKey, new Thickness(8, 6, 8, 6));
                Set(app, FormLabelMinWidthKey, 88d);
                Set(app, DataGridRowHeightKey, 26d);
                break;

            case LayoutBreakpoint.Medium:
                Set(app, InputMinHeightKey, 30d);
                Set(app, InputPaddingKey, new Thickness(10, 4, 10, 4));
                Set(app, InputFontSizeKey, 12.5d);
                Set(app, CompactInputMinHeightKey, 26d);
                Set(app, CompactInputPaddingKey, new Thickness(6, 2, 6, 2));
                Set(app, ComboMinHeightKey, 30d);
                Set(app, ButtonPaddingKey, new Thickness(12, 5, 12, 5));
                Set(app, ButtonMinHeightKey, 30d);
                Set(app, CardPaddingKey, new Thickness(12, 10, 12, 10));
                Set(app, FilterBarPaddingKey, new Thickness(12, 10, 12, 10));
                Set(app, PageTitleFontSizeKey, 20d);
                Set(app, SectionTitleFontSizeKey, 14d);
                Set(app, MetricValueFontSizeKey, 17d);
                Set(app, ContentMarginKey, new Thickness(10, 8, 10, 8));
                Set(app, FormLabelMinWidthKey, 100d);
                Set(app, DataGridRowHeightKey, 28d);
                break;

            default:
                Set(app, InputMinHeightKey, 32d);
                Set(app, InputPaddingKey, new Thickness(10, 5, 10, 5));
                Set(app, InputFontSizeKey, 13d);
                Set(app, CompactInputMinHeightKey, 26d);
                Set(app, CompactInputPaddingKey, new Thickness(6, 2, 6, 2));
                Set(app, ComboMinHeightKey, 32d);
                Set(app, ButtonPaddingKey, new Thickness(14, 6, 14, 6));
                Set(app, ButtonMinHeightKey, 32d);
                Set(app, CardPaddingKey, new Thickness(14, 12, 14, 12));
                Set(app, FilterBarPaddingKey, new Thickness(12, 10, 12, 10));
                Set(app, PageTitleFontSizeKey, 22d);
                Set(app, SectionTitleFontSizeKey, 14.5d);
                Set(app, MetricValueFontSizeKey, 18d);
                Set(app, ContentMarginKey, new Thickness(12, 8, 12, 8));
                Set(app, FormLabelMinWidthKey, 110d);
                Set(app, DataGridRowHeightKey, 30d);
                break;
        }
    }

    private static void Set(Application app, string key, object value) =>
        app.Resources[key] = value;

    private static void SetIfMissing(Application app, string key, object value)
    {
        if (!app.Resources.Contains(key))
            app.Resources[key] = value;
    }
}
