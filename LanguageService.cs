using System.IO;
using System.Text.Json;

namespace GlueDock;

public sealed class LanguageService
{
    private readonly Dictionary<string, string> _english =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["App.Name"] = "GlueDock",

            ["Welcome.Title"] = "Welcome to GlueDock",
            ["Welcome.Heading"] = "Welcome to GlueDock",
            ["Welcome.Version"] = "Version: {0}",
            ["Welcome.LicenseInfo"] = "GlueDock is free software distributed under the GNU General Public License version 3 (GPLv3). You may view the complete license before continuing.",
            ["Welcome.ViewLicense"] = "View GPLv3 license",
            ["Welcome.Acknowledge"] = "I acknowledge the GPLv3 license notice.",
            ["Welcome.Support"] = "GlueDock can be used free of charge. If this project helps you, I appreciate voluntary support through Ko-fi.",
            ["Welcome.Continue"] = "Continue",

            ["Context.Settings"] = "Settings",
            ["Context.DisableAutohide"] = "Disable autohide",
            ["Context.About"] = "About",
            ["Context.Exit"] = "Exit",
            ["Context.Remove"] = "Remove",
            ["Context.Show"] = "Show",
            ["Context.WidgetDragDropLock"] = "Lock drag & drop",

            ["Submenu.New"] = "New GlueDock submenu",
            ["Submenu.CreateTitle"] = "New GlueDock submenu",
            ["Submenu.Name"] = "Name",
            ["Submenu.DefaultName"] = "New submenu",

