using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GlueDock.ClockWidget;

public sealed class ClockWidgetQuickTimerWindow : Window
{
    private readonly ClockWidgetRuntimeState _state;
    private readonly Action _save;

    public ClockWidgetQuickTimerWindow(
        ClockWidgetRuntimeState state,
        Action save)
    {
        _state = state;
        _save = save;

        Title = "New timer";
        Width = 300;
        Height = 300;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Border shell =
            new()
            {
                Margin =
                    new Thickness(
                        12),
                Padding =
                    new Thickness(
                        16),
                CornerRadius =
                    new CornerRadius(
                        18),
                Background =
                    new SolidColorBrush(
                        Color.FromRgb(
                            24,
                            24,
                            24))
            };

        Grid root =
            new();

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

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

        TextBlock heading =
            new()
            {
                Text = "Timers",
                Foreground = Brushes.White,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        14)
            };

        Grid presets =
            new();

        presets.RowDefinitions.Add(
            new RowDefinition());

        presets.RowDefinitions.Add(
            new RowDefinition());

        presets.ColumnDefinitions.Add(
            new ColumnDefinition());

        presets.ColumnDefinitions.Add(
            new ColumnDefinition());

        AddPreset(
            presets,
            0,
            0,
            1);

        AddPreset(
            presets,
            0,
            1,
            3);

        AddPreset(
            presets,
            1,
            0,
            5);

        AddPreset(
            presets,
            1,
            1,
            10);

        Button customButton =
            new()
            {
                Content = "Custom / manage timers",
                Height = 34,
                Margin =
                    new Thickness(
                        0,
                        14,
                        0,
                        0)
            };

        customButton.Click +=
            (_, _) =>
            {
                ClockWidgetTimerWindow dialog =
                    new(
                        _state,
                        _save)
                    {
                        Owner = this
                    };

                Hide();
                dialog.ShowDialog();
                Close();
            };

        Grid.SetRow(
            heading,
            0);

        Grid.SetRow(
            presets,
            1);

        Grid.SetRow(
            customButton,
            2);

        root.Children.Add(
            heading);

        root.Children.Add(
            presets);

        root.Children.Add(
            customButton);

        shell.Child =
            root;

        Content =
            shell;
    }

    private void AddPreset(
        Grid grid,
        int row,
        int column,
        int minutes)
    {
        Button button =
            new()
            {
                Content =
                    minutes +
                    Environment.NewLine +
                    "MIN",
                Width = 88,
                Height = 88,
                Margin =
                    new Thickness(
                        6),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(
                            255,
                            159,
                            10)),
                Background =
                    new SolidColorBrush(
                        Color.FromRgb(
                            38,
                            38,
                            38)),
                BorderBrush =
                    new SolidColorBrush(
                        Color.FromRgb(
                            255,
                            159,
                            10)),
                BorderThickness =
                    new Thickness(
                        2)
            };

        button.Click +=
            (_, _) =>
            {
                int totalSeconds =
                    checked(minutes * 60);

                ClockWidgetTimerEntry timer =
                    new()
                    {
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
                    $"Quick timer added; Id={timer.Id}; Minutes={minutes}");

                DialogResult = true;
            };

        Grid.SetRow(
            button,
            row);

        Grid.SetColumn(
            button,
            column);

        grid.Children.Add(
            button);
    }
}
