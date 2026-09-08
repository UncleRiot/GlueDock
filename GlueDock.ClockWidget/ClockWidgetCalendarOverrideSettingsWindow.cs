using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

using GlueDock;

namespace GlueDock.ClockWidget;

// GlueDock rule: Color selection must use only System.Windows.Forms.ColorDialog.
// GlueDock rule: Windows must remain draggable from their free edge areas unless a technically mandatory reason prevents it.
public sealed class ClockWidgetCalendarOverrideSettingsWindow : DockSettingsSectionControl
{
    private readonly ClockWidgetCalendarOverrideSettings _settings;
    private readonly IReadOnlyList<GlueDockWidgetThemeAppearance> _themes;
    private readonly GlueDockWidgetAppearance _hostAppearance;
    private readonly Color _generalEventColor;
    private readonly Action _saveSettings;

    private Brush _windowBackgroundBrush;
    private Brush _controlBackgroundBrush;
    private Brush _controlBorderBrush;
    private Brush _textBrush;

    private readonly Border _windowBorder;
    private readonly Border _glassSurfaceBorder;
    private readonly Border _glassHighlightBorder;
    private readonly ClockWidgetNativeBackdropHost _nativeBackdropHost;
    private readonly ScrollViewer _settingsScrollViewer;
    private bool _initialBackdropReady;
    private double _pendingBlurRadius;
    private bool _loading;

    private readonly CheckBox _themeUseGeneralCheckBox;
    private readonly ComboBox _themeComboBox;

    private readonly CheckBox _opacityUseGeneralCheckBox;
    private readonly Slider _opacitySlider;
    private readonly TextBlock _opacityValueText;

    private readonly CheckBox _blurUseGeneralCheckBox;
    private readonly Slider _blurSlider;
    private readonly TextBlock _blurValueText;

    private readonly CheckBox _borderUseGeneralCheckBox;
    private readonly Border _borderColorPreview;
    private readonly Button _borderColorButton;
    private readonly Slider _borderThicknessSlider;
    private readonly TextBlock _borderThicknessValueText;

    private readonly CheckBox _cornerRadiusUseGeneralCheckBox;
    private readonly Slider _cornerRadiusSlider;
    private readonly TextBlock _cornerRadiusValueText;

    private readonly CheckBox _textColorUseGeneralCheckBox;
    private readonly Border _textColorPreview;
    private readonly Button _textColorButton;

    private readonly CheckBox _eventColorUseGeneralCheckBox;
    private readonly Border _eventColorPreview;
    private readonly Button _eventColorButton;

    public ClockWidgetCalendarOverrideSettingsWindow(
        ClockWidgetCalendarOverrideSettings settings,
        IReadOnlyList<GlueDockWidgetThemeAppearance> themes,
        GlueDockWidgetAppearance hostAppearance,
        Color generalEventColor,
        Action saveSettings)
    {
        _settings =
            settings;

        _themes =
            themes;

        _hostAppearance =
            hostAppearance;

        _generalEventColor =
            generalEventColor;

        _saveSettings =
            saveSettings;

        GlueDockWidgetAppearance effectiveAppearance =
            CreateEffectiveAppearance(
                hostAppearance);

        _windowBackgroundBrush =
            effectiveAppearance.DockBackgroundBrush.Clone();

        _controlBackgroundBrush =
            effectiveAppearance.DockItemBackgroundBrush.Clone();

        _controlBorderBrush =
            effectiveAppearance.DockItemBorderBrush.Clone();

        _textBrush =
            effectiveAppearance.DockTextBrush.Clone();

        Title =
            "Calendar settings (overrides)";

        double availableWidth =
            Math.Max(
                360,
                SystemParameters.WorkArea.Width * 0.92);

        Width =
            Math.Min(
                540,
                availableWidth);

        MinWidth =
            Math.Min(
                500,
                Width);

        SizeToContent =
            SizeToContent.Height;

        MaxHeight =
            SystemParameters.WorkArea.Height * 0.9;

        WindowStartupLocation =
            WindowStartupLocation.CenterOwner;

        ResizeMode =
            ResizeMode.NoResize;

        ShowInTaskbar =
            false;

        WindowStyle =
            WindowStyle.None;

        AllowsTransparency =
            true;

        Background =
            Brushes.Transparent;

        Foreground =
            _textBrush;

        _windowBorder =
            new Border
            {
                CornerRadius =
                    new CornerRadius(
                        Math.Max(
                            0,
                            effectiveAppearance.GlassCornerRadius)),
                Background =
                    _windowBackgroundBrush,
                BorderBrush =
                    _controlBorderBrush,
                BorderThickness =
                    new Thickness(
                        1),
                Padding =
                    new Thickness(
                        0)
            };

        _windowBorder.MouseLeftButtonDown +=
            WindowDragMouseLeftButtonDown;

        _glassSurfaceBorder =
            new Border
            {
                CornerRadius =
                    _windowBorder.CornerRadius,
                Background =
                    Brushes.Transparent,
                IsHitTestVisible =
                    false,
                Visibility =
                    Visibility.Collapsed
            };

        _glassHighlightBorder =
            new Border
            {
                CornerRadius =
                    _windowBorder.CornerRadius,
                Background =
                    Brushes.Transparent,
                BorderBrush =
                    Brushes.Transparent,
                BorderThickness =
                    new Thickness(
                        0),
                IsHitTestVisible =
                    false,
                Visibility =
                    Visibility.Collapsed
            };

        StackPanel content =
            new()
            {
                Margin =
                    new Thickness(
                        16,
                        8,
                        16,
                        16)
            };

        (_themeUseGeneralCheckBox, _themeComboBox) =
            CreateThemeRow(
                content);

        (_opacityUseGeneralCheckBox, _opacitySlider, _opacityValueText) =
            CreateSliderRow(
                content,
                "Opacity",
                0.10,
                1.00,
                0.05,
                FormatOpacityValue);

        (_blurUseGeneralCheckBox, _blurSlider, _blurValueText) =
            CreateSliderRow(
                content,
                "Blur",
                0,
                100,
                1,
                FormatIntegerValue);

        (
            _borderUseGeneralCheckBox,
            _borderColorPreview,
            _borderColorButton,
            _borderThicknessSlider,
            _borderThicknessValueText
        ) =
            CreateBorderRow(
                content);

        (
            _cornerRadiusUseGeneralCheckBox,
            _cornerRadiusSlider,
            _cornerRadiusValueText
        ) =
            CreateSliderRow(
                content,
                "Corner radius",
                0,
                80,
                1,
                FormatIntegerValue);

        (
            _textColorUseGeneralCheckBox,
            _textColorPreview,
            _textColorButton
        ) =
            CreateColorRow(
                content,
                "Text color");

        (
            _eventColorUseGeneralCheckBox,
            _eventColorPreview,
            _eventColorButton
        ) =
            CreateColorRow(
                content,
                "Event color");

        Button resetButton =
            CreateThemedButton(
                "Reset all overrides");

        resetButton.HorizontalAlignment =
            HorizontalAlignment.Left;

        resetButton.Width =
            150;

        resetButton.Margin =
            new Thickness(
                0,
                12,
                0,
                0);

        resetButton.Click +=
            (_, _) =>
            {
                _settings.Reset();
                RefreshControls();
                SaveAndApply();
            };

        content.Children.Add(
            resetButton);

        _settingsScrollViewer =
            new ScrollViewer
            {
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Disabled,
                Background =
                    Brushes.Transparent,
                Content =
                    content
            };

        Grid titleBar =
            CreateTitleBar();

        Grid layout =
            new();

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        Grid.SetRow(
            titleBar,
            0);

        Grid.SetRow(
            _settingsScrollViewer,
            1);

        layout.Children.Add(
            titleBar);

        layout.Children.Add(
            _settingsScrollViewer);

        Grid chrome =
            new();

        chrome.Children.Add(
            _glassSurfaceBorder);

        chrome.Children.Add(
            _glassHighlightBorder);

        chrome.Children.Add(
            layout);

        _windowBorder.Child =
            chrome;

        Content =
            _windowBorder;

        _nativeBackdropHost =
            new ClockWidgetNativeBackdropHost(
                this);

        _pendingBlurRadius =
            effectiveAppearance.BlurRadius;

        ApplyHostWindowMaterial(
            effectiveAppearance);

        Loaded +=
            async (_, _) =>
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(
                        650));

