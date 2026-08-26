namespace GlueDock;

public sealed class DockSettings
{
    public DockEdge Edge { get; set; } = DockEdge.Left;

    public double EdgePositionRatio { get; set; } = 0.5;

    public bool StartWithWindows { get; set; }

    public int CollapseDelayMilliseconds { get; set; } = 450;

    public bool CollapseDisabled { get; set; }

    public string BarColor { get; set; } = "#12161C";

    public bool DockBorderEnabled { get; set; } = true;

    public string DockBorderColor { get; set; } = "#FFFFFF";

    public double BarThickness { get; set; } = 7;

    public double DockScale { get; set; } = 1.0;

    public double Opacity { get; set; } = 0.45;

    public string AnimationStyle { get; set; } = "Fade";

    public List<string> Items { get; set; } = [];

    public bool FutureOption01 { get; set; }
    public bool FutureOption02 { get; set; }
    public bool FutureOption03 { get; set; }
    public bool FutureOption04 { get; set; }
    public bool FutureOption05 { get; set; }
    public bool FutureOption06 { get; set; }
    public bool FutureOption07 { get; set; }
    public bool FutureOption08 { get; set; }
    public bool FutureOption09 { get; set; }
    public bool FutureOption10 { get; set; }
    public bool FutureOption11 { get; set; }
    public bool FutureOption12 { get; set; }
    public bool FutureOption13 { get; set; }
    public bool FutureOption14 { get; set; }
    public bool FutureOption15 { get; set; }
    public bool FutureOption16 { get; set; }
    public bool FutureOption17 { get; set; }
    public bool FutureOption18 { get; set; }
    public bool FutureOption19 { get; set; }
    public bool FutureOption20 { get; set; }
}
