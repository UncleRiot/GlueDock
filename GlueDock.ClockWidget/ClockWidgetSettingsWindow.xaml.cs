using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

using GlueDock;

namespace GlueDock.ClockWidget;

// GlueDock UI rule: This dialog is the current reference implementation for the dock-wide settings/dialog theme.
// Styling must come from DockDialogTheme / DockDialogThemeService.
// Do not add dialog-local replacements for standard buttons, inputs, ComboBoxes, contrast or popup styling.
public partial class ClockWidgetSettingsWindow : DockSettingsSectionControl
{
    private const string SystemDefaultSound = "(System default)";

    private sealed record DateFormatOption(
        string Id,
        string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    private readonly string _soundsDirectory;
    private readonly Action<ClockWidgetSettings>? _previewChanged;
    private readonly ClockWidgetSettings _previewSettings;
    private readonly GlueDockWidgetAppearance? _hostAppearance;
    private readonly ClockWidgetNativeBackdropHost _nativeBackdropHost;
    private readonly Func<string, string> _localize;
    private readonly Action<EventHandler>? _subscribeLanguageChanged;
    private readonly Action<EventHandler>? _unsubscribeLanguageChanged;
    private bool _isInitializing = true;

    public ClockWidgetSettingsWindow(
        ClockWidgetSettings settings,
        string soundsDirectory,
        Action<ClockWidgetSettings>? previewChanged,
        GlueDockWidgetAppearance? hostAppearance,
        Func<string, string> localize,
        Action<EventHandler>? subscribeLanguageChanged,
        Action<EventHandler>? unsubscribeLanguageChanged)
    {
        _soundsDirectory = soundsDirectory;
        _previewChanged = previewChanged;
        _hostAppearance = hostAppearance;
        _localize = localize;
        _subscribeLanguageChanged = subscribeLanguageChanged;
        _unsubscribeLanguageChanged = unsubscribeLanguageChanged;
        _previewSettings =
            CloneSettings(
                settings);

        InitializeComponent();

        MaxHeight =
            SystemParameters.WorkArea.Height *
            0.9;

        _nativeBackdropHost =
            new ClockWidgetNativeBackdropHost(
                this);

        _subscribeLanguageChanged?.Invoke(
            LanguageChanged);

        Closed +=
            (_, _) =>
                _unsubscribeLanguageChanged?.Invoke(
                    LanguageChanged);

        RefreshLocalizedNewControls();

        ApplyHostAppearance();

        DateFormatComboBox.ItemsSource =
            new DateFormatOption[]
            {
                new("GermanShort", "29.08."),
                new("GermanLong", "29.08.2026"),
                new("UsLong", "08/29/2026"),
                new("IsoLong", "2026-08-29"),
                new("GermanWeekdayShort", "Sa. 29.08."),
                new("GermanWeekdayLong", "Sa. 29.08.2026")
            };

        ShowDateCheckBox.IsChecked =
            settings.ShowDate;

        DateFormatComboBox.SelectedValue =
            settings.DateFormatId;

        if (DateFormatComboBox.SelectedIndex < 0)
        {
            DateFormatComboBox.SelectedValue =
                "GermanShort";
        }

        TimeScaleSlider.Value =
            Math.Clamp(
                settings.TimeScalePercent,
                50,
                150);

        DateScaleSlider.Value =
            Math.Clamp(
                settings.DateScalePercent,
                50,
                150);

        CalendarUpcomingEventCountSlider.Value =
            Math.Clamp(
                settings.CalendarUpcomingEventCount,
                1,
                4);

        CalendarUpcomingDaysSlider.Value =
            Math.Clamp(
                settings.CalendarUpcomingDays,
                1,
                30);

        CalendarUpcomingFontSizeSlider.Value =
            Math.Clamp(
                settings.CalendarUpcomingFontSize,
                8,
                14);

        UpdateScaleValueTexts();
        UpdatePositionValueTexts();
        UpdateColorPreviews();

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

        string[] soundOptions =
            new[]
            {
                SystemDefaultSound
            }
            .Concat(
                soundFiles)
            .ToArray();

        AlarmSoundComboBox.ItemsSource =
            soundOptions;

        TimerSoundComboBox.ItemsSource =
            soundOptions;

        AlarmSoundComboBox.SelectedItem =
            SelectSound(
                settings.AlarmSoundFile,
                soundFiles);

        TimerSoundComboBox.SelectedItem =
            SelectSound(
                settings.TimerSoundFile,
                soundFiles);

        SoundsFolderText.Text =
            "MP3 folder: " +
            _soundsDirectory;

        CalendarCompanionDoubleWidthCheckBox.IsChecked =
            settings.CalendarCompanionDoubleWidth;

        DisableDefaultHoverEffectCheckBox.IsChecked =
            settings.DisableDefaultHoverEffect;

        UpdateDateFormatEnabledState();

        _isInitializing =
            false;
    }

    public ClockWidgetSettings Result { get; private set; } =
        new();

    public FrameworkElement CreateEmbeddedSettingsContent()
    {
        SettingsScrollViewer.Content =
            null;

        DialogButtonsPanel.Visibility =
            Visibility.Collapsed;

        SettingsContentPanel.Margin =
            new Thickness(
                0);

        Content =
            SettingsContentPanel;

        return this;
    }

    private void ApplyHostAppearance()
    {
        if (_hostAppearance is null)
        {
            return;
        }

        DockDialogThemePalette uiPalette =
            DockDialogThemeService.ApplyWindowControlResources(
                this,
                _hostAppearance);

        Brush windowBackgroundBrush =
            uiPalette.WindowBackgroundBrush;

        Brush controlBorderBrush =
            uiPalette.ControlBorderBrush;

        DockDialogThemeService.ApplyComboBox(
            DateFormatComboBox,
            uiPalette);

        DockDialogThemeService.ApplyComboBox(
            AlarmSoundComboBox,
            uiPalette);

        DockDialogThemeService.ApplyComboBox(
            TimerSoundComboBox,
            uiPalette);

        ClockColorPreview.BorderBrush =
            controlBorderBrush;

        TimerAlarmColorPreview.BorderBrush =
            controlBorderBrush;

        StopwatchColorPreview.BorderBrush =
            controlBorderBrush;

        AlarmColorPreview.BorderBrush =
            controlBorderBrush;

        CalendarEventColorPreview.BorderBrush =
            controlBorderBrush;

        string chooseColorText =
            _localize(
                "Settings.ChooseColor");

        ClockColorButton.Content =
            chooseColorText;

        TimerAlarmColorButton.Content =
            chooseColorText;

        StopwatchColorButton.Content =
            chooseColorText;

        AlarmColorButton.Content =
            chooseColorText;

        CalendarEventColorButton.Content =
            chooseColorText;

        ApplyHostWindowMaterial(
            _hostAppearance,
            windowBackgroundBrush,
            controlBorderBrush);

        _nativeBackdropHost.SetBlur(
            Math.Clamp(
                _hostAppearance.BlurRadius,
                0,
                100));
    }

    private void ApplyHostWindowMaterial(
        GlueDockWidgetAppearance appearance,
        Brush windowBackgroundBrush,
        Brush controlBorderBrush)
    {
        double cornerRadius =
            Math.Clamp(
                appearance.GlassCornerRadius,
                0,
                80);

        WindowBorder.CornerRadius =
            new CornerRadius(
                cornerRadius);

        GlassSurfaceBorder.CornerRadius =
            new CornerRadius(
                cornerRadius);

        GlassHighlightBorder.CornerRadius =
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

                WindowBorder.BorderBrush =
                    dockBorderBrush;
            }
            else
            {
                WindowBorder.BorderBrush =
                    new SolidColorBrush(
                        appearance.DockBorderColor);
            }

            WindowBorder.BorderThickness =
                new Thickness(
                    1);
        }
        else
        {
            WindowBorder.BorderBrush =
                Brushes.Transparent;

            WindowBorder.BorderThickness =
                new Thickness(
                    0);
        }