                if (!IsVisible)
                {
                    return;
                }

                _initialBackdropReady =
                    true;

                bool blurApplied =
                    _nativeBackdropHost.SetBlur(
                        _pendingBlurRadius);

                _nativeBackdropHost.Sync();

                ClockWidgetLog.Write(
                    "Widget.Clock.CalendarOverrideSettings",
                    $"Backdrop initialized; Theme={effectiveAppearance.ThemeName}; Blur={_pendingBlurRadius:0.##}; Applied={blurApplied}; Width={ActualWidth:0.0}; Height={ActualHeight:0.0}");
            };

        HookEvents();
        RefreshControls();
    }

    public FrameworkElement CreateEmbeddedSettingsContent()
    {
        if (_settingsScrollViewer.Content is not FrameworkElement content)
        {
            throw new InvalidOperationException(
                "Settings content is not available for embedding.");
        }

        _settingsScrollViewer.Content =
            null;

        content.Margin =
            new Thickness(
                0);

        Content =
            content;

        return this;
    }


    private (CheckBox UseGeneral, ComboBox Value) CreateThemeRow(
        Panel parent)
    {
        Grid row =
            CreateRowGrid(
                "Theme");

        CheckBox useGeneral =
            CreateUseGeneralCheckBox();

        Brush comboBoxTextBrush =
            GetContrastingTextBrush(
                _controlBackgroundBrush,
                _textBrush);

        ComboBox comboBox =
            new()
            {
                Width = 210,
                HorizontalAlignment =
                    HorizontalAlignment.Left,
                Background =
                    _controlBackgroundBrush,
                Foreground =
                    comboBoxTextBrush,
                BorderBrush =
                    _controlBorderBrush,
                DisplayMemberPath =
                    nameof(
                        GlueDockWidgetThemeAppearance.DisplayName),
                SelectedValuePath =
                    nameof(
                        GlueDockWidgetThemeAppearance.ThemeName),
                ItemsSource =
                    _themes
            };

        comboBox.ItemContainerStyle =
            CreateComboBoxItemStyle();

        Grid.SetColumn(
            useGeneral,
            1);

        Grid.SetColumn(
            comboBox,
            2);

        row.Children.Add(
            useGeneral);

        row.Children.Add(
            comboBox);

        parent.Children.Add(
            row);

        return (useGeneral, comboBox);
    }

    private (CheckBox UseGeneral, Slider Slider, TextBlock ValueText) CreateSliderRow(
        Panel parent,
        string label,
        double minimum,
        double maximum,
        double tickFrequency,
        Func<double, string> valueFormatter)
    {
        Grid row =
            CreateRowGrid(
                label);

        Slider slider =
            new()
            {
                Width =
                    DockDialogTheme.SliderWidth,
                Minimum =
                    minimum,
                Maximum =
                    maximum,
                TickFrequency =
                    tickFrequency,
                IsSnapToTickEnabled =
                    true,
                Foreground =
                    _textBrush,
                HorizontalAlignment =
                    HorizontalAlignment.Left,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock valueText =
            new()
            {
                Width = 52,
                Margin =
                    new Thickness(
                        8,
                        0,
                        0,
                        0),
                VerticalAlignment =
                    VerticalAlignment.Center,
                Foreground =
                    _textBrush,
                Text =
                    valueFormatter(
                        minimum)
            };

        StackPanel values =
            new()
            {
                Orientation =
                    Orientation.Horizontal,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        values.Children.Add(
            slider);

        values.Children.Add(
            valueText);

        CheckBox useGeneral =
            CreateUseGeneralCheckBox();

        Grid.SetColumn(
            values,
            1);

        Grid.SetColumn(
            useGeneral,
            2);

        row.Children.Add(
            values);

        row.Children.Add(
            useGeneral);

        parent.Children.Add(
            row);

        return (useGeneral, slider, valueText);
    }

    private (
        CheckBox UseGeneral,
        Border ColorPreview,
        Button ColorButton,
        Slider ThicknessSlider,
        TextBlock ThicknessValueText
    ) CreateBorderRow(
        Panel parent)
    {
        Grid row =
            CreateRowGrid(
                "Border");

        StackPanel values =
            new()
            {
                Orientation =
                    Orientation.Vertical
            };

        StackPanel colorRow =
            new()
            {
                Orientation =
                    Orientation.Horizontal
            };

        Border colorPreview =
            CreateColorPreview();

        Button colorButton =
            CreateThemedButton(
                "Choose color...");

        colorButton.Width =
            DockDialogTheme.ColorButtonWidth;

        colorButton.Margin =
            new Thickness(
                10,
                0,
                0,
                0);

        colorRow.Children.Add(
            colorPreview);

        colorRow.Children.Add(
            colorButton);

        StackPanel thicknessRow =
            new()
            {
                Orientation =
                    Orientation.Horizontal,
                Margin =
                    new Thickness(
                        0,
                        8,
                        0,
                        0)
            };

        Slider thicknessSlider =
            new()
            {
                Width =
                    DockDialogTheme.SliderWidth,
                Minimum = 0,
                Maximum = 12,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                Foreground =
                    _textBrush,
                HorizontalAlignment =
                    HorizontalAlignment.Left
            };

        TextBlock thicknessValueText =
            new()
            {
                Width = 52,
                Margin =
                    new Thickness(
                        8,
                        0,
                        0,
                        0),
                VerticalAlignment =
                    VerticalAlignment.Center,
                Foreground =
                    _textBrush
            };

        thicknessRow.Children.Add(
            thicknessSlider);

        thicknessRow.Children.Add(
            thicknessValueText);

        values.Children.Add(
            colorRow);

        values.Children.Add(
            thicknessRow);

        CheckBox useGeneral =
            CreateUseGeneralCheckBox();

        Grid.SetColumn(
            values,
            1);

        Grid.SetColumn(
            useGeneral,
            2);

        row.Children.Add(
            values);

        row.Children.Add(
            useGeneral);

        parent.Children.Add(
            row);

        return (
            useGeneral,
            colorPreview,
            colorButton,
            thicknessSlider,
            thicknessValueText);
    }

    private (CheckBox UseGeneral, Border ColorPreview, Button ColorButton) CreateColorRow(
        Panel parent,
        string label)
    {
        Grid row =
            CreateRowGrid(
                label);

        CheckBox useGeneral =
            CreateUseGeneralCheckBox();

        StackPanel values =
            new()
            {
                Orientation =
                    Orientation.Horizontal
            };

        Border colorPreview =
            CreateColorPreview();

        Button colorButton =
            CreateThemedButton(
                "Choose color...");

        colorButton.Width =
            112;

        colorButton.Margin =
            new Thickness(
                10,
                0,
                0,
                0);

        values.Children.Add(
            colorPreview);

        values.Children.Add(
            colorButton);

        Grid.SetColumn(
            useGeneral,
            1);

        Grid.SetColumn(
            values,
            2);

        row.Children.Add(
            useGeneral);

        row.Children.Add(
            values);

        parent.Children.Add(
            row);

        return (
            useGeneral,
            colorPreview,
            colorButton);
    }

    private Grid CreateRowGrid(
        string label)
    {
        Grid row =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        8)
            };

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    DockDialogTheme.LabelColumnWidth
            });

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        DockDialogTheme.SliderWidth +
                        60)
            });

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        TextBlock labelText =
            new()
            {
                Text =
                    label,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Foreground =
                    _textBrush,
                FontWeight =
                    FontWeights.SemiBold
            };

        Grid.SetColumn(
            labelText,
            0);

        row.Children.Add(
            labelText);

        return row;
    }

    private CheckBox CreateUseGeneralCheckBox()
    {
        return new CheckBox
        {
            Content =
                "Use general setting",
            VerticalAlignment =
                VerticalAlignment.Center,
            Foreground =
                _textBrush
        };
    }

    private Border CreateColorPreview()
    {
        return new Border
        {
            Width =
                DockDialogTheme.ColorPreviewWidth,
            Height =
                DockDialogTheme.ColorPreviewHeight,
            CornerRadius =
                DockDialogTheme.StandardCornerRadius,
            BorderBrush =
                _controlBorderBrush,
            BorderThickness =
                new Thickness(
                    1),
            HorizontalAlignment =
                HorizontalAlignment.Left
        };
    }

    private Button CreateThemedButton(
        object content)
    {
        Button button =
            new()
            {
                Content =
                    content,
                Height = 30,
                Padding =
                    new Thickness(
                        8,
                        4,
                        8,
                        4)
            };

        ApplyThemedButtonStyle(
            button);

        return button;
    }

    private static ControlTemplate CreateButtonTemplate()
    {
        FrameworkElementFactory border =
            new(
                typeof(
                    Border));

        border.SetBinding(
            Border.BackgroundProperty,
            new System.Windows.Data.Binding(
                nameof(
                    Control.Background))
            {
                RelativeSource =
                    new System.Windows.Data.RelativeSource(
                        System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });

        border.SetBinding(
            Border.BorderBrushProperty,
            new System.Windows.Data.Binding(
                nameof(
                    Control.BorderBrush))
            {
                RelativeSource =
                    new System.Windows.Data.RelativeSource(
                        System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });

        border.SetBinding(
            Border.BorderThicknessProperty,
            new System.Windows.Data.Binding(
                nameof(
                    Control.BorderThickness))
            {
                RelativeSource =
                    new System.Windows.Data.RelativeSource(
                        System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });

        border.SetValue(
            Border.CornerRadiusProperty,
            new CornerRadius(
                2));

        FrameworkElementFactory presenter =
            new(
                typeof(
                    ContentPresenter));

        presenter.SetValue(
            FrameworkElement.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);

        presenter.SetValue(
            FrameworkElement.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        presenter.SetBinding(
            TextElement.ForegroundProperty,
            new System.Windows.Data.Binding(
                nameof(
                    Control.Foreground))
            {
                RelativeSource =
                    new System.Windows.Data.RelativeSource(
                        System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });

        border.AppendChild(
            presenter);

        return new ControlTemplate(
            typeof(
                Button))
        {
            VisualTree =
                border
        };
    }

    private void RestyleButtons(
        DependencyObject? root)
    {
        if (root is null)
        {
            return;
        }

        if (root is Button button)
        {
            ApplyThemedButtonStyle(
                button);
        }

        int childCount =
            VisualTreeHelper.GetChildrenCount(
                root);

        for (int childIndex = 0;
             childIndex < childCount;
             childIndex++)
        {
            RestyleButtons(
                VisualTreeHelper.GetChild(
                    root,
                    childIndex));
        }
    }

    private void ApplyThemedButtonStyle(
        Button button)
    {
        Brush normalForeground =
            GetContrastingTextBrush(
                _controlBackgroundBrush,
                _textBrush);

        Brush hoverBackground =
            _textBrush;

        Brush hoverForeground =
            GetContrastingTextBrush(
                hoverBackground,
                _controlBackgroundBrush);

        Style style =
            new(
                typeof(
                    Button));

        style.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                _controlBackgroundBrush));

        style.Setters.Add(
            new Setter(
                Control.ForegroundProperty,
                normalForeground));

        style.Setters.Add(
            new Setter(
                Control.BorderBrushProperty,
                _controlBorderBrush));

        style.Setters.Add(
            new Setter(
                Control.BorderThicknessProperty,
                new Thickness(
                    1)));

        style.Setters.Add(
            new Setter(
                Control.TemplateProperty,
                CreateButtonTemplate()));

        Trigger hoverTrigger =
            new()
            {
                Property =
                    UIElement.IsMouseOverProperty,
                Value =
                    true
            };

        hoverTrigger.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                hoverBackground));

        hoverTrigger.Setters.Add(
            new Setter(
                Control.ForegroundProperty,
                hoverForeground));

        style.Triggers.Add(
            hoverTrigger);

        Trigger pressedTrigger =
            new()
            {
                Property =
                    ButtonBase.IsPressedProperty,
                Value =
                    true
            };

        pressedTrigger.Setters.Add(
            new Setter(
                UIElement.OpacityProperty,
                0.78));

        style.Triggers.Add(
            pressedTrigger);

        button.Style =
            style;
    }

    private Brush GetContrastingTextBrush(
        Brush backgroundBrush,
        Brush fallbackBrush)
    {
        return GlueDockWidgetUiContrast.GetContrastingTextBrush(
            backgroundBrush,
            _windowBackgroundBrush,
            fallbackBrush);
    }

    private Style CreateComboBoxItemStyle()
    {
        Brush itemForeground =
            GetContrastingTextBrush(
                _controlBackgroundBrush,
                _textBrush);

        Style style =
            new(
                typeof(
                    ComboBoxItem));

        style.Setters.Add(
            new Setter(
                Control.BackgroundProperty,
                _controlBackgroundBrush));

        style.Setters.Add(
            new Setter(
                Control.ForegroundProperty,
                itemForeground));

        style.Setters.Add(
            new Setter(
                Control.PaddingProperty,
                new Thickness(
                    6,
                    4,
                    6,
                    4)));

        return style;
    }

    private void HookEvents()
    {
        _themeUseGeneralCheckBox.Checked +=
            (_, _) =>
                SetThemeUseGeneral(
                    true);

        _themeUseGeneralCheckBox.Unchecked +=
            (_, _) =>
                SetThemeUseGeneral(
                    false);

        _themeComboBox.SelectionChanged +=
            (_, _) =>
            {
                if (_loading ||
                    _themeComboBox.SelectedValue is not string themeName)
                {
                    return;
                }

                _settings.ThemeName =
                    themeName;

                SaveAndApply();
            };

        _opacityUseGeneralCheckBox.Checked +=
            (_, _) =>
                SetValueUseGeneral(
                    OverrideValueKind.Opacity,
                    true);

        _opacityUseGeneralCheckBox.Unchecked +=
            (_, _) =>
                SetValueUseGeneral(
                    OverrideValueKind.Opacity,
                    false);

        _blurUseGeneralCheckBox.Checked +=
            (_, _) =>
                SetValueUseGeneral(
                    OverrideValueKind.Blur,
                    true);

        _blurUseGeneralCheckBox.Unchecked +=
            (_, _) =>
                SetValueUseGeneral(
                    OverrideValueKind.Blur,
                    false);

        _cornerRadiusUseGeneralCheckBox.Checked +=
            (_, _) =>
                SetValueUseGeneral(
                    OverrideValueKind.CornerRadius,
                    true);

        _cornerRadiusUseGeneralCheckBox.Unchecked +=
            (_, _) =>
                SetValueUseGeneral(
                    OverrideValueKind.CornerRadius,
                    false);

        _borderUseGeneralCheckBox.Checked +=
            (_, _) =>
                SetBorderUseGeneral(
                    true);

        _borderUseGeneralCheckBox.Unchecked +=
            (_, _) =>
                SetBorderUseGeneral(
                    false);

        _textColorUseGeneralCheckBox.Checked +=
            (_, _) =>
                SetColorUseGeneral(
                    OverrideColorKind.Text,
                    true);

        _textColorUseGeneralCheckBox.Unchecked +=
            (_, _) =>
                SetColorUseGeneral(
                    OverrideColorKind.Text,
                    false);

        _eventColorUseGeneralCheckBox.Checked +=
            (_, _) =>
                SetColorUseGeneral(
                    OverrideColorKind.Event,
                    true);

        _eventColorUseGeneralCheckBox.Unchecked +=
            (_, _) =>
                SetColorUseGeneral(
                    OverrideColorKind.Event,
                    false);

        _borderColorButton.Click +=
            (_, _) =>
                PickColor(
                    OverrideColorKind.Border);

        _textColorButton.Click +=
            (_, _) =>
                PickColor(
                    OverrideColorKind.Text);

        _eventColorButton.Click +=
            (_, _) =>
                PickColor(
                    OverrideColorKind.Event);

        _opacitySlider.ValueChanged +=
            (_, _) =>
                CommitSliderValue(
                    _opacitySlider,
                    OverrideValueKind.Opacity);

        _blurSlider.ValueChanged +=
            (_, _) =>
                CommitSliderValue(
                    _blurSlider,
                    OverrideValueKind.Blur);

        _borderThicknessSlider.ValueChanged +=
            (_, _) =>
                CommitSliderValue(
                    _borderThicknessSlider,
                    OverrideValueKind.BorderThickness);

        _cornerRadiusSlider.ValueChanged +=
            (_, _) =>
                CommitSliderValue(
                    _cornerRadiusSlider,
                    OverrideValueKind.CornerRadius);
    }

    private void RefreshControls()
    {
        _loading =
            true;

        try
        {
            _themeUseGeneralCheckBox.IsChecked =
                !_settings.ThemeOverrideEnabled;

            _themeComboBox.IsEnabled =
                true;

            _themeComboBox.IsHitTestVisible =
                _settings.ThemeOverrideEnabled;

            _themeComboBox.Focusable =
                _settings.ThemeOverrideEnabled;

            _themeComboBox.Opacity =
                _settings.ThemeOverrideEnabled
                    ? 1
                    : 0.72;

            string themeName =
                _settings.ThemeOverrideEnabled
                    ? _settings.ThemeName
                    : _hostAppearance.ThemeName;

            _themeComboBox.SelectedValue =
                themeName;

            _opacityUseGeneralCheckBox.IsChecked =
                !_settings.OpacityOverrideEnabled;

            _opacitySlider.IsEnabled =
                _settings.OpacityOverrideEnabled;

            _opacitySlider.Value =
                Math.Clamp(
                    _settings.OpacityOverrideEnabled
                        ? _settings.Opacity
                        : _hostAppearance.Opacity,
                    _opacitySlider.Minimum,
                    _opacitySlider.Maximum);

            _opacityValueText.Text =
                FormatOpacityValue(
                    _opacitySlider.Value);

            _blurUseGeneralCheckBox.IsChecked =
                !_settings.BlurOverrideEnabled;

            _blurSlider.IsEnabled =
                _settings.BlurOverrideEnabled;

            _blurSlider.Value =
                Math.Clamp(
                    _settings.BlurOverrideEnabled
                        ? _settings.BlurRadius
                        : _hostAppearance.BlurRadius,
                    _blurSlider.Minimum,
                    _blurSlider.Maximum);

            _blurValueText.Text =
                FormatIntegerValue(
                    _blurSlider.Value);

            _borderUseGeneralCheckBox.IsChecked =
                !_settings.BorderOverrideEnabled;

            _borderColorButton.IsEnabled =
                true;

            _borderColorButton.IsHitTestVisible =
                _settings.BorderOverrideEnabled;

            _borderColorButton.Focusable =
                _settings.BorderOverrideEnabled;

            _borderColorButton.Opacity =
                _settings.BorderOverrideEnabled
                    ? 1
                    : 0.72;

            _borderThicknessSlider.IsEnabled =
                _settings.BorderOverrideEnabled;

            Color generalBorderColor =
                _hostAppearance.DockBorderColor;

            Color borderColor =
                _settings.BorderOverrideEnabled
                    ? ParseColor(
                        _settings.BorderColor,
                        generalBorderColor)
                    : generalBorderColor;

            _borderColorPreview.Background =
                new SolidColorBrush(
                    borderColor);

            _borderThicknessSlider.Value =
                Math.Clamp(
                    _settings.BorderOverrideEnabled
                        ? _settings.BorderThickness
                        : _hostAppearance.DockBorderEnabled
                            ? 1
                            : 0,
                    _borderThicknessSlider.Minimum,
                    _borderThicknessSlider.Maximum);

            _borderThicknessValueText.Text =
                FormatIntegerValue(
                    _borderThicknessSlider.Value);

            _cornerRadiusUseGeneralCheckBox.IsChecked =
                !_settings.CornerRadiusOverrideEnabled;

            _cornerRadiusSlider.IsEnabled =
                _settings.CornerRadiusOverrideEnabled;

            _cornerRadiusSlider.Value =
                Math.Clamp(
                    _settings.CornerRadiusOverrideEnabled
                        ? _settings.CornerRadius
                        : _hostAppearance.GlassCornerRadius,
                    _cornerRadiusSlider.Minimum,
                    _cornerRadiusSlider.Maximum);

            _cornerRadiusValueText.Text =
                FormatIntegerValue(
                    _cornerRadiusSlider.Value);

            _textColorUseGeneralCheckBox.IsChecked =
                !_settings.TextColorOverrideEnabled;

            _textColorButton.IsEnabled =
                true;

            _textColorButton.IsHitTestVisible =
                _settings.TextColorOverrideEnabled;

            _textColorButton.Focusable =
                _settings.TextColorOverrideEnabled;

            _textColorButton.Opacity =
                _settings.TextColorOverrideEnabled
                    ? 1
                    : 0.72;

            Color generalTextColor =
                GetBrushColor(
                    _hostAppearance.DockTextBrush,
                    Colors.White);

            Color textColor =
                _settings.TextColorOverrideEnabled
                    ? ParseColor(
                        _settings.TextColor,
                        generalTextColor)
                    : generalTextColor;

            _textColorPreview.Background =
                new SolidColorBrush(
                    textColor);

            _eventColorUseGeneralCheckBox.IsChecked =
                !_settings.EventColorOverrideEnabled;

            _eventColorButton.IsEnabled =
                true;

            _eventColorButton.IsHitTestVisible =
                _settings.EventColorOverrideEnabled;

            _eventColorButton.Focusable =
                _settings.EventColorOverrideEnabled;

            _eventColorButton.Opacity =
                _settings.EventColorOverrideEnabled
                    ? 1
                    : 0.72;

            Color eventColor =
                _settings.EventColorOverrideEnabled
                    ? ParseColor(
                        _settings.EventColor,
                        _generalEventColor)
                    : _generalEventColor;

            _eventColorPreview.Background =
                new SolidColorBrush(
                    eventColor);
        }
        finally
        {
            _loading =
                false;
        }
    }

    private void SetThemeUseGeneral(
        bool useGeneral)
    {
        if (_loading)
        {
            return;
        }

        _settings.ThemeOverrideEnabled =
            !useGeneral;

        if (!useGeneral &&
            string.IsNullOrWhiteSpace(
                _settings.ThemeName))
        {
            _settings.ThemeName =
                _hostAppearance.ThemeName;
        }

        RefreshControls();
        SaveAndApply();
    }

    private void SetValueUseGeneral(
        OverrideValueKind kind,
        bool useGeneral)
    {
        if (_loading)
        {
            return;
        }

        switch (kind)
        {
            case OverrideValueKind.Opacity:
                _settings.OpacityOverrideEnabled =
                    !useGeneral;

                if (!useGeneral)
                {
                    _settings.Opacity =
                        _hostAppearance.Opacity;
                }

                break;

            case OverrideValueKind.Blur:
                _settings.BlurOverrideEnabled =
                    !useGeneral;

                if (!useGeneral)
                {
                    _settings.BlurRadius =
                        _hostAppearance.BlurRadius;
                }

                break;

            case OverrideValueKind.CornerRadius:
                _settings.CornerRadiusOverrideEnabled =
                    !useGeneral;

                if (!useGeneral)
                {
                    _settings.CornerRadius =
                        _hostAppearance.GlassCornerRadius;
                }

                break;
        }

        RefreshControls();
        SaveAndApply();
    }

    private void SetBorderUseGeneral(
        bool useGeneral)
    {
        if (_loading)
        {
            return;
        }

        _settings.BorderOverrideEnabled =
            !useGeneral;

        if (!useGeneral)
        {
            _settings.BorderColor =
                ToColorString(
                    _hostAppearance.DockBorderColor);

            _settings.BorderThickness =
                _hostAppearance.DockBorderEnabled
                    ? 1
                    : 0;
        }

        RefreshControls();
        SaveAndApply();
    }

    private void SetColorUseGeneral(
        OverrideColorKind kind,
        bool useGeneral)
    {
        if (_loading)
        {
            return;
        }

        if (kind == OverrideColorKind.Text)
        {
            _settings.TextColorOverrideEnabled =
                !useGeneral;

            if (!useGeneral)
            {
                _settings.TextColor =
                    GetBrushColorString(
                        _hostAppearance.DockTextBrush,
                        "#FFFFFFFF");
            }
        }
        else if (kind == OverrideColorKind.Event)
        {
            _settings.EventColorOverrideEnabled =
                !useGeneral;

            if (!useGeneral)
            {
                _settings.EventColor =
                    ToColorString(
                        _generalEventColor);
            }
        }

        RefreshControls();
        SaveAndApply();
    }

    private void CommitSliderValue(
        Slider slider,
        OverrideValueKind kind)
    {
        if (_loading)
        {
            return;
        }

        double value =
            slider.Value;

        switch (kind)
        {
            case OverrideValueKind.Opacity:
                _settings.Opacity =
                    Math.Clamp(
                        value,
                        0.10,
                        1.00);

                _opacityValueText.Text =
                    FormatOpacityValue(
                        _settings.Opacity);
                break;

            case OverrideValueKind.Blur:
                _settings.BlurRadius =
                    Math.Clamp(
                        value,
                        0,
                        100);

                _blurValueText.Text =
                    FormatIntegerValue(
                        _settings.BlurRadius);
                break;

            case OverrideValueKind.BorderThickness:
                _settings.BorderThickness =
                    Math.Clamp(
                        value,
                        0,
                        12);

                _borderThicknessValueText.Text =
                    FormatIntegerValue(
                        _settings.BorderThickness);
                break;

            case OverrideValueKind.CornerRadius:
                _settings.CornerRadius =
                    Math.Clamp(
                        value,
                        0,
                        80);

                _cornerRadiusValueText.Text =
                    FormatIntegerValue(
                        _settings.CornerRadius);
                break;
        }

        if (kind ==
            OverrideValueKind.Opacity)
        {
            _saveSettings();

            Dispatcher.BeginInvoke(
                ApplyCurrentAppearance,
                System.Windows.Threading.DispatcherPriority.Background);

            return;
        }

        SaveAndApply();
    }

    private void PickColor(
        OverrideColorKind kind)
    {
        Color initialColor =
            kind switch
            {
                OverrideColorKind.Border =>
                    ParseColor(
                        _settings.BorderColor,
                        _hostAppearance.DockBorderColor),
                OverrideColorKind.Text =>
                    ParseColor(
                        _settings.TextColor,
                        GetBrushColor(
                            _hostAppearance.DockTextBrush,
                            Colors.White)),
                _ =>
                    ParseColor(
                        _settings.EventColor,
                        _generalEventColor)
            };

        using WinForms.ColorDialog dialog =
            new()
            {
                FullOpen = true,
                Color =
                    Drawing.Color.FromArgb(
                        initialColor.A,
                        initialColor.R,
                        initialColor.G,
                        initialColor.B)
            };

        if (dialog.ShowDialog() !=
            WinForms.DialogResult.OK)
        {
            return;
        }

        Color selectedColor =
            Color.FromArgb(
                dialog.Color.A,
                dialog.Color.R,
                dialog.Color.G,
                dialog.Color.B);

        string colorText =
            ToColorString(
                selectedColor);

        switch (kind)
        {
            case OverrideColorKind.Border:
                _settings.BorderColor =
                    colorText;
                break;

            case OverrideColorKind.Text:
                _settings.TextColor =
                    colorText;
                break;

            case OverrideColorKind.Event:
                _settings.EventColor =
                    colorText;
                break;
        }

        RefreshControls();
        SaveAndApply();
    }


    private Grid CreateTitleBar()
    {
        Grid titleBar =
            new()
            {
                Margin =
                    new Thickness(
                        14,
                        10,
                        10,
                        0),
                Background =
                    Brushes.Transparent
            };

        titleBar.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        titleBar.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        TextBlock titleText =
            new()
            {
                Text =
                    "Calendar settings (overrides)",
                Foreground =
                    _textBrush,
                FontSize =
                    16,
                FontWeight =
                    FontWeights.SemiBold,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        Button closeButton =
            CreateThemedButton(
                "×");

        closeButton.Width =
            32;

        closeButton.Height =
            30;

        closeButton.FontSize =
            18;

        closeButton.Padding =
            new Thickness(
                0);

        closeButton.Click +=
            (_, _) =>
                Close();

        Grid.SetColumn(
            titleText,
            0);

        Grid.SetColumn(
            closeButton,
            1);

        titleBar.Children.Add(
            titleText);

        titleBar.Children.Add(
            closeButton);

        return titleBar;
    }

    private void WindowDragMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed ||
            IsInteractiveElement(
                e.OriginalSource as DependencyObject))
        {
            return;
        }

        e.Handled =
            true;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static bool IsInteractiveElement(
        DependencyObject? source)
    {
        DependencyObject? current =
            source;

        while (current is not null)
        {
            if (current is ButtonBase ||
                current is TextBoxBase ||
                current is ComboBox ||
                current is Slider ||
                current is ScrollBar ||
                current is Thumb ||
                current is CheckBox)
            {
                return true;
            }

            current =
                VisualTreeHelper.GetParent(
                    current);
        }

        return false;
    }

    private GlueDockWidgetAppearance CreateEffectiveAppearance(
        GlueDockWidgetAppearance hostAppearance)
    {
        GlueDockWidgetThemeAppearance? selectedTheme =
            null;

        if (_settings.ThemeOverrideEnabled &&
            !string.IsNullOrWhiteSpace(
                _settings.ThemeName))
        {
            selectedTheme =
                _themes.FirstOrDefault(
                    theme =>
                        string.Equals(
                            theme.ThemeName,
                            _settings.ThemeName,
                            StringComparison.OrdinalIgnoreCase));
        }

        Brush dockBackgroundBrush =
            selectedTheme?.DockBackgroundBrush.Clone() ??
            hostAppearance.DockBackgroundBrush.Clone();

        Brush itemBackgroundBrush =
            selectedTheme?.DockItemBackgroundBrush.Clone() ??
            hostAppearance.DockItemBackgroundBrush.Clone();

        Brush itemBorderBrush =
            selectedTheme?.DockItemBorderBrush.Clone() ??
            hostAppearance.DockItemBorderBrush.Clone();

        Brush textBrush =
            selectedTheme?.DockTextBrush.Clone() ??
            hostAppearance.DockTextBrush.Clone();

        double opacity =
            _settings.OpacityOverrideEnabled
                ? Math.Clamp(
                    _settings.Opacity,
                    0.10,
                    1.00)
                : selectedTheme?.Opacity ??
                  hostAppearance.Opacity;

        double blurRadius =
            _settings.BlurOverrideEnabled
                ? Math.Clamp(
                    _settings.BlurRadius,
                    0,
                    100)
                : selectedTheme?.BlurRadius ??
                  hostAppearance.BlurRadius;

        if (dockBackgroundBrush is SolidColorBrush solidDockBackgroundBrush)
        {
            Color color =
                solidDockBackgroundBrush.Color;

            dockBackgroundBrush =
                new SolidColorBrush(
                    Color.FromArgb(
                        (byte)Math.Clamp(
                            Math.Round(
                                opacity * 255),
                            0,
                            255),
                        color.R,
                        color.G,
                        color.B));
        }

        if (_settings.TextColorOverrideEnabled)
        {
            Color textColor =
                ParseColor(
                    _settings.TextColor,
                    GetBrushColor(
                        textBrush,
                        Colors.White));

            textBrush =
                new SolidColorBrush(
                    textColor);
        }

        bool borderEnabled =
            hostAppearance.DockBorderEnabled;

        Color borderColor =
            hostAppearance.DockBorderColor;

        if (_settings.BorderOverrideEnabled)
        {
            borderEnabled =
                Math.Clamp(
                    _settings.BorderThickness,
                    0,
                    12) > 0;

            borderColor =
                ParseColor(
                    _settings.BorderColor,
                    borderColor);
        }

        return new GlueDockWidgetAppearance
        {
            ThemeName =
                selectedTheme?.ThemeName ??
                hostAppearance.ThemeName,
            DockBackgroundBrush =
                dockBackgroundBrush,
            DockItemBackgroundBrush =
                itemBackgroundBrush,
            DockItemBorderBrush =
                itemBorderBrush,
            DockTextBrush =
                textBrush,
            DockBorderEnabled =
                borderEnabled,
            DockBorderColor =
                borderColor,
            Opacity =
                opacity,
            BlurRadius =
                blurRadius,
            GlassSurfaceEnabled =
                selectedTheme?.GlassSurfaceEnabled ??
                hostAppearance.GlassSurfaceEnabled,
            GlassTopColor =
                selectedTheme?.GlassTopColor ??
                hostAppearance.GlassTopColor,
            GlassBottomColor =
                selectedTheme?.GlassBottomColor ??
                hostAppearance.GlassBottomColor,
            GlassHighlightColor =
                selectedTheme?.GlassHighlightColor ??
                hostAppearance.GlassHighlightColor,
            GlassCornerRadius =
                _settings.CornerRadiusOverrideEnabled
                    ? Math.Clamp(
                        _settings.CornerRadius,
                        0,
                        80)
                    : selectedTheme?.GlassCornerRadius ??
                      hostAppearance.GlassCornerRadius,
            GlassGradientReferenceHeight =
                selectedTheme?.GlassGradientReferenceHeight ??
                hostAppearance.GlassGradientReferenceHeight,
            AvailableThemes =
                hostAppearance.AvailableThemes
        };
    }

    private void ApplyCurrentAppearance()
    {
        GlueDockWidgetAppearance appearance =
            CreateEffectiveAppearance(
                _hostAppearance);

        Brush oldWindowBackgroundBrush =
            _windowBackgroundBrush;

        Brush oldControlBackgroundBrush =
            _controlBackgroundBrush;

        Brush oldControlBorderBrush =
            _controlBorderBrush;

        Brush oldTextBrush =
            _textBrush;

        _windowBackgroundBrush =
            appearance.DockBackgroundBrush.Clone();

        _controlBackgroundBrush =
            appearance.DockItemBackgroundBrush.Clone();

        _controlBorderBrush =
            appearance.DockItemBorderBrush.Clone();

        _textBrush =
            appearance.DockTextBrush.Clone();

        Foreground =
            _textBrush;

        if (Content is DependencyObject content)
        {
            ReplaceBrushes(
                content,
                oldWindowBackgroundBrush,
                oldControlBackgroundBrush,
                oldControlBorderBrush,
                oldTextBrush);
        }

        _themeComboBox.Foreground =
            GetContrastingTextBrush(
                _controlBackgroundBrush,
                _textBrush);

        _themeComboBox.ItemContainerStyle =
            CreateComboBoxItemStyle();

        RestyleButtons(
            Content as DependencyObject);

        ApplyHostWindowMaterial(
            appearance);

        _pendingBlurRadius =
            appearance.BlurRadius;

        if (_initialBackdropReady)
        {
            _nativeBackdropHost.SetBlur(
                _pendingBlurRadius);

            _nativeBackdropHost.Sync();
        }

        ClockWidgetLog.Write(
            "Widget.Clock.CalendarOverrideSettings",
            $"Appearance updated; Theme={appearance.ThemeName}; Opacity={appearance.Opacity:0.00}; Blur={appearance.BlurRadius:0.##}; GlassSurfaceEnabled={appearance.GlassSurfaceEnabled}; CornerRadius={appearance.GlassCornerRadius:0.##}");
    }

    private void ApplyHostWindowMaterial(
        GlueDockWidgetAppearance appearance)
    {
        double cornerRadius =
            Math.Clamp(
                appearance.GlassCornerRadius,
                0,
                80);

        double borderThickness =
            _settings.BorderOverrideEnabled
                ? Math.Clamp(
                    _settings.BorderThickness,
                    0,
                    12)
                : appearance.DockBorderEnabled
                    ? 1
                    : 0;

        _windowBorder.CornerRadius =
            new CornerRadius(
                cornerRadius);

        _glassSurfaceBorder.CornerRadius =
            new CornerRadius(
                cornerRadius);

        _glassHighlightBorder.CornerRadius =
            new CornerRadius(
                cornerRadius);

        if (appearance.DockBorderEnabled)
        {
            if (appearance.GlassSurfaceEnabled)
            {
                LinearGradientBrush dockBorderBrush =
                    new()
                    {
                        StartPoint =
                            new Point(
                                0.5,
                                0),
                        EndPoint =
                            new Point(
                                0.5,
                                1)
                    };

                dockBorderBrush.GradientStops.Add(
                    new GradientStop(
                        Color.FromArgb(
                            0xC8,
                            appearance.DockBorderColor.R,
                            appearance.DockBorderColor.G,
                            appearance.DockBorderColor.B),
                        0));

                dockBorderBrush.GradientStops.Add(
                    new GradientStop(
                        Color.FromArgb(
                            0x78,
                            appearance.DockBorderColor.R,
                            appearance.DockBorderColor.G,
                            appearance.DockBorderColor.B),
                        0.55));

                dockBorderBrush.GradientStops.Add(
                    new GradientStop(
                        Color.FromArgb(
                            0x28,
                            appearance.DockBorderColor.R,
                            appearance.DockBorderColor.G,
                            appearance.DockBorderColor.B),
                        1));

                _windowBorder.BorderBrush =
                    dockBorderBrush;
            }
            else
            {
                _windowBorder.BorderBrush =
                    new SolidColorBrush(
                        appearance.DockBorderColor);
            }

            _windowBorder.BorderThickness =
                new Thickness(
                    borderThickness);
        }
        else
        {
            _windowBorder.BorderBrush =
                Brushes.Transparent;

            _windowBorder.BorderThickness =
                new Thickness(
                    0);
        }

        if (!appearance.GlassSurfaceEnabled)
        {
            _windowBorder.Background =
                _windowBackgroundBrush;

            _glassSurfaceBorder.Background =
                Brushes.Transparent;

            _glassSurfaceBorder.Visibility =
                Visibility.Collapsed;

            _glassHighlightBorder.Background =
                Brushes.Transparent;

            _glassHighlightBorder.BorderBrush =
                Brushes.Transparent;

            _glassHighlightBorder.BorderThickness =
                new Thickness(
                    0);

            _glassHighlightBorder.Visibility =
                Visibility.Collapsed;

            return;
        }

        _windowBorder.Background =
            Brushes.Transparent;

        double gradientReferenceHeight =
            Math.Max(
                1,
                appearance.GlassGradientReferenceHeight);

        LinearGradientBrush surfaceBrush =
            new(
                appearance.GlassTopColor,
                appearance.GlassBottomColor,
                new Point(
                    0,
                    0),
                new Point(
                    0,
                    gradientReferenceHeight))
            {
                MappingMode =
                    BrushMappingMode.Absolute,
                SpreadMethod =
                    GradientSpreadMethod.Pad,
                Opacity =
                    Math.Clamp(
                        appearance.Opacity,
                        0.10,
                        1.0)
            };

        LinearGradientBrush highlightBrush =
            new()
            {
                MappingMode =
                    BrushMappingMode.Absolute,
                SpreadMethod =
                    GradientSpreadMethod.Pad,
                StartPoint =
                    new Point(
                        0,
                        0),
                EndPoint =
                    new Point(
                        0,
                        gradientReferenceHeight)
            };

        highlightBrush.GradientStops.Add(
            new GradientStop(
                appearance.GlassHighlightColor,
                0));

        highlightBrush.GradientStops.Add(
            new GradientStop(
                Color.FromArgb(
                    0,
                    appearance.GlassHighlightColor.R,
                    appearance.GlassHighlightColor.G,
                    appearance.GlassHighlightColor.B),
                0.55));

        LinearGradientBrush frameBrush =
            new()
            {
                MappingMode =
                    BrushMappingMode.Absolute,
                SpreadMethod =
                    GradientSpreadMethod.Pad,
                StartPoint =
                    new Point(
                        0,
                        0),
                EndPoint =
                    new Point(
                        0,
                        gradientReferenceHeight)
            };

        frameBrush.GradientStops.Add(
            new GradientStop(
                appearance.GlassHighlightColor,
                0));

        frameBrush.GradientStops.Add(
            new GradientStop(
                Color.FromArgb(
                    (byte)(appearance.GlassHighlightColor.A * 0.35),
                    appearance.GlassHighlightColor.R,
                    appearance.GlassHighlightColor.G,
                    appearance.GlassHighlightColor.B),
                0.55));

        frameBrush.GradientStops.Add(
            new GradientStop(
                Color.FromArgb(
                    0,
                    appearance.GlassHighlightColor.R,
                    appearance.GlassHighlightColor.G,
                    appearance.GlassHighlightColor.B),
                1));

        _glassSurfaceBorder.Background =
            surfaceBrush;

        _glassSurfaceBorder.Visibility =
            Visibility.Visible;

        _glassHighlightBorder.Background =
            highlightBrush;

        _glassHighlightBorder.BorderBrush =
            frameBrush;

        _glassHighlightBorder.BorderThickness =
            new Thickness(
                1);

        _glassHighlightBorder.Visibility =
            Visibility.Visible;
    }

    private void ReplaceBrushes(
        DependencyObject root,
        Brush oldWindowBackgroundBrush,
        Brush oldControlBackgroundBrush,
        Brush oldControlBorderBrush,
        Brush oldTextBrush)
    {
        if (root is Control control)
        {
            if (ReferenceEquals(
                    control.Background,
                    oldWindowBackgroundBrush))
            {
                control.Background =
                    _windowBackgroundBrush;
            }
            else if (ReferenceEquals(
                         control.Background,
                         oldControlBackgroundBrush))
            {
                control.Background =
                    _controlBackgroundBrush;
            }

            if (ReferenceEquals(
                    control.BorderBrush,
                    oldControlBorderBrush))
            {
                control.BorderBrush =
                    _controlBorderBrush;
            }

            if (ReferenceEquals(
                    control.Foreground,
                    oldTextBrush))
            {
                control.Foreground =
                    _textBrush;
            }
        }

        if (root is TextBlock textBlock &&
            ReferenceEquals(
                textBlock.Foreground,
                oldTextBrush))
        {
            textBlock.Foreground =
                _textBrush;
        }

        if (root is Border border)
        {
            if (ReferenceEquals(
                    border.Background,
                    oldWindowBackgroundBrush))
            {
                border.Background =
                    _windowBackgroundBrush;
            }
            else if (ReferenceEquals(
                         border.Background,
                         oldControlBackgroundBrush))
            {
                border.Background =
                    _controlBackgroundBrush;
            }

            if (ReferenceEquals(
                    border.BorderBrush,
                    oldControlBorderBrush))
            {
                border.BorderBrush =
                    _controlBorderBrush;
            }
        }

        int childCount =
            VisualTreeHelper.GetChildrenCount(
                root);

        for (int childIndex = 0;
             childIndex < childCount;
             childIndex++)
        {
            ReplaceBrushes(
                VisualTreeHelper.GetChild(
                    root,
                    childIndex),
                oldWindowBackgroundBrush,
                oldControlBackgroundBrush,
                oldControlBorderBrush,
                oldTextBrush);
        }
    }

    private void SaveAndApply()
    {
        if (_loading)
        {
            return;
        }

        _saveSettings();
        ApplyCurrentAppearance();
    }

    private static string FormatOpacityValue(
        double value)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:0}%",
            value * 100);
    }

    private static string FormatIntegerValue(
        double value)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:0}",
            value);
    }

    private static Color ParseColor(
        string? value,
        Color fallback)
    {
        try
        {
            object? converted =
                ColorConverter.ConvertFromString(
                    value);

            if (converted is Color color)
            {
                return color;
            }
        }
        catch
        {
        }

        return fallback;
    }

    private static Color GetBrushColor(
        Brush brush,
        Color fallback)
    {
        return brush is SolidColorBrush solidColorBrush
            ? solidColorBrush.Color
            : fallback;
    }

    private static string GetBrushColorString(
        Brush brush,
        string fallback)
    {
        return brush is SolidColorBrush solidColorBrush
            ? ToColorString(
                solidColorBrush.Color)
            : fallback;
    }

    private static string ToColorString(
        Color color)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "#{0:X2}{1:X2}{2:X2}{3:X2}",
            color.A,
            color.R,
            color.G,
            color.B);
    }

    private enum OverrideValueKind
    {
        Opacity,
        Blur,
        BorderThickness,
        CornerRadius
    }

    private enum OverrideColorKind
    {
        Border,
        Text,
        Event
    }
}
