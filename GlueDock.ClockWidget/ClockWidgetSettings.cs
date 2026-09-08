namespace GlueDock.ClockWidget;

public sealed class ClockWidgetSettings
{
    public bool ShowDate { get; set; } = true;

    public bool ClockSettingsOpacityOverrideEnabled { get; set; }

    public double ClockSettingsOpacity { get; set; } = 0.45;

    public bool ClockSettingsBlurOverrideEnabled { get; set; }

    public double ClockSettingsBlurRadius { get; set; } = 45;

    public string DateFormatId { get; set; } = "GermanShort";

    public double TimeScalePercent { get; set; } = 100;

    public double DateScalePercent { get; set; } = 100;

    public double TimeYOffset { get; set; }

    public double DateYOffset { get; set; }

    public string ClockColor { get; set; } = "#FFFFFFFF";

    public string TimerAlarmColor { get; set; } = "#FFFF9F0A";

    public string StopwatchColor { get; set; } = "#FFFF9F0A";

    public double TimerCompanionFontSize { get; set; } = 11;

    public string TimerCompanionColor { get; set; } = string.Empty;

    public bool TimerCompanionSettingsOpacityOverrideEnabled { get; set; }

    public double TimerCompanionSettingsOpacity { get; set; } = 0.45;

    public bool TimerCompanionSettingsBlurOverrideEnabled { get; set; }

    public double TimerCompanionSettingsBlurRadius { get; set; } = 45;

    public double StopwatchCompanionFontSize { get; set; } = 11;

    public string StopwatchCompanionColor { get; set; } = string.Empty;

    public bool StopwatchCompanionSettingsOpacityOverrideEnabled { get; set; }

    public double StopwatchCompanionSettingsOpacity { get; set; } = 0.45;

    public bool StopwatchCompanionSettingsBlurOverrideEnabled { get; set; }

    public double StopwatchCompanionSettingsBlurRadius { get; set; } = 45;

    public string AlarmColor { get; set; } = "#FFFF9F0A";

    public double AlarmCompanionTimeFontSize { get; set; } = 15;

    public double AlarmCompanionPositionFontSize { get; set; } = 10;

    public double AlarmCompanionIndexOffsetX { get; set; }

    public double AlarmCompanionIndexOffsetY { get; set; }

    public string AlarmCompanionTimeColor { get; set; } = string.Empty;

    public string AlarmCompanionIndexColor { get; set; } = string.Empty;

    public bool AlarmCompanionSettingsOpacityOverrideEnabled { get; set; }

    public double AlarmCompanionSettingsOpacity { get; set; } = 0.45;

    public bool AlarmCompanionSettingsBlurOverrideEnabled { get; set; }

    public double AlarmCompanionSettingsBlurRadius { get; set; } = 45;

    public string CalendarEventColor { get; set; } = "#FFFF9F0A";

    public int CalendarUpcomingDays { get; set; } = 7;

    public int CalendarUpcomingEventCount { get; set; } = 3;

    public double CalendarUpcomingFontSize { get; set; } = 10;

    public bool CalendarCompanionDoubleWidth { get; set; }

    public bool CalendarCompanionSettingsOpacityOverrideEnabled { get; set; }

    public double CalendarCompanionSettingsOpacity { get; set; } = 0.45;

    public bool CalendarCompanionSettingsBlurOverrideEnabled { get; set; }

    public double CalendarCompanionSettingsBlurRadius { get; set; } = 45;

    public ClockWidgetCalendarOverrideSettings CalendarOverrides { get; set; } =
        new();

    public bool DisableDefaultHoverEffect { get; set; }

    public string AlarmSoundFile { get; set; } = string.Empty;

    public string TimerSoundFile { get; set; } = string.Empty;

