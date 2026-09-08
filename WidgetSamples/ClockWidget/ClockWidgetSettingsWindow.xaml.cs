using System.IO;
using System.Windows;

namespace GlueDock.ClockWidget;

public partial class ClockWidgetSettingsWindow : Window
{
    private const string SystemDefaultSound = "(System default)";

    private sealed record DateFormatOption(
        string Id,
        string DisplayName);

    private readonly string _soundsDirectory;

    public ClockWidgetSettingsWindow(
        ClockWidgetSettings settings,
        string soundsDirectory)
    {
        _soundsDirectory = soundsDirectory;

        InitializeComponent();

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

        DisableDefaultHoverEffectCheckBox.IsChecked =
            settings.DisableDefaultHoverEffect;

        UpdateDateFormatEnabledState();
    }

    public ClockWidgetSettings Result { get; private set; } =
        new();

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

    private void ShowDateCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        UpdateDateFormatEnabledState();
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
        string alarmSound =
            AlarmSoundComboBox.SelectedItem as string ??
            SystemDefaultSound;

        string timerSound =
            TimerSoundComboBox.SelectedItem as string ??
            SystemDefaultSound;

        Result =
            new ClockWidgetSettings
            {
                ShowDate =
                    ShowDateCheckBox.IsChecked == true,
                DateFormatId =
                    DateFormatComboBox.SelectedValue as string ??
                    "GermanShort",
                DisableDefaultHoverEffect =
                    DisableDefaultHoverEffectCheckBox.IsChecked == true,
                AlarmSoundFile =
                    string.Equals(
                        alarmSound,
                        SystemDefaultSound,
                        StringComparison.Ordinal)
                        ? string.Empty
                        : alarmSound,
                TimerSoundFile =
                    string.Equals(
                        timerSound,
                        SystemDefaultSound,
                        StringComparison.Ordinal)
                        ? string.Empty
                        : timerSound
            };

        DialogResult = true;
    }
}