            ["Settings.Title"] = "GlueDock Settings",
            ["Settings.Tab.General"] = "General",
            ["Settings.Tab.Appearance"] = "Appearance",
            ["Settings.Tab.RootDock"] = "Root Dock",
            ["Settings.Tab.SubDock"] = "SubDock",
            ["Settings.Tab.Behavior"] = "Behavior",
            ["Settings.Tab.Dock"] = "Dock",
            ["Settings.Tab.Widgets"] = "Widgets",
            ["Settings.Tab.Advanced"] = "Advanced",
            ["Settings.Navigation.WidgetClock"] = "Widget: Clock",
            ["Settings.Navigation.WidgetDriveUsage"] = "Widget: DriveUsage",
            ["Settings.Navigation.WidgetDesktopOverlay"] = "Widget: Desktop Overlay",
            ["Widget.DesktopOverlay.DisplayName"] = "Desktop Overlay",
            ["Widget.DesktopOverlay.Description"] = "Desktop-style overlay for files, shortcuts and groups.",
            ["Widget.DesktopOverlay.Settings.Title"] = "Desktop Overlay settings",
            ["Widget.DesktopOverlay.Settings.IconSize"] = "Icon size",
            ["Widget.DesktopOverlay.Settings.ZOrder"] = "Window layer",
            ["Widget.DesktopOverlay.Settings.AutoArrange"] = "Auto arrange icons",
            ["Widget.DesktopOverlay.Settings.SortBy"] = "Sort by",
            ["Widget.DesktopOverlay.Settings.OpenFolder"] = "Open storage folder",
            ["Widget.DesktopOverlay.Settings.Reset"] = "Reset",
            ["Widget.DesktopOverlay.IconSize.Small"] = "Small",
            ["Widget.DesktopOverlay.IconSize.Medium"] = "Medium",
            ["Widget.DesktopOverlay.IconSize.Large"] = "Large",
            ["Widget.DesktopOverlay.ZOrder.Normal"] = "Normal",
            ["Widget.DesktopOverlay.ZOrder.AlwaysOnTop"] = "Always on top",
            ["Widget.DesktopOverlay.ZOrder.Desktop"] = "Desktop layer",
            ["Widget.DesktopOverlay.Sort.Name"] = "Name",
            ["Widget.DesktopOverlay.Sort.Type"] = "Type",
            ["Widget.DesktopOverlay.Sort.Date"] = "Date modified",
            ["Widget.DesktopOverlay.Window.Title"] = "Desktop Overlay",
            ["Widget.DesktopOverlay.Group.DefaultName"] = "New group",
            ["Widget.DesktopOverlay.Context.Open"] = "Open",
            ["Widget.DesktopOverlay.Context.OpenGroup"] = "Open group",
            ["Widget.DesktopOverlay.Context.Rename"] = "Rename",
            ["Widget.DesktopOverlay.Context.Ungroup"] = "Ungroup",
            ["Widget.DesktopOverlay.Context.OpenFolder"] = "Open folder",
            ["Widget.DesktopOverlay.Context.Properties"] = "Properties",
            ["Widget.DesktopOverlay.Context.Delete"] = "Delete",
            ["Widget.DesktopOverlay.Context.RemoveFromGroup"] = "Remove from group",
            ["Widget.DesktopOverlay.Delete.Title"] = "Delete item",
            ["Widget.DesktopOverlay.Delete.Confirm"] = "Delete \"{0}\" permanently from the Desktop Overlay storage folder?",
            ["Widget.DesktopOverlay.Rename.Title"] = "Rename",
            ["Settings.WidgetSection.Instance"] = "Instance",
            ["Settings.WidgetSection.AlarmCompanion"] = "Alarm Companion",
            ["Settings.WidgetSection.TimerCompanion"] = "Timer Companion",
            ["Settings.WidgetSection.StopwatchCompanion"] = "Stopwatch Companion",
            ["Settings.WidgetSection.CalendarCompanion"] = "Calendar Companion",
            ["Widget.Clock.CalendarCompanion.UpcomingEvents"] = "Upcoming events",
            ["Widget.Clock.CalendarCompanion.DaysAhead"] = "Show events this many days ahead",
            ["Widget.Clock.CalendarCompanion.DoubleWidth"] = "Double-width calendar event preview",
            ["Widget.Clock.CalendarCompanion.FontSize"] = "Event font size",
            ["Widget.Clock.CalendarCompanion.EventColor"] = "Event color",
            ["Widget.Clock.TimerCompanion.FontSize"] = "Timer font size",
            ["Widget.Clock.TimerCompanion.Color"] = "Timer color",
            ["Widget.Clock.TimerCompanion.AlarmColor"] = "Timer alarm color",
            ["Widget.Clock.TimerCompanion.Sound"] = "Timer sound",
            ["Widget.Clock.StopwatchCompanion.FontSize"] = "Stopwatch font size",
            ["Widget.Clock.StopwatchCompanion.Color"] = "Stopwatch companion color",
            ["Widget.Clock.StopwatchCompanion.BaseColor"] = "Stopwatch color",
            ["Widget.Clock.AlarmCompanion.AlarmColor"] = "Alarm color",
            ["Widget.Clock.AlarmCompanion.Sound"] = "Alarm sound",
            ["Widget.Clock.Settings.SystemDefaultSound"] = "(System default)",
            ["Widget.Clock.Settings.Mp3Folder"] = "MP3 folder: {0}",
            ["Settings.WidgetSection.CalendarOverrides"] = "Calendar Overrides",
            ["Settings.WidgetSection.Companion"] = "Companion",
            ["Settings.Tab.Profiles"] = "Profiles",
            ["Settings.Navigation.GeneralDockSettings"] = "General Dock Settings",

            ["Settings.StartWithWindows"] = "Start with Windows",
            ["Settings.AlwaysOnTop"] = "Always on top",
            ["Settings.DisableCollapse"] = "Disable autohide",
            ["Settings.SubdockOpenOnClickOnly"] = "Open subdocks only by click",
            ["Settings.CollapseDelay"] = "Autohide delay",
            ["Settings.SubdockCollapseDelay"] = "Subdock autohide delay",

