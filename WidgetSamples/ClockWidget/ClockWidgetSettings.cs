namespace GlueDock.ClockWidget;

public sealed class ClockWidgetSettings
{
    public bool ShowDate { get; set; } = true;

    public string DateFormatId { get; set; } = "GermanShort";

    public bool DisableDefaultHoverEffect { get; set; }

    public string AlarmSoundFile { get; set; } = string.Empty;

    public string TimerSoundFile { get; set; } = string.Empty;
}
