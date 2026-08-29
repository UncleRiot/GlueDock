using System.IO;
using System.Text.Json;

namespace GlueDock;

public sealed class LanguageService
{
    private readonly Dictionary<string, string> _english =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["App.Name"] = "GlueDock",

            ["Context.Settings"] = "Settings",
            ["Context.About"] = "About",
            ["Context.Exit"] = "Exit",
            ["Context.Remove"] = "Remove",
            ["Context.Show"] = "Show",

            ["Submenu.New"] = "New GlueDock submenu",
            ["Submenu.CreateTitle"] = "New GlueDock submenu",
            ["Submenu.Name"] = "Name",
            ["Submenu.DefaultName"] = "New submenu",

            ["Settings.Title"] = "GlueDock Settings",
            ["Settings.Tab.General"] = "General",
            ["Settings.Tab.Appearance"] = "Appearance",
            ["Settings.Tab.Behavior"] = "Behavior",
            ["Settings.Tab.Dock"] = "Dock",
            ["Settings.Tab.Advanced"] = "Advanced",

            ["Settings.StartWithWindows"] = "Start with Windows",
            ["Settings.DisableCollapse"] = "Disable collapsing",
            ["Settings.CollapseDelay"] = "Collapse delay",
            ["Settings.SubdockCollapseDelay"] = "Subdock collapse delay",

            ["Settings.Theme"] = "Theme",
            ["Settings.EdgeBarColor"] = "Edge bar color",
            ["Settings.ChooseColor"] = "Choose color...",
            ["Settings.ShowDockBorder"] = "Show dock border",
            ["Settings.DockBorderColor"] = "Dock border color",
            ["Settings.EdgeBarThickness"] = "Edge bar thickness",
            ["Settings.DockScale"] = "Dock scaling",
            ["Settings.ItemSpacing"] = "Item spacing",
            ["Settings.DockThicknessScale"] = "Dock height",
            ["Settings.ShowItemLabels"] = "Show names below icons",
            ["Settings.ShowFilePreviews"] = "Show file previews when available",
            ["Settings.UseSmallShortcutOverlay"] = "Use smaller shortcut arrow",
            ["Settings.SubmenuIcon"] = "Subdock icon",
            ["Settings.SubmenuIcon.ChooseWindows"] = "Choose Windows icon...",
            ["Settings.SubmenuIcon.LoadImage"] = "Load image...",
            ["Settings.SubmenuIcon.UseDefault"] = "Use default",
            ["Settings.SubmenuIcon.RepositoryInfo"] = "External images are copied to GlueDock_Repository\\Icons and remain available for reuse.",
            ["Settings.Opacity"] = "Opacity",
            ["Settings.Blur"] = "Blur",
            ["Settings.Animation"] = "Animation",
            ["Settings.HoverEffect"] = "Hover effect",
            ["Settings.Language"] = "Language",
            ["Settings.DebugLogging"] = "Enable debug logging",
            ["Settings.DebugLoggingInfo"] = "Live log, 20 MB active file, rotated logs Brotli-compressed, maximum 10 files",

            ["Settings.ReservedBehavior"] = "Reserved for additional options",
            ["Settings.ReservedDock"] = "Reserved for additional dock options",
            ["Settings.ReservedAdvanced"] = "Reserved for additional advanced options",
            ["Settings.Option"] = "Option",
            ["Settings.Close"] = "Close",

            ["ColorPicker.Title"] = "Choose color",
            ["ColorPicker.Red"] = "Red",
            ["ColorPicker.Green"] = "Green",
            ["ColorPicker.Blue"] = "Blue",
            ["ColorPicker.Hex"] = "Hex:",
            ["ColorPicker.Cancel"] = "Cancel",
            ["ColorPicker.Ok"] = "OK",

            ["Message.LaunchFailed"] = "The item could not be launched.",
            ["Message.ImportFailed"] = "The item could not be saved in GlueDock.",
            ["Message.SubmenuIconImportFailed"] = "The image could not be copied to the GlueDock repository."
        };

    private Dictionary<string, string> _current =
        new(StringComparer.OrdinalIgnoreCase);

    public string CurrentLanguageCode { get; private set; } = "EN";

    public event EventHandler? LanguageChanged;

    public LanguageService()
    {
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
                    "Resources",
                    "GlueDock_Language",
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

    public IReadOnlyList<LanguageOption> GetAvailableLanguages()
    {
        List<LanguageOption> languages =
        [
            new LanguageOption("EN", "English")
        ];

        string directory =
            Path.Combine(
                AppContext.BaseDirectory,
                "Resources",
                "GlueDock_Language");

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
    string DisplayName);
