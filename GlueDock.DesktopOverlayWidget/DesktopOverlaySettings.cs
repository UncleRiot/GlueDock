using System.Text.Json.Serialization;

namespace GlueDock.DesktopOverlayWidget;

public sealed class DesktopOverlaySettings
{
    public string ThemeName { get; set; } = string.Empty;

    public double Opacity { get; set; } = 0.85;

    public double BlurRadius { get; set; } = 45;

    public bool? BorderEnabled { get; set; }

    public string BorderColor { get; set; } = string.Empty;

    public string IconSizeMode { get; set; } = "Medium";

    public bool ShowFilePreviews { get; set; } = true;

    public string ZOrderMode { get; set; } = "Normal";

    public bool AutoArrange { get; set; }

    public string SortMode { get; set; } = "Name";

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public double WindowWidth { get; set; } = 720;

    public double WindowHeight { get; set; } = 520;

    public Dictionary<string, DesktopOverlayGridPosition> Positions { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public DesktopOverlaySettings Clone()
    {
        DesktopOverlaySettings clone =
            new()
            {
                ThemeName = ThemeName,
                Opacity = Opacity,
                BlurRadius = BlurRadius,
                BorderEnabled = BorderEnabled,
                BorderColor = BorderColor,
                IconSizeMode = IconSizeMode,
                ShowFilePreviews = ShowFilePreviews,
                ZOrderMode = ZOrderMode,
                AutoArrange = AutoArrange,
                SortMode = SortMode,
                WindowLeft = WindowLeft,
                WindowTop = WindowTop,
                WindowWidth = WindowWidth,
                WindowHeight = WindowHeight
            };

        foreach (KeyValuePair<string, DesktopOverlayGridPosition> pair in Positions)
        {
            clone.Positions[pair.Key] =
                new DesktopOverlayGridPosition
                {
                    Column = pair.Value.Column,
                    Row = pair.Value.Row
                };
        }

        return clone;
    }

    public void CopyFrom(
        DesktopOverlaySettings source)
    {
        ThemeName = source.ThemeName;
        Opacity = source.Opacity;
        BlurRadius = source.BlurRadius;
        BorderEnabled = source.BorderEnabled;
        BorderColor = source.BorderColor;
        IconSizeMode = source.IconSizeMode;
        ShowFilePreviews = source.ShowFilePreviews;
        ZOrderMode = source.ZOrderMode;
        AutoArrange = source.AutoArrange;
        SortMode = source.SortMode;
        WindowLeft = source.WindowLeft;
        WindowTop = source.WindowTop;
        WindowWidth = source.WindowWidth;
        WindowHeight = source.WindowHeight;

        Positions =
            new Dictionary<string, DesktopOverlayGridPosition>(
                StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, DesktopOverlayGridPosition> pair in source.Positions)
        {
            Positions[pair.Key] =
                new DesktopOverlayGridPosition
                {
                    Column = pair.Value.Column,
                    Row = pair.Value.Row
                };
        }
    }
}

public sealed class DesktopOverlayGridPosition
{
    public int Column { get; set; }

    public int Row { get; set; }
}
