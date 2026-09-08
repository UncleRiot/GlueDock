using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GlueDock.ClockWidget;

public sealed class ClockWidgetTimerWindow : Window
{
    private readonly ClockWidgetRuntimeState _state;
    private readonly Action _save;
    private readonly ListBox _timerList;
    private readonly TextBox _hoursTextBox;
    private readonly TextBox _minutesTextBox;
    private readonly TextBox _secondsTextBox;
    private readonly TextBox _noteTextBox;
    private readonly DispatcherTimer _refreshTimer;

    public ClockWidgetTimerWindow(
        ClockWidgetRuntimeState state,
        Action save)
    {
        _state = state;
        _save = save;

        Title = "Clock widget timers";
        Width = 560;
        Height = 470;
        MinWidth = 560;
        MinHeight = 470;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        16)
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

        _timerList =
            new ListBox
            {
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        14)
            };

        Grid editor =
            new();

        editor.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        editor.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        for (int i = 0; i < 3; i++)
        {
            editor.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });
        }

        StackPanel durationPanel =
            new()
            {
                Orientation = Orientation.Horizontal,
                Margin =
                    new Thickness(
                        8,
                        0,
                        0,
                        8)
            };

        _hoursTextBox =
            CreateNumberBox(
                "0");

        _minutesTextBox =
            CreateNumberBox(
                "5");

        _secondsTextBox =
            CreateNumberBox(
                "0");

        durationPanel.Children.Add(
            _hoursTextBox);

        durationPanel.Children.Add(
            CreateUnitLabel(
                "h"));

        durationPanel.Children.Add(
            _minutesTextBox);

        durationPanel.Children.Add(
            CreateUnitLabel(
                "m"));

        durationPanel.Children.Add(
            _secondsTextBox);

        durationPanel.Children.Add(
            CreateUnitLabel(
                "s"));

        _noteTextBox =
            new TextBox
            {
                MinWidth = 300,
                Margin =
                    new Thickness(
                        8,
                        0,
                        0,
                        8)
            };

        AddEditorRow(
            editor,
            0,
            "Duration:",
            durationPanel);

        AddEditorRow(
            editor,
            1,
            "Note:",
            _noteTextBox);

        StackPanel buttons =
            new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin =
                    new Thickness(
                        0,
                        8,
                        0,
                        0)
            };

        Button addButton =
            CreateButton(
                "Add",
                70);

        Button startPauseButton =
            CreateButton(
                "Start / pause",
                105);

        Button resetButton =
            CreateButton(
                "Reset",
                75);

        Button removeButton =
            CreateButton(
                "Remove",
                75);

        Button closeButton =
            CreateButton(
                "Close",
                75);

        addButton.Click +=
            AddButton_Click;

        startPauseButton.Click +=
            StartPauseButton_Click;

        resetButton.Click +=
            ResetButton_Click;

        removeButton.Click +=
            RemoveButton_Click;

        closeButton.Click +=
            (_, _) =>
                Close();

        buttons.Children.Add(
            addButton);

        buttons.Children.Add(
            startPauseButton);

        buttons.Children.Add(
            resetButton);

        buttons.Children.Add(
            removeButton);

        buttons.Children.Add(
            closeButton);

        Grid.SetRow(
            buttons,
            2);

        Grid.SetColumnSpan(
            buttons,
            2);

        editor.Children.Add(
            buttons);

        Grid.SetRow(
            _timerList,
            0);

        Grid.SetRow(
            editor,
            1);

        root.Children.Add(
            _timerList);

        root.Children.Add(
            editor);

        Content = root;

        _refreshTimer =
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        250)
            };

        _refreshTimer.Tick +=
            RefreshTimer_Tick;

        Loaded +=
            (_, _) =>
            {
                RefreshList();
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

    private static TextBox CreateNumberBox(
        string text)
    {
        return new TextBox
        {
            Text = text,
            Width = 46,
            Margin =
                new Thickness(
                    0,
                    0,
                    4,
                    0)
        };
    }

    private static TextBlock CreateUnitLabel(
        string text)
    {
        return new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin =
                new Thickness(
                    0,
                    0,
                    8,
                    0)
        };
    }

    private static Button CreateButton(
        string content,
        double width)
    {
        return new Button
        {
            Content = content,
            Width = width,
            Height = 30,
            Margin =
                new Thickness(
                    6,
                    0,
                    0,
                    0)
        };
    }

    private static void AddEditorRow(
        Grid grid,
        int row,
        string labelText,
        FrameworkElement editor)
    {
        TextBlock label =
            new()
            {
                Text = labelText,
                VerticalAlignment = VerticalAlignment.Center,
                Margin =
                    new Thickness(
                        0,
                        0,
                        8,
                        8)
            };

        Grid.SetRow(
            label,
            row);

        Grid.SetColumn(
            label,
            0);

        Grid.SetRow(
            editor,
            row);

        Grid.SetColumn(
            editor,
            1);

        grid.Children.Add(
            label);

        grid.Children.Add(
            editor);
    }

    private void AddButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!TryGetDurationSeconds(
                out int totalSeconds))
        {
            MessageBox.Show(
                this,
                "Enter a timer duration greater than zero.",
                "Clock widget",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        ClockWidgetTimerEntry timer =
            new()
            {
                Note =
                    _noteTextBox.Text.Trim(),
                InitialSeconds = totalSeconds,
                RemainingSeconds = totalSeconds,
                IsRunning = true,
                EndUtc =
                    DateTime.UtcNow.AddSeconds(
                        totalSeconds),
                HasFired = false
            };

        _state.Timers.Add(
            timer);

        _save();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Timer added; Id={timer.Id}; Seconds={totalSeconds}; Note={timer.Note}");

        RefreshList(
            timer.Id);
    }

    private void StartPauseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ClockWidgetTimerEntry? timer =
            GetSelectedTimer();

        if (timer is null)
        {
            return;
        }

        UpdateRemaining(
            timer);

        if (timer.IsRunning)
        {
            timer.IsRunning = false;
            timer.EndUtc = null;
        }
        else
        {
            if (timer.RemainingSeconds <= 0)
            {
                timer.RemainingSeconds =
                    timer.InitialSeconds;
            }

            timer.HasFired = false;
            timer.IsRunning = true;
            timer.EndUtc =
                DateTime.UtcNow.AddSeconds(
                    timer.RemainingSeconds);
        }

        _save();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Timer state changed; Id={timer.Id}; Running={timer.IsRunning}; Remaining={timer.RemainingSeconds}");

        RefreshList(
            timer.Id);
    }

    private void ResetButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ClockWidgetTimerEntry? timer =
            GetSelectedTimer();

        if (timer is null)
        {
            return;
        }

        timer.IsRunning = false;
        timer.EndUtc = null;
        timer.RemainingSeconds =
            timer.InitialSeconds;
        timer.HasFired = false;

        _save();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Timer reset; Id={timer.Id}");

        RefreshList(
            timer.Id);
    }

    private void RemoveButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ClockWidgetTimerEntry? timer =
            GetSelectedTimer();

        if (timer is null)
        {
            return;
        }

        _state.Timers.Remove(
            timer);

        _save();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Timer removed; Id={timer.Id}");

        RefreshList();
    }

    private bool TryGetDurationSeconds(
        out int totalSeconds)
    {
        totalSeconds = 0;

        if (!int.TryParse(
                _hoursTextBox.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int hours) ||
            !int.TryParse(
                _minutesTextBox.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int minutes) ||
            !int.TryParse(
                _secondsTextBox.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int seconds) ||
            hours < 0 ||
            minutes < 0 ||
            minutes > 59 ||
            seconds < 0 ||
            seconds > 59)
        {
            return false;
        }

        long total =
            ((long)hours * 3600L) +
            ((long)minutes * 60L) +
            seconds;

        if (total <= 0 ||
            total > int.MaxValue)
        {
            return false;
        }

        totalSeconds =
            (int)total;

        return true;
    }

    private ClockWidgetTimerEntry? GetSelectedTimer()
    {
        int index =
            _timerList.SelectedIndex;

        if (index < 0 ||
            index >= _state.Timers.Count)
        {
            return null;
        }

        return _state.Timers[index];
    }

    private void RefreshTimer_Tick(
        object? sender,
        EventArgs e)
    {
        string? selectedId =
            GetSelectedTimer()?.Id;

        RefreshList(
            selectedId);
    }

    private void RefreshList(
        string? selectedId = null)
    {
        int selectedIndex = -1;

        _timerList.Items.Clear();

        for (int i = 0; i < _state.Timers.Count; i++)
        {
            ClockWidgetTimerEntry timer =
                _state.Timers[i];

            UpdateRemaining(
                timer);

            TimeSpan remaining =
                TimeSpan.FromSeconds(
                    Math.Max(
                        0,
                        timer.RemainingSeconds));

            string stateText =
                timer.IsRunning
                    ? "Running"
                    : timer.RemainingSeconds <= 0
                        ? "Done"
                        : "Paused";

            _timerList.Items.Add(
                $"{stateText,-7}  {FormatDuration(remaining)}  {timer.Note}");

            if (string.Equals(
                    timer.Id,
                    selectedId,
                    StringComparison.Ordinal))
            {
                selectedIndex = i;
            }
        }

        if (selectedIndex >= 0)
        {
            _timerList.SelectedIndex =
                selectedIndex;
        }
    }

    private static void UpdateRemaining(
        ClockWidgetTimerEntry timer)
    {
        if (!timer.IsRunning ||
            timer.EndUtc is not DateTime endUtc)
        {
            return;
        }

        double seconds =
            Math.Ceiling(
                (endUtc - DateTime.UtcNow).TotalSeconds);

        timer.RemainingSeconds =
            Math.Max(
                0,
                (int)seconds);
    }

    private static string FormatDuration(
        TimeSpan value)
    {
        int totalHours =
            (int)value.TotalHours;

        return $"{totalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }
}
