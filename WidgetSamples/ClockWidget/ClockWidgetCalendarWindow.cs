using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace GlueDock.ClockWidget;

public sealed class ClockWidgetCalendarWindow : Window
{
    private readonly TextBlock _selectedDateText;

    public ClockWidgetCalendarWindow()
    {
        Title = "Clock widget calendar";
        Width = 390;
        Height = 390;
        ResizeMode = ResizeMode.NoResize;
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

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        System.Windows.Controls.Calendar calendar =
            new()
            {
                SelectedDate = DateTime.Today,
                DisplayDate = DateTime.Today,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

        _selectedDateText =
            new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin =
                    new Thickness(
                        0,
                        10,
                        0,
                        10),
                FontSize = 15
            };

        Button closeButton =
            new()
            {
                Content = "Close",
                Width = 90,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Right
            };

        calendar.SelectedDatesChanged +=
            (_, _) =>
            {
                DateTime selected =
                    calendar.SelectedDate ??
                    DateTime.Today;

                _selectedDateText.Text =
                    selected.ToString(
                        "dddd, dd.MM.yyyy",
                        CultureInfo.GetCultureInfo(
                            "de-DE"));
            };

        closeButton.Click +=
            (_, _) =>
                Close();

        Grid.SetRow(
            calendar,
            0);

        Grid.SetRow(
            _selectedDateText,
            1);

        Grid.SetRow(
            closeButton,
            2);

        root.Children.Add(
            calendar);

        root.Children.Add(
            _selectedDateText);

        root.Children.Add(
            closeButton);

        Content = root;

        _selectedDateText.Text =
            DateTime.Today.ToString(
                "dddd, dd.MM.yyyy",
                CultureInfo.GetCultureInfo(
                    "de-DE"));
    }
}
