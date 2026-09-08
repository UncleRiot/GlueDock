using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfPath = System.Windows.Shapes.Path;
using System.Windows.Threading;
using GlueDock;

namespace GlueDock.ClockWidget;

public sealed class ClockWidget : IGlueDockWidget
{
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
    private TextBlock? _timeText;
    private TextBlock? _dateText;
    private Grid? _companionRoot;
    private WpfPath? _companionProgressPath;
    private TextBlock? _companionRemainingText;
    private TextBlock? _companionMoreText;
    private Button? _companionPauseButton;
    private bool _lastHasCompanionView;
    private GlueDockWidgetContext? _context;
    private ClockWidgetSettings _settings =
        new();
    private ClockWidgetRuntimeState _runtimeState =
        new();
    private bool _alertVisible;

    public string DisplayName =>
        "Clock";

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

    public bool DisableDefaultHoverEffect =>
        _settings.DisableDefaultHoverEffect;

    public bool HasCompanionView =>
        GetActiveTimers().Count > 0;

    public event EventHandler? CompanionViewChanged;

    public void Initialize(
        GlueDockWidgetContext context)
    {
        _context =
            context;

        _settings =
            LoadSettings();

        _runtimeState =
            LoadRuntimeState();

        Directory.CreateDirectory(
            GetSoundsDirectory());

        NormalizeRunningTimers();

        _lastHasCompanionView =
            HasCompanionView;

        DebugLog.Write(
            "Widget.Clock",
            $"Initialized; InstanceId={context.InstanceId}; Alarms={_runtimeState.Alarms.Count}; Timers={_runtimeState.Timers.Count}; StopwatchRunning={_runtimeState.Stopwatch.IsRunning}");
    }

    public FrameworkElement CreateView()
    {
        Grid root =
            new()
            {
                Width = 52,
                Height = 52
            };

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        _timeText =
            new TextBlock
            {
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

        _dateText =
            new TextBlock
            {
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        2)
            };

        Grid.SetRow(
            _timeText,
            0);

        Grid.SetRow(
            _dateText,
            1);

        root.Children.Add(
            _timeText);

        root.Children.Add(
            _dateText);

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
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand
            };

        WpfEllipse backgroundRing =
            new()
            {
                Width = 44,
                Height = 44,
                Stroke =
                    new SolidColorBrush(
                        Color.FromArgb(
                            90,
                            255,
                            159,
                            10)),
                StrokeThickness = 2.4,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

        _companionProgressPath =
            new WpfPath
            {
                Stroke =
                    new SolidColorBrush(
                        Color.FromRgb(
                            255,
                            159,
                            10)),
                StrokeThickness = 3.0,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

        _companionRemainingText =
            new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin =
                    new Thickness(
                        0,
                        -2,
                        0,
                        0)
            };

        _companionMoreText =
            new TextBlock
            {
                FontSize = 7,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        7),
                Opacity = 0.8
            };

        Button stopButton =
            CreateCompanionButton(
                "×",
                HorizontalAlignment.Left,
                VerticalAlignment.Top);

        Button addButton =
            CreateCompanionButton(
                "+",
                HorizontalAlignment.Right,
                VerticalAlignment.Top);

        _companionPauseButton =
            CreateCompanionButton(
                "Ⅱ",
                HorizontalAlignment.Right,
                VerticalAlignment.Bottom);

        stopButton.Click +=
            CompanionStopButton_Click;

        addButton.Click +=
            CompanionAddButton_Click;

        _companionPauseButton.Click +=
            CompanionPauseButton_Click;

        root.MouseLeftButtonUp +=
            CompanionRoot_MouseLeftButtonUp;

        root.Children.Add(
            backgroundRing);

        root.Children.Add(
            _companionProgressPath);

        root.Children.Add(
            _companionRemainingText);

        root.Children.Add(
            _companionMoreText);

        root.Children.Add(
            stopButton);

        root.Children.Add(
            addButton);

        root.Children.Add(
            _companionPauseButton);

        _companionRoot =
            root;

        UpdateCompanionView();