            ["Settings.Theme"] = "Theme",
            ["Settings.EdgeBarColor"] = "Autohide strip color",
            ["Settings.ChooseColor"] = "Choose color...",
            ["Settings.CollapsedBarOpacity"] = "Autohide strip opacity",
            ["Settings.ShowDockBorder"] = "Show dock border",
            ["Settings.DockBorderColor"] = "Dock border color",
            ["Settings.EdgeBarThickness"] = "Autohide strip thickness",
            ["Settings.DockScale"] = "Dock scaling",
            ["Settings.ItemSpacing"] = "Item spacing",
            ["Settings.RootLayout"] = "Root dock layout",
            ["Settings.SubDockLayout"] = "Subdock layout",
            ["Settings.SubDockThemeMode"] = "Subdock theme",
            ["Settings.SubDockThemeOverride"] = "Theme",
            ["Settings.SubDockThemeMode.InheritFull"] = "Inherit: Full theme",
            ["Settings.SubDockThemeMode.InheritAppearance"] = "Inherit: Appearance only",
            ["Settings.SubDockThemeMode.Override"] = "Override",
            ["Settings.SubDockScale"] = "Subdock scaling",
            ["Settings.SubDockThicknessScale"] = "Subdock height",
            ["Settings.SubDockItemSpacing"] = "Subdock item spacing",
            ["Settings.MaxColumns"] = "Max columns",
            ["Settings.MaxRows"] = "Max rows",
            ["Message.LayoutRowsAdjusted"] = "{0} rows are required for {1} items. The setting was adjusted automatically.",
            ["Settings.DockThicknessScale"] = "Dock height",
            ["Settings.ShowItemLabels"] = "Show names below icons",
            ["Settings.ShowFilePreviews"] = "Show file and folder previews when available",
            ["Settings.UseSmallShortcutOverlay"] = "Use smaller shortcut arrow",
            ["Settings.ShortcutArrow"] = "Shortcut arrow",
            ["Settings.ShortcutArrow.Default"] = "Use default shortcut arrow size",
            ["Settings.ShortcutArrow.Small"] = "Use smaller shortcut arrow",
            ["Settings.ShortcutArrow.None"] = "No shortcut arrow",
            ["Settings.SubmenuIcon"] = "Subdock icon",
            ["Settings.SubmenuIcon.ChooseWindows"] = "Choose Windows icon...",
            ["Settings.SubmenuIcon.LoadImage"] = "Load image...",
            ["Settings.SubmenuIcon.UseDefault"] = "Use default",
            ["Settings.SubmenuIcon.RepositoryInfo"] = "External images are copied to GlueDock_Repository\\Icons and remain available for reuse.",
            ["Settings.Opacity"] = "Opacity",
            ["Settings.SettingsDialogOpacity"] = "Settings opacity",
            ["Settings.UseGeneralSetting"] = "Use general setting",
            ["Widget.DriveUsage.Settings.Title"] = "DriveUsage settings",
            ["Widget.DriveUsage.Settings.SelectDrives"] = "Select up to four drives.",
            ["Widget.DriveUsage.Settings.Drive1"] = "Drive 1",
            ["Widget.DriveUsage.Settings.Drive2"] = "Drive 2",
            ["Widget.DriveUsage.Settings.Drive3"] = "Drive 3",
            ["Widget.DriveUsage.Settings.Drive4"] = "Drive 4",
            ["Widget.DriveUsage.Settings.None"] = "(None)",
            ["Widget.DriveUsage.Settings.RingAppearance"] = "Ring appearance",
            ["Widget.DriveUsage.Settings.RingColor"] = "Ring color",
            ["Widget.DriveUsage.Settings.RingScale"] = "Ring scale",
            ["Widget.DriveUsage.Settings.RingThickness"] = "Ring thickness",
            ["Widget.DriveUsage.Settings.RingFill"] = "Ring fill",
            ["Widget.DriveUsage.Settings.CompanionScale"] = "Companion scale",
            ["Widget.DriveUsage.Settings.Used"] = "Used",
            ["Widget.DriveUsage.Settings.Free"] = "Free",
            ["Widget.DriveUsage.Settings.DisableDefaultHoverEffect"] = "Disable GlueDock default hover effect",
            ["Widget.DriveUsage.Settings.DefaultInfo"] = "Default: orange ring, 100% scale, 100% thickness, Used, companion 100%.",
            ["Widget.DriveUsage.Settings.DuplicateDriveMessage"] = "Each drive can only be selected once.",
            ["Settings.Blur"] = "Blur",
            ["Settings.Animation"] = "Animation",
            ["Settings.HoverEffect"] = "Hover effect",
            ["Settings.Animation.Fade"] = "Fade",
            ["Settings.Animation.Slide"] = "Slide",
            ["Settings.Animation.Zoom"] = "Zoom",
            ["Settings.HoverEffect.None"] = "None",
            ["Settings.HoverEffect.Zoom"] = "Zoom",
            ["Settings.HoverEffect.Glow"] = "Glow",
            ["Settings.HoverEffect.Highlight"] = "Highlight",
            ["Settings.ShowRootDockTooltips"] = "Show tooltips in root dock",
            ["Settings.ShowSubDockTooltips"] = "Show tooltips in subdocks",
            ["Settings.Language"] = "Language",
            ["Settings.Profiles.Info"] = "Select a profile for live preview. Changes remain temporary until Apply is clicked.",
            ["Settings.Profiles.New"] = "New",
            ["Settings.Profiles.SaveCurrent"] = "Save current",
            ["Settings.Profiles.Rename"] = "Rename",
            ["Settings.Profiles.Delete"] = "Delete",
            ["Settings.Profiles.CancelPreview"] = "Cancel preview",
            ["Settings.Profiles.Apply"] = "Apply",
            ["Settings.DebugLogging"] = "Enable debug logging",
            ["Settings.DebugLogMaxSize"] = "Max size",
            ["Settings.DebugLoggingInfo"] = "Live log, 20 MB active file, rotated logs Brotli-compressed, maximum 10 files",
            ["Settings.BackupRestore.Title"] = "Backup & Restore",
            ["Settings.BackupRestore.Info"] = "Back up GlueDock settings, items, widgets, widget data, profiles, themes and repository files.",
            ["Settings.BackupRestore.Create"] = "Create backup...",
            ["Settings.BackupRestore.Restore"] = "Restore backup...",
            ["Settings.BackupRestore.BackupDialogTitle"] = "Create GlueDock backup",
            ["Settings.BackupRestore.RestoreDialogTitle"] = "Restore GlueDock backup",
            ["Settings.BackupRestore.BackupSuccess"] = "GlueDock backup created successfully.",
            ["Settings.BackupRestore.BackupFailed"] = "The GlueDock backup could not be created.\n\n{0}",
            ["Settings.BackupRestore.RestoreConfirm"] = "The selected backup will replace the current GlueDock settings, items, widgets, widget data, profiles, themes and repository files on the next start. Continue?",
            ["Settings.BackupRestore.RestoreQueued"] = "Restore prepared. Close GlueDock and start it again to apply the backup.",
            ["Settings.BackupRestore.RestoreFailed"] = "The GlueDock backup could not be prepared for restore.\n\n{0}",

