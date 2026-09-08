using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace GlueDock.ClockWidget;

public sealed class ClockWidgetAlarmWindow : Window
{
    private readonly ClockWidgetRuntimeState _state;
    private readonly Action _save;
    private readonly ListBox _alarmList;
    private readonly DatePicker _datePicker;
    private readonly TextBox _timeTextBox;
    private readonly TextBox _noteTextBox;

    public ClockWidgetAlarmWindow(
        ClockWidgetRuntimeState state,
        Action save)
    {
        _state = state;
        _save = save;

        Title = "Clock widget alarms";
        Width = 520;
        Height = 430;
        MinWidth = 520;
        MinHeight = 430;
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

        _alarmList =
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
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Auto)
            });

        editor.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        for (int i = 0; i < 4; i++)
        {
            editor.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });
        }

        _datePicker =
            new DatePicker
            {
                SelectedDate = DateTime.Today,
                Width = 160,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin =
                    new Thickness(
                        8,
                        0,
                        0,
                        8)
            };

        _timeTextBox =
            new TextBox
            {
                Text =
                    DateTime.Now.AddMinutes(
                        5).ToString(
                        "HH:mm",
                        CultureInfo.InvariantCulture),
                Width = 90,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin =
                    new Thickness(
                        8,
                        0,
                        0,
                        8)
            };

        _noteTextBox =
            new TextBox
            {
                MinWidth = 280,
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
            "Date:",
            _datePicker);

        AddEditorRow(
            editor,
            1,
            "Time (HH:mm):",
            _timeTextBox);

        AddEditorRow(
            editor,
            2,
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
                80);

        Button toggleButton =
            CreateButton(
                "Enable / disable",
                120);

        Button removeButton =
            CreateButton(
                "Remove",
                80);

        Button closeButton =
            CreateButton(
                "Close",
                80);

        addButton.Click +=
            AddButton_Click;

        toggleButton.Click +=
            ToggleButton_Click;

        removeButton.Click +=
            RemoveButton_Click;

        closeButton.Click +=
            (_, _) =>
                Close();

        buttons.Children.Add(
            addButton);

        buttons.Children.Add(
            toggleButton);

        buttons.Children.Add(
            removeButton);

        buttons.Children.Add(
            closeButton);

        Grid.SetColumnSpan(
            buttons,
            2);

        Grid.SetRow(
            buttons,
            3);

        editor.Children.Add(
            buttons);

        Grid.SetRow(
            _alarmList,
            0);

        Grid.SetRow(
            editor,
            1);

        root.Children.Add(
            _alarmList);

        root.Children.Add(
            editor);

        Content = root;

        RefreshList();
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

    private void AddButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_datePicker.SelectedDate is not DateTime date ||
            !TimeSpan.TryParse(
                _timeTextBox.Text,
                CultureInfo.InvariantCulture,
                out TimeSpan time) ||
            time < TimeSpan.Zero ||
            time >= TimeSpan.FromDays(
                1))
        {
            MessageBox.Show(
                this,
                "Enter a valid date and time in HH:mm format.",
                "Clock widget",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        DateTime triggerAt =
            date.Date +
            time;

        if (triggerAt <= DateTime.Now)
        {
            MessageBox.Show(
                this,
                "The alarm time must be in the future.",
                "Clock widget",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        ClockWidgetAlarmEntry alarm =
            new()
            {
                TriggerAt = triggerAt,
                Note =
                    _noteTextBox.Text.Trim(),
                Enabled = true
            };

        _state.Alarms.Add(
            alarm);

        _save();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Alarm added; Id={alarm.Id}; TriggerAt={alarm.TriggerAt:O}; Note={alarm.Note}");

        RefreshList(
            alarm.Id);
    }

    private void ToggleButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ClockWidgetAlarmEntry? alarm =
            GetSelectedAlarm();

        if (alarm is null)
        {
            return;
        }

        alarm.Enabled =
            !alarm.Enabled;

        _save();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Alarm toggled; Id={alarm.Id}; Enabled={alarm.Enabled}");

        RefreshList(
            alarm.Id);
    }

    private void RemoveButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ClockWidgetAlarmEntry? alarm =
            GetSelectedAlarm();

        if (alarm is null)
        {
            return;
        }

        _state.Alarms.Remove(
            alarm);

        _save();

        ClockWidgetLog.Write(
            "Widget.Clock",
            $"Alarm removed; Id={alarm.Id}");

        RefreshList();
    }

    private ClockWidgetAlarmEntry? GetSelectedAlarm()
    {
        int index =
            _alarmList.SelectedIndex;

        if (index < 0 ||
            index >= _state.Alarms.Count)
        {
            return null;
        }

        return _state.Alarms[index];
    }

    private void RefreshList(
        string? selectedId = null)
    {
        _state.Alarms.Sort(
            static (left, right) =>
                left.TriggerAt.CompareTo(
                    right.TriggerAt));

        _alarmList.Items.Clear();

        int selectedIndex = -1;

        for (int i = 0; i < _state.Alarms.Count; i++)
        {
            ClockWidgetAlarmEntry alarm =
                _state.Alarms[i];

            _alarmList.Items.Add(
                $"{(alarm.Enabled ? "On " : "Off")}  {alarm.TriggerAt:dd.MM.yyyy HH:mm}  {alarm.Note}");

            if (string.Equals(
                    alarm.Id,
                    selectedId,
                    StringComparison.Ordinal))
            {
                selectedIndex = i;
            }
        }

        if (selectedIndex >= 0)
        {
            _alarmList.SelectedIndex =
                selectedIndex;
        }
    }
}
