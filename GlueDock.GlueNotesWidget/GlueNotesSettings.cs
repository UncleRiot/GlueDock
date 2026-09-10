namespace GlueNotes;

public sealed class GlueNotesSettings
{
    public const int CurrentVersion = 2;

    public int SettingsVersion { get; set; } =
        CurrentVersion;

    public string ThemeName { get; set; } =
        string.Empty;

    public double Opacity { get; set; } =
        0.85;

    public double BlurRadius { get; set; } =
        45;

    public bool? BorderEnabled { get; set; }

    public string BorderColor { get; set; } =
        string.Empty;

    public double DefaultFontSize { get; set; } =
        14;

    public int AutoSaveDelayMilliseconds { get; set; } =
        650;

    public double MaxImageWidth { get; set; } =
        420;

    public GlueNotesSettings Clone()
    {
        return new GlueNotesSettings
        {
            SettingsVersion =
                SettingsVersion,
            ThemeName =
                ThemeName,
            Opacity =
                Opacity,
            BlurRadius =
                BlurRadius,
            BorderEnabled =
                BorderEnabled,
            BorderColor =
                BorderColor,
            DefaultFontSize =
                DefaultFontSize,
            AutoSaveDelayMilliseconds =
                AutoSaveDelayMilliseconds,
            MaxImageWidth =
                MaxImageWidth
        };
    }

    public void CopyFrom(
        GlueNotesSettings source)
    {
        SettingsVersion =
            source.SettingsVersion;

        ThemeName =
            source.ThemeName;

        Opacity =
            source.Opacity;

        BlurRadius =
            source.BlurRadius;

        BorderEnabled =
            source.BorderEnabled;

        BorderColor =
            source.BorderColor;

        DefaultFontSize =
            source.DefaultFontSize;

        AutoSaveDelayMilliseconds =
            source.AutoSaveDelayMilliseconds;

        MaxImageWidth =
            source.MaxImageWidth;
    }
}
