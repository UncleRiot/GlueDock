using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GlueDock.ClockWidget;

public sealed class ClockWidgetStopwatchWindow : Window
{
    private readonly ClockWidgetStopwatchState _state;
    private readonly Action _save;
    private readonly TextBlock _display;
    private readonly DispatcherTimer _refreshTimer;

    public ClockWidgetStopwatchWindow(
        ClockWidgetStopwatchState state,
        Action save)
    {
        _state = state;
        _save = save;

        Title = "Clock widget stopwatch";
        Width = 390;
        Height = 210;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        18)
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

        _display =
            new TextBlock
            {
                FontSize = 30,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

        StackPanel buttons =
            new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

        Button startPauseButton =
            CreateButton(
                "Start / pause");

        Button resetButton =
            CreateButton(
                "Reset");

        Button closeButton =
            CreateButton(
                "Close");

        startPauseButton.Click +=
            StartPauseButton_Click;

        resetButton.Click +=
            ResetButton_Click;

        closeButton.Click +=
            (_, _) =>
                Close();

        buttons.Children.Add(
            startPauseButton);

        buttons.Children.Add(
            resetButton);

        buttons.Children.Add(
            closeButton);

        Grid.SetRow(
            _display,
            0);

        Grid.SetRow(
            buttons,
            1);

        root.Children.Add(
            _display);

        root.Children.Add(
            buttons);

        Content = root;

        _refreshTimer =
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        50)
            };

        _refreshTimer.Tick +=
            RefreshTimer_Tick;

        Loaded +=
            (_, _) =>
            {
                UpdateDisplay();
                _refreshTimer.Start();
            };

        Closed +=
            (_, _) =>
            {
                _refreshTimer.Stop();
                _refreshTimer.Tick -=
                    RefreshTimer_Tick;
            };
    }

    private static Button CreateButton(
        string content)
    {
        return new Button
        {
            Content = content,
            Width = 100,
            Height = 32,
            Margin =
                new Thickness(
                    5,
                    0,
                    5,
                    0)
        };
    }

    private void StartPauseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_state.IsRunning)
        {
            _state.ElapsedMilliseconds =
                GetElapsedMilliseconds();

            _state.IsRunning = false;
            _state.StartedUtc = null;
        }
        else
        {
            _state.IsRunning = true;
            _state.StartedUtc =
                DateTime.UtcNow;
        }

        _save();

        DebugLog.Write(
            "Widget.Clock",
            $"Stopwatch state changed; Running={_state.IsRunning}; ElapsedMilliseconds={_state.ElapsedMilliseconds}");

        UpdateDisplay();
    }

    private void ResetButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _state.ElapsedMilliseconds = 0;

        if (_state.IsRunning)
        {
            _state.StartedUtc =
                DateTime.UtcNow;
        }

        _save();

        DebugLog.Write(
            "Widget.Clock",
            "Stopwatch reset");

        UpdateDisplay();
    }

    private void RefreshTimer_Tick(
        object? sender,
        EventArgs e)
    {
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        TimeSpan elapsed =
            TimeSpan.FromMilliseconds(
                GetElapsedMilliseconds());

        int totalHours =
            (int)elapsed.TotalHours;

        _display.Text =
            $"{totalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds / 10:00}";
    }

    private long GetElapsedMilliseconds()
    {
        long elapsed =
            _state.ElapsedMilliseconds;

        if (_state.IsRunning &&
            _state.StartedUtc is DateTime startedUtc)
        {
            elapsed +=
                Math.Max(
                    0L,
                    (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds);
        }

        return elapsed;
    }
}
