using System.ComponentModel;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace GlueDock.ClockWidget;

public sealed class ClockWidgetAlertWindow : Window
{
    private readonly DispatcherTimer _soundTimer;
    private readonly string? _soundPath;
    private MediaPlayer? _mediaPlayer;
    private bool _acknowledged;

    public ClockWidgetAlertWindow(
        string title,
        string note,
        string? soundPath)
    {
        _soundPath = soundPath;

        Title = title;
        Width = 420;
        Height = 230;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = true;
        Topmost = true;

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        20)
            };

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
                Text = title,
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        14)
            };

        TextBlock noteText =
            new()
            {
                Text =
                    string.IsNullOrWhiteSpace(
                        note)
                        ? "No note."
                        : note,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 15
            };

        Button acknowledgeButton =
            new()
            {
                Content = "Acknowledge",
                Width = 120,
                Height = 32,
                IsDefault = true,
                HorizontalAlignment = HorizontalAlignment.Right
            };

        acknowledgeButton.Click +=
            (_, _) =>
            {
                _acknowledged = true;
                StopSound();
                Close();
            };

        Grid.SetRow(
            heading,
            0);

        Grid.SetRow(
            noteText,
            1);

        Grid.SetRow(
            acknowledgeButton,
            2);

        root.Children.Add(
            heading);

        root.Children.Add(
            noteText);

        root.Children.Add(
            acknowledgeButton);

        Content = root;

        _soundTimer =
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromSeconds(
                        1)
            };

        _soundTimer.Tick +=
            SoundTimer_Tick;

        Loaded +=
            (_, _) =>
            {
                PlayAlarmSound();
                _soundTimer.Start();
            };

        Closed +=
            (_, _) =>
            {
                _soundTimer.Stop();
                _soundTimer.Tick -=
                    SoundTimer_Tick;

                StopSound();
            };

        Closing +=
            AlertWindow_Closing;
    }

    private void SoundTimer_Tick(
        object? sender,
        EventArgs e)
    {
        PlayAlarmSound();
    }

    private void PlayAlarmSound()
    {
        string? soundPath =
            _soundPath;

        if (!string.IsNullOrWhiteSpace(
                soundPath) &&
            File.Exists(
                soundPath))
        {
            try
            {
                _mediaPlayer ??=
                    new MediaPlayer();

                _mediaPlayer.Stop();
                _mediaPlayer.Open(
                    new Uri(
                        soundPath,
                        UriKind.Absolute));
                _mediaPlayer.Position =
                    TimeSpan.Zero;
                _mediaPlayer.Play();

                return;
            }
            catch
            {
                StopSound();
            }
        }

        SystemSounds.Exclamation.Play();
    }

    private void StopSound()
    {
        if (_mediaPlayer is null)
        {
            return;
        }

        _mediaPlayer.Stop();
        _mediaPlayer.Close();
        _mediaPlayer = null;
    }

    private void AlertWindow_Closing(
        object? sender,
        CancelEventArgs e)
    {
        if (_acknowledged ||
            Dispatcher.HasShutdownStarted)
        {
            return;
        }

        e.Cancel = true;
    }
}
