using System.Windows;
using System.Windows.Media;

namespace GlueDock;

// GlueDock UI rule:
// DockDialogTheme is the single central definition for GlueDock settings/dialog layout dimensions.
// All core dialogs, widget settings, companion settings and future configuration windows must use these values
// unless a technically mandatory reason requires a documented exception.
public static class DockDialogTheme
{
    public const double DialogWidth = 540;
    public const double DialogMinWidth = 500;

    public static readonly GridLength LabelColumnWidth =
        new(
            150);

    public static readonly GridLength SecondaryColumnWidth =
        new(
            135);

    public const double SliderWidth = 105;

    public static readonly GridLength SliderColumnWidth =
        new(
            SliderWidth);

    public const double StandardControlHeight = 30;
    public const double ColorPreviewWidth = 38;
    public const double ColorPreviewHeight = 24;
    public const double ColorButtonWidth = 112;
    public const double CompactButtonWidth = 34;
    public const double CompactButtonHeight = 26;

    public const double StandardCornerRadiusValue = 4;

    public static readonly CornerRadius StandardCornerRadius =
        new(
            StandardCornerRadiusValue);

    public const double StandardRowBottomMargin = 8;

    // GlueDock UI rule: The central Settings Hub uses the existing 220 DIP search width
    // plus the established dialog margins for its navigation pane.
    public const double SettingsHubNavigationPaneWidthValue = 250;

    public static readonly GridLength SettingsHubNavigationPaneWidth =
        new(
            SettingsHubNavigationPaneWidthValue);

    public const double SettingsHubDialogWidth =
        DialogWidth +
        SettingsHubNavigationPaneWidthValue;

    public const double SettingsHubWidgetGroupBorderThickness = 1;
    public const double SettingsHubWidgetSectionIndent = 12;

    public static readonly Brush SettingsSearchHighlightBrush =
        Brushes.DarkGoldenrod;
}

public sealed record DockDialogThemePalette(
    Brush WindowBackgroundBrush,
    Brush ControlBackgroundBrush,
    Brush ControlBorderBrush,
    Brush TextBrush,
    Brush ControlTextBrush,
    Brush PopupBackgroundBrush,
    Brush PopupTextBrush,
    Brush PopupHighlightBrush,
    Brush PopupHighlightTextBrush,
    Style ComboBoxStyle,
    Style ComboBoxItemStyle);