        return root;
    }

    private static Button CreateCompanionButton(
        string content,
        HorizontalAlignment horizontalAlignment,
        VerticalAlignment verticalAlignment)
    {
        Button button =
            new()
            {
                Content = content,
                Width = 16,
                Height = 16,
                Padding =
                    new Thickness(
                        0),
                Margin =
                    new Thickness(
                        1),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = horizontalAlignment,
                VerticalAlignment = verticalAlignment,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip =
                    content switch
                    {
                        "+" => "New timer",
                        "×" => "End timer",
                        _ => "Pause / resume"
                    }
            };

        return button;
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
            return;
        }

        _runtimeState.Timers.Remove(
            timer);

        SaveRuntimeStateAndNotifyCompanion();

        DebugLog.Write(
            "Widget.Clock",
            $"Companion timer ended; Id={timer.Id}");
    }

    private void CompanionAddButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        Window? owner =
            GetActiveOwnerWindow();

        ClockWidgetQuickTimerWindow dialog =
            new(
                _runtimeState,
                SaveRuntimeStateAndNotifyCompanion);

        if (owner is not null)
        {
            dialog.Owner =
                owner;
        }

        dialog.ShowDialog();

        ProcessAlarmsAndTimers();
    }

    private void CompanionPauseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        ClockWidgetTimerEntry? timer =
            GetPrimaryTimer();

        if (timer is null)
        {
            return;
        }

        UpdateTimerRemaining(
            timer);

        if (timer.IsRunning)
        {
            timer.IsRunning = false;
            timer.EndUtc = null;
        }
        else if (timer.RemainingSeconds > 0)
        {
            timer.IsRunning = true;
            timer.EndUtc =
                DateTime.UtcNow.AddSeconds(
                    timer.RemainingSeconds);
            timer.HasFired = false;
        }

        SaveRuntimeStateAndNotifyCompanion();

        DebugLog.Write(
            "Widget.Clock",
            $"Companion timer toggled; Id={timer.Id}; Running={timer.IsRunning}; Remaining={timer.RemainingSeconds}");
    }

    private void CompanionRoot_MouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Button ||
            FindParentButton(
                e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        Window? owner =
            GetActiveOwnerWindow();

        if (owner is not null)
        {
            OpenTimer(
                owner);
        }

        e.Handled = true;
    }

    private static Button? FindParentButton(
        DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is Button button)
            {
                return button;
            }

            current =
                VisualTreeHelper.GetParent(
                    current);
        }

        return null;
    }

    public void OpenSettings(
        Window owner)
    {
        ClockWidgetSettingsWindow dialog =
            new(
                _settings,
                GetSoundsDirectory())
            {
                Owner = owner
            };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _settings =
            dialog.Result;

        SaveSettings();
        UpdateText();

        DebugLog.Write(
            "Widget.Clock",
            $"Settings saved; ShowDate={_settings.ShowDate}; DateFormatId={_settings.DateFormatId}; DisableDefaultHoverEffect={_settings.DisableDefaultHoverEffect}; AlarmSound={_settings.AlarmSoundFile}; TimerSound={_settings.TimerSoundFile}");
    }

    public void OpenAlarm(
        Window owner)
    {
        ClockWidgetAlarmWindow dialog =
            new(
                _runtimeState,
                SaveRuntimeState)
            {
                Owner = owner
            };

        dialog.ShowDialog();

        ProcessAlarmsAndTimers();
    }

    public void OpenTimer(
        Window owner)
    {
        ClockWidgetTimerWindow dialog =
            new(
                _runtimeState,
                SaveRuntimeStateAndNotifyCompanion)
            {
                Owner = owner
            };

        dialog.ShowDialog();

        ProcessAlarmsAndTimers();
    }

    public void OpenStopwatch(
        Window owner)
    {
        ClockWidgetStopwatchWindow dialog =
            new(
                _runtimeState.Stopwatch,
                SaveRuntimeState)
            {
                Owner = owner
            };

        dialog.ShowDialog();
    }

    public void OpenCalendar(
        Window owner)
    {
        ClockWidgetCalendarWindow dialog =
            new()
            {
                Owner = owner
            };

        dialog.ShowDialog();
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

            _pendingAlerts.Enqueue(
                new PendingAlert(
                    "Alarm",
                    alarm.Id,
                    "Alarm",
                    alarm.Note));

            DebugLog.Write(
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

            _pendingAlerts.Enqueue(
                new PendingAlert(
                    "Timer",
                    timer.Id,
                    "Timer finished",
                    timer.Note));

            DebugLog.Write(
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

        DebugLog.Write(
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
            DebugLog.Write(
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

            return state;
        }
        catch (Exception ex)
        {
            DebugLog.Write(
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
            DebugLog.Write(
                "Widget.Clock",
                $"Settings save failed; Type={ex.GetType().Name}; Message={ex.Message}");
        }
    }

    private void SaveRuntimeStateAndNotifyCompanion()
    {
        SaveRuntimeState();
        UpdateCompanionView();
        NotifyCompanionIfNeeded();
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
            DebugLog.Write(
                "Widget.Clock",
                $"State save failed; Type={ex.GetType().Name}; Message={ex.Message}");
        }
    }

    private void NormalizeRunningTimers()
    {
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
        return _runtimeState.Timers
            .Where(
                timer =>
                    timer.RemainingSeconds > 0 &&
                    !timer.HasFired)
            .OrderBy(
                GetEffectiveRemainingSeconds)
            .ToList();
    }

    private ClockWidgetTimerEntry? GetPrimaryTimer()
    {
        return GetActiveTimers()
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

    private void UpdateCompanionView()
    {
        if (_companionRoot is null ||
            _companionRemainingText is null ||
            _companionMoreText is null ||
            _companionPauseButton is null ||
            _companionProgressPath is null)
        {
            return;
        }

        List<ClockWidgetTimerEntry> activeTimers =
            GetActiveTimers();

        ClockWidgetTimerEntry? timer =
            activeTimers.FirstOrDefault();

        if (timer is null)
        {
            return;
        }

        int remainingSeconds =
            GetEffectiveRemainingSeconds(
                timer);

        timer.RemainingSeconds =
            remainingSeconds;

        _companionRemainingText.Text =
            FormatTimerRemaining(
                remainingSeconds);

        _companionMoreText.Text =
            activeTimers.Count > 1
                ? "+" +
                  (activeTimers.Count - 1)
                    .ToString(
                        CultureInfo.InvariantCulture)
                : string.Empty;

        _companionPauseButton.Content =
            timer.IsRunning
                ? "Ⅱ"
                : "▶";

        double progress =
            timer.InitialSeconds <= 0
                ? 0
                : Math.Clamp(
                    remainingSeconds /
                    (double)timer.InitialSeconds,
                    0,
                    1);

        _companionProgressPath.Data =
            CreateProgressArc(
                progress);
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

    private void NotifyCompanionIfNeeded()
    {
        bool hasCompanion =
            HasCompanionView;

        if (hasCompanion ==
            _lastHasCompanionView)
        {
            return;
        }

        _lastHasCompanionView =
            hasCompanion;

        DebugLog.Write(
            "Widget.Clock",
            $"Companion changed; Visible={hasCompanion}; ActiveTimers={GetActiveTimers().Count}");

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
            DirectoryInfo instanceDirectory =
                new(
                    _context.SettingsDirectory);

            DirectoryInfo? widgetDirectory =
                instanceDirectory.Parent?.Parent;

            if (widgetDirectory is not null)
            {
                return Path.Combine(
                    widgetDirectory.FullName,
                    "Sounds");
            }
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            "GlueDock_Widgets",
            "GlueDock.ClockWidget",
            "Sounds");
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

    public void Dispose()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -=
                Timer_Tick;

            _timer = null;
        }

        _pendingAlerts.Clear();
        _timeText = null;
        _dateText = null;
        _companionRoot = null;
        _companionProgressPath = null;
        _companionRemainingText = null;
        _companionMoreText = null;
        _companionPauseButton = null;
    }
}