            ["Settings.ReservedBehavior"] = "Reserved for additional options",
            ["Settings.ReservedDock"] = "Reserved for additional dock options",
            ["Settings.ReservedAdvanced"] = "Reserved for additional advanced options",
            ["Settings.Widgets.Available"] = "Available widgets",
            ["Settings.Widgets.Column.Name"] = "Name",
            ["Settings.Widgets.Column.Description"] = "Description",
            ["Settings.Widgets.Column.Dll"] = "DLL",
            ["Settings.Widgets.Column.Added"] = "Added",
            ["Settings.Widgets.Added"] = "Added",
            ["Settings.Widgets.DuplicateTitle"] = "Add widget",
            ["Settings.Widgets.DuplicatePrompt"] = "This widget has already been added {0} time(s). Do you want to add another widget of the same type?",
            ["Settings.Widgets.AddSelected"] = "Add selected",
            ["Settings.Widgets.Browse"] = "Browse...",
            ["Settings.Option"] = "Option",
            ["Settings.Close"] = "Close",

            ["ColorPicker.Title"] = "Choose color",
            ["ColorPicker.Red"] = "Red",
            ["ColorPicker.Green"] = "Green",
            ["ColorPicker.Blue"] = "Blue",
            ["ColorPicker.Hex"] = "Hex:",
            ["ColorPicker.Cancel"] = "Cancel",
            ["ColorPicker.Ok"] = "OK",

