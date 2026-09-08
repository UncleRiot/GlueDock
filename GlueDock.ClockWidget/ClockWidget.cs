using System.Globalization;
using System.IO;
using System.Media;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfPath = System.Windows.Shapes.Path;
using System.Windows.Threading;
using GlueDock;

namespace GlueDock.ClockWidget;

public sealed class ClockWidget : IGlueDockWidget
{
    private enum CompanionMode
    {
        None,
        Alarm,
        Timer,
        Stopwatch,
        Calendar
    }

    private sealed record PendingAlert(
        string Source,
        string Id,
        string Title,
        string Note);

    private static readonly CultureInfo GermanCulture =
        CultureInfo.GetCultureInfo(
            "de-DE");

    private readonly Queue<PendingAlert> _pendingAlerts =
        new();

    private DispatcherTimer? _timer;
    private DispatcherTimer? _stopwatchRefreshTimer;
    private DispatcherTimer? _timerAlarmBlinkTimer;
    private DispatcherTimer? _timerAlarmSoundTimer;
    private MediaPlayer? _timerAlarmMediaPlayer;
    private DispatcherTimer? _alarmBlinkTimer;
    private DispatcherTimer? _alarmSoundTimer;
    private MediaPlayer? _alarmMediaPlayer;
    private TextBlock? _timeText;
    private TextBlock? _dateText;
    private Grid? _companionRoot;
    private StackPanel? _alarmCompanionContent;
    private StackPanel? _timerCompanionContent;
    private StackPanel? _stopwatchCompanionContent;
    private StackPanel? _calendarCompanionContent;
    private string? _calendarCompanionRenderKey;
    private TextBox? _companionAlarmInput;
    private TextBlock? _companionAlarmPositionText;
    private Button? _companionAlarmAddButton;
    private Button? _companionAlarmToggleButton;
    private TextBox? _companionTimerInput;
    private Button? _companionPlayPauseButton;
    private Button? _companionStopButton;
    private TextBlock? _companionStopwatchText;
    private Button? _companionStopwatchPlayPauseButton;
    private Button? _companionStopwatchStopButton;
    private CompanionMode _companionMode;
    private int _selectedAlarmIndex;
    private bool _alarmSignalActive;
    private bool _alarmBlinkOn;
    private string? _activeAlarmId;
    private readonly Queue<string> _pendingAlarmIds =
        new();
    private bool _timerAlarmActive;
    private bool _timerAlarmBlinkOn;
    private string? _timerAlarmId;
    private bool _lastHasCompanionView;
    private bool _lastIsAlarmActive;
    private bool _companionClosedByUser;
    private GlueDockWidgetContext? _context;
    private GlueDockWidgetAppearance? _hostAppearance;
    private readonly List<ClockWidgetCalendarWindow> _openCalendarWindows =
        new();
    private ClockWidgetAlarmCompanionSettingsWindow? _alarmCompanionSettingsWindow;
    private ClockWidgetAlarmNamePromptWindow? _alarmNamePromptWindow;
    private ClockWidgetCompanionSettingsWindow? _timerCompanionSettingsWindow;
    private ClockWidgetCompanionSettingsWindow? _stopwatchCompanionSettingsWindow;
    private ClockWidgetCompanionSettingsWindow? _calendarCompanionSettingsWindow;
    private readonly ClockWidgetSettings _settings =
        new();
    private ClockWidgetRuntimeState _runtimeState =
        new();
    private bool _alertVisible;

    public string DisplayName =>
        "Clock";

    public string Description =>
        "Clock with alarm, timer, stopwatch and calendar.";

    public bool HasSettings =>
        true;

    public bool HasAlarm =>
        true;

    public bool HasTimer =>
        true;

    public bool HasStopwatch =>
        true;

    public bool HasCalendar =>
        true;

    public bool IsAlarmActive =>
        _alarmSignalActive ||
        _runtimeState.Alarms.Any(
            alarm =>
                alarm.Enabled);

    public bool IsTimerActive =>
        _companionMode == CompanionMode.Timer ||
        _timerAlarmActive ||
        GetActiveTimers().Count > 0;

    public bool IsStopwatchActive =>
        _companionMode == CompanionMode.Stopwatch ||
        _runtimeState.Stopwatch.IsRunning;

    public bool IsCalendarActive =>
        _openCalendarWindows.Any(
            window =>
                window.IsLoaded &&
                window.IsVisible);

    public bool DisableDefaultHoverEffect =>
        _settings.DisableDefaultHoverEffect;

    public bool HasCompanionView =>
        !_companionClosedByUser &&
        (_companionMode != CompanionMode.None ||
         _timerAlarmActive ||
         GetActiveTimers().Count > 0 ||
         _runtimeState.Stopwatch.IsRunning ||
         GetUpcomingCalendarEvents().Count > 0);

    public int CompanionSlotSpan =>
        _settings.CalendarCompanionDoubleWidth &&
        (_companionMode == CompanionMode.Calendar ||
         (_companionMode == CompanionMode.None &&
          !_alarmSignalActive &&
          !_timerAlarmActive &&
          !_runtimeState.Stopwatch.IsRunning &&
          GetActiveTimers().Count == 0 &&
          GetUpcomingCalendarEvents().Count > 0))
            ? 2
            : 1;

    public event EventHandler? CompanionViewChanged;

    public void Initialize(
        GlueDockWidgetContext context)
    {
        _context =
            context;

        ClockWidgetLog.Writer =
            context.Log;

        context.SubscribeLanguageChanged?.Invoke(
            Context_LanguageChanged);

        ClockWidgetSettings loadedSettings =
            LoadSettings();

        loadedSettings.CalendarOverrides ??=
            new ClockWidgetCalendarOverrideSettings();

        _settings.CopyFrom(
            loadedSettings);

        _runtimeState =
            LoadRuntimeState();

        Directory.CreateDirectory(
            GetSoundsDirectory());

        NormalizeRunningTimers();

        if (_runtimeState.Stopwatch.IsRunning)
        {
            _companionMode =
                CompanionMode.Stopwatch;
        }
        else if (GetActiveTimers().Count > 0)
        {
            _companionMode =
                CompanionMode.Timer;
        }

        _lastHasCompanionView =
            HasCompanionView;

        _lastIsAlarmActive =
            IsAlarmActive;

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Initialized; InstanceId={context.InstanceId}; Alarms={_runtimeState.Alarms.Count}; Timers={_runtimeState.Timers.Count}; StopwatchRunning={_runtimeState.Stopwatch.IsRunning}");
    }

    public FrameworkElement CreateView()
    {
        Grid root =
            new()
            {
                Width = 52,
                Height = 52,
                Background = Brushes.Transparent
            };

        // The dock host handles mouse input on the parent DockItem first.
        // Direct clock access is therefore resolved on this widget root via the handled preview mouse-up route.
        root.AddHandler(
            UIElement.PreviewMouseLeftButtonUpEvent,
            new MouseButtonEventHandler(
                ClockRoot_PreviewMouseLeftButtonUp),
            true);

        _timeText =
            new TextBlock
            {
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                LineHeight = double.NaN,
                Margin =
                    new Thickness(
                        0)
            };

        _dateText =
            new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                TextAlignment = TextAlignment.Center,
                LineHeight = double.NaN,
                Margin =
                    new Thickness(
                        0)
            };

        root.Children.Add(
            _timeText);

        root.Children.Add(
            _dateText);

        ApplyClockAppearance();
        UpdateText();

        _timer =
            new DispatcherTimer(
                DispatcherPriority.Background)
            {
                Interval =
                    TimeSpan.FromSeconds(
                        1)
            };

        _timer.Tick +=
            Timer_Tick;

        _timer.Start();

        ProcessAlarmsAndTimers();

