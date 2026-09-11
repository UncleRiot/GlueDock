namespace GlueNotes;

public sealed class GlueNotesSettings
{
    public const int CurrentVersion = 4;

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


    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public double? WindowWidth { get; set; }

    public double? WindowHeight { get; set; }

    public bool WindowMaximized { get; set; }

    public double LeftPaneWidth { get; set; } =
        250;

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
                MaxImageWidth,
            WindowLeft =
                WindowLeft,
            WindowTop =
                WindowTop,
            WindowWidth =
                WindowWidth,
            WindowHeight =
                WindowHeight,
            WindowMaximized =
                WindowMaximized,
            LeftPaneWidth =
                LeftPaneWidth
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

        WindowLeft =
            source.WindowLeft;

        WindowTop =
            source.WindowTop;

        WindowWidth =
            source.WindowWidth;

        WindowHeight =
            source.WindowHeight;

        WindowMaximized =
            source.WindowMaximized;

        LeftPaneWidth =
            source.LeftPaneWidth;
    }
}
