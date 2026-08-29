using System.IO;
using System.Text.Json;

namespace GlueDock;

public static class DockThemeService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    public static string ThemeDirectory =>
        Path.Combine(
            AppContext.BaseDirectory,
            "GlueDock_Themes");

    public static IReadOnlyList<DockThemeOption> GetAvailableThemes()
    {
        List<DockThemeOption> themes = [];

        try
        {
            Directory.CreateDirectory(
                ThemeDirectory);

            foreach (string file in Directory.GetFiles(
                         ThemeDirectory,
                         "*.json",
                         SearchOption.TopDirectoryOnly))
            {
                DockTheme? theme =
                    LoadFile(
                        file);

                if (theme is null)
                {
                    continue;
                }

                string id =
                    Path.GetFileNameWithoutExtension(
                        file);

                string displayName =
                    string.IsNullOrWhiteSpace(
                        theme.Name)
                        ? id
                        : theme.Name.Trim();

                themes.Add(
                    new DockThemeOption(
                        id,
                        displayName));
            }
        }
        catch
        {
        }

        return themes
            .OrderBy(
                theme =>
                    string.Equals(
                        theme.Id,
                        "Default",
                        StringComparison.OrdinalIgnoreCase)
                        ? 0
                        : 1)
            .ThenBy(
                theme => theme.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static DockTheme Load(
        string? themeName)
    {
        string normalizedName =
            string.IsNullOrWhiteSpace(
                themeName)
                ? "Default"
                : Path.GetFileNameWithoutExtension(
                    themeName.Trim());

        DockTheme? selectedTheme =
            LoadFile(
                Path.Combine(
                    ThemeDirectory,
                    $"{normalizedName}.json"));

        if (selectedTheme is not null)
        {
            return selectedTheme;
        }

        DockTheme? defaultTheme =
            LoadFile(
                Path.Combine(
                    ThemeDirectory,
                    "Default.json"));

        return defaultTheme ??
               new DockTheme();
    }

    private static DockTheme? LoadFile(
        string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            string json =
                File.ReadAllText(
                    path);

            return JsonSerializer.Deserialize<DockTheme>(
                json,
                JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
