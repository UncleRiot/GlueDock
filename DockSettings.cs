namespace GlueDock;

public sealed class DockSettings
{
    public DockEdge Edge { get; set; } = DockEdge.Left;

    public double EdgePositionRatio { get; set; } = 0.5;

    public bool StartWithWindows { get; set; }

    public bool AlwaysOnTop { get; set; } = true;

    public int CollapseDelayMilliseconds { get; set; } = 450;

    public int SubdockCollapseDelayMilliseconds { get; set; } = 180;

    public bool SubdockOpenOnClickOnly { get; set; }

    public bool CollapseDisabled { get; set; }

    public string BarColor { get; set; } = "#12161C";

    public double CollapsedBarOpacity { get; set; } = 0.45;

    public string ThemeName { get; set; } = "Default";

    public const string SubDockThemeModeInheritFull = "InheritFull";

    public const string SubDockThemeModeInheritAppearance = "InheritAppearance";

    public const string SubDockThemeModeOverride = "Override";

    public string SubDockThemeMode { get; set; } = SubDockThemeModeInheritFull;

    public string SubDockThemeName { get; set; } = "Default";

    public double? SubDockScale { get; set; }

    public double? SubDockItemSpacing { get; set; }

    public double? SubDockThicknessScale { get; set; }

    public bool DockBorderEnabled { get; set; } = true;

    public string DockBorderColor { get; set; } = "#FFFFFF";

    public double BarThickness { get; set; } = 7;

    public double DockScale { get; set; } = 1.0;

    public double ItemSpacing { get; set; } = 6.0;

    public int RootMaxColumns { get; set; } = 50;

    public int RootMaxRows { get; set; } = 1;

    public int SubDockMaxColumns { get; set; } = 50;

    public int SubDockMaxRows { get; set; } = 1;

    public double DockThicknessScale { get; set; } = 1.0;

    public bool ShowItemLabels { get; set; } = false;

    public bool ShowRootDockTooltips { get; set; } = true;

    public bool ShowSubDockTooltips { get; set; } = false;

    public bool ShowFilePreviews { get; set; } = true;

    public const string ShortcutOverlayDefault = "Default";

    public const string ShortcutOverlaySmall = "Small";

    public const string ShortcutOverlayNone = "None";

    public string ShortcutOverlayMode { get; set; } = ShortcutOverlayDefault;

    public bool UseSmallShortcutOverlay
    {
        get =>
            string.Equals(
                ShortcutOverlayMode,
                ShortcutOverlaySmall,
                StringComparison.OrdinalIgnoreCase);

        set
        {
            if (value)
            {
                ShortcutOverlayMode =
                    ShortcutOverlaySmall;
            }
        }
    }

    public string SubmenuIconMode { get; set; } = "Default";

    public string SubmenuWindowsIcon { get; set; } = "Folder";

    public string SubmenuWindowsIconPath { get; set; } = string.Empty;

    public int SubmenuWindowsIconIndex { get; set; }

    public string SubmenuIconRepositoryPath { get; set; } = string.Empty;

    public double Opacity { get; set; } = 0.45;

    public double SettingsDialogOpacity { get; set; } = 0.85;

    public double BlurRadius { get; set; } = 45;

    public string AnimationStyle { get; set; } = "Fade";

    public string HoverEffect { get; set; } = "None";

    public string LanguageCode { get; set; } = "EN";

    public string LastLicenseNoticeVersion { get; set; } = string.Empty;

    public bool DebugLoggingEnabled { get; set; }

    public int DebugLogMaxSizeMegabytes { get; set; } = 10;

    public List<DockEntrySettings> DockItems { get; set; } = [];

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