        if (!appearance.GlassSurfaceEnabled)
        {
            WindowBorder.Background =
                DockDialogThemeService.CreateDialogSurfaceBrush(
                    windowBackgroundBrush,
                    Math.Clamp(
                        appearance.Opacity,
                        0.10,
                        1.00));

            GlassSurfaceBorder.Background =
                Brushes.Transparent;

            GlassSurfaceBorder.Visibility =
                Visibility.Collapsed;

            GlassHighlightBorder.Background =
                Brushes.Transparent;

            GlassHighlightBorder.BorderBrush =
                Brushes.Transparent;

            GlassHighlightBorder.BorderThickness =
                new Thickness(
                    0);

            GlassHighlightBorder.Visibility =
                Visibility.Collapsed;

            return;
        }

        WindowBorder.Background =
            Brushes.Transparent;

        double gradientReferenceHeight =
            Math.Max(
                1,
                appearance.GlassGradientReferenceHeight);

        LinearGradientBrush surfaceBrush =
            new(
                Color.FromArgb(
                    255,
                    appearance.GlassTopColor.R,
                    appearance.GlassTopColor.G,
                    appearance.GlassTopColor.B),
                Color.FromArgb(
                    255,
                    appearance.GlassBottomColor.R,
                    appearance.GlassBottomColor.G,
                    appearance.GlassBottomColor.B),
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
                        1.00)
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

