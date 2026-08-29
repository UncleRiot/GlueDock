using System.IO;
using System.Text.Json;

namespace GlueDock;

public sealed class ProfileStore
{
    private readonly string _profilesDirectory;

    public ProfileStore()
    {
        _profilesDirectory =
            Path.Combine(
                AppContext.BaseDirectory,
                "GlueDock_Profiles");
    }

    public IReadOnlyList<string> GetProfileNames()
    {
        if (!Directory.Exists(_profilesDirectory))
        {
            return [];
        }

        List<string> names = [];

        foreach (string file in Directory.GetFiles(
                     _profilesDirectory,
                     "*.json"))
        {
            try
            {
                DockProfileFile? profile =
                    JsonSerializer.Deserialize<DockProfileFile>(
                        File.ReadAllText(file));

                if (profile is not null &&
                    !string.IsNullOrWhiteSpace(profile.Name))
                {
                    names.Add(profile.Name.Trim());
                }
            }
            catch
            {
            }
        }

        return names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(
                name => name,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ProfileListEntry> GetProfileListEntries()
    {
        List<ProfileListEntry> entries = [];

        foreach (string profileName in GetProfileNames())
        {
            DockSettings? settings =
                Load(
                    profileName);

            if (settings is null)
            {
                continue;
            }

            entries.Add(
                new ProfileListEntry(
                    profileName,
                    settings));
        }

        return entries;
    }

    public DockSettings? Load(
        string profileName)
    {
        string? file =
            FindProfileFile(
                profileName);

        if (file is null)
        {
            return null;
        }

        try
        {
            DockProfileFile? profile =
                JsonSerializer.Deserialize<DockProfileFile>(
                    File.ReadAllText(file));

            return profile?.Settings;
        }
        catch
        {
            return null;
        }
    }

    public void Save(
        string profileName,
        DockSettings settings)
    {
        string name =
            profileName.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Profile name must not be empty.",
                nameof(profileName));
        }

        Directory.CreateDirectory(
            _profilesDirectory);

        DockProfileFile profile =
            new()
            {
                Name = name,
                Settings = CreateProfileSettings(
                    settings)
            };

        string? existingFile =
            FindProfileFile(
                name);

        string file =
            existingFile ??
            Path.Combine(
                _profilesDirectory,
                $"{CreateFileName(name)}.json");

        string json =
            JsonSerializer.Serialize(
                profile,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        File.WriteAllText(
            file,
            json);
    }

    public bool Delete(
        string profileName)
    {
        string? file =
            FindProfileFile(
                profileName);

        if (file is null)
        {
            return false;
        }

        File.Delete(file);
        return true;
    }

    public bool Rename(
        string profileName,
        string newProfileName)
    {
        string? oldFile =
            FindProfileFile(
                profileName);

        if (oldFile is null)
        {
            return false;
        }

        string newName =
            newProfileName.Trim();

        if (string.IsNullOrWhiteSpace(newName))
        {
            return false;
        }

        DockSettings? settings =
            Load(
                profileName);

        if (settings is null)
        {
            return false;
        }

        string? existingNewFile =
            FindProfileFile(
                newName);

        if (existingNewFile is not null &&
            !string.Equals(
                existingNewFile,
                oldFile,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Save(
            newName,
            settings);

        string? newFile =
            FindProfileFile(
                newName);

        if (newFile is not null &&
            !string.Equals(
                newFile,
                oldFile,
                StringComparison.OrdinalIgnoreCase) &&
            File.Exists(oldFile))
        {
            File.Delete(oldFile);
        }
        else if (string.Equals(
                     newFile,
                     oldFile,
                     StringComparison.OrdinalIgnoreCase))
        {
            DockProfileFile profile =
                new()
                {
                    Name = newName,
                    Settings = CreateProfileSettings(
                        settings)
                };

            File.WriteAllText(
                oldFile,
                JsonSerializer.Serialize(
                    profile,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
        }

        return true;
    }

    public DockSettings CreateSnapshot(
        DockSettings settings)
    {
        string json =
            JsonSerializer.Serialize(
                settings);

        return JsonSerializer.Deserialize<DockSettings>(
                   json)
               ?? new DockSettings();
    }

    public void CopyProfileSettings(
        DockSettings source,
        DockSettings target)
    {
        foreach (System.Reflection.PropertyInfo property
                 in typeof(DockSettings).GetProperties())
        {
            if (!property.CanRead ||
                !property.CanWrite ||
                string.Equals(
                    property.Name,
                    nameof(DockSettings.DockItems),
                    StringComparison.Ordinal) ||
                string.Equals(
                    property.Name,
                    nameof(DockSettings.Items),
                    StringComparison.Ordinal))
            {
                continue;
            }

            property.SetValue(
                target,
                property.GetValue(source));
        }
    }

    private DockSettings CreateProfileSettings(
        DockSettings settings)
    {
        DockSettings profileSettings =
            new();

        CopyProfileSettings(
            settings,
            profileSettings);

        profileSettings.DockItems = [];
        profileSettings.Items = [];

        return profileSettings;
    }

    private string? FindProfileFile(
        string profileName)
    {
        if (!Directory.Exists(_profilesDirectory))
        {
            return null;
        }

        foreach (string file in Directory.GetFiles(
                     _profilesDirectory,
                     "*.json"))
        {
            try
            {
                DockProfileFile? profile =
                    JsonSerializer.Deserialize<DockProfileFile>(
                        File.ReadAllText(file));

                if (profile is not null &&
                    string.Equals(
                        profile.Name,
                        profileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static string CreateFileName(
        string profileName)
    {
        string fileName =
            profileName.Trim();

        foreach (char invalidChar
                 in Path.GetInvalidFileNameChars())
        {
            fileName =
                fileName.Replace(
                    invalidChar,
                    '_');
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "Profile";
        }

        string baseName =
            fileName;

        int suffix = 2;

        while (File.Exists(
                   Path.Combine(
                       AppContext.BaseDirectory,
                       "GlueDock_Profiles",
                       $"{fileName}.json")))
        {
            fileName =
                $"{baseName} {suffix}";

            suffix++;
        }

        return fileName;
    }

    public sealed class ProfileListEntry
    {
        public string Name { get; }

        public string Summary { get; }

        public ProfileListEntry(
            string name,
            DockSettings settings)
        {
            Name = name;

            Summary =
                $"{settings.ThemeName} · Scale {settings.DockScale * 100:0}% · Opacity {settings.Opacity * 100:0}% · Blur {settings.BlurRadius:0} · Spacing {settings.ItemSpacing:0} · {(settings.ShowItemLabels ? "Labels On" : "Labels Off")} · {settings.HoverEffect}";
        }
    }

    private sealed class DockProfileFile
    {
        public string Name { get; set; } = string.Empty;

        public DockSettings Settings { get; set; } = new();
    }
}