            ["Widget.Clock.AlarmCompanion.SettingsTitle"] = "Alarm companion settings",
            ["Widget.Clock.AlarmCompanion.UseGeneralSetting"] = "Use general setting",
            ["Widget.Clock.AlarmCompanion.TimeFontSize"] = "Alarm time font size",
            ["Widget.Clock.AlarmCompanion.IndexFontSize"] = "Alarm index font size",
            ["Widget.Clock.AlarmCompanion.TimeColor"] = "Alarm time color",
            ["Widget.Clock.AlarmCompanion.IndexColor"] = "Alarm index color",
            ["Widget.Clock.AlarmCompanion.IndexPosition"] = "Alarm index position",
            ["Widget.Clock.AlarmCompanion.Alarms"] = "Alarms",
            ["Widget.Clock.AlarmCompanion.New"] = "New",
            ["Widget.Clock.AlarmCompanion.Edit"] = "Edit",
            ["Widget.Clock.AlarmCompanion.Delete"] = "Delete",
            ["Widget.Clock.AlarmCompanion.Reset"] = "Reset",
            ["Widget.Clock.AlarmCompanion.Name"] = "Name",
            ["Widget.Clock.AlarmCompanion.NameOptional"] = "Alarm name (optional)",
            ["Widget.Clock.AlarmCompanion.NewAlarmTitle"] = "New alarm",
            ["Widget.Clock.AlarmCompanion.Date"] = "Date",
            ["Widget.Clock.AlarmCompanion.Time"] = "Time",
            ["Widget.Clock.AlarmCompanion.Enabled"] = "Enabled",
            ["Widget.Clock.AlarmCompanion.AddAlarm"] = "Add alarm",
            ["Widget.Clock.AlarmCompanion.RemoveAlarm"] = "Remove alarm",
            ["Widget.Clock.AlarmCompanion.CloseCompanion"] = "Close Companion",
            ["Widget.Clock.AlarmCompanion.AlarmTime"] = "Alarm time",
            ["Widget.Clock.AlarmCompanion.IndexNavigationHint"] = "Click: next alarm / right-click: previous alarm",
            ["Widget.Clock.AlarmCompanion.EnableHint"] = "Enable / disable selected alarm",
            ["Widget.Clock.AlarmCompanion.On"] = "On",
            ["Widget.Clock.AlarmCompanion.Off"] = "Off",