        GlassSurfaceBorder.Background =
            surfaceBrush;

        GlassSurfaceBorder.Visibility =
            Visibility.Visible;

        GlassHighlightBorder.Background =
            highlightBrush;

        GlassHighlightBorder.BorderBrush =
            frameBrush;

        GlassHighlightBorder.BorderThickness =
            new Thickness(
                1);

        GlassHighlightBorder.Visibility =
            Visibility.Visible;
    }

    // GlueDock rule: Windows must remain draggable from their free edge areas unless a technically mandatory reason prevents it.
    private void WindowChrome_PreviewMouseLeftButtonDown(
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

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult =
            false;
    }

    private void TimeScaleSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (TimeScaleSlider is null)
        {
            return;
        }

        _previewSettings.TimeScalePercent =
            Math.Clamp(
                TimeScaleSlider.Value,
                50,
                150);

        UpdateScaleValueTexts();
        PublishPreview();
    }

    private void DateScaleSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (DateScaleSlider is null)
        {
            return;
        }

        _previewSettings.DateScalePercent =
            Math.Clamp(
                DateScaleSlider.Value,
                50,
                150);

        UpdateScaleValueTexts();
        PublishPreview();
    }

    private void TimeYUpButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _previewSettings.TimeYOffset -=
            1;

        UpdatePositionValueTexts();
        PublishPreview();
    }

    private void TimeYDownButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _previewSettings.TimeYOffset +=
            1;

        UpdatePositionValueTexts();
        PublishPreview();
    }

    private void DateYUpButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _previewSettings.DateYOffset -=
            1;

        UpdatePositionValueTexts();
        PublishPreview();
    }

    private void DateYDownButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _previewSettings.DateYOffset +=
            1;

        UpdatePositionValueTexts();
        PublishPreview();
    }

    // GlueDock rule: Color selection must use only System.Windows.Forms.ColorDialog.
    private void ClockColorButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Color initialColor =
            ParseColor(
                _previewSettings.ClockColor,
                Colors.White);

        if (!ClockWidgetColorPickerWindow.TryChooseColor(
                initialColor,
                out Color selectedColor))
        {
            return;
        }

        _previewSettings.ClockColor =
            selectedColor.ToString();

        UpdateColorPreviews();
        PublishPreview();
    }

    // GlueDock rule: Color selection must use only System.Windows.Forms.ColorDialog.
    private void TimerAlarmColorButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Color initialColor =
            ParseColor(
                _previewSettings.TimerAlarmColor,
                Color.FromRgb(
                    255,
                    159,
                    10));

        if (!ClockWidgetColorPickerWindow.TryChooseColor(
                initialColor,
                out Color selectedColor))
        {
            return;
        }

        _previewSettings.TimerAlarmColor =
            selectedColor.ToString();

        UpdateColorPreviews();
        PublishPreview();
    }

    // GlueDock rule: Color selection must use only System.Windows.Forms.ColorDialog.
    private void StopwatchColorButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Color initialColor =
            ParseColor(
                _previewSettings.StopwatchColor,
                Color.FromRgb(
                    255,
                    159,
                    10));

        if (!ClockWidgetColorPickerWindow.TryChooseColor(
                initialColor,
                out Color selectedColor))
        {
            return;
        }

        _previewSettings.StopwatchColor =
            selectedColor.ToString();

        UpdateColorPreviews();
        PublishPreview();
    }

    // GlueDock rule: Color selection must use only System.Windows.Forms.ColorDialog.
    private void AlarmColorButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Color initialColor =
            ParseColor(
                _previewSettings.AlarmColor,
                Color.FromRgb(
                    255,
                    159,
                    10));

        if (!ClockWidgetColorPickerWindow.TryChooseColor(
                initialColor,
                out Color selectedColor))
        {
            return;
        }

        _previewSettings.AlarmColor =
            selectedColor.ToString();

        UpdateColorPreviews();
        PublishPreview();
    }

    // GlueDock rule: Color selection must use only System.Windows.Forms.ColorDialog.
    private void CalendarEventColorButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Color initialColor =
            ParseColor(
                _previewSettings.CalendarEventColor,
                Color.FromRgb(
                    255,
                    159,
                    10));

        if (!ClockWidgetColorPickerWindow.TryChooseColor(
                initialColor,
                out Color selectedColor))
        {
            return;
        }

        _previewSettings.CalendarEventColor =
            selectedColor.ToString();

        UpdateColorPreviews();
        PublishPreview();
    }

    private void CalendarUpcomingFontSizeSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (CalendarUpcomingFontSizeSlider is null)
        {
            return;
        }

        _previewSettings.CalendarUpcomingFontSize =
            Math.Clamp(
                CalendarUpcomingFontSizeSlider.Value,
                8,
                14);

        UpdateScaleValueTexts();
        PublishPreview();
    }

    private void CalendarUpcomingEventCountSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (CalendarUpcomingEventCountSlider is null)
        {
            return;
        }

        _previewSettings.CalendarUpcomingEventCount =
            Math.Clamp(
                (int)Math.Round(
                    CalendarUpcomingEventCountSlider.Value),
                1,
                4);

        UpdateScaleValueTexts();
        PublishPreview();
    }

    private void CalendarUpcomingDaysSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (CalendarUpcomingDaysSlider is null)
        {
            return;
        }

        _previewSettings.CalendarUpcomingDays =
            Math.Clamp(
                (int)Math.Round(
                    CalendarUpcomingDaysSlider.Value),
                1,
                30);

        UpdateScaleValueTexts();
        PublishPreview();
    }



    private void UpdateScaleValueTexts()
    {
        if (TimeScaleValueText is not null &&
            TimeScaleSlider is not null)
        {
            TimeScaleValueText.Text =
                $"{TimeScaleSlider.Value:0} %";
        }

        if (DateScaleValueText is not null &&
            DateScaleSlider is not null)
        {
            DateScaleValueText.Text =
                $"{DateScaleSlider.Value:0} %";
        }

        if (CalendarUpcomingEventCountValueText is not null &&
            CalendarUpcomingEventCountSlider is not null)
        {
            CalendarUpcomingEventCountValueText.Text =
                $"{CalendarUpcomingEventCountSlider.Value:0}";
        }

        if (CalendarUpcomingDaysValueText is not null &&
            CalendarUpcomingDaysSlider is not null)
        {
            CalendarUpcomingDaysValueText.Text =
                $"{CalendarUpcomingDaysSlider.Value:0} d";
        }

        if (CalendarUpcomingFontSizeValueText is not null &&
            CalendarUpcomingFontSizeSlider is not null)
        {
            CalendarUpcomingFontSizeValueText.Text =
                $"{CalendarUpcomingFontSizeSlider.Value:0} pt";
        }
    }

    private void UpdatePositionValueTexts()
    {
        if (TimeYOffsetValueText is not null)
        {
            TimeYOffsetValueText.Text =
                $"{_previewSettings.TimeYOffset:0} DIP";
        }

        if (DateYOffsetValueText is not null)
        {
            DateYOffsetValueText.Text =
                $"{_previewSettings.DateYOffset:0} DIP";
        }
    }

    private void UpdateColorPreviews()
    {
        if (ClockColorPreview is not null)
        {
            ClockColorPreview.Background =
                new SolidColorBrush(
                    ParseColor(
                        _previewSettings.ClockColor,
                        Colors.White));
        }

        if (TimerAlarmColorPreview is not null)
        {
            TimerAlarmColorPreview.Background =
                new SolidColorBrush(
                    ParseColor(
                        _previewSettings.TimerAlarmColor,
                        Color.FromRgb(
                            255,
                            159,
                            10)));
        }

        if (StopwatchColorPreview is not null)
        {
            StopwatchColorPreview.Background =
                new SolidColorBrush(
                    ParseColor(
                        _previewSettings.StopwatchColor,
                        Color.FromRgb(
                            255,
                            159,
                            10)));
        }

        if (AlarmColorPreview is not null)
        {
            AlarmColorPreview.Background =
                new SolidColorBrush(
                    ParseColor(
                        _previewSettings.AlarmColor,
                        Color.FromRgb(
                            255,
                            159,
                            10)));
        }

        if (CalendarEventColorPreview is not null)
        {
            CalendarEventColorPreview.Background =
                new SolidColorBrush(
                    ParseColor(
                        _previewSettings.CalendarEventColor,
                        Color.FromRgb(
                            255,
                            159,
                            10)));
        }
    }

    private void CalendarCompanionDoubleWidthCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (CalendarCompanionDoubleWidthCheckBox is null)
        {
            return;
        }

        _previewSettings.CalendarCompanionDoubleWidth =
            CalendarCompanionDoubleWidthCheckBox.IsChecked == true;

        PublishPreview();
    }

    private void PublishPreview()
    {
        if (_isInitializing)
        {
            return;
        }

        UpdatePreviewSettingsFromControls();

        _previewChanged?.Invoke(
            CloneSettings(
                _previewSettings));
    }

    private void UpdatePreviewSettingsFromControls()
    {
        if (ShowDateCheckBox is null ||
            DateFormatComboBox is null ||
            AlarmSoundComboBox is null ||
            TimerSoundComboBox is null ||
            DisableDefaultHoverEffectCheckBox is null)
        {
            return;
        }

        string alarmSound =
            AlarmSoundComboBox.SelectedItem as string ??
            SystemDefaultSound;

        string timerSound =
            TimerSoundComboBox.SelectedItem as string ??
            SystemDefaultSound;

        _previewSettings.ShowDate =
            ShowDateCheckBox.IsChecked == true;

        _previewSettings.DateFormatId =
            DateFormatComboBox.SelectedValue as string ??
            "GermanShort";

        _previewSettings.DisableDefaultHoverEffect =
            DisableDefaultHoverEffectCheckBox.IsChecked == true;

        _previewSettings.AlarmSoundFile =
            string.Equals(
                alarmSound,
                SystemDefaultSound,
                StringComparison.Ordinal)
                ? string.Empty
                : alarmSound;

        _previewSettings.TimerSoundFile =
            string.Equals(
                timerSound,
                SystemDefaultSound,
                StringComparison.Ordinal)
                ? string.Empty
                : timerSound;
    }

    private static string SelectSound(
        string configuredFile,
        string[] availableFiles)
    {
        if (string.IsNullOrWhiteSpace(
                configuredFile))
        {
            return SystemDefaultSound;
        }

        return availableFiles.FirstOrDefault(
                   fileName =>
                       string.Equals(
                           fileName,
                           configuredFile,
                           StringComparison.OrdinalIgnoreCase)) ??
               SystemDefaultSound;
    }

    private void RefreshLocalizedNewControls()
    {
        string chooseColorText =
            _localize(
                "Settings.ChooseColor");

        ClockColorButton.Content =
            chooseColorText;

        TimerAlarmColorButton.Content =
            chooseColorText;

        StopwatchColorButton.Content =
            chooseColorText;

        AlarmColorButton.Content =
            chooseColorText;

        CalendarEventColorButton.Content =
            chooseColorText;
    }

    private void LanguageChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Invoke(
            RefreshLocalizedNewControls);
    }

    private void ShowDateCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        UpdateDateFormatEnabledState();
        PublishPreview();
    }

    private void DateFormatComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        PublishPreview();
    }

    private void SoundComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        PublishPreview();
    }

    private void DisableDefaultHoverEffectCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        PublishPreview();
    }

    private void UpdateDateFormatEnabledState()
    {
        if (DateFormatComboBox is null)
        {
            return;
        }

        DateFormatComboBox.IsEnabled =
            ShowDateCheckBox.IsChecked == true;
    }

    private void OkButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        UpdatePreviewSettingsFromControls();

        string alarmSound =
            AlarmSoundComboBox.SelectedItem as string ??
            SystemDefaultSound;

        string timerSound =
            TimerSoundComboBox.SelectedItem as string ??
            SystemDefaultSound;

        _previewSettings.ShowDate =
            ShowDateCheckBox.IsChecked == true;

        _previewSettings.DateFormatId =
            DateFormatComboBox.SelectedValue as string ??
            "GermanShort";

        _previewSettings.TimeScalePercent =
            Math.Clamp(
                TimeScaleSlider.Value,
                50,
                150);

        _previewSettings.DateScalePercent =
            Math.Clamp(
                DateScaleSlider.Value,
                50,
                150);

        _previewSettings.DisableDefaultHoverEffect =
            DisableDefaultHoverEffectCheckBox.IsChecked == true;

        _previewSettings.AlarmSoundFile =
            string.Equals(
                alarmSound,
                SystemDefaultSound,
                StringComparison.Ordinal)
                ? string.Empty
                : alarmSound;

        _previewSettings.TimerSoundFile =
            string.Equals(
                timerSound,
                SystemDefaultSound,
                StringComparison.Ordinal)
                ? string.Empty
                : timerSound;

        _previewSettings.CalendarUpcomingEventCount =
            Math.Clamp(
                (int)Math.Round(
                    CalendarUpcomingEventCountSlider.Value),
                1,
                4);

        _previewSettings.CalendarUpcomingDays =
            Math.Clamp(
                (int)Math.Round(
                    CalendarUpcomingDaysSlider.Value),
                1,
                30);

        _previewSettings.CalendarUpcomingFontSize =
            Math.Clamp(
                CalendarUpcomingFontSizeSlider.Value,
                8,
                14);

        _previewSettings.CalendarCompanionDoubleWidth =
            CalendarCompanionDoubleWidthCheckBox.IsChecked == true;

        Result =
            CloneSettings(
                _previewSettings);

        DialogResult = true;
    }

    private static ClockWidgetSettings CloneSettings(
        ClockWidgetSettings source)
    {
        return new ClockWidgetSettings
        {
            ShowDate =
                source.ShowDate,
            ClockSettingsOpacityOverrideEnabled =
                source.ClockSettingsOpacityOverrideEnabled,
            ClockSettingsOpacity =
                source.ClockSettingsOpacity,
            ClockSettingsBlurOverrideEnabled =
                source.ClockSettingsBlurOverrideEnabled,
            ClockSettingsBlurRadius =
                source.ClockSettingsBlurRadius,
            DateFormatId =
                source.DateFormatId,
            TimeScalePercent =
                source.TimeScalePercent,
            DateScalePercent =
                source.DateScalePercent,
            TimeYOffset =
                source.TimeYOffset,
            DateYOffset =
                source.DateYOffset,
            ClockColor =
                source.ClockColor,
            TimerAlarmColor =
                source.TimerAlarmColor,
            StopwatchColor =
                source.StopwatchColor,
            TimerCompanionFontSize =
                source.TimerCompanionFontSize,
            TimerCompanionColor =
                source.TimerCompanionColor,
            TimerCompanionSettingsOpacityOverrideEnabled =
                source.TimerCompanionSettingsOpacityOverrideEnabled,
            TimerCompanionSettingsOpacity =
                source.TimerCompanionSettingsOpacity,
            TimerCompanionSettingsBlurOverrideEnabled =
                source.TimerCompanionSettingsBlurOverrideEnabled,
            TimerCompanionSettingsBlurRadius =
                source.TimerCompanionSettingsBlurRadius,
            StopwatchCompanionFontSize =
                source.StopwatchCompanionFontSize,
            StopwatchCompanionColor =
                source.StopwatchCompanionColor,
            StopwatchCompanionSettingsOpacityOverrideEnabled =
                source.StopwatchCompanionSettingsOpacityOverrideEnabled,
            StopwatchCompanionSettingsOpacity =
                source.StopwatchCompanionSettingsOpacity,
            StopwatchCompanionSettingsBlurOverrideEnabled =
                source.StopwatchCompanionSettingsBlurOverrideEnabled,
            StopwatchCompanionSettingsBlurRadius =
                source.StopwatchCompanionSettingsBlurRadius,
            AlarmColor =
                source.AlarmColor,
            AlarmCompanionTimeFontSize =
                source.AlarmCompanionTimeFontSize,
            AlarmCompanionPositionFontSize =
                source.AlarmCompanionPositionFontSize,
            AlarmCompanionIndexOffsetX =
                source.AlarmCompanionIndexOffsetX,
            AlarmCompanionIndexOffsetY =
                source.AlarmCompanionIndexOffsetY,
            AlarmCompanionTimeColor =
                source.AlarmCompanionTimeColor,
            AlarmCompanionIndexColor =
                source.AlarmCompanionIndexColor,
            AlarmCompanionSettingsOpacityOverrideEnabled =
                source.AlarmCompanionSettingsOpacityOverrideEnabled,
            AlarmCompanionSettingsOpacity =
                source.AlarmCompanionSettingsOpacity,
            AlarmCompanionSettingsBlurOverrideEnabled =
                source.AlarmCompanionSettingsBlurOverrideEnabled,
            AlarmCompanionSettingsBlurRadius =
                source.AlarmCompanionSettingsBlurRadius,
            CalendarEventColor =
                source.CalendarEventColor,
            CalendarUpcomingDays =
                source.CalendarUpcomingDays,
            CalendarUpcomingEventCount =
                source.CalendarUpcomingEventCount,
            CalendarUpcomingFontSize =
                source.CalendarUpcomingFontSize,
            CalendarCompanionDoubleWidth =
                source.CalendarCompanionDoubleWidth,
            CalendarCompanionSettingsOpacityOverrideEnabled =
                source.CalendarCompanionSettingsOpacityOverrideEnabled,
            CalendarCompanionSettingsOpacity =
                source.CalendarCompanionSettingsOpacity,
            CalendarCompanionSettingsBlurOverrideEnabled =
                source.CalendarCompanionSettingsBlurOverrideEnabled,
            CalendarCompanionSettingsBlurRadius =
                source.CalendarCompanionSettingsBlurRadius,
            CalendarOverrides =
                new ClockWidgetCalendarOverrideSettings
                {
                    ThemeOverrideEnabled =
                        source.CalendarOverrides.ThemeOverrideEnabled,
                    ThemeName =
                        source.CalendarOverrides.ThemeName,
                    OpacityOverrideEnabled =
                        source.CalendarOverrides.OpacityOverrideEnabled,
                    Opacity =
                        source.CalendarOverrides.Opacity,
                    BlurOverrideEnabled =
                        source.CalendarOverrides.BlurOverrideEnabled,
                    BlurRadius =
                        source.CalendarOverrides.BlurRadius,
                    BorderOverrideEnabled =
                        source.CalendarOverrides.BorderOverrideEnabled,
                    BorderColor =
                        source.CalendarOverrides.BorderColor,
                    BorderThickness =
                        source.CalendarOverrides.BorderThickness,
                    CornerRadiusOverrideEnabled =
                        source.CalendarOverrides.CornerRadiusOverrideEnabled,
                    CornerRadius =
                        source.CalendarOverrides.CornerRadius,
                    TextColorOverrideEnabled =
                        source.CalendarOverrides.TextColorOverrideEnabled,
                    TextColor =
                        source.CalendarOverrides.TextColor,
                    EventColorOverrideEnabled =
                        source.CalendarOverrides.EventColorOverrideEnabled,
                    EventColor =
                        source.CalendarOverrides.EventColor
                },
            DisableDefaultHoverEffect =
                source.DisableDefaultHoverEffect,
            AlarmSoundFile =
                source.AlarmSoundFile,
            TimerSoundFile =
                source.TimerSoundFile
        };
    }

    private static Color ParseColor(
        string value,
        Color fallback)
    {
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

    private static string ToRgbHex(
        Color color)
    {
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "#{0:X2}{1:X2}{2:X2}",
            color.R,
            color.G,
            color.B);
    }
}