        return root;
    }

    private void ClockRoot_PreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            sender is not Grid root)
        {
            return;
        }

        Window? owner =
            Window.GetWindow(
                root);

        if (owner is null)
        {
            return;
        }

        Point position =
            e.GetPosition(
                root);

        double splitY =
            root.ActualHeight > 0
                ? root.ActualHeight / 2
                : 26;

        e.Handled =
            true;

        if (position.Y < splitY)
        {
            CycleClockCompanion();

            ClockWidgetLog.Write(
                "Widget.Clock",
                $"Clock root clicked; Zone=Time; X={position.X:0.##}; Y={position.Y:0.##}; CompanionMode={_companionMode}.");

            return;
        }

        ToggleCalendar(
            owner);

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Clock root clicked; Zone=Date; X={position.X:0.##}; Y={position.Y:0.##}; Calendar toggled.");
    }

    private void CycleClockCompanion()
    {
        bool wasClosedByUser =
            _companionClosedByUser;

        _companionClosedByUser =
            false;

        if (!wasClosedByUser)
        {
            _companionMode =
                _companionMode switch
                {
                    CompanionMode.Alarm =>
                        CompanionMode.Timer,
                    CompanionMode.Timer =>
                        CompanionMode.Stopwatch,
                    CompanionMode.Stopwatch =>
                        CompanionMode.Calendar,
                    CompanionMode.Calendar =>
                        CompanionMode.Alarm,
                    _ =>
                        CompanionMode.Alarm
                };
        }
        else if (_companionMode == CompanionMode.None)
        {
            _companionMode =
                CompanionMode.Alarm;
        }

        if (_companionMode == CompanionMode.Alarm)
        {
            ClampSelectedAlarmIndex();
        }

        UpdateCompanionView();
        NotifyCompanionIfNeeded(
            force: true);

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Clock companion cycled; Mode={_companionMode}; AlarmActive={IsAlarmActive}; TimerActive={IsTimerActive}; StopwatchActive={IsStopwatchActive}; CalendarEvents={GetUpcomingCalendarEvents().Count}; StopwatchRunning={_runtimeState.Stopwatch.IsRunning}; RunningTimers={GetActiveTimers().Count}");
    }

    private void TimeText_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            sender is not DependencyObject source)
        {
            return;
        }

        Window? owner =
            Window.GetWindow(
                source);

        if (owner is null)
        {
            return;
        }

        e.Handled =
            true;

        OpenAlarm(
            owner);

        ClockWidgetLog.Write(
            "Widget.Clock",
            "Clock time clicked; Alarm companion requested.");
    }

    private void DateText_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            sender is not DependencyObject source)
        {
            return;
        }

        Window? owner =
            Window.GetWindow(
                source);

        if (owner is null)
        {
            return;
        }

        e.Handled =
            true;

        OpenCalendar(
            owner);

        ClockWidgetLog.Write(
            "Widget.Clock",
            "Clock date clicked; Calendar opened.");
    }

    public void ApplyHostAppearance(
        GlueDockWidgetAppearance appearance)
    {
        _hostAppearance =
            appearance;

        foreach (ClockWidgetCalendarWindow calendarWindow in
                 _openCalendarWindows.ToArray())
        {
            if (!calendarWindow.IsLoaded)
            {
                continue;
            }

            calendarWindow.ApplyHostAppearance(
                appearance);
        }

        ClockWidgetLog.Write(
            "Widget.Clock.CalendarTheme",
            $"Host appearance applied; Theme={appearance.ThemeName}; Opacity={appearance.Opacity:0.00}; Blur={appearance.BlurRadius:0.##}; OpenWindows={_openCalendarWindows.Count}");
    }

    public IReadOnlyList<GlueDockWidgetSettingsSection> CreateSettingsSections()
    {
        if (_hostAppearance is null)
        {
            return Array.Empty<GlueDockWidgetSettingsSection>();
        }

        ClockWidgetSettingsWindow generalWindow =
            new(
                _settings,
                GetSoundsDirectory(),
                CommitEmbeddedSettings,
                _hostAppearance,
                L,
                _context?.SubscribeLanguageChanged,
                _context?.UnsubscribeLanguageChanged);

        ClockWidgetAlarmCompanionSettingsWindow alarmWindow =
            new(
                _settings,
                _runtimeState,
                _hostAppearance,
                () =>
                {
                    SaveSettings();
                    UpdateAlarmCompanionView();
                },
                () =>
                {
                    SaveRuntimeStateAndNotifyCompanion();
                    UpdateAlarmCompanionView();
                },
                L,
                GetSoundsDirectory());

        ClockWidgetCompanionSettingsWindow timerWindow =
            new(
                _settings,
                _hostAppearance,
                () =>
                {
                    SaveSettings();
                    UpdateTimerCompanionView();
                },
                ClockWidgetCompanionSettingsKind.Timer,
                L,
                GetSoundsDirectory());

        ClockWidgetCompanionSettingsWindow stopwatchWindow =
            new(
                _settings,
                _hostAppearance,
                () =>
                {
                    SaveSettings();
                    UpdateStopwatchCompanionView();
                },
                ClockWidgetCompanionSettingsKind.Stopwatch,
                L,
                GetSoundsDirectory());

        ClockWidgetCompanionSettingsWindow calendarWindow =
            new(
                _settings,
                _hostAppearance,
                () =>
                {
                    SaveSettings();
                    _calendarCompanionRenderKey =
                        null;

                    UpdateCalendarCompanionView();
                    UpdateCompanionView();
                    NotifyCompanionIfNeeded(
                        force: true);
                },
                ClockWidgetCompanionSettingsKind.Calendar,
                L,
                GetSoundsDirectory());

        ClockWidgetCalendarOverrideSettingsWindow calendarOverrideWindow =
            new(
                _settings.CalendarOverrides,
                _hostAppearance.AvailableThemes,
                _hostAppearance,
                GetCalendarEventColor(),
                SaveCalendarOverrideSettings);

        FrameworkElement generalContent =
            generalWindow.CreateEmbeddedSettingsContent();

        FrameworkElement alarmContent =
            alarmWindow.CreateEmbeddedSettingsContent();

        FrameworkElement timerContent =
            timerWindow.CreateEmbeddedSettingsContent();

        FrameworkElement stopwatchContent =
            stopwatchWindow.CreateEmbeddedSettingsContent();

        FrameworkElement calendarContent =
            calendarWindow.CreateEmbeddedSettingsContent();

        FrameworkElement calendarOverrideContent =
            calendarOverrideWindow.CreateEmbeddedSettingsContent();

        return new[]
        {
            new GlueDockWidgetSettingsSection(
                "Clock",
                "Settings.Navigation.WidgetClock",
                100,
                "General",
                "Settings.Tab.General",
                0,
                generalContent,
                generalWindow.Close),
            new GlueDockWidgetSettingsSection(
                "Clock",
                "Settings.Navigation.WidgetClock",
                100,
                "AlarmCompanion",
                "Settings.WidgetSection.AlarmCompanion",
                1,
                alarmContent,
                alarmWindow.Close),
            new GlueDockWidgetSettingsSection(
                "Clock",
                "Settings.Navigation.WidgetClock",
                100,
                "TimerCompanion",
                "Settings.WidgetSection.TimerCompanion",
                2,
                timerContent,
                timerWindow.Close),
            new GlueDockWidgetSettingsSection(
                "Clock",
                "Settings.Navigation.WidgetClock",
                100,
                "StopwatchCompanion",
                "Settings.WidgetSection.StopwatchCompanion",
                3,
                stopwatchContent,
                stopwatchWindow.Close),
            new GlueDockWidgetSettingsSection(
                "Clock",
                "Settings.Navigation.WidgetClock",
                100,
                "CalendarCompanion",
                "Settings.WidgetSection.CalendarCompanion",
                4,
                calendarContent,
                calendarWindow.Close),
            new GlueDockWidgetSettingsSection(
                "Clock",
                "Settings.Navigation.WidgetClock",
                100,
                "CalendarOverrides",
                "Settings.WidgetSection.CalendarOverrides",
                5,
                calendarOverrideContent,
                calendarOverrideWindow.Close)
        };
    }

    public void CloseCompanion()
    {
        _companionClosedByUser =
            true;

        UpdateCompanionView();
        NotifyCompanionIfNeeded(
            force: true);

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Companion closed by host; AlarmActive={IsAlarmActive}; TimerActive={IsTimerActive}; StopwatchActive={IsStopwatchActive}; RunningTimers={GetActiveTimers().Count}; StopwatchRunning={_runtimeState.Stopwatch.IsRunning}");
    }

    public FrameworkElement? CreateCompanionView()
    {
        if (!HasCompanionView)
        {
            return null;
        }

        Grid root =
            new()
            {
                Width = 52,
                Height = 52,
                Background = Brushes.Transparent
            };

        root.PreviewMouseLeftButtonDown +=
            CompanionRoot_PreviewMouseLeftButtonDown;

        root.PreviewMouseRightButtonDown +=
            CompanionRoot_PreviewMouseRightButtonDown;

        _alarmCompanionContent =
            CreateAlarmCompanionContent();

        _timerCompanionContent =
            CreateTimerCompanionContent();

        _stopwatchCompanionContent =
            CreateStopwatchCompanionContent();

        _calendarCompanionContent =
            CreateCalendarCompanionContent();

        _calendarCompanionRenderKey =
            null;

        root.Children.Add(
            _alarmCompanionContent);

        root.Children.Add(
            _timerCompanionContent);

        root.Children.Add(
            _stopwatchCompanionContent);

        root.Children.Add(
            _calendarCompanionContent);

        _companionRoot =
            root;

        UpdateCompanionView();

        return root;
    }

    private StackPanel CreateAlarmCompanionContent()
    {
        Color alarmColor =
            GetAlarmColor();

        Color alarmTimeColor =
            GetAlarmCompanionTimeColor();

        Color alarmIndexColor =
            GetAlarmCompanionIndexColor();

        StackPanel content =
            new()
            {
                Width = 52,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = L("Widget.Clock.AlarmCompanion.AlarmTime")
            };

        Grid header =
            new()
            {
                Width = 52,
                Height = 21,
                Margin =
                    new Thickness(
                        2,
                        0,
                        2,
                        2)
            };

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        38)
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        14)
            });

        _companionAlarmInput =
            new TextBox
            {
                Width = 38,
                Height = 21,
                MaxLength = 5,
                FontSize =
                    Math.Clamp(
                        _settings.AlarmCompanionTimeFontSize,
                        10,
                        20),
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding =
                    new Thickness(
                        0),
                Background = Brushes.Transparent,
                Foreground =
                    new SolidColorBrush(
                        alarmTimeColor),
                CaretBrush =
                    new SolidColorBrush(
                        alarmTimeColor),
                BorderThickness =
                    new Thickness(
                        0),
                Text = "00:00",
                ToolTip = L("Widget.Clock.AlarmCompanion.AlarmTime")
            };

        _companionAlarmInput.PreviewTextInput +=
            CompanionAlarmInput_PreviewTextInput;

        _companionAlarmInput.LostKeyboardFocus +=
            CompanionAlarmInput_LostKeyboardFocus;

        _companionAlarmInput.KeyDown +=
            CompanionAlarmInput_KeyDown;

        _companionAlarmPositionText =
            new TextBlock
            {
                FontSize =
                    Math.Clamp(
                        _settings.AlarmCompanionPositionFontSize,
                        7,
                        14),
                Foreground =
                    new SolidColorBrush(
                        alarmIndexColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                ToolTip = L("Widget.Clock.AlarmCompanion.IndexNavigationHint")
            };

        _companionAlarmPositionText.MouseLeftButtonDown +=
            CompanionAlarmPositionText_MouseLeftButtonDown;

        _companionAlarmPositionText.MouseRightButtonDown +=
            CompanionAlarmPositionText_MouseRightButtonDown;

        Grid.SetColumn(
            _companionAlarmInput,
            0);

        Grid.SetColumn(
            _companionAlarmPositionText,
            1);

        header.Children.Add(
            _companionAlarmInput);

        header.Children.Add(
            _companionAlarmPositionText);

        Grid controls =
            CreateCompanionControlsGrid();

        _companionAlarmAddButton =
            CreateRoundCompanionButton(
                "+",
                alarmColor);

        _companionAlarmToggleButton =
            CreateRoundCompanionButton(
                "○",
                alarmColor);

        _companionAlarmAddButton.ToolTip =
            L(
                "Widget.Clock.AlarmCompanion.AddAlarm");

        _companionAlarmToggleButton.ToolTip =
            L(
                "Widget.Clock.AlarmCompanion.EnableHint");

        _companionAlarmAddButton.Click +=
            CompanionAlarmAddButton_Click;

        _companionAlarmToggleButton.Click +=
            CompanionAlarmToggleButton_Click;

        Grid.SetColumn(
            _companionAlarmAddButton,
            0);

        Grid.SetColumn(
            _companionAlarmToggleButton,
            1);

        controls.Children.Add(
            _companionAlarmAddButton);

        controls.Children.Add(
            _companionAlarmToggleButton);

        content.Children.Add(
            header);

        content.Children.Add(
            controls);

        content.PreviewMouseWheel +=
            AlarmCompanionContent_PreviewMouseWheel;

        content.ContextMenu =
            CreateAlarmCompanionContextMenu();

        header.ContextMenu =
            CreateAlarmCompanionContextMenu();

        _companionAlarmInput.ContextMenu =
            CreateAlarmCompanionContextMenu();

        _companionAlarmPositionText.ContextMenu =
            CreateAlarmCompanionContextMenu();

        controls.ContextMenu =
            CreateAlarmCompanionContextMenu();

        _companionAlarmAddButton.ContextMenu =
            CreateAlarmCompanionContextMenu();

        _companionAlarmToggleButton.ContextMenu =
            CreateAlarmCompanionContextMenu();

        return content;
    }

    private System.Windows.Controls.ContextMenu CreateAlarmCompanionContextMenu()
    {
        System.Windows.Controls.ContextMenu contextMenu =
            new();

        MenuItem addAlarmMenuItem =
            new()
            {
                Header = L("Widget.Clock.AlarmCompanion.AddAlarm")
            };

        addAlarmMenuItem.Click +=
            CompanionAlarmAddMenuItem_Click;

        MenuItem removeAlarmMenuItem =
            new()
            {
                Header = L("Widget.Clock.AlarmCompanion.RemoveAlarm")
            };

        removeAlarmMenuItem.Click +=
            CompanionAlarmRemoveMenuItem_Click;

        contextMenu.Items.Add(
            addAlarmMenuItem);

        contextMenu.Items.Add(
            removeAlarmMenuItem);

        contextMenu.Items.Add(
            new Separator());

        MenuItem closeCompanionMenuItem =
            new()
            {
                Header = L("Widget.Clock.AlarmCompanion.CloseCompanion")
            };

        closeCompanionMenuItem.Click +=
            CompanionCloseMenuItem_Click;

        contextMenu.Items.Add(
            closeCompanionMenuItem);

        return contextMenu;
    }

    private StackPanel CreateTimerCompanionContent()
    {
        StackPanel content =
            new()
            {
                Width = 52,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

        _companionTimerInput =
            new TextBox
            {
                Width = 48,
                Height = 21,
                MaxLength = 5,
                FontSize =
                    Math.Clamp(
                        _settings.TimerCompanionFontSize,
                        8,
                        16),
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding =
                    new Thickness(
                        0),
                Margin =
                    new Thickness(
                        2,
                        0,
                        2,
                        2),
                Background = Brushes.Transparent,
                Foreground =
                    new SolidColorBrush(
                        GetTimerCompanionColor()),
                CaretBrush =
                    new SolidColorBrush(
                        GetTimerCompanionColor()),
                BorderBrush =
                    new SolidColorBrush(
                        Color.FromArgb(
                            130,
                            GetTimerCompanionColor().R,
                            GetTimerCompanionColor().G,
                            GetTimerCompanionColor().B)),
                Text = "00:00.000"
            };

        _companionTimerInput.PreviewTextInput +=
            CompanionTimerInput_PreviewTextInput;

        _companionTimerInput.LostKeyboardFocus +=
            CompanionTimerInput_LostKeyboardFocus;

        _companionTimerInput.GotKeyboardFocus +=
            CompanionTimerInput_GotKeyboardFocus;

        Grid controls =
            CreateCompanionControlsGrid();

        Color timerColor =
            GetTimerCompanionColor();

        _companionPlayPauseButton =
            CreateRoundCompanionButton(
                "▶",
                timerColor);

        _companionStopButton =
            CreateRoundCompanionButton(
                "■",
                timerColor);

        _companionPlayPauseButton.Click +=
            CompanionPlayPauseButton_Click;

        _companionStopButton.Click +=
            CompanionStopButton_Click;

        Grid.SetColumn(
            _companionPlayPauseButton,
            0);

        Grid.SetColumn(
            _companionStopButton,
            1);

        controls.Children.Add(
            _companionPlayPauseButton);

        controls.Children.Add(
            _companionStopButton);

        content.Children.Add(
            _companionTimerInput);

        content.Children.Add(
            controls);

        return content;
    }

    private StackPanel CreateStopwatchCompanionContent()
    {
        StackPanel content =
            new()
            {
                Width = 52,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

        Color stopwatchDisplayColor =
            GetStopwatchCompanionColor();

        _companionStopwatchText =
            new TextBlock
            {
                FontSize =
                    Math.Clamp(
                        _settings.StopwatchCompanionFontSize,
                        8,
                        16),
                FontWeight = FontWeights.SemiBold,
                Foreground =
                    new SolidColorBrush(
                        stopwatchDisplayColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Text = "00:00"
            };

        Border stopwatchDisplay =
            new()
            {
                Width = 48,
                Height = 21,
                Margin =
                    new Thickness(
                        2,
                        0,
                        2,
                        2),
                Background = Brushes.Transparent,
                BorderBrush =
                    new SolidColorBrush(
                        Color.FromArgb(
                            130,
                            stopwatchDisplayColor.R,
                            stopwatchDisplayColor.G,
                            stopwatchDisplayColor.B)),
                BorderThickness =
                    new Thickness(
                        1),
                Child =
                    _companionStopwatchText
            };

        Typography.SetNumeralAlignment(
            _companionStopwatchText,
            FontNumeralAlignment.Tabular);

        Grid controls =
            CreateCompanionControlsGrid();

        Color stopwatchColor =
            GetStopwatchCompanionColor();

        _companionStopwatchPlayPauseButton =
            CreateRoundCompanionButton(
                "▶",
                stopwatchColor);

        _companionStopwatchStopButton =
            CreateRoundCompanionButton(
                "■",
                stopwatchColor);

        _companionStopwatchPlayPauseButton.Click +=
            CompanionStopwatchPlayPauseButton_Click;

        _companionStopwatchStopButton.Click +=
            CompanionStopwatchStopButton_Click;

        Grid.SetColumn(
            _companionStopwatchPlayPauseButton,
            0);

        Grid.SetColumn(
            _companionStopwatchStopButton,
            1);

        controls.Children.Add(
            _companionStopwatchPlayPauseButton);

        controls.Children.Add(
            _companionStopwatchStopButton);

        content.Children.Add(
            stopwatchDisplay);

        content.Children.Add(
            controls);

        return content;
    }

    private StackPanel CreateCalendarCompanionContent()
    {
        StackPanel content =
            new()
            {
                Width = 52,
                Height = 52,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent,
                ToolTip = "Upcoming calendar events"
            };

        content.ContextMenu =
            CreateCalendarCompanionContextMenu();

        return content;
    }

    private List<ClockWidgetCalendarEventEntry> GetUpcomingCalendarEvents()
    {
        DateTime now =
            DateTime.Now;

        DateTime latest =
            now.AddDays(
                Math.Clamp(
                    _settings.CalendarUpcomingDays,
                    1,
                    30));

        int maximumCount =
            Math.Clamp(
                _settings.CalendarUpcomingEventCount,
                1,
                4);

        return _runtimeState.CalendarEvents
            .Where(
                calendarEvent =>
                    calendarEvent.IsAllDay
                        ? calendarEvent.StartAt.Date >= now.Date &&
                          calendarEvent.StartAt.Date <= latest.Date
                        : calendarEvent.StartAt >= now &&
                          calendarEvent.StartAt <= latest)
            .OrderBy(
                calendarEvent =>
                    calendarEvent.StartAt)
            .Take(
                maximumCount)
            .ToList();
    }

    private string FormatCalendarCompanionDate(
        DateTime value)
    {
        return _settings.DateFormatId switch
        {
            "UsLong" =>
                value.ToString(
                    "MM/dd",
                    CultureInfo.InvariantCulture),

            "IsoLong" =>
                value.ToString(
                    "MM-dd",
                    CultureInfo.InvariantCulture),

            _ =>
                value.ToString(
                    "dd.MM.",
                    GermanCulture)
        };
    }

    private System.Windows.Controls.ContextMenu CreateCalendarCompanionContextMenu(
        string? eventId = null)
    {
        System.Windows.Controls.ContextMenu contextMenu =
            new();

        MenuItem addEventMenuItem =
            new()
            {
                Header = "Add event"
            };

        addEventMenuItem.Click +=
            CalendarCompanionAddEventMenuItem_Click;

        contextMenu.Items.Add(
            addEventMenuItem);

        if (!string.IsNullOrWhiteSpace(
                eventId))
        {
            MenuItem deleteMenuItem =
                new()
                {
                    Header = "Delete Event",
                    Tag =
                        eventId
                };

            deleteMenuItem.Click +=
                CalendarCompanionDeleteMenuItem_Click;

            contextMenu.Items.Add(
                deleteMenuItem);
        }

        contextMenu.Items.Add(
            new Separator());

        MenuItem calendarCompanionSettingsMenuItem =
            new()
            {
                Header =
                    "Calendar companion settings"
            };

        calendarCompanionSettingsMenuItem.Click +=
            (_, _) =>
                OpenCalendarCompanionSettings();

        contextMenu.Items.Add(
            calendarCompanionSettingsMenuItem);

        contextMenu.Items.Add(
            new Separator());

        MenuItem closeCompanionMenuItem =
            new()
            {
                Header = L("Widget.Clock.AlarmCompanion.CloseCompanion")
            };

        closeCompanionMenuItem.Click +=
            CompanionCloseMenuItem_Click;

        contextMenu.Items.Add(
            closeCompanionMenuItem);

        return contextMenu;
    }

    private void CalendarCompanionAddEventMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Parent is not System.Windows.Controls.ContextMenu contextMenu ||
            contextMenu.PlacementTarget is not DependencyObject placementTarget)
        {
            return;
        }

        Window? owner =
            Window.GetWindow(
                placementTarget);

        if (owner is null)
        {
            return;
        }

        OpenCalendar(
            owner);
    }

    private void CompanionCloseMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        CloseCompanion();
    }

    private void UpdateCalendarCompanionView()
    {
        if (_calendarCompanionContent is null)
        {
            return;
        }

        List<ClockWidgetCalendarEventEntry> upcomingEvents =
            GetUpcomingCalendarEvents();

        Color eventColor =
            GetCalendarEventColor();

        double fontSize =
            Math.Clamp(
                _settings.CalendarUpcomingFontSize,
                8,
                14);

        string renderKey =
            string.Join(
                "|",
                upcomingEvents.Select(
                    calendarEvent =>
                        string.Join(
                            ";",
                            calendarEvent.Id,
                            calendarEvent.StartAt.Ticks.ToString(
                                CultureInfo.InvariantCulture),
                            calendarEvent.IsAllDay
                                ? "1"
                                : "0",
                            calendarEvent.Title))) +
            $"|{eventColor}|{fontSize.ToString(CultureInfo.InvariantCulture)}|{_settings.DateFormatId}|{_settings.CalendarCompanionDoubleWidth}";

        if (string.Equals(
                _calendarCompanionRenderKey,
                renderKey,
                StringComparison.Ordinal))
        {
            return;
        }

        ClockWidgetLog.Write(
            "Widget.Clock.CalendarPreview",
            $"Rebuild; Events={upcomingEvents.Count}; PreviousKeyChanged={_calendarCompanionRenderKey is not null}");

        _calendarCompanionRenderKey =
            renderKey;

        _calendarCompanionContent.Children.Clear();

        if (upcomingEvents.Count == 0)
        {
            return;
        }

        double rowHeight =
            48.0 /
            upcomingEvents.Count;

        foreach (ClockWidgetCalendarEventEntry calendarEvent in
                 upcomingEvents)
        {
            ToolTip eventToolTip =
                new()
                {
                    Content =
                        calendarEvent.IsAllDay
                            ? $"{calendarEvent.StartAt:dd.MM.yyyy}  All day  {calendarEvent.Title}"
                            : $"{calendarEvent.StartAt:dd.MM.yyyy HH:mm}  {calendarEvent.Title}"
                };

            eventToolTip.Opened +=
                (_, _) =>
                    ClockWidgetLog.Write(
                        "Widget.Clock.CalendarPreview",
                        $"Tooltip opened; EventId={calendarEvent.Id}; Start={calendarEvent.StartAt:O}; AllDay={calendarEvent.IsAllDay}");

            eventToolTip.Closed +=
                (_, _) =>
                    ClockWidgetLog.Write(
                        "Widget.Clock.CalendarPreview",
                        $"Tooltip closed; EventId={calendarEvent.Id}");

            FrameworkElementFactory eventContentPresenter =
                new(
                    typeof(
                        ContentPresenter));

            eventContentPresenter.SetBinding(
                ContentPresenter.ContentProperty,
                new Binding(
                    "Content")
                {
                    RelativeSource =
                        RelativeSource.TemplatedParent
                });

            eventContentPresenter.SetValue(
                ContentPresenter.HorizontalAlignmentProperty,
                HorizontalAlignment.Left);

            eventContentPresenter.SetValue(
                ContentPresenter.VerticalAlignmentProperty,
                VerticalAlignment.Center);

            FrameworkElementFactory eventButtonBorder =
                new(
                    typeof(
                        Border));

            eventButtonBorder.SetBinding(
                Border.BackgroundProperty,
                new Binding(
                    "Background")
                {
                    RelativeSource =
                        RelativeSource.TemplatedParent
                });

            eventButtonBorder.SetBinding(
                Border.BorderBrushProperty,
                new Binding(
                    "BorderBrush")
                {
                    RelativeSource =
                        RelativeSource.TemplatedParent
                });

            eventButtonBorder.SetBinding(
                Border.BorderThicknessProperty,
                new Binding(
                    "BorderThickness")
                {
                    RelativeSource =
                        RelativeSource.TemplatedParent
                });

            eventButtonBorder.SetBinding(
                Border.PaddingProperty,
                new Binding(
                    "Padding")
                {
                    RelativeSource =
                        RelativeSource.TemplatedParent
                });

            eventButtonBorder.SetValue(
                Border.CornerRadiusProperty,
                new CornerRadius(
                    3));

            eventButtonBorder.AppendChild(
                eventContentPresenter);

            ControlTemplate eventButtonTemplate =
                new(
                    typeof(
                        Button))
                {
                    VisualTree =
                        eventButtonBorder
                };

            Button eventButton =
                new()
                {
                    Height =
                        rowHeight,
                    Padding =
                        new Thickness(
                            1,
                            0,
                            1,
                            0),
                    Margin =
                        new Thickness(
                            0),
                    Background =
                        Brushes.Transparent,
                    BorderBrush =
                        Brushes.Transparent,
                    BorderThickness =
                        new Thickness(
                            1),
                    Foreground =
                        new SolidColorBrush(
                            eventColor),
                    FontSize =
                        fontSize,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Cursor =
                        Cursors.Hand,
                    Tag =
                        calendarEvent.Id,
                    ToolTip =
                        eventToolTip,
                    Template =
                        eventButtonTemplate,
                    Content =
                        new TextBlock
                        {
                            Text =
                                $"{FormatCalendarCompanionDate(calendarEvent.StartAt)} {calendarEvent.Title}",
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            TextWrapping = TextWrapping.NoWrap,
                            Foreground =
                                new SolidColorBrush(
                                    eventColor),
                            FontSize =
                                fontSize,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                };

            eventButton.MouseEnter +=
                (_, _) =>
                {
                    eventButton.Background =
                        new SolidColorBrush(
                            Color.FromArgb(
                                102,
                                48,
                                48,
                                48));

                    eventButton.BorderBrush =
                        new SolidColorBrush(
                            Color.FromArgb(
                                153,
                                255,
                                255,
                                255));

                    ClockWidgetLog.Write(
                        "Widget.Clock.CalendarPreview",
                        $"MouseEnter; EventId={calendarEvent.Id}");
                };

            eventButton.MouseLeave +=
                (_, _) =>
                {
                    eventButton.Background =
                        Brushes.Transparent;

                    eventButton.BorderBrush =
                        Brushes.Transparent;

                    ClockWidgetLog.Write(
                        "Widget.Clock.CalendarPreview",
                        $"MouseLeave; EventId={calendarEvent.Id}");
                };

            eventButton.Click +=
                CalendarCompanionEventButton_Click;

            eventButton.ContextMenu =
                CreateCalendarCompanionContextMenu(
                    calendarEvent.Id);

            _calendarCompanionContent.Children.Add(
                eventButton);
        }
    }

    private void CalendarCompanionEventButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not string eventId)
        {
            return;
        }

        Window? owner =
            Window.GetWindow(
                button);

        if (owner is null)
        {
            return;
        }

        OpenCalendar(
            owner,
            eventId);
    }

    private void CalendarCompanionDeleteMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Tag is not string eventId)
        {
            return;
        }

        ClockWidgetCalendarEventEntry? calendarEvent =
            _runtimeState.CalendarEvents.FirstOrDefault(
                item =>
                    item.Id ==
                    eventId);

        if (calendarEvent is null)
        {
            return;
        }

        Window? owner =
            menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu &&
            contextMenu.PlacementTarget is DependencyObject placementTarget
                ? Window.GetWindow(
                    placementTarget)
                : null;

        MessageBoxResult result =
            MessageBox.Show(
                owner,
                $"Delete event \"{calendarEvent.Title}\"?",
                "Delete event",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _runtimeState.CalendarEvents.Remove(
            calendarEvent);

        SaveRuntimeStateAndNotifyCompanion();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Calendar event removed from companion; Id={eventId}");
    }


    private static Grid CreateCompanionControlsGrid()
    {
        Grid controls =
            new()
            {
                Width = 52,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

        controls.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        26)
            });

        controls.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        26)
            });

        return controls;
    }

        private static Button CreateRoundCompanionButton(
        string symbol,
        Color mainColor)
    {
        FrameworkElementFactory contentPresenter =
            new(
                typeof(
                    ContentPresenter));

        contentPresenter.SetBinding(
            ContentPresenter.ContentProperty,
            new Binding(
                "Content")
            {
                RelativeSource =
                    RelativeSource.TemplatedParent
            });

        contentPresenter.SetValue(
            ContentPresenter.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);

        contentPresenter.SetValue(
            ContentPresenter.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        ControlTemplate template =
            new(
                typeof(
                    Button))
            {
                VisualTree =
                    contentPresenter
            };

        Button button =
            new()
            {
                Width = 26,
                Height = 26,
                Padding =
                    new Thickness(
                        0),
                Margin =
                    new Thickness(
                        0),
                BorderThickness =
                    new Thickness(
                        0),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Template =
                    template,
                Content =
                    CreateRoundCompanionButtonContent(
                        symbol,
                        mainColor)
            };

        button.ContextMenuOpening +=
            CompanionButton_ContextMenuOpening;

        return button;
    }


    private static void CompanionButton_ContextMenuOpening(
        object sender,
        ContextMenuEventArgs e)
    {
        e.Handled = true;
    }

        private static Border CreateRoundCompanionButtonContent(
        string symbol,
        Color mainColor)
    {
        SolidColorBrush mainBrush =
            new(
                mainColor);

        SolidColorBrush idleBackgroundBrush =
            new(
                Color.FromArgb(
                    28,
                    mainColor.R,
                    mainColor.G,
                    mainColor.B));

        Style borderStyle =
            new(
                typeof(
                    Border));

        borderStyle.Setters.Add(
            new Setter(
                Border.BackgroundProperty,
                idleBackgroundBrush));

        DataTrigger borderHoverTrigger =
            new()
            {
                Binding =
                    new Binding(
                        "IsMouseOver")
                    {
                        RelativeSource =
                            new RelativeSource(
                                RelativeSourceMode.FindAncestor,
                                typeof(
                                    Button),
                                1)
                    },
                Value = true
            };

        borderHoverTrigger.Setters.Add(
            new Setter(
                Border.BackgroundProperty,
                mainBrush));

        borderStyle.Triggers.Add(
            borderHoverTrigger);

        Grid symbolHost =
            new()
            {
                Width = 22,
                Height = 22,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        FrameworkElement symbolElement;

        if (symbol == "○")
        {
            Style symbolStyle =
                new(
                    typeof(
                        WpfEllipse));

            symbolStyle.Setters.Add(
                new Setter(
                    System.Windows.Shapes.Shape.StrokeProperty,
                    mainBrush));

            DataTrigger symbolHoverTrigger =
                new()
                {
                    Binding =
                        new Binding(
                            "IsMouseOver")
                        {
                            RelativeSource =
                                new RelativeSource(
                                    RelativeSourceMode.FindAncestor,
                                    typeof(
                                        Button),
                                    1)
                        },
                    Value = true
                };

            symbolHoverTrigger.Setters.Add(
                new Setter(
                    System.Windows.Shapes.Shape.StrokeProperty,
                    Brushes.Black));

            symbolStyle.Triggers.Add(
                symbolHoverTrigger);

            symbolElement =
                new WpfEllipse
                {
                    Width = 7,
                    Height = 7,
                    StrokeThickness = 1.4,
                    Style = symbolStyle,
                    HorizontalAlignment =
                        HorizontalAlignment.Center,
                    VerticalAlignment =
                        VerticalAlignment.Center
                };
        }
        else
        {
            Geometry symbolGeometry =
                symbol switch
                {
                    "+" =>
                        Geometry.Parse(
                            "M 4,0 L 6,0 L 6,4 L 10,4 L 10,6 L 6,6 L 6,10 L 4,10 L 4,6 L 0,6 L 0,4 L 4,4 Z"),
                    "▶" =>
                        Geometry.Parse(
                            "M 0,0 L 8,5 L 0,10 Z"),
                    "■" =>
                        Geometry.Parse(
                            "M 1,1 L 9,1 L 9,9 L 1,9 Z"),
                    "Ⅱ" =>
                        Geometry.Parse(
                            "M 1,0 L 4,0 L 4,10 L 1,10 Z M 6,0 L 9,0 L 9,10 L 6,10 Z"),
                    "✓" =>
                        Geometry.Parse(
                            "M 0,5 L 2,3 L 4,5 L 8,0 L 10,2 L 4,9 Z"),
                    _ =>
                        Geometry.Empty
                };

            Style symbolStyle =
                new(
                    typeof(
                        WpfPath));

            symbolStyle.Setters.Add(
                new Setter(
                    System.Windows.Shapes.Shape.FillProperty,
                    mainBrush));

            DataTrigger symbolHoverTrigger =
                new()
                {
                    Binding =
                        new Binding(
                            "IsMouseOver")
                        {
                            RelativeSource =
                                new RelativeSource(
                                    RelativeSourceMode.FindAncestor,
                                    typeof(
                                        Button),
                                    1)
                        },
                    Value = true
                };

            symbolHoverTrigger.Setters.Add(
                new Setter(
                    System.Windows.Shapes.Shape.FillProperty,
                    Brushes.Black));

            symbolStyle.Triggers.Add(
                symbolHoverTrigger);

            WpfPath path =
                new()
                {
                    Width =
                        symbol switch
                        {
                            "▶" => 7,
                            "■" => 7,
                            _ => 8
                        },
                    Height =
                        symbol == "■"
                            ? 7
                            : 8,
                    Data =
                        symbolGeometry,
                    Stretch =
                        Stretch.Fill,
                    Style =
                        symbolStyle,
                    HorizontalAlignment =
                        HorizontalAlignment.Center,
                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            symbolElement =
                path;
        }

        symbolHost.Children.Add(
            symbolElement);

        return new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius =
                new CornerRadius(
                    11),
            BorderThickness =
                new Thickness(
                    1),
            BorderBrush =
                mainBrush,
            Style =
                borderStyle,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center,
            Child =
                symbolHost
        };
    }


    // GlueDock rule: Every companion must provide its own right-click configuration menu
    // with plausible companion-specific settings and the shared GlueDock companion settings layout/theme/general-setting pattern.
    private void CompanionRoot_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton !=
            MouseButton.Right)
        {
            return;
        }

        System.Windows.Controls.ContextMenu contextMenu =
            new();

        if (_alarmCompanionContent?.Visibility ==
            Visibility.Visible)
        {
            MenuItem addAlarmMenuItem =
                new()
                {
                    Header =
                        L(
                            "Widget.Clock.AlarmCompanion.AddAlarm")
                };

            addAlarmMenuItem.Click +=
                CompanionAlarmAddMenuItem_Click;

            contextMenu.Items.Add(
                addAlarmMenuItem);

            MenuItem removeAlarmMenuItem =
                new()
                {
                    Header =
                        L(
                            "Widget.Clock.AlarmCompanion.RemoveAlarm")
                };

            removeAlarmMenuItem.Click +=
                CompanionAlarmRemoveMenuItem_Click;

            contextMenu.Items.Add(
                removeAlarmMenuItem);

            contextMenu.Items.Add(
                new Separator());

            MenuItem alarmCompanionSettingsMenuItem =
                new()
                {
                    Header =
                        L(
                            "Widget.Clock.AlarmCompanion.SettingsTitle")
                };

            alarmCompanionSettingsMenuItem.Click +=
                (_, _) =>
                    OpenAlarmCompanionSettings();

            contextMenu.Items.Add(
                alarmCompanionSettingsMenuItem);
        }

        if (_timerCompanionContent?.Visibility ==
            Visibility.Visible)
        {
            MenuItem timerCompanionSettingsMenuItem =
                new()
                {
                    Header =
                        "Timer companion settings"
                };

            timerCompanionSettingsMenuItem.Click +=
                (_, _) =>
                    OpenTimerCompanionSettings();

            contextMenu.Items.Add(
                timerCompanionSettingsMenuItem);
        }

        if (_stopwatchCompanionContent?.Visibility ==
            Visibility.Visible)
        {
            MenuItem stopwatchCompanionSettingsMenuItem =
                new()
                {
                    Header =
                        "Stopwatch companion settings"
                };

            stopwatchCompanionSettingsMenuItem.Click +=
                (_, _) =>
                    OpenStopwatchCompanionSettings();

            contextMenu.Items.Add(
                stopwatchCompanionSettingsMenuItem);
        }

        string? calendarEventId =
            null;

        if (_calendarCompanionContent?.Visibility ==
            Visibility.Visible)
        {
            MenuItem addEventMenuItem =
                new()
                {
                    Header =
                        "Add event"
                };

            addEventMenuItem.Click +=
                CalendarCompanionAddEventMenuItem_Click;

            contextMenu.Items.Add(
                addEventMenuItem);

            DependencyObject? current =
                e.OriginalSource as DependencyObject;

            while (current is not null &&
                   !ReferenceEquals(
                       current,
                       _companionRoot))
            {
                if (current is Button button &&
                    button.Tag is string eventId &&
                    _runtimeState.CalendarEvents.Any(
                        calendarEvent =>
                            string.Equals(
                                calendarEvent.Id,
                                eventId,
                                StringComparison.Ordinal)))
                {
                    calendarEventId =
                        eventId;

                    break;
                }

                current =
                    VisualTreeHelper.GetParent(
                        current);
            }

            if (!string.IsNullOrWhiteSpace(
                    calendarEventId))
            {
                MenuItem deleteMenuItem =
                    new()
                    {
                        Header =
                            "Delete Event",
                        Tag =
                            calendarEventId
                    };

                deleteMenuItem.Click +=
                    CalendarCompanionDeleteMenuItem_Click;

                contextMenu.Items.Add(
                    deleteMenuItem);
            }

            if (contextMenu.Items.Count > 0)
            {
                contextMenu.Items.Add(
                    new Separator());
            }

            MenuItem calendarCompanionSettingsMenuItem =
                new()
                {
                    Header =
                        "Calendar companion settings"
                };

            calendarCompanionSettingsMenuItem.Click +=
                (_, _) =>
                    OpenCalendarCompanionSettings();

            contextMenu.Items.Add(
                calendarCompanionSettingsMenuItem);
        }

        if (contextMenu.Items.Count > 0)
        {
            contextMenu.Items.Add(
                new Separator());
        }

        MenuItem closeMenuItem =
            new()
            {
                Header =
                    L(
                        "Widget.Clock.AlarmCompanion.CloseCompanion")
            };

        closeMenuItem.Click +=
            (_, _) =>
                CloseCompanion();

        contextMenu.Items.Add(
            closeMenuItem);

        contextMenu.PlacementTarget =
            _companionRoot;

        contextMenu.IsOpen =
            true;

        e.Handled =
            true;
    }

    private void CompanionRoot_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (_alarmSignalActive)
        {
            AcknowledgeAlarmSignal();
            e.Handled = true;
            return;
        }

        if (!_timerAlarmActive)
        {
            return;
        }

        AcknowledgeTimerAlarm();
        e.Handled = true;
    }

    private void CompanionAlarmInput_PreviewTextInput(
        object sender,
        TextCompositionEventArgs e)
    {
        e.Handled =
            e.Text.Any(
                character =>
                    !char.IsDigit(
                        character) &&
                    character != ':');
    }

    private void CompanionAlarmInput_KeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        CommitSelectedAlarmTime();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void CompanionAlarmInput_LostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        CommitSelectedAlarmTime();
    }

    private void CompanionAlarmPositionText_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        SelectRelativeAlarm(
            1);

        e.Handled = true;
    }

    private void CompanionAlarmPositionText_MouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        SelectRelativeAlarm(
            -1);

        e.Handled = true;
    }

    private void AlarmCompanionContent_PreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        SelectRelativeAlarm(
            e.Delta < 0
                ? 1
                : -1);

        e.Handled = true;
    }

    private void CompanionAlarmAddMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_companionAlarmAddButton is null)
        {
            return;
        }

        _companionAlarmAddButton.RaiseEvent(
            new RoutedEventArgs(
                Button.ClickEvent));
    }

    private void CompanionAlarmRemoveMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        RemoveSelectedAlarmFromCompanion();
    }

    private void CompanionAlarmAddButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (_hostAppearance is null)
        {
            return;
        }

        if (_alarmNamePromptWindow?.IsLoaded == true)
        {
            _alarmNamePromptWindow.Activate();
            return;
        }

        ClockWidgetAlarmNamePromptWindow window =
            new(
                _hostAppearance,
                L,
                AddAlarmFromCompanionWithName);

        _alarmNamePromptWindow =
            window;

        window.Closed +=
            (_, _) =>
            {
                if (ReferenceEquals(
                        _alarmNamePromptWindow,
                        window))
                {
                    _alarmNamePromptWindow =
                        null;
                }
            };

        window.Show();
    }

    private void AddAlarmFromCompanionWithName(
        string name)
    {
        DateTime triggerAt =
            DateTime.Now.AddMinutes(
                5);

        triggerAt =
            new DateTime(
                triggerAt.Year,
                triggerAt.Month,
                triggerAt.Day,
                triggerAt.Hour,
                triggerAt.Minute,
                0);

        ClockWidgetAlarmEntry alarm =
            new()
            {
                TriggerAt =
                    GetNextAlarmTriggerAt(
                        triggerAt.TimeOfDay),
                Note =
                    name,
                Enabled =
                    false
            };

        _runtimeState.Alarms.Add(
            alarm);

        _selectedAlarmIndex =
            _runtimeState.Alarms.Count - 1;

        SaveRuntimeStateAndNotifyCompanion();
        UpdateAlarmCompanionView();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Alarm added from companion; Id={alarm.Id}; TriggerAt={alarm.TriggerAt:O}; Name={alarm.Note}");
    }

    private void CompanionAlarmAddButton_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        e.Handled = true;

        RemoveSelectedAlarmFromCompanion();
    }

    private void RemoveSelectedAlarmFromCompanion()
    {
        ClockWidgetAlarmEntry? alarm =
            GetSelectedAlarm();

        if (alarm is null)
        {
            return;
        }

        string id =
            alarm.Id;

        _runtimeState.Alarms.Remove(
            alarm);

        ClampSelectedAlarmIndex();

        SaveRuntimeStateAndNotifyCompanion();
        UpdateAlarmCompanionView();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Alarm removed from companion; Id={id}");
    }

    private void CompanionAlarmToggleButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        ClockWidgetAlarmEntry? alarm =
            GetSelectedAlarm();

        if (alarm is null)
        {
            AddAlarmFromCompanionWithName(
                string.Empty);

            alarm =
                GetSelectedAlarm();

            if (alarm is null)
            {
                return;
            }
        }

        if (!alarm.Enabled)
        {
            if (_companionAlarmInput is not null &&
                TryParseAlarmTime(
                    _companionAlarmInput.Text,
                    out TimeSpan time))
            {
                alarm.TriggerAt =
                    GetNextAlarmTriggerAt(
                        time);
            }
            else
            {
                alarm.TriggerAt =
                    GetNextAlarmTriggerAt(
                        alarm.TriggerAt.TimeOfDay);
            }
        }

        alarm.Enabled =
            !alarm.Enabled;

        SaveRuntimeStateAndNotifyCompanion();
        UpdateAlarmCompanionView();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Alarm toggled from companion; Id={alarm.Id}; Enabled={alarm.Enabled}; TriggerAt={alarm.TriggerAt:O}");
    }

    private void SelectRelativeAlarm(
        int offset)
    {
        if (_runtimeState.Alarms.Count == 0)
        {
            _selectedAlarmIndex = 0;
            UpdateAlarmCompanionView();
            return;
        }

        ClampSelectedAlarmIndex();

        _selectedAlarmIndex =
            (_selectedAlarmIndex +
             offset +
             _runtimeState.Alarms.Count) %
            _runtimeState.Alarms.Count;

        UpdateAlarmCompanionView();
    }

    private void ClampSelectedAlarmIndex()
    {
        if (_runtimeState.Alarms.Count == 0)
        {
            _selectedAlarmIndex = 0;
            return;
        }

        _selectedAlarmIndex =
            Math.Clamp(
                _selectedAlarmIndex,
                0,
                _runtimeState.Alarms.Count - 1);
    }

    private ClockWidgetAlarmEntry? GetSelectedAlarm()
    {
        ClampSelectedAlarmIndex();

        if (_runtimeState.Alarms.Count == 0)
        {
            return null;
        }

        return _runtimeState.Alarms[_selectedAlarmIndex];
    }

    private void CommitSelectedAlarmTime()
    {
        ClockWidgetAlarmEntry? alarm =
            GetSelectedAlarm();

        if (alarm is null ||
            _companionAlarmInput is null ||
            !TryParseAlarmTime(
                _companionAlarmInput.Text,
                out TimeSpan time))
        {
            UpdateAlarmCompanionView();
            return;
        }

        alarm.TriggerAt =
            GetNextAlarmTriggerAt(
                time);

        SaveRuntimeStateAndNotifyCompanion();
        UpdateAlarmCompanionView();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Alarm time changed from companion; Id={alarm.Id}; TriggerAt={alarm.TriggerAt:O}; Enabled={alarm.Enabled}");
    }

    private static bool TryParseAlarmTime(
        string value,
        out TimeSpan time)
    {
        return TimeSpan.TryParseExact(
                   value,
                   "hh\\:mm",
                   CultureInfo.InvariantCulture,
                   out time) &&
               time >= TimeSpan.Zero &&
               time < TimeSpan.FromDays(
                   1);
    }

    private static DateTime GetNextAlarmTriggerAt(
        TimeSpan time)
    {
        DateTime now =
            DateTime.Now;

        DateTime triggerAt =
            now.Date +
            time;

        if (triggerAt <= now)
        {
            triggerAt =
                triggerAt.AddDays(
                    1);
        }

        return triggerAt;
    }

    private void CompanionTimerInput_GotKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        _companionTimerInput?.SelectAll();
    }

    private void CompanionTimerInput_PreviewTextInput(
        object sender,
        TextCompositionEventArgs e)
    {
        e.Handled =
            e.Text.Any(
                character =>
                    !char.IsDigit(
                        character) &&
                    character != ':');
    }

    private void CompanionTimerInput_LostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (_companionTimerInput is null ||
            _companionTimerInput.IsReadOnly)
        {
            return;
        }

        if (!TryParseTimerInput(
                _companionTimerInput.Text,
                out int totalSeconds))
        {
            _companionTimerInput.Text =
                GetTimerInputText();

            return;
        }

        _companionTimerInput.Text =
            FormatTimerInput(
                totalSeconds);
    }

    private void CompanionPlayPauseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        ClockWidgetTimerEntry? timer =
            GetPrimaryTimer();

        if (timer?.IsRunning == true)
        {
            UpdateTimerRemaining(
                timer);

            timer.IsRunning = false;
            timer.EndUtc = null;

            SaveRuntimeStateAndNotifyCompanion();

            ClockWidgetLog.Write(
                "Widget.Clock",
                $"Timer paused; Id={timer.Id}; Remaining={timer.RemainingSeconds}");

            return;
        }

        if (timer is not null &&
            !timer.HasFired &&
            timer.RemainingSeconds > 0 &&
            timer.RemainingSeconds < timer.InitialSeconds)
        {
            timer.IsRunning = true;

            timer.EndUtc =
                DateTime.UtcNow.AddSeconds(
                    timer.RemainingSeconds);

            SaveRuntimeStateAndNotifyCompanion();

            ClockWidgetLog.Write(
                "Widget.Clock",
                $"Timer resumed; Id={timer.Id}; Remaining={timer.RemainingSeconds}");

            return;
        }

        if (_companionTimerInput is null ||
            !TryParseTimerInput(
                _companionTimerInput.Text,
                out int totalSeconds) ||
            totalSeconds <= 0)
        {
            return;
        }

        ClockWidgetTimerEntry newTimer =
            new()
            {
                InitialSeconds =
                    totalSeconds,
                RemainingSeconds =
                    totalSeconds,
                IsRunning = true,
                EndUtc =
                    DateTime.UtcNow.AddSeconds(
                        totalSeconds),
                HasFired = false
            };

        _runtimeState.Timers.Clear();

        _runtimeState.Timers.Add(
            newTimer);

        SaveRuntimeStateAndNotifyCompanion();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Timer started; Id={newTimer.Id}; Duration={totalSeconds}");
    }

    private void CompanionStopButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        ClockWidgetTimerEntry? timer =
            GetPrimaryTimer();

        if (timer is null)
        {
            if (_companionTimerInput is not null)
            {
                _companionTimerInput.Text =
                    "00:00";
            }

            return;
        }

        timer.IsRunning = false;
        timer.EndUtc = null;
        timer.RemainingSeconds =
            timer.InitialSeconds;
        timer.HasFired = false;

        SaveRuntimeStateAndNotifyCompanion();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Timer stopped and reset; Id={timer.Id}; Duration={timer.InitialSeconds}");
    }

    private void CompanionStopwatchPlayPauseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        ClockWidgetStopwatchState stopwatch =
            _runtimeState.Stopwatch;

        if (stopwatch.IsRunning)
        {
            stopwatch.ElapsedMilliseconds =
                GetStopwatchElapsedMilliseconds();

            stopwatch.IsRunning = false;
            stopwatch.StartedUtc = null;
        }
        else
        {
            stopwatch.IsRunning = true;
            stopwatch.StartedUtc =
                DateTime.UtcNow;
        }

        SaveRuntimeState();
        UpdateStopwatchCompanionView();
        UpdateStopwatchRefreshTimer();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Stopwatch state changed; Running={stopwatch.IsRunning}; ElapsedMilliseconds={stopwatch.ElapsedMilliseconds}");
    }

    private void CompanionStopwatchStopButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        ClockWidgetStopwatchState stopwatch =
            _runtimeState.Stopwatch;

        stopwatch.ElapsedMilliseconds = 0;
        stopwatch.IsRunning = false;
        stopwatch.StartedUtc = null;

        SaveRuntimeState();
        UpdateStopwatchCompanionView();
        UpdateStopwatchRefreshTimer();

        ClockWidgetLog.Write(
            "Widget.Clock",
            "Stopwatch stopped and reset");
    }

    private void StopwatchRefreshTimer_Tick(
        object? sender,
        EventArgs e)
    {
        UpdateStopwatchCompanionView();
    }

    private void UpdateStopwatchRefreshTimer()
    {
        bool shouldRun =
            _companionMode == CompanionMode.Stopwatch &&
            _runtimeState.Stopwatch.IsRunning &&
            _companionStopwatchContentVisible();

        if (!shouldRun)
        {
            _stopwatchRefreshTimer?.Stop();
            return;
        }

        _stopwatchRefreshTimer ??=
            new DispatcherTimer(
                DispatcherPriority.Background)
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        100)
            };

        _stopwatchRefreshTimer.Tick -=
            StopwatchRefreshTimer_Tick;

        _stopwatchRefreshTimer.Tick +=
            StopwatchRefreshTimer_Tick;

        if (!_stopwatchRefreshTimer.IsEnabled)
        {
            _stopwatchRefreshTimer.Start();
        }
    }

    private bool _companionStopwatchContentVisible()
    {
        return _stopwatchCompanionContent?.Visibility ==
               Visibility.Visible;
    }

    private void UpdateStopwatchCompanionView()
    {
        if (_companionStopwatchText is null ||
            _companionStopwatchPlayPauseButton is null ||
            _companionStopwatchStopButton is null)
        {
            return;
        }

        Color stopwatchColor =
            GetStopwatchCompanionColor();

        _companionStopwatchText.FontSize =
            Math.Clamp(
                _settings.StopwatchCompanionFontSize,
                8,
                16);

        _companionStopwatchText.Foreground =
            new SolidColorBrush(
                stopwatchColor);

        _companionStopwatchText.Text =
            FormatStopwatchElapsed(
                GetStopwatchElapsedMilliseconds());

        _companionStopwatchPlayPauseButton.Content =
            CreateRoundCompanionButtonContent(
                _runtimeState.Stopwatch.IsRunning
                    ? "Ⅱ"
                    : "▶",
                stopwatchColor);

        _companionStopwatchStopButton.Content =
            CreateRoundCompanionButtonContent(
                "■",
                stopwatchColor);
    }

    private long GetStopwatchElapsedMilliseconds()
    {
        long elapsed =
            _runtimeState.Stopwatch.ElapsedMilliseconds;

        if (_runtimeState.Stopwatch.IsRunning &&
            _runtimeState.Stopwatch.StartedUtc is DateTime startedUtc)
        {
            elapsed +=
                Math.Max(
                    0L,
                    (long)(DateTime.UtcNow - startedUtc)
                        .TotalMilliseconds);
        }

        return elapsed;
    }

    private static string FormatStopwatchElapsed(
        long elapsedMilliseconds)
    {
        elapsedMilliseconds =
            Math.Max(
                0,
                elapsedMilliseconds);

        TimeSpan elapsed =
            TimeSpan.FromMilliseconds(
                elapsedMilliseconds);

        long totalMinutes =
            (long)elapsed.TotalMinutes;

        if (totalMinutes <= 99)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}.{2:00}",
                totalMinutes,
                elapsed.Seconds,
                elapsed.Milliseconds / 10);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}:{1:00}.{2:00}",
            (long)elapsed.TotalHours,
            elapsed.Minutes,
            elapsed.Milliseconds / 10);
    }

    private static bool TryParseTimerInput(
        string value,
        out int totalSeconds)
    {
        totalSeconds = 0;

        string[] parts =
            value.Split(
                ':');

        if (parts.Length != 2 ||
            !int.TryParse(
                parts[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int minutes) ||
            !int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int seconds) ||
            minutes < 0 ||
            minutes > 99 ||
            seconds < 0 ||
            seconds > 59)
        {
            return false;
        }

        totalSeconds =
            (minutes * 60) +
            seconds;

        return true;
    }

    private static string FormatTimerInput(
        int totalSeconds)
    {
        totalSeconds =
            Math.Clamp(
                totalSeconds,
                0,
                (99 * 60) + 59);

        int minutes =
            totalSeconds /
            60;

        int seconds =
            totalSeconds %
            60;

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:00}:{1:00}",
            minutes,
            seconds);
    }

    private string GetTimerInputText()
    {
        ClockWidgetTimerEntry? timer =
            GetPrimaryTimer();

        if (timer is null)
        {
            return "00:00";
        }

        if (timer.IsRunning ||
            (!timer.HasFired &&
             timer.RemainingSeconds > 0 &&
             timer.RemainingSeconds < timer.InitialSeconds))
        {
            return FormatTimerInput(
                GetEffectiveRemainingSeconds(
                    timer));
        }

        if (timer.HasFired)
        {
            return "00:00";
        }

        return FormatTimerInput(
            timer.InitialSeconds);
    }

    private void CommitEmbeddedSettings(
        ClockWidgetSettings settings)
    {
        _settings.ShowDate =
            settings.ShowDate;
        _settings.ClockSettingsOpacityOverrideEnabled =
            settings.ClockSettingsOpacityOverrideEnabled;
        _settings.ClockSettingsOpacity =
            settings.ClockSettingsOpacity;
        _settings.ClockSettingsBlurOverrideEnabled =
            settings.ClockSettingsBlurOverrideEnabled;
        _settings.ClockSettingsBlurRadius =
            settings.ClockSettingsBlurRadius;
        _settings.DateFormatId =
            settings.DateFormatId;
        _settings.TimeScalePercent =
            settings.TimeScalePercent;
        _settings.DateScalePercent =
            settings.DateScalePercent;
        _settings.TimeYOffset =
            settings.TimeYOffset;
        _settings.DateYOffset =
            settings.DateYOffset;
        _settings.ClockColor =
            settings.ClockColor;
        _settings.DisableDefaultHoverEffect =
            settings.DisableDefaultHoverEffect;

        SaveSettings();
        ApplyClockAppearance();
        UpdateText();
        UpdateCompanionView();
        ApplyAlarmVisual();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"General settings saved from Settings Hub; ShowDate={_settings.ShowDate}; DateFormatId={_settings.DateFormatId}; TimeScale={_settings.TimeScalePercent:0}; DateScale={_settings.DateScalePercent:0}");
    }

    public void OpenSettings(
        Window owner)
    {

        if (_context?.OpenSettingsSection is not null)
        {
            _context.OpenSettingsSection(
                "General");

            return;
        }

        ClockWidgetSettings originalSettings =
            CloneSettings(
                _settings);

        ClockWidgetSettingsWindow dialog =
            new(
                _settings,
                GetSoundsDirectory(),
                ApplySettingsPreview,
                _hostAppearance,
                L,
                _context?.SubscribeLanguageChanged,
                _context?.UnsubscribeLanguageChanged)
            {
                Owner = owner
            };

        if (dialog.ShowDialog() != true)
        {
            bool companionSlotSpanChanged =
                _settings.CalendarCompanionDoubleWidth !=
                originalSettings.CalendarCompanionDoubleWidth;

            _settings.CopyFrom(
                originalSettings);

            ApplyClockAppearance();
            UpdateText();
            UpdateCompanionView();
            ApplyAlarmVisual();

            if (companionSlotSpanChanged)
            {
                NotifyCompanionIfNeeded(
                    force: true);
            }

            return;
        }

        bool savedCompanionSlotSpanChanged =
            _settings.CalendarCompanionDoubleWidth !=
            dialog.Result.CalendarCompanionDoubleWidth;

        _settings.CopyFrom(
            dialog.Result);

        SaveSettings();
        ApplyClockAppearance();
        UpdateText();
        UpdateCompanionView();
        ApplyAlarmVisual();

        if (savedCompanionSlotSpanChanged)
        {
            NotifyCompanionIfNeeded(
                force: true);
        }

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Settings saved; ShowDate={_settings.ShowDate}; DateFormatId={_settings.DateFormatId}; TimeScale={_settings.TimeScalePercent:0}; DateScale={_settings.DateScalePercent:0}; TimeY={_settings.TimeYOffset:0}; DateY={_settings.DateYOffset:0}; ClockColor={_settings.ClockColor}; TimerAlarmColor={_settings.TimerAlarmColor}; StopwatchColor={_settings.StopwatchColor}; AlarmColor={_settings.AlarmColor}; CalendarEventColor={_settings.CalendarEventColor}; CalendarUpcomingDays={_settings.CalendarUpcomingDays}; CalendarUpcomingEventCount={_settings.CalendarUpcomingEventCount}; CalendarUpcomingFontSize={_settings.CalendarUpcomingFontSize:0}; CalendarCompanionDoubleWidth={_settings.CalendarCompanionDoubleWidth}; DisableDefaultHoverEffect={_settings.DisableDefaultHoverEffect}; AlarmSound={_settings.AlarmSoundFile}; TimerSound={_settings.TimerSoundFile}");
    }

    private void OpenAlarmCompanionSettings()
    {
        _context?.OpenSettingsSection?.Invoke(
            "AlarmCompanion");
    }

    private void OpenTimerCompanionSettings()
    {
        _context?.OpenSettingsSection?.Invoke(
            "TimerCompanion");
    }

    private void OpenStopwatchCompanionSettings()
    {
        _context?.OpenSettingsSection?.Invoke(
            "StopwatchCompanion");
    }

    private void OpenCalendarCompanionSettings()
    {
        _context?.OpenSettingsSection?.Invoke(
            "CalendarCompanion");
    }

    public void OpenAlarm(
        Window owner)
    {
        _companionClosedByUser =
            false;

        if (_companionMode == CompanionMode.Alarm &&
            !_alarmSignalActive)
        {
            _companionMode =
                CompanionMode.None;
        }
        else
        {
            _companionMode =
                CompanionMode.Alarm;

            ClampSelectedAlarmIndex();
        }

        UpdateCompanionView();
        NotifyCompanionIfNeeded(
            force: true);

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Alarm companion toggled; Visible={HasCompanionView}; Active={IsAlarmActive}; Count={_runtimeState.Alarms.Count}");
    }

    public void OpenTimer(
        Window owner)
    {
        _companionClosedByUser =
            false;

        ClockWidgetTimerEntry? timer =
            GetPrimaryTimer();

        bool timerInProgress =
            timer?.IsRunning == true ||
            (timer is not null &&
             !timer.HasFired &&
             timer.RemainingSeconds > 0 &&
             timer.RemainingSeconds < timer.InitialSeconds);

        if (_companionMode == CompanionMode.Timer &&
            !timerInProgress &&
            !_timerAlarmActive)
        {
            _companionMode =
                CompanionMode.None;
        }
        else
        {
            _companionMode =
                CompanionMode.Timer;
        }

        UpdateCompanionView();
        NotifyCompanionIfNeeded(
            force: true);

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Timer companion toggled; Visible={HasCompanionView}; Active={IsTimerActive}; Running={timer?.IsRunning == true}");
    }

    public void OpenStopwatch(
        Window owner)
    {
        _companionClosedByUser =
            false;

        if (_companionMode == CompanionMode.Stopwatch &&
            !_runtimeState.Stopwatch.IsRunning)
        {
            _companionMode =
                CompanionMode.None;
        }
        else
        {
            _companionMode =
                CompanionMode.Stopwatch;
        }

        UpdateCompanionView();
        NotifyCompanionIfNeeded(
            force: true);

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Stopwatch companion toggled; Visible={HasCompanionView}; Active={IsStopwatchActive}; Running={_runtimeState.Stopwatch.IsRunning}");
    }
    private void ToggleCalendar(
        Window owner)
    {
        ClockWidgetCalendarWindow[] openWindows =
            _openCalendarWindows
                .Where(
                    window =>
                        window.IsLoaded &&
                        window.IsVisible)
                .ToArray();

        if (openWindows.Length > 0)
        {
            foreach (ClockWidgetCalendarWindow calendarWindow in
                     openWindows)
            {
                calendarWindow.Close();
            }

            ClockWidgetLog.Write(
                "Widget.Clock.CalendarWindow",
                $"Toggled closed; ClosedWindows={openWindows.Length}");

            return;
        }

        OpenCalendar(
            owner,
            null);

        ClockWidgetLog.Write(
            "Widget.Clock.CalendarWindow",
            "Toggled open");
    }

    public void OpenCalendar(
        Window owner)
    {
        OpenCalendar(
            owner,
            null);
    }

    private void OpenCalendar(
        Window owner,
        string? selectedEventId)
    {
        Brush windowBackgroundBrush =
            owner.TryFindResource(
                    "DockBackgroundBrush") as Brush ??
            new SolidColorBrush(
                Color.FromArgb(
                    238,
                    18,
                    22,
                    28));

        Brush panelBackgroundBrush =
            owner.TryFindResource(
                    "DockItemBackgroundBrush") as Brush ??
            new SolidColorBrush(
                Color.FromArgb(
                    204,
                    32,
                    32,
                    32));

        Brush borderBrush =
            owner.TryFindResource(
                    "DockItemBorderBrush") as Brush ??
            new SolidColorBrush(
                Color.FromArgb(
                    102,
                    255,
                    255,
                    255));

        Brush textBrush =
            owner.TryFindResource(
                    "DockTextBrush") as Brush ??
            Brushes.White;

        ClockWidgetCalendarWindow dialog =
            new(
                _runtimeState.CalendarEvents,
                () =>
                {
                    SaveRuntimeState();

                    _calendarCompanionRenderKey =
                        null;

                    UpdateCompanionView();

                    NotifyCompanionIfNeeded(
                        force: true);
                },
                GetCalendarEventColor(),
                windowBackgroundBrush,
                panelBackgroundBrush,
                borderBrush,
                textBrush,
                _settings,
                SaveCalendarOverrideSettings,
                () =>
                    _context?.OpenSettingsSection?.Invoke(
                        "CalendarOverrides"),
                selectedEventId);

        if (_hostAppearance is not null)
        {
            dialog.ApplyHostAppearance(
                _hostAppearance);
        }

        _openCalendarWindows.Add(
            dialog);

        dialog.Closed +=
            (_, _) =>
                _openCalendarWindows.Remove(
                    dialog);

        dialog.Show();

        UpdateCompanionView();
        NotifyCompanionIfNeeded();
    }

    private void SaveCalendarOverrideSettings()
    {
        SaveSettings();

        if (_hostAppearance is null)
        {
            return;
        }

        foreach (ClockWidgetCalendarWindow calendarWindow in
                 _openCalendarWindows.ToArray())
        {
            if (!calendarWindow.IsLoaded)
            {
                continue;
            }

            calendarWindow.ApplyHostAppearance(
                _hostAppearance);
        }

        ClockWidgetLog.Write(
            "Widget.Clock.CalendarOverrides",
            $"Saved; OpenWindows={_openCalendarWindows.Count}");
    }

    private void Timer_Tick(
        object? sender,
        EventArgs e)
    {
        UpdateText();
        ProcessAlarmsAndTimers();
        UpdateCompanionView();
        NotifyCompanionIfNeeded();
    }

    private void ProcessAlarmsAndTimers()
    {
        DateTime now =
            DateTime.Now;

        DateTime utcNow =
            DateTime.UtcNow;

        bool changed =
            false;

        foreach (ClockWidgetAlarmEntry alarm in
                 _runtimeState.Alarms)
        {
            if (!alarm.Enabled ||
                alarm.TriggerAt > now)
            {
                continue;
            }

            alarm.Enabled =
                false;

            changed =
                true;

            if (!_alarmSignalActive)
            {
                StartAlarmSignal(
                    alarm);
            }
            else if (!string.Equals(
                         _activeAlarmId,
                         alarm.Id,
                         StringComparison.Ordinal) &&
                     !_pendingAlarmIds.Contains(
                         alarm.Id))
            {
                _pendingAlarmIds.Enqueue(
                    alarm.Id);
            }

            ClockWidgetLog.Write(
                "Widget.Clock",
                $"Alarm due; Id={alarm.Id}; TriggerAt={alarm.TriggerAt:O}; Note={alarm.Note}");
        }

        foreach (ClockWidgetTimerEntry timer in
                 _runtimeState.Timers)
        {
            if (!timer.IsRunning ||
                timer.EndUtc is not DateTime endUtc)
            {
                continue;
            }

            double seconds =
                Math.Ceiling(
                    (endUtc - utcNow).TotalSeconds);

            timer.RemainingSeconds =
                Math.Max(
                    0,
                    (int)seconds);

            if (endUtc > utcNow)
            {
                continue;
            }

            timer.IsRunning =
                false;

            timer.EndUtc =
                null;

            timer.RemainingSeconds =
                0;

            changed =
                true;

            if (timer.HasFired)
            {
                continue;
            }

            timer.HasFired =
                true;

            StartTimerAlarm(
                timer);

            ClockWidgetLog.Write(
                "Widget.Clock",
                $"Timer due; Id={timer.Id}; Note={timer.Note}");
        }

        if (changed)
        {
            SaveRuntimeState();
        }

        UpdateCompanionView();
        NotifyCompanionIfNeeded();
        ShowNextAlert();
    }

    private void ShowNextAlert()
    {
        if (_alertVisible ||
            _pendingAlerts.Count == 0 ||
            Application.Current is null)
        {
            return;
        }

        PendingAlert alert =
            _pendingAlerts.Dequeue();

        _alertVisible =
            true;

        ClockWidgetAlertWindow dialog =
            new(
                alert.Title,
                alert.Note,
                ResolveAlertSoundPath(
                    alert.Source));

        Window? owner =
            Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(
                    window =>
                        window.IsActive &&
                        window.IsVisible);

        if (owner is not null)
        {
            dialog.Owner =
                owner;
        }

        dialog.ShowDialog();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"{alert.Source} acknowledged; Id={alert.Id}");

        _alertVisible =
            false;

        if (_pendingAlerts.Count > 0)
        {
            Application.Current.Dispatcher.BeginInvoke(
                ShowNextAlert,
                DispatcherPriority.Background);
        }
    }

    private void ApplyClockAppearance()
    {
        Color clockColor =
            ParseSettingColor(
                _settings.ClockColor,
                Colors.White);

        if (_timeText is not null)
        {
            _timeText.FontSize =
                19.5 *
                (Math.Clamp(
                    _settings.TimeScalePercent,
                    50,
                    150) /
                 100.0);

            _timeText.Foreground =
                new SolidColorBrush(
                    clockColor);

            _timeText.RenderTransform =
                new TranslateTransform(
                    0,
                    _settings.TimeYOffset);
        }

        if (_dateText is not null)
        {
            _dateText.FontSize =
                10.0 *
                (Math.Clamp(
                    _settings.DateScalePercent,
                    50,
                    150) /
                 100.0);

            _dateText.Foreground =
                new SolidColorBrush(
                    clockColor);

            _dateText.RenderTransform =
                new TranslateTransform(
                    0,
                    _settings.DateYOffset);
        }
    }

    private void ApplySettingsPreview(
        ClockWidgetSettings previewSettings)
    {
        bool companionSlotSpanChanged =
            _settings.CalendarCompanionDoubleWidth !=
            previewSettings.CalendarCompanionDoubleWidth;

        _settings.CopyFrom(
            previewSettings);

        ApplyClockAppearance();
        UpdateText();
        UpdateCompanionView();
        ApplyAlarmVisual();

        if (companionSlotSpanChanged)
        {
            NotifyCompanionIfNeeded(
                force: true);
        }
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

    private static Color ParseSettingColor(
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

    private void StartAlarmSignal(
        ClockWidgetAlarmEntry alarm)
    {
        _alarmSignalActive = true;
        _alarmBlinkOn = true;
        _activeAlarmId =
            alarm.Id;
        _companionMode =
            CompanionMode.Alarm;

        int index =
            _runtimeState.Alarms.FindIndex(
                item =>
                    string.Equals(
                        item.Id,
                        alarm.Id,
                        StringComparison.Ordinal));

        if (index >= 0)
        {
            _selectedAlarmIndex =
                index;
        }

        _alarmBlinkTimer ??=
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        450)
            };

        _alarmBlinkTimer.Tick -=
            AlarmBlinkTimer_Tick;

        _alarmBlinkTimer.Tick +=
            AlarmBlinkTimer_Tick;

        _alarmBlinkTimer.Start();

        _alarmSoundTimer ??=
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromSeconds(
                        1)
            };

        _alarmSoundTimer.Tick -=
            AlarmSoundTimer_Tick;

        _alarmSoundTimer.Tick +=
            AlarmSoundTimer_Tick;

        PlayAlarmSound();
        _alarmSoundTimer.Start();

        UpdateCompanionView();
        NotifyCompanionIfNeeded();
        ApplyAlarmVisual();
    }

    private void AlarmBlinkTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _alarmBlinkOn =
            !_alarmBlinkOn;

        ApplyAlarmVisual();
    }

    private void AlarmSoundTimer_Tick(
        object? sender,
        EventArgs e)
    {
        PlayAlarmSound();
    }

    private void PlayAlarmSound()
    {
        string? soundPath =
            ResolveAlertSoundPath(
                "Alarm");

        if (!string.IsNullOrWhiteSpace(
                soundPath) &&
            File.Exists(
                soundPath))
        {
            try
            {
                _alarmMediaPlayer ??=
                    new MediaPlayer();

                _alarmMediaPlayer.Stop();
                _alarmMediaPlayer.Open(
                    new Uri(
                        soundPath,
                        UriKind.Absolute));
                _alarmMediaPlayer.Position =
                    TimeSpan.Zero;
                _alarmMediaPlayer.Play();

                return;
            }
            catch
            {
                StopAlarmSound();
            }
        }

        SystemSounds.Exclamation.Play();
    }

    private void StopAlarmSound()
    {
        if (_alarmMediaPlayer is null)
        {
            return;
        }

        _alarmMediaPlayer.Stop();
        _alarmMediaPlayer.Close();
        _alarmMediaPlayer = null;
    }

    private void AcknowledgeAlarmSignal()
    {
        if (!_alarmSignalActive)
        {
            return;
        }

        string? alarmId =
            _activeAlarmId;

        _alarmSignalActive = false;
        _alarmBlinkOn = false;
        _activeAlarmId = null;

        _alarmBlinkTimer?.Stop();
        _alarmSoundTimer?.Stop();
        StopAlarmSound();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Alarm acknowledged; Id={alarmId}");

        while (_pendingAlarmIds.Count > 0)
        {
            string nextId =
                _pendingAlarmIds.Dequeue();

            ClockWidgetAlarmEntry? nextAlarm =
                _runtimeState.Alarms.FirstOrDefault(
                    alarm =>
                        string.Equals(
                            alarm.Id,
                            nextId,
                            StringComparison.Ordinal));

            if (nextAlarm is null)
            {
                continue;
            }

            StartAlarmSignal(
                nextAlarm);

            return;
        }

        UpdateCompanionView();
        NotifyCompanionIfNeeded();
        ApplyAlarmVisual();
    }

    private Color GetTimerColor()
    {
        return ParseSettingColor(
            _settings.TimerAlarmColor,
            Color.FromRgb(
                255,
                159,
                10));
    }

    private Color GetTimerCompanionColor()
    {
        Color fallback =
            GetTimerColor();

        return string.IsNullOrWhiteSpace(
                _settings.TimerCompanionColor)
            ? fallback
            : ParseSettingColor(
                _settings.TimerCompanionColor,
                fallback);
    }

    private Color GetStopwatchColor()
    {
        return ParseSettingColor(
            _settings.StopwatchColor,
            Color.FromRgb(
                255,
                159,
                10));
    }

    private Color GetStopwatchCompanionColor()
    {
        Color fallback =
            GetStopwatchColor();

        return string.IsNullOrWhiteSpace(
                _settings.StopwatchCompanionColor)
            ? fallback
            : ParseSettingColor(
                _settings.StopwatchCompanionColor,
                fallback);
    }

    private Color GetAlarmColor()
    {
        return ParseSettingColor(
            _settings.AlarmColor,
            Color.FromRgb(
                255,
                159,
                10));
    }

    private Color GetAlarmCompanionTimeColor()
    {
        return string.IsNullOrWhiteSpace(
                _settings.AlarmCompanionTimeColor)
            ? GetAlarmColor()
            : ParseSettingColor(
                _settings.AlarmCompanionTimeColor,
                GetAlarmColor());
    }

    private Color GetAlarmCompanionIndexColor()
    {
        return string.IsNullOrWhiteSpace(
                _settings.AlarmCompanionIndexColor)
            ? GetAlarmColor()
            : ParseSettingColor(
                _settings.AlarmCompanionIndexColor,
                GetAlarmColor());
    }

    private static string ToRomanNumeral(
        int value)
    {
        if (value <= 0)
        {
            return string.Empty;
        }

        if (value > 3999)
        {
            return value.ToString(
                CultureInfo.InvariantCulture);
        }

        (int Value, string Symbol)[] symbols =
        [
            (1000, "M"),
            (900, "CM"),
            (500, "D"),
            (400, "CD"),
            (100, "C"),
            (90, "XC"),
            (50, "L"),
            (40, "XL"),
            (10, "X"),
            (9, "IX"),
            (5, "V"),
            (4, "IV"),
            (1, "I")
        ];

        System.Text.StringBuilder result =
            new();

        int remaining =
            value;

        foreach ((int number, string symbol) in symbols)
        {
            while (remaining >= number)
            {
                result.Append(
                    symbol);

                remaining -=
                    number;
            }
        }

        return result.ToString();
    }

    private Color GetCalendarEventColor()
    {
        return ParseSettingColor(
            _settings.CalendarEventColor,
            Color.FromRgb(
                255,
                159,
                10));
    }


    private void StartTimerAlarm(
        ClockWidgetTimerEntry timer)
    {
        _timerAlarmActive = true;
        _timerAlarmBlinkOn = true;
        _timerAlarmId =
            timer.Id;
        _companionMode =
            CompanionMode.Timer;

        _timerAlarmBlinkTimer ??=
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        450)
            };

        _timerAlarmBlinkTimer.Tick -=
            TimerAlarmBlinkTimer_Tick;

        _timerAlarmBlinkTimer.Tick +=
            TimerAlarmBlinkTimer_Tick;

        _timerAlarmBlinkTimer.Start();

        _timerAlarmSoundTimer ??=
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromSeconds(
                        1)
            };

        _timerAlarmSoundTimer.Tick -=
            TimerAlarmSoundTimer_Tick;

        _timerAlarmSoundTimer.Tick +=
            TimerAlarmSoundTimer_Tick;

        PlayTimerAlarmSound();
        _timerAlarmSoundTimer.Start();

        ApplyTimerAlarmVisual();
    }

    private void TimerAlarmBlinkTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _timerAlarmBlinkOn =
            !_timerAlarmBlinkOn;

        ApplyTimerAlarmVisual();
    }

    private void TimerAlarmSoundTimer_Tick(
        object? sender,
        EventArgs e)
    {
        PlayTimerAlarmSound();
    }
    private void ApplyTimerAlarmVisual()
    {
        ApplyAlarmVisual();
    }

    private void ApplyAlarmVisual()
    {
        if (_companionRoot is null)
        {
            return;
        }

        if (_alarmSignalActive)
        {
            Color alarmColor =
                GetAlarmColor();

            _companionRoot.Background =
                _alarmBlinkOn
                    ? new SolidColorBrush(
                        Color.FromArgb(
                            145,
                            alarmColor.R,
                            alarmColor.G,
                            alarmColor.B))
                    : Brushes.Transparent;

            return;
        }

        if (_timerAlarmActive)
        {
            Color alarmColor =
                GetTimerColor();

            _companionRoot.Background =
                _timerAlarmBlinkOn
                    ? new SolidColorBrush(
                        Color.FromArgb(
                            145,
                            alarmColor.R,
                            alarmColor.G,
                            alarmColor.B))
                    : Brushes.Transparent;

            return;
        }

        _companionRoot.Background =
            Brushes.Transparent;
    }

    private void PlayTimerAlarmSound()
    {
        string? soundPath =
            ResolveAlertSoundPath(
                "Timer");

        if (!string.IsNullOrWhiteSpace(
                soundPath) &&
            File.Exists(
                soundPath))
        {
            try
            {
                _timerAlarmMediaPlayer ??=
                    new MediaPlayer();

                _timerAlarmMediaPlayer.Stop();
                _timerAlarmMediaPlayer.Open(
                    new Uri(
                        soundPath,
                        UriKind.Absolute));
                _timerAlarmMediaPlayer.Position =
                    TimeSpan.Zero;
                _timerAlarmMediaPlayer.Play();

                return;
            }
            catch
            {
                StopTimerAlarmSound();
            }
        }

        SystemSounds.Exclamation.Play();
    }

    private void StopTimerAlarmSound()
    {
        if (_timerAlarmMediaPlayer is null)
        {
            return;
        }

        _timerAlarmMediaPlayer.Stop();
        _timerAlarmMediaPlayer.Close();
        _timerAlarmMediaPlayer = null;
    }

    private void AcknowledgeTimerAlarm()
    {
        if (!_timerAlarmActive)
        {
            return;
        }

        string? alarmId =
            _timerAlarmId;

        _timerAlarmActive = false;
        _timerAlarmBlinkOn = false;
        _timerAlarmId = null;

        _timerAlarmBlinkTimer?.Stop();
        _timerAlarmSoundTimer?.Stop();
        StopTimerAlarmSound();

        ClockWidgetTimerEntry? timer =
            GetPrimaryTimer();

        if (timer is not null &&
            (string.IsNullOrWhiteSpace(
                 alarmId) ||
             string.Equals(
                 timer.Id,
                 alarmId,
                 StringComparison.Ordinal)))
        {
            timer.IsRunning = false;
            timer.EndUtc = null;
            timer.RemainingSeconds =
                timer.InitialSeconds;
            timer.HasFired = false;
        }

        SaveRuntimeState();
        UpdateCompanionView();
        ApplyTimerAlarmVisual();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Timer alarm acknowledged; Id={alarmId}");
    }

    private void UpdateText()
    {
        DateTime now =
            DateTime.Now;

        if (_timeText is not null)
        {
            _timeText.Text =
                now.ToString(
                    "HH:mm",
                    CultureInfo.InvariantCulture);
        }

        if (_dateText is not null)
        {
            _dateText.Visibility =
                _settings.ShowDate
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            _dateText.Text =
                _settings.ShowDate
                    ? FormatDate(
                        now)
                    : string.Empty;
        }
    }

    private string FormatDate(
        DateTime value)
    {
        return _settings.DateFormatId switch
        {
            "GermanLong" =>
                value.ToString(
                    "dd.MM.yyyy",
                    GermanCulture),

            "UsLong" =>
                value.ToString(
                    "MM/dd/yyyy",
                    CultureInfo.InvariantCulture),

            "IsoLong" =>
                value.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture),

            "GermanWeekdayShort" =>
                value.ToString(
                    "ddd dd.MM.",
                    GermanCulture),

            "GermanWeekdayLong" =>
                value.ToString(
                    "ddd dd.MM.yyyy",
                    GermanCulture),

            _ =>
                value.ToString(
                    "dd.MM.",
                    GermanCulture)
        };
    }

    private ClockWidgetSettings LoadSettings()
    {
        string? settingsPath =
            GetSettingsPath();

        if (string.IsNullOrWhiteSpace(
                settingsPath) ||
            !File.Exists(
                settingsPath))
        {
            return new ClockWidgetSettings();
        }

        try
        {
            string json =
                File.ReadAllText(
                    settingsPath);

            return JsonSerializer.Deserialize<ClockWidgetSettings>(
                       json) ??
                   new ClockWidgetSettings();
        }
        catch (Exception ex)
        {
            ClockWidgetLog.Write(
                "Widget.Clock",
                $"Settings load failed; Type={ex.GetType().Name}; Message={ex.Message}");

            return new ClockWidgetSettings();
        }
    }

    private ClockWidgetRuntimeState LoadRuntimeState()
    {
        string? statePath =
            GetRuntimeStatePath();

        if (string.IsNullOrWhiteSpace(
                statePath) ||
            !File.Exists(
                statePath))
        {
            return new ClockWidgetRuntimeState();
        }

        try
        {
            string json =
                File.ReadAllText(
                    statePath);

            ClockWidgetRuntimeState state =
                JsonSerializer.Deserialize<ClockWidgetRuntimeState>(
                    json) ??
                new ClockWidgetRuntimeState();

            state.Alarms ??=
                new List<ClockWidgetAlarmEntry>();

            state.Timers ??=
                new List<ClockWidgetTimerEntry>();

            state.Stopwatch ??=
                new ClockWidgetStopwatchState();

            state.CalendarEvents ??=
                new List<ClockWidgetCalendarEventEntry>();

            return state;
        }
        catch (Exception ex)
        {
            ClockWidgetLog.Write(
                "Widget.Clock",
                $"State load failed; Type={ex.GetType().Name}; Message={ex.Message}");

            return new ClockWidgetRuntimeState();
        }
    }

    private void SaveSettings()
    {
        string? settingsPath =
            GetSettingsPath();

        if (string.IsNullOrWhiteSpace(
                settingsPath))
        {
            return;
        }

        try
        {
            string? directory =
                Path.GetDirectoryName(
                    settingsPath);

            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }

            string json =
                JsonSerializer.Serialize(
                    _settings,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

            File.WriteAllText(
                settingsPath,
                json);
        }
        catch (Exception ex)
        {
            ClockWidgetLog.Write(
                "Widget.Clock",
                $"Settings save failed; Type={ex.GetType().Name}; Message={ex.Message}");
        }
    }

    private void SaveRuntimeStateAndNotifyCompanion()
    {
        SaveRuntimeState();
        UpdateCompanionView();
        NotifyCompanionIfNeeded();
        _alarmCompanionSettingsWindow?.RefreshAlarms();
    }

    private void SaveRuntimeState()
    {
        string? statePath =
            GetRuntimeStatePath();

        if (string.IsNullOrWhiteSpace(
                statePath))
        {
            return;
        }

        try
        {
            string? directory =
                Path.GetDirectoryName(
                    statePath);

            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }

            string json =
                JsonSerializer.Serialize(
                    _runtimeState,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

            File.WriteAllText(
                statePath,
                json);
        }
        catch (Exception ex)
        {
            ClockWidgetLog.Write(
                "Widget.Clock",
                $"State save failed; Type={ex.GetType().Name}; Message={ex.Message}");
        }
    }

    private void NormalizeRunningTimers()
    {
        if (_runtimeState.Timers.Count > 1)
        {
            _runtimeState.Timers =
                _runtimeState.Timers
                    .Take(
                        1)
                    .ToList();
        }

        DateTime utcNow =
            DateTime.UtcNow;

        foreach (ClockWidgetTimerEntry timer in
                 _runtimeState.Timers)
        {
            if (!timer.IsRunning ||
                timer.EndUtc is not DateTime endUtc)
            {
                continue;
            }

            timer.RemainingSeconds =
                Math.Max(
                    0,
                    (int)Math.Ceiling(
                        (endUtc - utcNow).TotalSeconds));

            if (timer.RemainingSeconds > 0)
            {
                continue;
            }

            timer.IsRunning = false;
            timer.EndUtc = null;
        }
    }

    private List<ClockWidgetTimerEntry> GetActiveTimers()
    {
        ClockWidgetTimerEntry? timer =
            GetPrimaryTimer();

        if (timer is null ||
            timer.HasFired ||
            timer.RemainingSeconds <= 0 ||
            (!timer.IsRunning &&
             timer.RemainingSeconds >= timer.InitialSeconds))
        {
            return new List<ClockWidgetTimerEntry>();
        }

        return new List<ClockWidgetTimerEntry>
        {
            timer
        };
    }

    private ClockWidgetTimerEntry? GetPrimaryTimer()
    {
        return _runtimeState.Timers
            .FirstOrDefault();
    }

    private static int GetEffectiveRemainingSeconds(
        ClockWidgetTimerEntry timer)
    {
        if (timer.IsRunning &&
            timer.EndUtc is DateTime endUtc)
        {
            return Math.Max(
                0,
                (int)Math.Ceiling(
                    (endUtc -
                     DateTime.UtcNow)
                    .TotalSeconds));
        }

        return Math.Max(
            0,
            timer.RemainingSeconds);
    }

    private static void UpdateTimerRemaining(
        ClockWidgetTimerEntry timer)
    {
        if (!timer.IsRunning ||
            timer.EndUtc is not DateTime endUtc)
        {
            return;
        }

        timer.RemainingSeconds =
            Math.Max(
                0,
                (int)Math.Ceiling(
                    (endUtc -
                     DateTime.UtcNow)
                    .TotalSeconds));
    }

    private void UpdateAlarmCompanionView()
    {
        if (_companionAlarmInput is null ||
            _companionAlarmPositionText is null ||
            _companionAlarmAddButton is null ||
            _companionAlarmToggleButton is null)
        {
            return;
        }

        Color alarmColor =
            GetAlarmColor();

        Color alarmTimeColor =
            GetAlarmCompanionTimeColor();

        Color alarmIndexColor =
            GetAlarmCompanionIndexColor();

        _companionAlarmInput.Foreground =
            new SolidColorBrush(
                alarmTimeColor);

        _companionAlarmInput.CaretBrush =
            new SolidColorBrush(
                alarmTimeColor);

        _companionAlarmPositionText.Foreground =
            new SolidColorBrush(
                alarmIndexColor);

        _companionAlarmPositionText.RenderTransform =
            new TranslateTransform(
                Math.Clamp(
                    _settings.AlarmCompanionIndexOffsetX,
                    -12,
                    12),
                Math.Clamp(
                    _settings.AlarmCompanionIndexOffsetY,
                    -12,
                    12));

        _companionAlarmInput.FontSize =
            Math.Clamp(
                _settings.AlarmCompanionTimeFontSize,
                10,
                18);

        _companionAlarmPositionText.FontSize =
            Math.Clamp(
                _settings.AlarmCompanionPositionFontSize,
                7,
                12);

        ClockWidgetAlarmEntry? alarm =
            GetSelectedAlarm();

        if (alarm is null)
        {
            _companionAlarmInput.Text =
                DateTime.Now.AddMinutes(
                    5).ToString(
                    "HH:mm",
                    CultureInfo.InvariantCulture);

            _companionAlarmPositionText.Text =
                string.Empty;

            _companionAlarmToggleButton.Content =
                CreateRoundCompanionButtonContent(
                    "○",
                    alarmColor);

            _companionAlarmAddButton.Content =
                CreateRoundCompanionButtonContent(
                    "+",
                    alarmColor);

            return;
        }

        if (!_companionAlarmInput.IsKeyboardFocusWithin)
        {
            _companionAlarmInput.Text =
                alarm.TriggerAt.ToString(
                    "HH:mm",
                    CultureInfo.InvariantCulture);
        }

        _companionAlarmPositionText.Text =
            ToRomanNumeral(
                _selectedAlarmIndex + 1);

        string alarmHint =
            string.IsNullOrWhiteSpace(
                alarm.Note)
                ? $"{L("Widget.Clock.AlarmCompanion.AlarmTime")}: {alarm.TriggerAt:HH:mm}"
                : $"{alarm.Note}\n{L("Widget.Clock.AlarmCompanion.AlarmTime")}: {alarm.TriggerAt:HH:mm}";

        _alarmCompanionContent!.ToolTip =
            alarmHint;

        _companionAlarmInput.ToolTip =
            alarmHint;

        _companionAlarmPositionText.ToolTip =
            alarmHint;

        _companionAlarmToggleButton.Content =
            CreateRoundCompanionButtonContent(
                alarm.Enabled
                    ? "✓"
                    : "○",
                alarmColor);

        _companionAlarmAddButton.Content =
            CreateRoundCompanionButtonContent(
                "+",
                alarmColor);
    }

    private void UpdateCompanionView()
    {
        if (_companionRoot is null ||
            _alarmCompanionContent is null ||
            _timerCompanionContent is null ||
            _stopwatchCompanionContent is null ||
            _calendarCompanionContent is null)
        {
            return;
        }

        CompanionMode visibleMode =
            _alarmSignalActive
                ? CompanionMode.Alarm
                : _timerAlarmActive
                    ? CompanionMode.Timer
                    : _companionMode;

        if (visibleMode == CompanionMode.None)
        {
            if (_runtimeState.Stopwatch.IsRunning)
            {
                visibleMode =
                    CompanionMode.Stopwatch;
            }
            else if (GetActiveTimers().Count > 0)
            {
                visibleMode =
                    CompanionMode.Timer;
            }
            else if (GetUpcomingCalendarEvents().Count > 0)
            {
                visibleMode =
                    CompanionMode.Calendar;
            }
        }

        double companionWidth =
            visibleMode == CompanionMode.Calendar &&
            _settings.CalendarCompanionDoubleWidth
                ? 116
                : 52;

        _companionRoot.Width =
            companionWidth;

        _calendarCompanionContent.Width =
            companionWidth - 2;

        _calendarCompanionContent.Height =
            48;

        _alarmCompanionContent.Visibility =
            visibleMode == CompanionMode.Alarm
                ? Visibility.Visible
                : Visibility.Collapsed;

        _timerCompanionContent.Visibility =
            visibleMode == CompanionMode.Timer
                ? Visibility.Visible
                : Visibility.Collapsed;

        _stopwatchCompanionContent.Visibility =
            visibleMode == CompanionMode.Stopwatch
                ? Visibility.Visible
                : Visibility.Collapsed;

        _calendarCompanionContent.Visibility =
            visibleMode == CompanionMode.Calendar
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (visibleMode == CompanionMode.Alarm)
        {
            UpdateAlarmCompanionView();
        }

        if (visibleMode == CompanionMode.Timer)
        {
            UpdateTimerCompanionView();
        }

        if (visibleMode == CompanionMode.Stopwatch)
        {
            UpdateStopwatchCompanionView();
        }

        if (visibleMode == CompanionMode.Calendar)
        {
            UpdateCalendarCompanionView();
        }

        UpdateStopwatchRefreshTimer();
        ApplyAlarmVisual();
    }

    private void UpdateTimerCompanionView()
    {
        if (_companionTimerInput is null ||
            _companionPlayPauseButton is null ||
            _companionStopButton is null)
        {
            return;
        }

        ClockWidgetTimerEntry? timer =
            GetPrimaryTimer();

        bool isRunning =
            timer?.IsRunning == true;

        bool isPaused =
            timer is not null &&
            !timer.IsRunning &&
            !timer.HasFired &&
            timer.RemainingSeconds > 0 &&
            timer.RemainingSeconds < timer.InitialSeconds;

        Color timerColor =
            GetTimerCompanionColor();

        _companionTimerInput.FontSize =
            Math.Clamp(
                _settings.TimerCompanionFontSize,
                8,
                16);

        _companionTimerInput.Foreground =
            new SolidColorBrush(
                timerColor);

        _companionTimerInput.CaretBrush =
            new SolidColorBrush(
                timerColor);

        _companionTimerInput.BorderBrush =
            new SolidColorBrush(
                Color.FromArgb(
                    130,
                    timerColor.R,
                    timerColor.G,
                    timerColor.B));

        if (!_companionTimerInput.IsKeyboardFocusWithin ||
            isRunning ||
            isPaused)
        {
            _companionTimerInput.Text =
                GetTimerInputText();
        }

        _companionTimerInput.IsReadOnly =
            isRunning ||
            isPaused;

        _companionPlayPauseButton.Content =
            CreateRoundCompanionButtonContent(
                isRunning
                    ? "Ⅱ"
                    : "▶",
                timerColor);

        _companionStopButton.Content =
            CreateRoundCompanionButtonContent(
                "■",
                timerColor);
    }

    private static string FormatTimerRemaining(
        int totalSeconds)
    {
        totalSeconds =
            Math.Max(
                0,
                totalSeconds);

        TimeSpan remaining =
            TimeSpan.FromSeconds(
                totalSeconds);

        if (remaining.TotalHours >= 1)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1:00}",
                (int)remaining.TotalHours,
                remaining.Minutes);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}:{1:00}",
            remaining.Minutes,
            remaining.Seconds);
    }

    private static Geometry CreateProgressArc(
        double progress)
    {
        const double radius = 20;
        const double center = 26;

        progress =
            Math.Clamp(
                progress,
                0.001,
                0.9999);

        double angle =
            progress *
            360.0;

        double startAngle =
            -90.0;

        double endAngle =
            startAngle +
            angle;

        Point start =
            PointOnCircle(
                center,
                center,
                radius,
                startAngle);

        Point end =
            PointOnCircle(
                center,
                center,
                radius,
                endAngle);

        PathFigure figure =
            new()
            {
                StartPoint = start,
                IsClosed = false
            };

        figure.Segments.Add(
            new ArcSegment
            {
                Point = end,
                Size =
                    new Size(
                        radius,
                        radius),
                SweepDirection =
                    SweepDirection.Clockwise,
                IsLargeArc =
                    angle > 180
            });

        PathGeometry geometry =
            new();

        geometry.Figures.Add(
            figure);

        return geometry;
    }

    private static Point PointOnCircle(
        double centerX,
        double centerY,
        double radius,
        double angleDegrees)
    {
        double angleRadians =
            angleDegrees *
            Math.PI /
            180.0;

        return new Point(
            centerX +
            (radius *
             Math.Cos(
                 angleRadians)),
            centerY +
            (radius *
             Math.Sin(
                 angleRadians)));
    }

    private void NotifyCompanionIfNeeded(
        bool force = false)
    {
        bool hasCompanion =
            HasCompanionView;

        bool isAlarmActive =
            IsAlarmActive;

        if (!force &&
            hasCompanion ==
                _lastHasCompanionView &&
            isAlarmActive ==
                _lastIsAlarmActive)
        {
            return;
        }

        bool companionVisibilityChanged =
            hasCompanion !=
            _lastHasCompanionView;

        bool alarmActiveChanged =
            isAlarmActive !=
            _lastIsAlarmActive;

        _lastHasCompanionView =
            hasCompanion;

        _lastIsAlarmActive =
            isAlarmActive;

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Widget state changed; CompanionVisible={hasCompanion}; CompanionVisibilityChanged={companionVisibilityChanged}; AlarmActive={isAlarmActive}; AlarmActiveChanged={alarmActiveChanged}; Mode={_companionMode}; ActiveTimers={GetActiveTimers().Count}; StopwatchRunning={_runtimeState.Stopwatch.IsRunning}");

        CompanionViewChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private Window? GetActiveOwnerWindow()
    {
        if (Application.Current is null)
        {
            return null;
        }

        return Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(
                window =>
                    window.IsActive &&
                    window.IsVisible) ??
               Application.Current.Windows
                   .OfType<Window>()
                   .FirstOrDefault(
                       window =>
                           window.IsVisible);
    }

    private string GetSoundsDirectory()
    {
        if (_context is not null)
        {
            return Path.Combine(
                _context.WidgetDirectory,
                "Resources",
                "GlueDock_Sounds");
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            "GlueDock_Widgets",
            "GlueDock.ClockWidget",
            "Resources",
            "GlueDock_Sounds");
    }

    private string? ResolveAlertSoundPath(
        string source)
    {
        string configuredFile =
            string.Equals(
                source,
                "Alarm",
                StringComparison.Ordinal)
                ? _settings.AlarmSoundFile
                : _settings.TimerSoundFile;

        if (string.IsNullOrWhiteSpace(
                configuredFile))
        {
            return null;
        }

        string fullPath =
            Path.Combine(
                GetSoundsDirectory(),
                Path.GetFileName(
                    configuredFile));

        return File.Exists(
            fullPath)
            ? fullPath
            : null;
    }

    private string? GetSettingsPath()
    {
        if (_context is null)
        {
            return null;
        }

        return Path.Combine(
            _context.SettingsDirectory,
            "settings.json");
    }

    private string? GetRuntimeStatePath()
    {
        if (_context is null)
        {
            return null;
        }

        return Path.Combine(
            _context.SettingsDirectory,
            "state.json");
    }

    private string L(
        string key)
    {
        return _context?.Localize?.Invoke(
                   key) ??
               key;
    }

    private void Context_LanguageChanged(
        object? sender,
        EventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(
            () =>
            {
                UpdateAlarmCompanionView();
                _alarmCompanionSettingsWindow?.RefreshLanguage();
                _alarmNamePromptWindow?.RefreshLanguage();
            });
    }

    public void Dispose()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -=
                Timer_Tick;

            _timer = null;
        }

        if (_stopwatchRefreshTimer is not null)
        {
            _stopwatchRefreshTimer.Stop();
            _stopwatchRefreshTimer.Tick -=
                StopwatchRefreshTimer_Tick;

            _stopwatchRefreshTimer = null;
        }

        if (_alarmBlinkTimer is not null)
        {
            _alarmBlinkTimer.Stop();
            _alarmBlinkTimer.Tick -=
                AlarmBlinkTimer_Tick;

            _alarmBlinkTimer = null;
        }

        if (_alarmSoundTimer is not null)
        {
            _alarmSoundTimer.Stop();
            _alarmSoundTimer.Tick -=
                AlarmSoundTimer_Tick;

            _alarmSoundTimer = null;
        }

        StopAlarmSound();

        if (_timerAlarmBlinkTimer is not null)
        {
            _timerAlarmBlinkTimer.Stop();
            _timerAlarmBlinkTimer.Tick -=
                TimerAlarmBlinkTimer_Tick;

            _timerAlarmBlinkTimer = null;
        }

        if (_timerAlarmSoundTimer is not null)
        {
            _timerAlarmSoundTimer.Stop();
            _timerAlarmSoundTimer.Tick -=
                TimerAlarmSoundTimer_Tick;

            _timerAlarmSoundTimer = null;
        }

        StopTimerAlarmSound();

        _context?.UnsubscribeLanguageChanged?.Invoke(
            Context_LanguageChanged);

        if (_alarmNamePromptWindow is not null)
        {
            _alarmNamePromptWindow.Close();
            _alarmNamePromptWindow =
                null;
        }

        if (_alarmCompanionSettingsWindow is not null)
        {
            _alarmCompanionSettingsWindow.Close();
            _alarmCompanionSettingsWindow =
                null;
        }

        if (_timerCompanionSettingsWindow is not null)
        {
            _timerCompanionSettingsWindow.Close();
            _timerCompanionSettingsWindow =
                null;
        }

        if (_stopwatchCompanionSettingsWindow is not null)
        {
            _stopwatchCompanionSettingsWindow.Close();
            _stopwatchCompanionSettingsWindow =
                null;
        }

        if (_calendarCompanionSettingsWindow is not null)
        {
            _calendarCompanionSettingsWindow.Close();
            _calendarCompanionSettingsWindow =
                null;
        }

        _pendingAlerts.Clear();
        _pendingAlarmIds.Clear();
        _timeText = null;
        _dateText = null;
        _companionRoot = null;
        _alarmCompanionContent = null;
        _timerCompanionContent = null;
        _stopwatchCompanionContent = null;
        _calendarCompanionContent = null;
        _companionAlarmInput = null;
        _companionAlarmPositionText = null;
        _companionAlarmAddButton = null;
        _companionAlarmToggleButton = null;
        _companionTimerInput = null;
        _companionPlayPauseButton = null;
        _companionStopButton = null;
        _companionStopwatchText = null;
        _companionStopwatchPlayPauseButton = null;
        _companionStopwatchStopButton = null;
    }
}
