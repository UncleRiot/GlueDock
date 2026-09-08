using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;
using GlueDock;

namespace GlueDock.ClockWidget;

// GlueDock rule: Color selection must use only System.Windows.Forms.ColorDialog.
// GlueDock rule: Windows must remain draggable from their free edge areas unless a technically mandatory reason prevents it.
// GlueDock rule: Every companion settings window follows the same GlueDock layout/theme/general-setting pattern
// and exposes only settings that are technically meaningful for that companion.
public enum ClockWidgetCompanionSettingsKind
{
    Timer,
    Stopwatch,
    Calendar
}

public sealed class ClockWidgetCompanionSettingsWindow : DockSettingsSectionControl
{
    private readonly ClockWidgetSettings _settings;
    private readonly GlueDockWidgetAppearance _hostAppearance;
    private readonly Action _applyAndSave;
    private readonly ClockWidgetCompanionSettingsKind _kind;

    private readonly Brush _windowBackgroundBrush;
    private readonly Brush _controlBackgroundBrush;
    private readonly Brush _controlBorderBrush;
    private readonly Brush _textBrush;

    private readonly Border _windowBorder;
    private readonly Border _glassSurfaceBorder;
    private readonly Border _glassHighlightBorder;
    private readonly ClockWidgetNativeBackdropHost _nativeBackdropHost;
    private readonly ScrollViewer _settingsScrollViewer;

    private readonly Slider _fontSizeSlider;
    private readonly TextBlock _fontSizeValueText;
    private readonly Border _colorPreview;
    private readonly Button _colorButton;
    private readonly Border? _timerAlarmColorPreview;
    private readonly Button? _timerAlarmColorButton;
    private readonly ComboBox? _timerSoundComboBox;
    private readonly Border? _stopwatchColorPreview;
    private readonly Button? _stopwatchColorButton;
    private readonly TextBlock? _soundsFolderText;

    private readonly CheckBox _opacityUseGeneralCheckBox;
    private readonly Slider _opacitySlider;
    private readonly TextBlock _opacityValueText;

    private readonly CheckBox _blurUseGeneralCheckBox;
    private readonly Slider _blurSlider;
    private readonly TextBlock _blurValueText;

    private readonly Slider? _calendarUpcomingEventCountSlider;
    private readonly TextBlock? _calendarUpcomingEventCountValueText;
    private readonly Slider? _calendarUpcomingDaysSlider;
    private readonly TextBlock? _calendarUpcomingDaysValueText;
    private readonly CheckBox? _calendarCompanionDoubleWidthCheckBox;

    private readonly Func<string, string> _localize;
    private readonly string _soundsDirectory;
    private readonly string _systemDefaultSound;

    private bool _loading;
    private bool _initialBackdropReady;

