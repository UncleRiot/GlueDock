using System.IO;
using System.Text.Json;

namespace GlueDock;

public sealed class SettingsStore
{
    private readonly string _settingsDirectory;
    private readonly string _settingsFile;

    public SettingsStore()
    {
        _settingsDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "GlueDock_settings");

        _settingsFile = Path.Combine(
            _settingsDirectory,
            "settings.json");
    }

    public DockSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFile))
            {
                return new DockSettings();
            }

            string json = File.ReadAllText(_settingsFile);

            return JsonSerializer.Deserialize<DockSettings>(json)
                   ?? new DockSettings();
        }
        catch
        {
            return new DockSettings();
        }
    }

    public void Save(DockSettings settings)
    {
        Directory.CreateDirectory(_settingsDirectory);

        string json = JsonSerializer.Serialize(
            settings,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(_settingsFile, json);
    }
}