    public void CopyFrom(
        ClockWidgetSettings source)
    {
        ShowDate =
            source.ShowDate;
        ClockSettingsOpacityOverrideEnabled =
            source.ClockSettingsOpacityOverrideEnabled;
        ClockSettingsOpacity =
            source.ClockSettingsOpacity;
        ClockSettingsBlurOverrideEnabled =
            source.ClockSettingsBlurOverrideEnabled;
        ClockSettingsBlurRadius =
            source.ClockSettingsBlurRadius;
        DateFormatId =
            source.DateFormatId;
        TimeScalePercent =
            source.TimeScalePercent;
        DateScalePercent =
            source.DateScalePercent;
        TimeYOffset =
            source.TimeYOffset;
        DateYOffset =
            source.DateYOffset;
        ClockColor =
            source.ClockColor;
        TimerAlarmColor =
            source.TimerAlarmColor;
        StopwatchColor =
            source.StopwatchColor;
        TimerCompanionFontSize =
            source.TimerCompanionFontSize;
        TimerCompanionColor =
            source.TimerCompanionColor;
        TimerCompanionSettingsOpacityOverrideEnabled =
            source.TimerCompanionSettingsOpacityOverrideEnabled;
        TimerCompanionSettingsOpacity =
            source.TimerCompanionSettingsOpacity;
        TimerCompanionSettingsBlurOverrideEnabled =
            source.TimerCompanionSettingsBlurOverrideEnabled;
        TimerCompanionSettingsBlurRadius =
            source.TimerCompanionSettingsBlurRadius;
        StopwatchCompanionFontSize =
            source.StopwatchCompanionFontSize;
        StopwatchCompanionColor =
            source.StopwatchCompanionColor;
        StopwatchCompanionSettingsOpacityOverrideEnabled =
            source.StopwatchCompanionSettingsOpacityOverrideEnabled;
        StopwatchCompanionSettingsOpacity =
            source.StopwatchCompanionSettingsOpacity;
        StopwatchCompanionSettingsBlurOverrideEnabled =
            source.StopwatchCompanionSettingsBlurOverrideEnabled;
        StopwatchCompanionSettingsBlurRadius =
            source.StopwatchCompanionSettingsBlurRadius;
        AlarmColor =
            source.AlarmColor;
        AlarmCompanionTimeFontSize =
            source.AlarmCompanionTimeFontSize;
        AlarmCompanionPositionFontSize =
            source.AlarmCompanionPositionFontSize;
        AlarmCompanionIndexOffsetX =
            source.AlarmCompanionIndexOffsetX;
        AlarmCompanionIndexOffsetY =
            source.AlarmCompanionIndexOffsetY;
        AlarmCompanionTimeColor =
            source.AlarmCompanionTimeColor;
        AlarmCompanionIndexColor =
            source.AlarmCompanionIndexColor;
        AlarmCompanionSettingsOpacityOverrideEnabled =
            source.AlarmCompanionSettingsOpacityOverrideEnabled;
        AlarmCompanionSettingsOpacity =
            source.AlarmCompanionSettingsOpacity;
        AlarmCompanionSettingsBlurOverrideEnabled =
            source.AlarmCompanionSettingsBlurOverrideEnabled;
        AlarmCompanionSettingsBlurRadius =
            source.AlarmCompanionSettingsBlurRadius;
        CalendarEventColor =
            source.CalendarEventColor;
        CalendarUpcomingDays =
            source.CalendarUpcomingDays;
        CalendarUpcomingEventCount =
            source.CalendarUpcomingEventCount;
        CalendarUpcomingFontSize =
            source.CalendarUpcomingFontSize;
        CalendarCompanionDoubleWidth =
            source.CalendarCompanionDoubleWidth;
        CalendarCompanionSettingsOpacityOverrideEnabled =
            source.CalendarCompanionSettingsOpacityOverrideEnabled;
        CalendarCompanionSettingsOpacity =
            source.CalendarCompanionSettingsOpacity;
        CalendarCompanionSettingsBlurOverrideEnabled =
            source.CalendarCompanionSettingsBlurOverrideEnabled;
        CalendarCompanionSettingsBlurRadius =
            source.CalendarCompanionSettingsBlurRadius;

        CalendarOverrides.CopyFrom(
            source.CalendarOverrides);

        DisableDefaultHoverEffect =
            source.DisableDefaultHoverEffect;
        AlarmSoundFile =
            source.AlarmSoundFile;
        TimerSoundFile =
            source.TimerSoundFile;
    }

}

public sealed class ClockWidgetCalendarOverrideSettings
{
    public bool ThemeOverrideEnabled { get; set; }

    public string ThemeName { get; set; } = string.Empty;

    public bool OpacityOverrideEnabled { get; set; }

    public double Opacity { get; set; } = 0.45;

    public bool BlurOverrideEnabled { get; set; }

    public double BlurRadius { get; set; } = 45;

    public bool BorderOverrideEnabled { get; set; }

    public string BorderColor { get; set; } = "#FFFFFFFF";

    public double BorderThickness { get; set; } = 1;

    public bool CornerRadiusOverrideEnabled { get; set; }

    public double CornerRadius { get; set; } = 18;

    public bool TextColorOverrideEnabled { get; set; }

    public string TextColor { get; set; } = "#FFFFFFFF";

    public bool EventColorOverrideEnabled { get; set; }

    public string EventColor { get; set; } = "#FFFF9F0A";

    public void CopyFrom(
        ClockWidgetCalendarOverrideSettings source)
    {
        ThemeOverrideEnabled =
            source.ThemeOverrideEnabled;
        ThemeName =
            source.ThemeName;
        OpacityOverrideEnabled =
            source.OpacityOverrideEnabled;
        Opacity =
            source.Opacity;
        BlurOverrideEnabled =
            source.BlurOverrideEnabled;
        BlurRadius =
            source.BlurRadius;
        BorderOverrideEnabled =
            source.BorderOverrideEnabled;
        BorderColor =
            source.BorderColor;
        BorderThickness =
            source.BorderThickness;
        CornerRadiusOverrideEnabled =
            source.CornerRadiusOverrideEnabled;
        CornerRadius =
            source.CornerRadius;
        TextColorOverrideEnabled =
            source.TextColorOverrideEnabled;
        TextColor =
            source.TextColor;
        EventColorOverrideEnabled =
            source.EventColorOverrideEnabled;
        EventColor =
            source.EventColor;
    }

    public void Reset()
    {
        ThemeOverrideEnabled = false;
        ThemeName = string.Empty;
        OpacityOverrideEnabled = false;
        BlurOverrideEnabled = false;
        BorderOverrideEnabled = false;
        CornerRadiusOverrideEnabled = false;
        TextColorOverrideEnabled = false;
        EventColorOverrideEnabled = false;
    }
}