            ["Message.LaunchFailed"] = "The item could not be launched.",
            ["Message.ImportFailed"] = "The item could not be saved in GlueDock.",
            ["Message.WidgetIncompatible"] = "The selected DLL is not a compatible GlueDock widget.",
            ["Message.SubmenuIconImportFailed"] = "The image could not be copied to the GlueDock repository."
        };

    private Dictionary<string, string> _current =
        new(StringComparer.OrdinalIgnoreCase);

    public string CurrentLanguageCode { get; private set; } = "EN";

    public event EventHandler? LanguageChanged;

    public LanguageService()
    {
        EnsureBundledLanguageFiles();
        Load("EN");
    }

    public string this[string key]
    {
        get
        {
            if (_current.TryGetValue(
                    key,
                    out string? value))
            {
                return value;
            }

            if (_english.TryGetValue(
                    key,
                    out value))
            {
                return value;
            }

            return key;
        }
    }

    public void Load(
        string? languageCode)
    {
        string normalized =
            string.IsNullOrWhiteSpace(languageCode)
                ? "EN"
                : languageCode.Trim().ToUpperInvariant();

        _current =
            new Dictionary<string, string>(
                _english,
                StringComparer.OrdinalIgnoreCase);

        CurrentLanguageCode = normalized;

        if (!string.Equals(
                normalized,
                "EN",
                StringComparison.OrdinalIgnoreCase))
        {
            string languageFile =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "GlueDock_Data",
                    "Languages",
                    $"GlueDock_{normalized}.json");

            try
            {
                if (File.Exists(languageFile))
                {
                    string json =
                        File.ReadAllText(languageFile);

                    Dictionary<string, string>? values =
                        JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                    if (values is not null)
                    {
                        foreach (KeyValuePair<string, string> entry in values)
                        {
                            if (!string.IsNullOrWhiteSpace(entry.Value))
                            {
                                _current[entry.Key] = entry.Value;
                            }
                        }
                    }
                }
                else
                {
                    CurrentLanguageCode = "EN";
                }
            }
            catch
            {
                CurrentLanguageCode = "EN";
            }
        }

        LanguageChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private static void EnsureBundledLanguageFiles()
    {
        string sourceDirectory =
            Path.Combine(
                AppContext.BaseDirectory,
                "Resources",
                "GlueDock_Language");

        if (!Directory.Exists(
                sourceDirectory))
        {
            return;
        }

        string targetDirectory =
            Path.Combine(
                AppContext.BaseDirectory,
                "GlueDock_Data",
                "Languages");

        try
        {
            Directory.CreateDirectory(
                targetDirectory);

            foreach (string sourceFile in Directory.GetFiles(
                         sourceDirectory,
                         "GlueDock_*.json",
                         SearchOption.TopDirectoryOnly))
            {
                string targetFile =
                    Path.Combine(
                        targetDirectory,
                        Path.GetFileName(
                            sourceFile));

                if (!File.Exists(
                        targetFile))
                {
                    File.Copy(
                        sourceFile,
                        targetFile,
                        overwrite: false);

                    continue;
                }

                Dictionary<string, string>? sourceValues =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(
                        File.ReadAllText(
                            sourceFile));

                Dictionary<string, string>? targetValues =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(
                        File.ReadAllText(
                            targetFile));

                if (sourceValues is null ||
                    targetValues is null)
                {
                    continue;
                }

                bool changed =
                    false;

                foreach (KeyValuePair<string, string> entry in sourceValues)
                {
                    if (targetValues.ContainsKey(
                            entry.Key))
                    {
                        continue;
                    }

                    targetValues[entry.Key] =
                        entry.Value;

                    changed =
                        true;
                }

                if (changed)
                {
                    File.WriteAllText(
                        targetFile,
                        JsonSerializer.Serialize(
                            targetValues,
                            new JsonSerializerOptions
                            {
                                WriteIndented =
                                    true
                            }));
                }
            }
        }
        catch
        {
        }
    }

    public IReadOnlyList<LanguageOption> GetAvailableLanguages()
    {
        List<LanguageOption> languages =
        [
            new LanguageOption("EN", "English")
        ];

        string directory =
            Path.Combine(
                AppContext.BaseDirectory,
                "GlueDock_Data",
                "Languages");

        try
        {
            Directory.CreateDirectory(directory);

            foreach (string file in Directory.GetFiles(
                         directory,
                         "GlueDock_*.json",
                         SearchOption.TopDirectoryOnly))
            {
                string fileName =
                    Path.GetFileNameWithoutExtension(file);

                const string prefix = "GlueDock_";

                if (!fileName.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string code =
                    fileName[prefix.Length..]
                        .Trim()
                        .ToUpperInvariant();

                if (string.IsNullOrWhiteSpace(code) ||
                    string.Equals(
                        code,
                        "EN",
                        StringComparison.OrdinalIgnoreCase) ||
                    languages.Any(
                        current =>
                            string.Equals(
                                current.Code,
                                code,
                                StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                languages.Add(
                    new LanguageOption(
                        code,
                        GetLanguageDisplayName(code)));
            }
        }
        catch
        {
        }

        return languages
            .OrderBy(
                language =>
                    string.Equals(
                        language.Code,
                        "EN",
                        StringComparison.OrdinalIgnoreCase)
                        ? 0
                        : 1)
            .ThenBy(
                language => language.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string GetLanguageDisplayName(
        string code)
    {
        return code switch
        {
            "DE" => "Deutsch",
            _ => code
        };
    }
}

public sealed record LanguageOption(
    string Code,
    string DisplayName)
{
    public override string ToString()
    {
        return DisplayName;
    }
}