    public ClockWidgetCompanionSettingsWindow(
        ClockWidgetSettings settings,
        GlueDockWidgetAppearance hostAppearance,
        Action applyAndSave,
        ClockWidgetCompanionSettingsKind kind,
        Func<string, string> localize,
        string soundsDirectory)
    {
        _settings =
            settings;

        _hostAppearance =
            hostAppearance;

        _applyAndSave =
            applyAndSave;

        _kind =
            kind;

        _localize =
            localize;

        _soundsDirectory =
            soundsDirectory;

        _systemDefaultSound =
            _localize(
                "Widget.Clock.Settings.SystemDefaultSound");

        _windowBackgroundBrush =
            hostAppearance.DockBackgroundBrush.Clone();

        _controlBackgroundBrush =
            hostAppearance.DockItemBackgroundBrush.Clone();

        _controlBorderBrush =
            hostAppearance.DockItemBorderBrush.Clone();

        _textBrush =
            hostAppearance.DockTextBrush.Clone();

        Title =
            kind switch
            {
                ClockWidgetCompanionSettingsKind.Timer =>
                    _localize(
                        "Settings.WidgetSection.TimerCompanion"),
                ClockWidgetCompanionSettingsKind.Stopwatch =>
                    _localize(
                        "Settings.WidgetSection.StopwatchCompanion"),
                _ =>
                    _localize(
                        "Settings.WidgetSection.CalendarCompanion")
            };

        Width =
            Math.Min(
                440,
                Math.Max(
                    400,
                    SystemParameters.WorkArea.Width * 0.84));

        MinWidth =
            Math.Min(
                400,
                Width);

        SizeToContent =
            SizeToContent.Height;

        MaxHeight =
            SystemParameters.WorkArea.Height * 0.9;

        WindowStartupLocation =
            WindowStartupLocation.CenterScreen;

        ResizeMode =
            ResizeMode.NoResize;

        ShowInTaskbar =
            true;

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
                            hostAppearance.GlassCornerRadius)),
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
                    false
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
                    false
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

        (_fontSizeSlider, _fontSizeValueText) =
            CreateSliderRow(
                content,
                kind == ClockWidgetCompanionSettingsKind.Calendar
                    ? _localize(
                        "Widget.Clock.CalendarCompanion.FontSize")
                    : kind == ClockWidgetCompanionSettingsKind.Timer
                        ? _localize(
                            "Widget.Clock.TimerCompanion.FontSize")
                        : _localize(
                            "Widget.Clock.StopwatchCompanion.FontSize"),
                8,
                16,
                1);

        (_colorPreview, _colorButton) =
            CreateColorRow(
                content,
                kind == ClockWidgetCompanionSettingsKind.Calendar
                    ? _localize(
                        "Widget.Clock.CalendarCompanion.EventColor")
                    : kind == ClockWidgetCompanionSettingsKind.Timer
                        ? _localize(
                            "Widget.Clock.TimerCompanion.Color")
                        : _localize(
                            "Widget.Clock.StopwatchCompanion.Color"));


        if (kind == ClockWidgetCompanionSettingsKind.Timer)
        {
            (_timerAlarmColorPreview, _timerAlarmColorButton) =
                CreateColorRow(
                    content,
                    _localize(
                        "Widget.Clock.TimerCompanion.AlarmColor"));

            _timerSoundComboBox =
                CreateSoundComboBox();

            AddComboBoxRow(
                content,
                _localize(
                    "Widget.Clock.TimerCompanion.Sound"),
                _timerSoundComboBox);

            _soundsFolderText =
                CreateSoundsFolderText();

            content.Children.Add(
                _soundsFolderText);
        }
        else if (kind == ClockWidgetCompanionSettingsKind.Stopwatch)
        {
            (_stopwatchColorPreview, _stopwatchColorButton) =
                CreateColorRow(
                    content,
                    _localize(
                        "Widget.Clock.StopwatchCompanion.BaseColor"));
        }

        if (kind == ClockWidgetCompanionSettingsKind.Calendar)
        {
            (_calendarUpcomingEventCountSlider, _calendarUpcomingEventCountValueText) =
                CreateSliderRow(
                    content,
                    _localize(
                        "Widget.Clock.CalendarCompanion.UpcomingEvents"),
                    1,
                    4,
                    1);

            (_calendarUpcomingDaysSlider, _calendarUpcomingDaysValueText) =
                CreateSliderRow(
                    content,
                    _localize(
                        "Widget.Clock.CalendarCompanion.DaysAhead"),
                    1,
                    30,
                    1);

            _calendarCompanionDoubleWidthCheckBox =
                new CheckBox
                {
                    Content =
                        _localize(
                            "Widget.Clock.CalendarCompanion.DoubleWidth"),
                    Foreground =
                        _textBrush,
                    Margin =
                        new Thickness(
                            0,
                            7,
                            0,
                            7)
                };

        }

        (_opacityUseGeneralCheckBox, _opacitySlider, _opacityValueText) =
            CreateAppearanceSliderRow(
                content,
                _localize(
                    "Settings.Opacity"),
                10,
                100,
                1);

        (_blurUseGeneralCheckBox, _blurSlider, _blurValueText) =
            CreateAppearanceSliderRow(
                content,
                _localize(
                    "Settings.Blur"),
                0,
                100,
                1);

        if (_calendarCompanionDoubleWidthCheckBox is not null)
        {
            content.Children.Add(
                _calendarCompanionDoubleWidthCheckBox);
        }

        Button resetButton =
            CreateThemedButton(
                _localize(
                    "Widget.Clock.AlarmCompanion.Reset"));

        resetButton.HorizontalAlignment =
            HorizontalAlignment.Left;

        resetButton.Width =
            112;

        resetButton.Margin =
            new Thickness(
                0,
                12,
                0,
                0);

        resetButton.Click +=
            (_, _) =>
            {
                ResetSettings();
                RefreshControls();
                ApplyWindowMaterial();
                _applyAndSave();
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

        Grid titleBar =
            CreateTitleBar();

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

        _fontSizeSlider.ValueChanged +=
            (_, _) =>
                SaveFontValue();

        _colorButton.Click +=
            (_, _) =>
                ChooseCompanionColor();

        if (_timerAlarmColorButton is not null)
        {
            _timerAlarmColorButton.Click +=
                (_, _) =>
                    ChooseTimerAlarmColor();
        }

        if (_stopwatchColorButton is not null)
        {
            _stopwatchColorButton.Click +=
                (_, _) =>
                    ChooseStopwatchColor();
        }

        if (_timerSoundComboBox is not null)
        {
            _timerSoundComboBox.SelectionChanged +=
                (_, _) =>
                    SaveTimerSoundValue();
        }

        _opacityUseGeneralCheckBox.Checked +=
            (_, _) =>
                SaveAppearanceValues();

        _opacityUseGeneralCheckBox.Unchecked +=
            (_, _) =>
                SaveAppearanceValues();

        _opacitySlider.ValueChanged +=
            (_, _) =>
                SaveAppearanceValues();

        _blurUseGeneralCheckBox.Checked +=
            (_, _) =>
                SaveAppearanceValues();

        _blurUseGeneralCheckBox.Unchecked +=
            (_, _) =>
                SaveAppearanceValues();

        _blurSlider.ValueChanged +=
            (_, _) =>
                SaveAppearanceValues();

        if (_calendarUpcomingEventCountSlider is not null)
        {
            _calendarUpcomingEventCountSlider.ValueChanged +=
                (_, _) =>
                    SaveCalendarPreviewValues();
        }

        if (_calendarUpcomingDaysSlider is not null)
        {
            _calendarUpcomingDaysSlider.ValueChanged +=
                (_, _) =>
                    SaveCalendarPreviewValues();
        }

        if (_calendarCompanionDoubleWidthCheckBox is not null)
        {
            _calendarCompanionDoubleWidthCheckBox.Checked +=
                (_, _) =>
                    SaveCalendarPreviewValues();

            _calendarCompanionDoubleWidthCheckBox.Unchecked +=
                (_, _) =>
                    SaveCalendarPreviewValues();
        }

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

                _nativeBackdropHost.SetBlur(
                    GetEffectiveBlurRadius());

                _nativeBackdropHost.Sync();
            };

        RefreshControls();
        ApplyWindowMaterial();
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


    private (Slider Slider, TextBlock ValueText) CreateSliderRow(
        Panel parent,
        string label,
        double minimum,
        double maximum,
        double tickFrequency)
    {
        Grid row =
            CreateRowGrid(
                label);

        Slider slider =
            CreateThemedSlider(
                minimum,
                maximum,
                tickFrequency);

        TextBlock valueText =
            CreateValueText();

        Grid.SetColumn(
            slider,
            1);

        Grid.SetColumn(
            valueText,
            2);

        row.Children.Add(
            slider);

        row.Children.Add(
            valueText);

        parent.Children.Add(
            row);

        return (
            slider,
            valueText);
    }

    private (Border Preview, Button Button) CreateColorRow(
        Panel parent,
        string label)
    {
        Grid row =
            CreateRowGrid(
                label);

        Border preview =
            new()
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
                    HorizontalAlignment.Left,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        Button button =
            CreateThemedButton(
                _localize(
                    "Settings.ChooseColor"));

        button.Width =
            DockDialogTheme.ColorButtonWidth;

        StackPanel controls =
            new()
            {
                Orientation =
                    Orientation.Horizontal,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        controls.Children.Add(
            preview);

        button.Margin =
            new Thickness(
                8,
                0,
                0,
                0);

        controls.Children.Add(
            button);

        Grid.SetColumn(
            controls,
            1);

        Grid.SetColumnSpan(
            controls,
            2);

        row.Children.Add(
            controls);

        parent.Children.Add(
            row);

        return (
            preview,
            button);
    }

    private ComboBox CreateSoundComboBox()
    {
        Directory.CreateDirectory(
            _soundsDirectory);

        string[] soundFiles =
            Directory.GetFiles(
                    _soundsDirectory,
                    "*.mp3",
                    SearchOption.TopDirectoryOnly)
                .Select(
                    Path.GetFileName)
                .Where(
                    fileName =>
                        !string.IsNullOrWhiteSpace(
                            fileName))
                .Cast<string>()
                .OrderBy(
                    fileName =>
                        fileName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

        ComboBox comboBox =
            new()
            {
                Width =
                    300,
                Height =
                    DockDialogTheme.StandardControlHeight,
                HorizontalAlignment =
                    HorizontalAlignment.Left,
                ItemsSource =
                    new[]
                    {
                        _systemDefaultSound
                    }
                    .Concat(
                        soundFiles)
                    .ToArray()
            };

        comboBox.SelectedItem =
            SelectSound(
                _settings.TimerSoundFile,
                soundFiles);

        DockDialogThemePalette palette =
            DockDialogThemeService.ApplyWindowControlResources(
                this,
                _hostAppearance);

        DockDialogThemeService.ApplyComboBox(
            comboBox,
            palette);

        return comboBox;
    }

    private void AddComboBoxRow(
        Panel parent,
        string label,
        ComboBox comboBox)
    {
        Grid row =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        7,
                        0,
                        7)
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
                        1,
                        GridUnitType.Star)
            });

        TextBlock labelText =
            new()
            {
                Text =
                    label,
                Foreground =
                    _textBrush,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Margin =
                    new Thickness(
                        0,
                        0,
                        10,
                        0)
            };

        Grid.SetColumn(
            labelText,
            0);

        Grid.SetColumn(
            comboBox,
            1);

        row.Children.Add(
            labelText);

        row.Children.Add(
            comboBox);

        parent.Children.Add(
            row);
    }

    private TextBlock CreateSoundsFolderText()
    {
        return new TextBlock
        {
            Text =
                string.Format(
                    _localize(
                        "Widget.Clock.Settings.Mp3Folder"),
                    _soundsDirectory),
            Foreground =
                _textBrush,
            TextWrapping =
                TextWrapping.Wrap,
            Opacity =
                0.72,
            Margin =
                new Thickness(
                    0,
                    4,
                    0,
                    8)
        };
    }

    private string SelectSound(
        string configuredFile,
        string[] availableFiles)
    {
        if (string.IsNullOrWhiteSpace(
                configuredFile))
        {
            return _systemDefaultSound;
        }

        return availableFiles.FirstOrDefault(
                   fileName =>
                       string.Equals(
                           fileName,
                           configuredFile,
                           StringComparison.OrdinalIgnoreCase)) ??
               _systemDefaultSound;
    }

    private (CheckBox UseGeneral, Slider Slider, TextBlock ValueText) CreateAppearanceSliderRow(
        Panel parent,
        string label,
        double minimum,
        double maximum,
        double tickFrequency)
    {
        Grid row =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        7,
                        0,
                        7)
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
                        DockDialogTheme.SliderWidth)
            });

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        58)
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
                Foreground =
                    _textBrush,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Margin =
                    new Thickness(
                        0,
                        0,
                        10,
                        0)
            };

        Slider slider =
            CreateThemedSlider(
                minimum,
                maximum,
                tickFrequency);

        TextBlock valueText =
            CreateValueText();

        CheckBox useGeneral =
            new()
            {
                Content =
                    _localize(
                        "Widget.Clock.AlarmCompanion.UseGeneralSetting"),
                Foreground =
                    _textBrush,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Margin =
                    new Thickness(
                        12,
                        0,
                        0,
                        0)
            };

        Grid.SetColumn(
            labelText,
            0);

        Grid.SetColumn(
            slider,
            1);

        Grid.SetColumn(
            valueText,
            2);

        Grid.SetColumn(
            useGeneral,
            3);

        row.Children.Add(
            labelText);

        row.Children.Add(
            slider);

        row.Children.Add(
            valueText);

        row.Children.Add(
            useGeneral);

        parent.Children.Add(
            row);

        return (
            useGeneral,
            slider,
            valueText);
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
                        7,
                        0,
                        7)
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
                        DockDialogTheme.SliderWidth)
            });

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        58)
            });

        TextBlock labelText =
            new()
            {
                Text =
                    label,
                Foreground =
                    _textBrush,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Margin =
                    new Thickness(
                        0,
                        0,
                        10,
                        0)
            };

        Grid.SetColumn(
            labelText,
            0);

        row.Children.Add(
            labelText);

        return row;
    }

    private Slider CreateThemedSlider(
        double minimum,
        double maximum,
        double tickFrequency)
    {
        return new Slider
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
            HorizontalAlignment =
                HorizontalAlignment.Left,
            VerticalAlignment =
                VerticalAlignment.Center
        };
    }

    private TextBlock CreateValueText()
    {
        return new TextBlock
        {
            Foreground =
                _textBrush,
            HorizontalAlignment =
                HorizontalAlignment.Right,
            VerticalAlignment =
                VerticalAlignment.Center
        };
    }

    private Grid CreateTitleBar()
    {
        Grid titleBar =
            new()
            {
                Height =
                    42,
                Margin =
                    new Thickness(
                        14,
                        4,
                        8,
                        0)
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
                    Title,
                Foreground =
                    _textBrush,
                FontSize =
                    15,
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
            28;

        closeButton.FontSize =
            18;

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

    private Button CreateThemedButton(
        string content)
    {
        Brush buttonBackground =
            _controlBackgroundBrush.Clone();

        Brush buttonForeground =
            GetContrastingTextBrush(
                buttonBackground,
                _textBrush);

        return new Button
        {
            Content =
                content,
            Background =
                buttonBackground,
            BorderBrush =
                _controlBorderBrush,
            BorderThickness =
                new Thickness(
                    1),
            Foreground =
                buttonForeground,
            Padding =
                new Thickness(
                    8,
                    3,
                    8,
                    3),
            Cursor =
                Cursors.Hand
        };
    }

    private void RefreshControls()
    {
        _loading =
            true;

        try
        {
            _fontSizeSlider.Value =
                Math.Clamp(
                    GetFontSize(),
                    8,
                    16);

            _fontSizeValueText.Text =
                $"{_fontSizeSlider.Value:0} pt";

            Color color =
                GetCompanionColor();

            _colorPreview.Background =
                new SolidColorBrush(
                    color);

            if (_timerAlarmColorPreview is not null)
            {
                _timerAlarmColorPreview.Background =
                    new SolidColorBrush(
                        ParseColor(
                            _settings.TimerAlarmColor,
                            Color.FromRgb(
                                255,
                                159,
                                10)));
            }

            if (_stopwatchColorPreview is not null)
            {
                _stopwatchColorPreview.Background =
                    new SolidColorBrush(
                        ParseColor(
                            _settings.StopwatchColor,
                            Color.FromRgb(
                                255,
                                159,
                                10)));
            }

            if (_timerSoundComboBox is not null)
            {
                string[] availableFiles =
                    _timerSoundComboBox.Items
                        .Cast<string>()
                        .Where(
                            item =>
                                !string.Equals(
                                    item,
                                    _systemDefaultSound,
                                    StringComparison.Ordinal))
                        .ToArray();

                _timerSoundComboBox.SelectedItem =
                    SelectSound(
                        _settings.TimerSoundFile,
                        availableFiles);
            }

            _opacityUseGeneralCheckBox.IsChecked =
                !GetOpacityOverrideEnabled();

            _blurUseGeneralCheckBox.IsChecked =
                !GetBlurOverrideEnabled();

            _opacitySlider.Value =
                GetEffectiveOpacity() *
                100.0;

            _blurSlider.Value =
                GetEffectiveBlurRadius();

            _opacitySlider.IsEnabled =
                GetOpacityOverrideEnabled();

            _blurSlider.IsEnabled =
                GetBlurOverrideEnabled();

            _opacityValueText.Text =
                $"{_opacitySlider.Value:0}%";

            _blurValueText.Text =
                $"{_blurSlider.Value:0}";

            if (_calendarUpcomingEventCountSlider is not null &&
                _calendarUpcomingEventCountValueText is not null)
            {
                _calendarUpcomingEventCountSlider.Value =
                    Math.Clamp(
                        _settings.CalendarUpcomingEventCount,
                        1,
                        4);

                _calendarUpcomingEventCountValueText.Text =
                    $"{_calendarUpcomingEventCountSlider.Value:0}";
            }

            if (_calendarUpcomingDaysSlider is not null &&
                _calendarUpcomingDaysValueText is not null)
            {
                _calendarUpcomingDaysSlider.Value =
                    Math.Clamp(
                        _settings.CalendarUpcomingDays,
                        1,
                        30);

                _calendarUpcomingDaysValueText.Text =
                    $"{_calendarUpcomingDaysSlider.Value:0} d";
            }

            if (_calendarCompanionDoubleWidthCheckBox is not null)
            {
                _calendarCompanionDoubleWidthCheckBox.IsChecked =
                    _settings.CalendarCompanionDoubleWidth;
            }
        }
        finally
        {
            _loading =
                false;
        }
    }

    private void SaveFontValue()
    {
        if (_loading)
        {
            return;
        }

        SetFontSize(
            _fontSizeSlider.Value);

        _fontSizeValueText.Text =
            $"{_fontSizeSlider.Value:0} pt";

        _applyAndSave();
    }

    private void SaveCalendarPreviewValues()
    {
        if (_loading ||
            _kind != ClockWidgetCompanionSettingsKind.Calendar ||
            _calendarUpcomingEventCountSlider is null ||
            _calendarUpcomingEventCountValueText is null ||
            _calendarUpcomingDaysSlider is null ||
            _calendarUpcomingDaysValueText is null ||
            _calendarCompanionDoubleWidthCheckBox is null)
        {
            return;
        }

        _settings.CalendarUpcomingEventCount =
            Math.Clamp(
                (int)Math.Round(
                    _calendarUpcomingEventCountSlider.Value),
                1,
                4);

        _settings.CalendarUpcomingDays =
            Math.Clamp(
                (int)Math.Round(
                    _calendarUpcomingDaysSlider.Value),
                1,
                30);

        _settings.CalendarCompanionDoubleWidth =
            _calendarCompanionDoubleWidthCheckBox.IsChecked ==
            true;

        _calendarUpcomingEventCountValueText.Text =
            $"{_settings.CalendarUpcomingEventCount}";

        _calendarUpcomingDaysValueText.Text =
            $"{_settings.CalendarUpcomingDays} d";

        _applyAndSave();
    }

    private void SaveAppearanceValues()
    {
        if (_loading)
        {
            return;
        }

        SetOpacityOverrideEnabled(
            _opacityUseGeneralCheckBox.IsChecked != true);

        SetBlurOverrideEnabled(
            _blurUseGeneralCheckBox.IsChecked != true);

        if (GetOpacityOverrideEnabled())
        {
            SetOpacity(
                Math.Clamp(
                    _opacitySlider.Value / 100.0,
                    0.10,
                    1.00));
        }

        if (GetBlurOverrideEnabled())
        {
            SetBlurRadius(
                Math.Clamp(
                    _blurSlider.Value,
                    0,
                    100));
        }

        _opacitySlider.IsEnabled =
            GetOpacityOverrideEnabled();

        _blurSlider.IsEnabled =
            GetBlurOverrideEnabled();

        _opacitySlider.Value =
            GetEffectiveOpacity() *
            100.0;

        _blurSlider.Value =
            GetEffectiveBlurRadius();

        _opacityValueText.Text =
            $"{_opacitySlider.Value:0}%";

        _blurValueText.Text =
            $"{_blurSlider.Value:0}";

        ApplyWindowMaterial();
        _applyAndSave();
    }

    private void ChooseCompanionColor()
    {
        Color? selectedColor =
            ChooseColor(
                GetCompanionColor());

        if (selectedColor is null)
        {
            return;
        }

        SetCompanionColor(
            ToColorString(
                selectedColor.Value));

        RefreshControls();
        _applyAndSave();
    }

    private void ChooseTimerAlarmColor()
    {
        Color? selectedColor =
            ChooseColor(
                ParseColor(
                    _settings.TimerAlarmColor,
                    Color.FromRgb(
                        255,
                        159,
                        10)));

        if (selectedColor is null)
        {
            return;
        }

        _settings.TimerAlarmColor =
            ToColorString(
                selectedColor.Value);

        RefreshControls();
        _applyAndSave();
    }

    private void ChooseStopwatchColor()
    {
        Color? selectedColor =
            ChooseColor(
                ParseColor(
                    _settings.StopwatchColor,
                    Color.FromRgb(
                        255,
                        159,
                        10)));

        if (selectedColor is null)
        {
            return;
        }

        _settings.StopwatchColor =
            ToColorString(
                selectedColor.Value);

        RefreshControls();
        _applyAndSave();
    }

    private void SaveTimerSoundValue()
    {
        if (_loading ||
            _timerSoundComboBox is null)
        {
            return;
        }

        string selectedSound =
            _timerSoundComboBox.SelectedItem as string ??
            _systemDefaultSound;

        _settings.TimerSoundFile =
            string.Equals(
                selectedSound,
                _systemDefaultSound,
                StringComparison.Ordinal)
                ? string.Empty
                : selectedSound;

        _applyAndSave();
    }

    private void ResetSettings()
    {
        switch (_kind)
        {
            case ClockWidgetCompanionSettingsKind.Timer:
                _settings.TimerCompanionFontSize = 11;
                _settings.TimerCompanionColor = string.Empty;
                _settings.TimerCompanionSettingsOpacityOverrideEnabled = false;
                _settings.TimerCompanionSettingsBlurOverrideEnabled = false;
                break;

            case ClockWidgetCompanionSettingsKind.Stopwatch:
                _settings.StopwatchCompanionFontSize = 11;
                _settings.StopwatchCompanionColor = string.Empty;
                _settings.StopwatchCompanionSettingsOpacityOverrideEnabled = false;
                _settings.StopwatchCompanionSettingsBlurOverrideEnabled = false;
                break;

            default:
                _settings.CalendarUpcomingFontSize = 10;
                _settings.CalendarEventColor = "#FFFF9F0A";
                _settings.CalendarUpcomingEventCount = 3;
                _settings.CalendarUpcomingDays = 7;
                _settings.CalendarCompanionDoubleWidth = false;
                _settings.CalendarCompanionSettingsOpacityOverrideEnabled = false;
                _settings.CalendarCompanionSettingsBlurOverrideEnabled = false;
                break;
        }
    }

    private double GetFontSize()
    {
        return _kind switch
        {
            ClockWidgetCompanionSettingsKind.Timer =>
                _settings.TimerCompanionFontSize,
            ClockWidgetCompanionSettingsKind.Stopwatch =>
                _settings.StopwatchCompanionFontSize,
            _ =>
                _settings.CalendarUpcomingFontSize
        };
    }

    private void SetFontSize(
        double value)
    {
        switch (_kind)
        {
            case ClockWidgetCompanionSettingsKind.Timer:
                _settings.TimerCompanionFontSize =
                    value;
                break;

            case ClockWidgetCompanionSettingsKind.Stopwatch:
                _settings.StopwatchCompanionFontSize =
                    value;
                break;

            default:
                _settings.CalendarUpcomingFontSize =
                    value;
                break;
        }
    }

    private Color GetCompanionColor()
    {
        return _kind switch
        {
            ClockWidgetCompanionSettingsKind.Timer =>
                ParseColor(
                    _settings.TimerCompanionColor,
                    ParseColor(
                        _settings.TimerAlarmColor,
                        Color.FromRgb(
                            255,
                            159,
                            10))),
            ClockWidgetCompanionSettingsKind.Stopwatch =>
                ParseColor(
                    _settings.StopwatchCompanionColor,
                    ParseColor(
                        _settings.StopwatchColor,
                        Color.FromRgb(
                            255,
                            159,
                            10))),
            _ =>
                ParseColor(
                    _settings.CalendarEventColor,
                    Color.FromRgb(
                        255,
                        159,
                        10))
        };
    }

    private void SetCompanionColor(
        string value)
    {
        switch (_kind)
        {
            case ClockWidgetCompanionSettingsKind.Timer:
                _settings.TimerCompanionColor =
                    value;
                break;

            case ClockWidgetCompanionSettingsKind.Stopwatch:
                _settings.StopwatchCompanionColor =
                    value;
                break;

            default:
                _settings.CalendarEventColor =
                    value;
                break;
        }
    }

    private bool GetOpacityOverrideEnabled()
    {
        return _kind switch
        {
            ClockWidgetCompanionSettingsKind.Timer =>
                _settings.TimerCompanionSettingsOpacityOverrideEnabled,
            ClockWidgetCompanionSettingsKind.Stopwatch =>
                _settings.StopwatchCompanionSettingsOpacityOverrideEnabled,
            _ =>
                _settings.CalendarCompanionSettingsOpacityOverrideEnabled
        };
    }

    private void SetOpacityOverrideEnabled(
        bool value)
    {
        switch (_kind)
        {
            case ClockWidgetCompanionSettingsKind.Timer:
                _settings.TimerCompanionSettingsOpacityOverrideEnabled =
                    value;
                break;

            case ClockWidgetCompanionSettingsKind.Stopwatch:
                _settings.StopwatchCompanionSettingsOpacityOverrideEnabled =
                    value;
                break;

            default:
                _settings.CalendarCompanionSettingsOpacityOverrideEnabled =
                    value;
                break;
        }
    }

    private double GetOpacity()
    {
        return _kind switch
        {
            ClockWidgetCompanionSettingsKind.Timer =>
                _settings.TimerCompanionSettingsOpacity,
            ClockWidgetCompanionSettingsKind.Stopwatch =>
                _settings.StopwatchCompanionSettingsOpacity,
            _ =>
                _settings.CalendarCompanionSettingsOpacity
        };
    }

    private void SetOpacity(
        double value)
    {
        switch (_kind)
        {
            case ClockWidgetCompanionSettingsKind.Timer:
                _settings.TimerCompanionSettingsOpacity =
                    value;
                break;

            case ClockWidgetCompanionSettingsKind.Stopwatch:
                _settings.StopwatchCompanionSettingsOpacity =
                    value;
                break;

            default:
                _settings.CalendarCompanionSettingsOpacity =
                    value;
                break;
        }
    }

    private bool GetBlurOverrideEnabled()
    {
        return _kind switch
        {
            ClockWidgetCompanionSettingsKind.Timer =>
                _settings.TimerCompanionSettingsBlurOverrideEnabled,
            ClockWidgetCompanionSettingsKind.Stopwatch =>
                _settings.StopwatchCompanionSettingsBlurOverrideEnabled,
            _ =>
                _settings.CalendarCompanionSettingsBlurOverrideEnabled
        };
    }

    private void SetBlurOverrideEnabled(
        bool value)
    {
        switch (_kind)
        {
            case ClockWidgetCompanionSettingsKind.Timer:
                _settings.TimerCompanionSettingsBlurOverrideEnabled =
                    value;
                break;

            case ClockWidgetCompanionSettingsKind.Stopwatch:
                _settings.StopwatchCompanionSettingsBlurOverrideEnabled =
                    value;
                break;

            default:
                _settings.CalendarCompanionSettingsBlurOverrideEnabled =
                    value;
                break;
        }
    }

    private double GetBlurRadius()
    {
        return _kind switch
        {
            ClockWidgetCompanionSettingsKind.Timer =>
                _settings.TimerCompanionSettingsBlurRadius,
            ClockWidgetCompanionSettingsKind.Stopwatch =>
                _settings.StopwatchCompanionSettingsBlurRadius,
            _ =>
                _settings.CalendarCompanionSettingsBlurRadius
        };
    }

    private void SetBlurRadius(
        double value)
    {
        switch (_kind)
        {
            case ClockWidgetCompanionSettingsKind.Timer:
                _settings.TimerCompanionSettingsBlurRadius =
                    value;
                break;

            case ClockWidgetCompanionSettingsKind.Stopwatch:
                _settings.StopwatchCompanionSettingsBlurRadius =
                    value;
                break;

            default:
                _settings.CalendarCompanionSettingsBlurRadius =
                    value;
                break;
        }
    }

    private double GetEffectiveOpacity()
    {
        return GetOpacityOverrideEnabled()
            ? Math.Clamp(
                GetOpacity(),
                0.10,
                1.00)
            : Math.Clamp(
                _hostAppearance.Opacity,
                0.10,
                1.00);
    }

    private double GetEffectiveBlurRadius()
    {
        return GetBlurOverrideEnabled()
            ? Math.Clamp(
                GetBlurRadius(),
                0,
                100)
            : Math.Clamp(
                _hostAppearance.BlurRadius,
                0,
                100);
    }

    private static Color? ChooseColor(
        Color currentColor)
    {
        using WinForms.ColorDialog dialog =
            new()
            {
                FullOpen =
                    true,
                Color =
                    Drawing.Color.FromArgb(
                        currentColor.A,
                        currentColor.R,
                        currentColor.G,
                        currentColor.B)
            };

        return dialog.ShowDialog() ==
               WinForms.DialogResult.OK
            ? Color.FromArgb(
                dialog.Color.A,
                dialog.Color.R,
                dialog.Color.G,
                dialog.Color.B)
            : null;
    }

    private void ApplyWindowMaterial()
    {
        double opacity =
            GetEffectiveOpacity();

        double blurRadius =
            GetEffectiveBlurRadius();

        _windowBorder.CornerRadius =
            new CornerRadius(
                Math.Max(
                    0,
                    _hostAppearance.GlassCornerRadius));

        _glassSurfaceBorder.CornerRadius =
            _windowBorder.CornerRadius;

        _glassHighlightBorder.CornerRadius =
            _windowBorder.CornerRadius;

        if (_hostAppearance.GlassSurfaceEnabled)
        {
            _windowBorder.Background =
                Brushes.Transparent;

            double gradientReferenceHeight =
                Math.Max(
                    1,
                    _hostAppearance.GlassGradientReferenceHeight);

            _glassSurfaceBorder.Background =
                new LinearGradientBrush(
                    _hostAppearance.GlassTopColor,
                    _hostAppearance.GlassBottomColor,
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
                        opacity
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
                    _hostAppearance.GlassHighlightColor,
                    0));

            highlightBrush.GradientStops.Add(
                new GradientStop(
                    Color.FromArgb(
                        0,
                        _hostAppearance.GlassHighlightColor.R,
                        _hostAppearance.GlassHighlightColor.G,
                        _hostAppearance.GlassHighlightColor.B),
                    0.55));

            _glassHighlightBorder.Background =
                highlightBrush;
        }
        else
        {
            _glassSurfaceBorder.Background =
                Brushes.Transparent;

            _glassHighlightBorder.Background =
                Brushes.Transparent;

            if (_windowBackgroundBrush is SolidColorBrush solidColorBrush)
            {
                Color color =
                    solidColorBrush.Color;

                _windowBorder.Background =
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
            else
            {
                _windowBorder.Background =
                    _windowBackgroundBrush;
            }
        }

        if (_initialBackdropReady)
        {
            _nativeBackdropHost.SetBlur(
                blurRadius);

            _nativeBackdropHost.Sync();
        }
    }

    private void WindowDragMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton !=
            MouseButton.Left ||
            e.ButtonState !=
            MouseButtonState.Pressed ||
            IsInteractiveElement(
                e.OriginalSource as DependencyObject))
        {
            return;
        }

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
                current is Slider ||
                current is TextBox ||
                current is ComboBox ||
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

    private static Color ParseColor(
        string value,
        Color fallback)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return fallback;
        }

        try
        {
            object? converted =
                ColorConverter.ConvertFromString(
                    value);

            return converted is Color color
                ? color
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static string ToColorString(
        Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
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
}
